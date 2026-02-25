/**
 * @file NodePairingFSM.cpp
 * @brief V2 Pairing State Machine Implementation for Tower Nodes
 * 
 * Implements the node-side pairing protocol with:
 * - Advertisement broadcasting with jitter
 * - Nonce rotation for replay protection
 * - Offer validation and acceptance
 * - Credential storage to NVS
 * - Timeout handling
 */

#include "NodePairingFSM.h"
#include "../config/TowerConfig.h"
#include <esp_random.h>

// Use timing constants from shared header
using namespace PairingConstants;

// Logging tag
static const char* TAG = "NodePairingFSM";

// ============================================================================
// Constructor
// ============================================================================

NodePairingFSM::NodePairingFSM(TowerConfig& config)
    : _config(config)
    , _deviceType(DeviceType::TOWER)
    , _firmwareVersion(0)
    , _capabilities(0)
    , _state(NodePairingState::INIT)
    , _initialized(false)
    , _advStartMs(0)
    , _lastAdvMs(0)
    , _nextAdvMs(0)
    , _sequenceNum(0)
    , _currentNonce(0)
    , _lastNonceRotationMs(0)
    , _pendingOfferToken(0)
    , _pendingTowerId(0)
    , _acceptSentMs(0)
    , _assignedTowerId(0)
{
    memset(_nodeMac, 0, 6);
    memset(_pendingCoordMac, 0, 6);
}

// ============================================================================
// Initialization
// ============================================================================

void NodePairingFSM::begin(const uint8_t* nodeMac, DeviceType deviceType,
                           uint32_t firmwareVersion, uint16_t capabilities) {
    if (_initialized) {
        Serial.printf("[%s] Already initialized, call reset() first\n", TAG);
        return;
    }

    // Store node identity
    memcpy(_nodeMac, nodeMac, 6);
    _deviceType = deviceType;
    _firmwareVersion = firmwareVersion;
    _capabilities = capabilities;

    // Initialize sequence number with random offset
    _sequenceNum = (uint16_t)(esp_random() & 0xFFFF);

    // Generate initial nonce
    generateNewNonce();

    Serial.printf("[%s] Initialized for node %s, type=%d, fw=0x%08X\n",
                  TAG, macToString(_nodeMac).c_str(), 
                  static_cast<int>(_deviceType), _firmwareVersion);

    // Check if we have existing credentials
    if (loadExistingCredentials()) {
        Serial.printf("[%s] Found existing credentials, tower_id=%d\n", 
                      TAG, _assignedTowerId);
        transitionTo(NodePairingState::BOUND);
    } else {
        Serial.printf("[%s] No credentials found, transitioning to UNPAIRED\n", TAG);
        transitionTo(NodePairingState::UNPAIRED);
    }

    _initialized = true;
}

// ============================================================================
// Main Tick
// ============================================================================

void NodePairingFSM::tick() {
    if (!_initialized) return;

    uint32_t now = millis();

    switch (_state) {
        case NodePairingState::ADVERTISING:
            // Rotate nonce if needed
            rotateNonceIfNeeded();

            // Check for advertising timeout (5 minutes)
            if ((now - _advStartMs) >= ADV_TIMEOUT_MS) {
                handleAdvertisingTimeout();
                return;
            }

            // Send advertisement if it's time
            if (now >= _nextAdvMs) {
                sendAdvertisementMessage();
                _lastAdvMs = now;
                _nextAdvMs = now + calculateNextAdvDelay();
            }
            break;

        case NodePairingState::WAITING_CONFIRM:
            // Check for confirm timeout
            if ((now - _acceptSentMs) >= CONFIRM_TIMEOUT_MS) {
                handleConfirmTimeout();
            }
            break;

        case NodePairingState::OFFER_RECEIVED:
            // Brief transitional state, should be quick
            // If stuck here for >1s, something went wrong
            if ((now - _lastAdvMs) > 1000) {
                Serial.printf("[%s] Stuck in OFFER_RECEIVED, returning to ADVERTISING\n", TAG);
                transitionTo(NodePairingState::ADVERTISING);
            }
            break;

        default:
            // Other states don't need tick processing
            break;
    }
}

// ============================================================================
// Commands
// ============================================================================

bool NodePairingFSM::startAdvertising() {
    if (_state != NodePairingState::UNPAIRED) {
        Serial.printf("[%s] Cannot start advertising from state %s\n", 
                      TAG, getStateString());
        return false;
    }

    if (!_sendAdvertisement) {
        Serial.printf("[%s] No advertisement callback set\n", TAG);
        return false;
    }

    uint32_t now = millis();
    
    // Reset advertising state
    _advStartMs = now;
    _lastAdvMs = 0;
    _nextAdvMs = now;  // Send first advertisement immediately
    _sequenceNum = (uint16_t)(esp_random() & 0xFFFF);  // Reset sequence
    
    // Generate fresh nonce
    generateNewNonce();
    _lastNonceRotationMs = now;

    Serial.printf("[%s] Starting advertising, timeout in %lu seconds\n",
                  TAG, ADV_TIMEOUT_MS / 1000);

    transitionTo(NodePairingState::ADVERTISING);
    return true;
}

bool NodePairingFSM::cancelPairing() {
    if (!isPairing()) {
        Serial.printf("[%s] Not in pairing mode, nothing to cancel\n", TAG);
        return false;
    }

    NodePairingState previousState = _state;

    // If we were waiting for confirm, send abort
    if (_state == NodePairingState::WAITING_CONFIRM && _sendAbort) {
        PairingAbortMessage abort;
        memcpy(abort.sender_mac, _nodeMac, 6);
        abort.reason_code = PairingRejectReason::USER_REJECTED;
        abort.offer_token = _pendingOfferToken;

        if (_sendAbort(_pendingCoordMac, abort)) {
            Serial.printf("[%s] Sent PAIRING_ABORT to coordinator\n", TAG);
        }
    }

    Serial.printf("[%s] Pairing cancelled from state %s\n", TAG, getStateString());
    
    completePairing(NodePairingResult::USER_CANCELLED);
    return true;
}

void NodePairingFSM::enterOperational() {
    if (_state != NodePairingState::BOUND) {
        Serial.printf("[%s] Cannot enter operational from state %s\n", 
                      TAG, getStateString());
        return;
    }

    Serial.printf("[%s] Entering operational mode, tower_id=%d\n", 
                  TAG, _assignedTowerId);
    transitionTo(NodePairingState::OPERATIONAL);
}

void NodePairingFSM::reset() {
    Serial.printf("[%s] Resetting pairing state\n", TAG);

    // Clear credentials from NVS
    _config.clearPairing();

    // Reset internal state
    _assignedTowerId = 0;
    _pendingOfferToken = 0;
    _pendingTowerId = 0;
    memset(_pendingCoordMac, 0, 6);

    transitionTo(NodePairingState::UNPAIRED);
}

// ============================================================================
// Message Handlers
// ============================================================================

bool NodePairingFSM::onPairingOfferReceived(const PairingOfferMessage& offer, 
                                             const uint8_t* senderMac) {
    Serial.printf("[%s] Received PAIRING_OFFER from %s\n", TAG, 
                  macToString(senderMac).c_str());

    // Must be in advertising state to receive offers
    if (_state != NodePairingState::ADVERTISING) {
        Serial.printf("[%s] Ignoring offer - not advertising (state=%s)\n", 
                      TAG, getStateString());
        return false;
    }

    // Validate protocol version
    if (offer.protocol_version != PROTOCOL_VERSION) {
        Serial.printf("[%s] Protocol version mismatch: got %d, expected %d\n",
                      TAG, offer.protocol_version, PROTOCOL_VERSION);
        return false;
    }

    // Validate nonce echo (critical for security)
    if (offer.nonce_echo != _currentNonce) {
        Serial.printf("[%s] Nonce mismatch: got 0x%08X, expected 0x%08X\n",
                      TAG, offer.nonce_echo, _currentNonce);
        completePairing(NodePairingResult::NONCE_MISMATCH);
        return false;
    }

    Serial.printf("[%s] Valid offer: coord_id=%d, farm_id=%d, tower_id=%d, token=0x%08X\n",
                  TAG, offer.coord_id, offer.farm_id, offer.offered_tower_id, offer.offer_token);

    // Store pending binding info
    memcpy(_pendingCoordMac, offer.coord_mac, 6);
    _pendingOfferToken = offer.offer_token;
    _pendingTowerId = offer.offered_tower_id;

    transitionTo(NodePairingState::OFFER_RECEIVED);

    // Check if we have the accept callback
    if (!_sendAccept) {
        Serial.printf("[%s] No accept callback set!\n", TAG);
        transitionTo(NodePairingState::ADVERTISING);
        return false;
    }

    // Build and send PAIRING_ACCEPT
    PairingAcceptMessage accept;
    memcpy(accept.node_mac, _nodeMac, 6);
    accept.offer_token = offer.offer_token;
    accept.accepted_tower_id = offer.offered_tower_id;

    if (!_sendAccept(_pendingCoordMac, accept)) {
        Serial.printf("[%s] Failed to send PAIRING_ACCEPT\n", TAG);
        transitionTo(NodePairingState::ADVERTISING);
        return false;
    }

    Serial.printf("[%s] Sent PAIRING_ACCEPT, waiting for PAIRING_CONFIRM\n", TAG);
    
    _acceptSentMs = millis();
    transitionTo(NodePairingState::WAITING_CONFIRM);
    return true;
}

bool NodePairingFSM::onPairingConfirmReceived(const PairingConfirmMessage& confirm) {
    Serial.printf("[%s] Received PAIRING_CONFIRM\n", TAG);

    // Must be waiting for confirm
    if (_state != NodePairingState::WAITING_CONFIRM) {
        Serial.printf("[%s] Ignoring confirm - not waiting (state=%s)\n", 
                      TAG, getStateString());
        return false;
    }

    // Validate sender MAC matches the coordinator we accepted
    if (!compareMac(confirm.coord_mac, _pendingCoordMac)) {
        Serial.printf("[%s] Confirm from wrong coordinator: expected %s, got %s\n",
                      TAG, macToString(_pendingCoordMac).c_str(),
                      macToString(confirm.coord_mac).c_str());
        return false;
    }

    // Validate tower ID matches what we accepted
    if (confirm.tower_id != _pendingTowerId) {
        Serial.printf("[%s] Tower ID mismatch: expected %d, got %d\n",
                      TAG, _pendingTowerId, confirm.tower_id);
        completePairing(NodePairingResult::TOKEN_MISMATCH);
        return false;
    }

    Serial.printf("[%s] Valid confirm: tower_id=%d, encryption=%s\n",
                  TAG, confirm.tower_id, 
                  confirm.hasEncryption() ? "enabled" : "disabled");

    // Save credentials to NVS
    if (!saveCredentials(confirm)) {
        Serial.printf("[%s] Failed to save credentials to NVS\n", TAG);
        completePairing(NodePairingResult::NVS_ERROR);
        return false;
    }

    _assignedTowerId = confirm.tower_id;
    Serial.printf("[%s] Pairing complete! Tower ID: %d\n", TAG, _assignedTowerId);
    
    completePairing(NodePairingResult::SUCCESS);
    return true;
}

bool NodePairingFSM::onPairingRejectReceived(const PairingRejectMessage& reject) {
    Serial.printf("[%s] Received PAIRING_REJECT, reason=%d\n", 
                  TAG, static_cast<int>(reject.reason_code));

    // Can receive reject in advertising or waiting states
    if (!isPairing()) {
        Serial.printf("[%s] Ignoring reject - not pairing (state=%s)\n", 
                      TAG, getStateString());
        return false;
    }

    // If we were waiting for confirm and token matches, this is for us
    if (_state == NodePairingState::WAITING_CONFIRM) {
        if (reject.offer_token != _pendingOfferToken && reject.offer_token != 0) {
            Serial.printf("[%s] Reject token mismatch, ignoring\n", TAG);
            return false;
        }
    }

    Serial.printf("[%s] Coordinator rejected pairing: reason=%d\n", 
                  TAG, static_cast<int>(reject.reason_code));
    
    completePairing(NodePairingResult::REJECTED);
    return true;
}

// ============================================================================
// Internal Helpers
// ============================================================================

void NodePairingFSM::transitionTo(NodePairingState newState) {
    if (_state == newState) return;

    NodePairingState oldState = _state;
    _state = newState;

    Serial.printf("[%s] State: %s -> %s\n", TAG, 
                  getStateString(), // Note: this returns new state now
                  getStateString());

    if (_onStateChanged) {
        _onStateChanged(oldState, newState);
    }
}

void NodePairingFSM::generateNewNonce() {
    _currentNonce = esp_random();
    Serial.printf("[%s] Generated new nonce: 0x%08X\n", TAG, _currentNonce);
}

void NodePairingFSM::rotateNonceIfNeeded() {
    uint32_t now = millis();
    if ((now - _lastNonceRotationMs) >= NONCE_ROTATION_MS) {
        generateNewNonce();
        _lastNonceRotationMs = now;
    }
}

uint32_t NodePairingFSM::calculateNextAdvDelay() {
    // Base interval with random jitter: 100ms ± 20ms
    int32_t jitter = (int32_t)(esp_random() % (ADV_JITTER_MS * 2 + 1)) - (int32_t)ADV_JITTER_MS;
    uint32_t delay = ADV_INTERVAL_MS + jitter;
    return delay;
}

void NodePairingFSM::sendAdvertisementMessage() {
    if (!_sendAdvertisement) return;

    PairingAdvertisementMessage adv;
    adv.protocol_version = PROTOCOL_VERSION;
    memcpy(adv.node_mac, _nodeMac, 6);
    adv.device_type = _deviceType;
    adv.firmware_version = _firmwareVersion;
    adv.capabilities = _capabilities;
    adv.nonce = _currentNonce;
    adv.sequence_num = _sequenceNum++;
    adv.rssi_request = 0;  // Will be filled by receiver

    if (_sendAdvertisement(adv)) {
        // Success - logged at debug level to reduce spam
        if (_sequenceNum % 50 == 0) {  // Log every 50th advertisement
            Serial.printf("[%s] Advertising... seq=%d, nonce=0x%08X\n", 
                          TAG, _sequenceNum, _currentNonce);
        }
    } else {
        Serial.printf("[%s] Failed to send advertisement\n", TAG);
    }
}

void NodePairingFSM::handleAdvertisingTimeout() {
    Serial.printf("[%s] Advertising timeout after %lu ms\n", TAG, ADV_TIMEOUT_MS);
    completePairing(NodePairingResult::TIMEOUT);
}

void NodePairingFSM::handleConfirmTimeout() {
    Serial.printf("[%s] Confirm timeout after %lu ms\n", TAG, CONFIRM_TIMEOUT_MS);
    
    // Send abort to coordinator
    if (_sendAbort) {
        PairingAbortMessage abort;
        memcpy(abort.sender_mac, _nodeMac, 6);
        abort.reason_code = PairingRejectReason::TIMEOUT;
        abort.offer_token = _pendingOfferToken;
        _sendAbort(_pendingCoordMac, abort);
    }

    completePairing(NodePairingResult::TIMEOUT);
}

void NodePairingFSM::completePairing(NodePairingResult result) {
    uint16_t towerId = (result == NodePairingResult::SUCCESS) ? _assignedTowerId : 0;

    // Clear pending state
    _pendingOfferToken = 0;
    _pendingTowerId = 0;
    memset(_pendingCoordMac, 0, 6);

    if (result == NodePairingResult::SUCCESS) {
        transitionTo(NodePairingState::BOUND);
    } else {
        transitionTo(NodePairingState::UNPAIRED);
    }

    if (_onPairingComplete) {
        _onPairingComplete(result, towerId);
    }
}

bool NodePairingFSM::saveCredentials(const PairingConfirmMessage& confirm) {
    // Build tower ID string
    char towerIdStr[8];
    snprintf(towerIdStr, sizeof(towerIdStr), "%d", confirm.tower_id);

    // We need coord_id and farm_id from the original offer
    // These should have been stored when we received the offer
    // For now, use coordinator MAC as coord_id placeholder
    String coordId = macToString(confirm.coord_mac);
    coordId.replace(":", "");  // Remove colons for compact storage

    // Convert encryption key to hex string if present
    String lmkHex = "";
    if (confirm.hasEncryption()) {
        char hexBuf[33];
        for (int i = 0; i < 16; i++) {
            snprintf(&hexBuf[i * 2], 3, "%02X", confirm.encryption_key[i]);
        }
        hexBuf[32] = '\0';
        lmkHex = String(hexBuf);
    }

    // Save all credentials
    bool ok = true;
    ok &= _config.setTowerId(String(towerIdStr));
    ok &= _config.setCoordMac(confirm.coord_mac);
    
    if (!lmkHex.isEmpty()) {
        ok &= _config.setLmk(lmkHex);
    }

    // Config flags indicate channel in lower nibble (if set)
    uint8_t channel = confirm.config_flags >> 4;
    if (channel > 0 && channel <= 13) {
        ok &= _config.setWifiChannel(channel);
    }

    if (ok) {
        Serial.printf("[%s] Credentials saved to NVS\n", TAG);
    }

    return ok;
}

bool NodePairingFSM::loadExistingCredentials() {
    if (!_config.isPaired()) {
        return false;
    }

    // Load tower ID
    String towerIdStr = _config.getTowerId();
    if (towerIdStr.isEmpty()) {
        return false;
    }

    _assignedTowerId = (uint16_t)towerIdStr.toInt();
    
    // Load coordinator MAC
    uint8_t coordMac[6];
    if (_config.getCoordMac(coordMac)) {
        memcpy(_pendingCoordMac, coordMac, 6);
    }

    Serial.printf("[%s] Loaded credentials: tower_id=%d, coord=%s\n",
                  TAG, _assignedTowerId, _config.getCoordMacString().c_str());

    return true;
}

bool NodePairingFSM::compareMac(const uint8_t* mac1, const uint8_t* mac2) const {
    return memcmp(mac1, mac2, 6) == 0;
}

String NodePairingFSM::macToString(const uint8_t* mac) const {
    char buf[18];
    snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    return String(buf);
}

uint32_t NodePairingFSM::getAdvertisingRemainingMs() const {
    if (_state != NodePairingState::ADVERTISING) {
        return 0;
    }

    uint32_t elapsed = millis() - _advStartMs;
    if (elapsed >= ADV_TIMEOUT_MS) {
        return 0;
    }

    return ADV_TIMEOUT_MS - elapsed;
}
