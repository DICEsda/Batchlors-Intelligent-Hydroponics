#include "PairingStateMachine.h"
#include <esp_random.h>

PairingStateMachine::PairingStateMachine(DiscoveryManager& discoveryMgr) :
    discovery(discoveryMgr),
    state(CoordinatorPairingState::OPERATIONAL),
    coordinatorId(0),
    farmId(0),
    nextTowerId(1),
    permitJoinDl(),
    bindingActive(false),
    onPermitJoinChanged(nullptr),
    onBindingStarted(nullptr),
    onBindingCompleted(nullptr),
    sendOffer(nullptr),
    sendConfirm(nullptr),
    sendReject(nullptr) {
    memset(coordinatorMac, 0, 6);
}

void PairingStateMachine::begin(const uint8_t* coordMac, uint16_t coordId, uint16_t fId) {
    memcpy(coordinatorMac, coordMac, 6);
    coordinatorId = coordId;
    farmId = fId;
    state = CoordinatorPairingState::OPERATIONAL;
    bindingActive = false;
    permitJoinDl.clear();
    
    char macStr[18];
    snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
             coordMac[0], coordMac[1], coordMac[2], coordMac[3], coordMac[4], coordMac[5]);
    Serial.printf("[PairingStateMachine] Initialized - MAC: %s, CoordID: %u, FarmID: %u\n",
                  macStr, coordId, fId);
}

void PairingStateMachine::tick() {
    uint32_t now = millis();
    
    // Handle permit-join timeout
    if (state == CoordinatorPairingState::DISCOVERY_ACTIVE) {
        if (permitJoinDl.expired()) {
            Serial.println("[PairingStateMachine] Permit-join window expired");
            disablePermitJoin();
        }
    }
    
    // Handle binding timeout
    if (state == CoordinatorPairingState::BINDING && bindingActive) {
        if (currentBinding.isTimedOut(now)) {
            Serial.printf("[PairingStateMachine] Binding timeout for node %s\n",
                         currentBinding.macString().c_str());
            handleBindingTimeout();
        }
    }
}

uint32_t PairingStateMachine::getPermitJoinRemainingMs() const {
    if (state == CoordinatorPairingState::OPERATIONAL) {
        return 0;
    }
    
    return permitJoinDl.remainingMs();
}

bool PairingStateMachine::enablePermitJoin(uint32_t duration_ms) {
    // Clamp duration to max
    if (duration_ms > PairingConstants::MAX_PERMIT_JOIN_MS) {
        duration_ms = PairingConstants::MAX_PERMIT_JOIN_MS;
    }
    
    // Can enable from OPERATIONAL or extend if already active
    if (state != CoordinatorPairingState::OPERATIONAL && 
        state != CoordinatorPairingState::DISCOVERY_ACTIVE) {
        Serial.println("[PairingStateMachine] Cannot enable permit-join during binding");
        return false;
    }
    
    permitJoinDl.set(duration_ms);
    
    if (state == CoordinatorPairingState::OPERATIONAL) {
        transitionTo(CoordinatorPairingState::DISCOVERY_ACTIVE);
    }
    
    Serial.printf("[PairingStateMachine] Permit-join enabled for %lu ms\n", duration_ms);
    
    if (onPermitJoinChanged) {
        onPermitJoinChanged(true, duration_ms);
    }
    
    return true;
}

void PairingStateMachine::disablePermitJoin() {
    // If we're in the middle of binding, complete it first
    if (state == CoordinatorPairingState::BINDING && bindingActive) {
        Serial.println("[PairingStateMachine] Completing active binding before disabling permit-join");
        completeBinding(BindingResult::INTERNAL_ERROR);
    }
    
    permitJoinDl.clear();
    transitionTo(CoordinatorPairingState::OPERATIONAL);
    
    // Clear discovered nodes that aren't bound
    discovery.clearAll();
    
    Serial.println("[PairingStateMachine] Permit-join disabled");
    
    if (onPermitJoinChanged) {
        onPermitJoinChanged(false, 0);
    }
}

bool PairingStateMachine::approveNode(const uint8_t* mac) {
    // Must be in discovery mode
    if (state != CoordinatorPairingState::DISCOVERY_ACTIVE) {
        Serial.println("[PairingStateMachine] Cannot approve node - not in discovery mode");
        return false;
    }
    
    // Cannot approve if already binding another node
    if (bindingActive) {
        Serial.println("[PairingStateMachine] Cannot approve node - already binding another node");
        return false;
    }
    
    // Find the node in discovery table
    DiscoveredNode* node = discovery.findByMac(mac);
    if (!node) {
        char macStr[18];
        snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
                 mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
        Serial.printf("[PairingStateMachine] Node not found: %s\n", macStr);
        return false;
    }
    
    // Check node state
    if (node->state == DiscoveryState::BOUND) {
        Serial.printf("[PairingStateMachine] Node %s already bound\n", node->macString().c_str());
        return false;
    }
    
    // Check we have a send callback
    if (!sendOffer) {
        Serial.println("[PairingStateMachine] No sendOffer callback configured");
        return false;
    }
    
    // Generate offer token
    uint32_t token = generateOfferToken();
    
    // Build the offer message
    PairingOfferMessage offer;
    offer.protocol_version = PairingConstants::PROTOCOL_VERSION;
    memcpy(offer.coord_mac, coordinatorMac, 6);
    offer.coord_id = coordinatorId;
    offer.farm_id = farmId;
    offer.offered_tower_id = nextTowerId;
    offer.nonce_echo = node->last_nonce;  // Echo their advertisement nonce
    offer.offer_token = token;
    offer.channel = 0;  // Use current channel
    
    // Setup binding attempt tracking
    memcpy(currentBinding.node_mac, mac, 6);
    currentBinding.offer_token = token;
    currentBinding.assigned_tower_id = nextTowerId;
    currentBinding.started_ms = millis();
    currentBinding.accept_received = false;
    bindingActive = true;
    
    // Update node state in discovery manager
    discovery.updateNodeState(mac, DiscoveryState::OFFER_SENT);
    discovery.setOfferToken(mac, token);
    
    // Send the offer
    if (!sendOffer(mac, offer)) {
        Serial.printf("[PairingStateMachine] Failed to send offer to %s\n", node->macString().c_str());
        bindingActive = false;
        discovery.updateNodeState(mac, DiscoveryState::DISCOVERED);
        return false;
    }
    
    // Transition to binding state
    transitionTo(CoordinatorPairingState::BINDING);
    
    Serial.printf("[PairingStateMachine] Sent PAIRING_OFFER to %s, token: 0x%08X, tower_id: %u\n",
                  node->macString().c_str(), token, nextTowerId);
    
    if (onBindingStarted) {
        onBindingStarted(*node);
    }
    
    return true;
}

bool PairingStateMachine::approveNodeByMacString(const String& macStr) {
    uint8_t mac[6];
    if (!stringToMac(macStr, mac)) {
        Serial.printf("[PairingStateMachine] Invalid MAC string: %s\n", macStr.c_str());
        return false;
    }
    return approveNode(mac);
}

bool PairingStateMachine::rejectNode(const uint8_t* mac) {
    // Find the node
    DiscoveredNode* node = discovery.findByMac(mac);
    if (!node) {
        char macStr[18];
        snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
                 mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
        Serial.printf("[PairingStateMachine] Node not found for rejection: %s\n", macStr);
        return false;
    }
    
    // Check we have a send callback
    if (!sendReject) {
        Serial.println("[PairingStateMachine] No sendReject callback configured");
        return false;
    }
    
    // Build reject message
    PairingRejectMessage reject;
    memcpy(reject.sender_mac, coordinatorMac, 6);
    reject.reason_code = PairingRejectReason::USER_REJECTED;
    reject.offer_token = node->offer_token;  // May be 0 if never offered
    
    // Send rejection
    if (!sendReject(mac, reject)) {
        Serial.printf("[PairingStateMachine] Failed to send rejection to %s\n", node->macString().c_str());
        return false;
    }
    
    Serial.printf("[PairingStateMachine] Sent PAIRING_REJECT to %s\n", node->macString().c_str());
    
    // Update node state
    discovery.updateNodeState(mac, DiscoveryState::REJECTED);
    
    // If this was the node we were binding, cancel the binding
    if (bindingActive && compareMac(currentBinding.node_mac, mac)) {
        completeBinding(BindingResult::NODE_REJECTED);
    }
    
    // Remove from discovery table
    discovery.removeNode(mac);
    
    return true;
}

bool PairingStateMachine::rejectNodeByMacString(const String& macStr) {
    uint8_t mac[6];
    if (!stringToMac(macStr, mac)) {
        Serial.printf("[PairingStateMachine] Invalid MAC string: %s\n", macStr.c_str());
        return false;
    }
    return rejectNode(mac);
}

bool PairingStateMachine::onPairingAcceptReceived(const PairingAcceptMessage& msg) {
    // Must be in binding state
    if (state != CoordinatorPairingState::BINDING || !bindingActive) {
        Serial.println("[PairingStateMachine] Received PAIRING_ACCEPT but not in binding state");
        return false;
    }
    
    // Validate it's from the node we're binding
    if (!compareMac(currentBinding.node_mac, msg.node_mac)) {
        char expectedMac[18], receivedMac[18];
        snprintf(expectedMac, sizeof(expectedMac), "%02X:%02X:%02X:%02X:%02X:%02X",
                 currentBinding.node_mac[0], currentBinding.node_mac[1], currentBinding.node_mac[2],
                 currentBinding.node_mac[3], currentBinding.node_mac[4], currentBinding.node_mac[5]);
        snprintf(receivedMac, sizeof(receivedMac), "%02X:%02X:%02X:%02X:%02X:%02X",
                 msg.node_mac[0], msg.node_mac[1], msg.node_mac[2],
                 msg.node_mac[3], msg.node_mac[4], msg.node_mac[5]);
        Serial.printf("[PairingStateMachine] PAIRING_ACCEPT from unexpected node: expected %s, got %s\n",
                     expectedMac, receivedMac);
        return false;
    }
    
    // Validate token
    if (msg.offer_token != currentBinding.offer_token) {
        Serial.printf("[PairingStateMachine] Token mismatch: expected 0x%08X, got 0x%08X\n",
                     currentBinding.offer_token, msg.offer_token);
        return false;
    }
    
    // Validate tower ID
    if (msg.accepted_tower_id != currentBinding.assigned_tower_id) {
        Serial.printf("[PairingStateMachine] Tower ID mismatch: expected %u, got %u\n",
                     currentBinding.assigned_tower_id, msg.accepted_tower_id);
        return false;
    }
    
    currentBinding.accept_received = true;
    
    Serial.printf("[PairingStateMachine] Received valid PAIRING_ACCEPT from %s\n",
                 currentBinding.macString().c_str());
    
    // Update node state
    discovery.updateNodeState(msg.node_mac, DiscoveryState::BINDING);
    
    // Check we have send callback
    if (!sendConfirm) {
        Serial.println("[PairingStateMachine] No sendConfirm callback configured");
        completeBinding(BindingResult::INTERNAL_ERROR);
        return false;
    }
    
    // Send confirmation
    PairingConfirmMessage confirm;
    memcpy(confirm.coord_mac, coordinatorMac, 6);
    confirm.tower_id = currentBinding.assigned_tower_id;
    memset(confirm.encryption_key, 0, 16);  // No encryption for now
    confirm.config_flags = 0;  // No encryption
    
    if (!sendConfirm(msg.node_mac, confirm)) {
        Serial.printf("[PairingStateMachine] Failed to send PAIRING_CONFIRM\n");
        completeBinding(BindingResult::INTERNAL_ERROR);
        return false;
    }
    
    Serial.printf("[PairingStateMachine] Sent PAIRING_CONFIRM to %s, tower_id: %u\n",
                 currentBinding.macString().c_str(), confirm.tower_id);
    
    // Binding successful
    completeBinding(BindingResult::SUCCESS);
    
    return true;
}

bool PairingStateMachine::onPairingAbortReceived(const PairingAbortMessage& msg) {
    // Check if from a node we know about
    DiscoveredNode* node = discovery.findByMac(msg.sender_mac);
    
    char macStr[18];
    snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
             msg.sender_mac[0], msg.sender_mac[1], msg.sender_mac[2],
             msg.sender_mac[3], msg.sender_mac[4], msg.sender_mac[5]);
    
    Serial.printf("[PairingStateMachine] Received PAIRING_ABORT from %s, reason: %d\n",
                 macStr, static_cast<int>(msg.reason_code));
    
    // If this is the node we're currently binding
    if (bindingActive && compareMac(currentBinding.node_mac, msg.sender_mac)) {
        // Validate token if we sent an offer
        if (currentBinding.offer_token != 0 && msg.offer_token != currentBinding.offer_token) {
            Serial.printf("[PairingStateMachine] Token mismatch in PAIRING_ABORT, ignoring\n");
            return false;
        }
        
        completeBinding(BindingResult::NODE_ABORTED);
    }
    
    // Remove from discovery table
    if (node) {
        discovery.updateNodeState(msg.sender_mac, DiscoveryState::FAILED);
        discovery.removeNode(msg.sender_mac);
    }
    
    return true;
}

uint32_t PairingStateMachine::generateOfferToken() {
    // Use ESP32 hardware random number generator
    return esp_random();
}

void PairingStateMachine::transitionTo(CoordinatorPairingState newState) {
    if (state == newState) {
        return;
    }
    
    const char* stateNames[] = { "OPERATIONAL", "DISCOVERY_ACTIVE", "BINDING" };
    Serial.printf("[PairingStateMachine] State: %s -> %s\n",
                 stateNames[static_cast<int>(state)],
                 stateNames[static_cast<int>(newState)]);
    
    state = newState;
}

void PairingStateMachine::handleBindingTimeout() {
    if (!bindingActive) {
        return;
    }
    
    Serial.printf("[PairingStateMachine] Binding timeout for %s\n",
                 currentBinding.macString().c_str());
    
    // Send rejection to node so it knows the offer expired
    if (sendReject) {
        PairingRejectMessage reject;
        memcpy(reject.sender_mac, coordinatorMac, 6);
        reject.reason_code = PairingRejectReason::TIMEOUT;
        reject.offer_token = currentBinding.offer_token;
        
        sendReject(currentBinding.node_mac, reject);
    }
    
    completeBinding(BindingResult::TIMEOUT);
}

void PairingStateMachine::completeBinding(BindingResult result) {
    if (!bindingActive) {
        return;
    }
    
    const char* resultNames[] = { "SUCCESS", "TIMEOUT", "NODE_REJECTED", "NODE_ABORTED", "INTERNAL_ERROR", "ALREADY_BOUND" };
    Serial.printf("[PairingStateMachine] Binding completed: %s\n", resultNames[static_cast<int>(result)]);
    
    // Get node info before we potentially remove it
    DiscoveredNode* node = discovery.findByMac(currentBinding.node_mac);
    DiscoveredNode nodeCopy;
    if (node) {
        nodeCopy = *node;
    } else {
        // Create minimal copy from binding info
        memcpy(nodeCopy.mac, currentBinding.node_mac, 6);
        nodeCopy.state = DiscoveryState::FAILED;
    }
    
    if (result == BindingResult::SUCCESS) {
        // Mark node as bound
        if (node) {
            discovery.updateNodeState(currentBinding.node_mac, DiscoveryState::BOUND);
        }
        
        // Increment tower ID for next pairing
        nextTowerId++;
        
        Serial.printf("[PairingStateMachine] Node %s bound as tower %u\n",
                     currentBinding.macString().c_str(),
                     currentBinding.assigned_tower_id);
    } else {
        // Mark node as failed
        if (node) {
            discovery.updateNodeState(currentBinding.node_mac, DiscoveryState::FAILED);
        }
    }
    
    // Clear binding state
    bindingActive = false;
    memset(currentBinding.node_mac, 0, 6);
    currentBinding.offer_token = 0;
    
    // Transition back to discovery if permit-join still active, otherwise operational
    if (permitJoinDl.running()) {
        transitionTo(CoordinatorPairingState::DISCOVERY_ACTIVE);
    } else {
        transitionTo(CoordinatorPairingState::OPERATIONAL);
    }
    
    // Invoke callback
    if (onBindingCompleted) {
        onBindingCompleted(nodeCopy, result);
    }
}

bool PairingStateMachine::compareMac(const uint8_t* mac1, const uint8_t* mac2) const {
    return memcmp(mac1, mac2, 6) == 0;
}
