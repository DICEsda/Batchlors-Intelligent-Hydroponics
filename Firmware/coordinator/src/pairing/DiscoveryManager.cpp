#include "DiscoveryManager.h"

DiscoveryManager::DiscoveryManager() :
    onNodeDiscovered(nullptr),
    onNodeUpdated(nullptr),
    onNodeExpired(nullptr),
    onNodeStateChanged(nullptr) {
    nodes.reserve(MAX_DISCOVERED_NODES);
}

void DiscoveryManager::begin() {
    nodes.clear();
    Serial.println("[DiscoveryManager] Initialized");
}

void DiscoveryManager::tick() {
    // Remove expired nodes
    size_t removed = clearExpired();
    if (removed > 0) {
        Serial.printf("[DiscoveryManager] Expired %zu nodes\n", removed);
    }
}

bool DiscoveryManager::onAdvertisementReceived(const PairingAdvertisementMessage& msg, int8_t rssi) {
    uint32_t now = millis();
    
    // Check if we already know this node
    int idx = findNodeIndex(msg.node_mac);
    
    if (idx >= 0) {
        // Existing node - check for duplicate nonce
        DiscoveredNode& node = nodes[idx];
        
        // Skip if same nonce (already processed this advertisement cycle)
        if (node.last_nonce == msg.nonce && node.last_sequence == msg.sequence_num) {
            return false;
        }
        
        // Update existing node
        node.last_nonce = msg.nonce;
        node.last_sequence = msg.sequence_num;
        node.last_seen_ms = now;
        node.rssi = rssi;
        
        // Update capabilities/firmware if changed (node may have been updated)
        node.firmware_version = msg.firmware_version;
        node.capabilities = msg.capabilities;
        node.device_type = msg.device_type;
        
        Serial.printf("[DiscoveryManager] Updated node %s, RSSI: %d, seq: %u\n",
                      node.macString().c_str(), rssi, msg.sequence_num);
        
        if (onNodeUpdated) {
            onNodeUpdated(node);
        }
        return true;
    }
    
    // New node - check capacity
    if (!hasCapacity()) {
        Serial.println("[DiscoveryManager] WARN: Capacity full, ignoring new node");
        return false;
    }
    
    // Add new discovered node
    DiscoveredNode newNode;
    memcpy(newNode.mac, msg.node_mac, 6);
    newNode.device_type = msg.device_type;
    newNode.firmware_version = msg.firmware_version;
    newNode.capabilities = msg.capabilities;
    newNode.last_nonce = msg.nonce;
    newNode.last_sequence = msg.sequence_num;
    newNode.rssi = rssi;
    newNode.first_seen_ms = now;
    newNode.last_seen_ms = now;
    newNode.state = DiscoveryState::DISCOVERED;
    newNode.offer_token = 0;
    
    nodes.push_back(newNode);
    
    Serial.printf("[DiscoveryManager] NEW node discovered: %s, type: %d, RSSI: %d, caps: 0x%04X\n",
                  newNode.macString().c_str(), 
                  static_cast<int>(newNode.device_type),
                  rssi,
                  newNode.capabilities);
    
    if (onNodeDiscovered) {
        onNodeDiscovered(newNode);
    }
    return true;
}

DiscoveredNode* DiscoveryManager::findByMac(const uint8_t* mac) {
    int idx = findNodeIndex(mac);
    if (idx >= 0) {
        return &nodes[idx];
    }
    return nullptr;
}

const DiscoveredNode* DiscoveryManager::findByMac(const uint8_t* mac) const {
    int idx = findNodeIndex(mac);
    if (idx >= 0) {
        return &nodes[idx];
    }
    return nullptr;
}

DiscoveredNode* DiscoveryManager::findByMacString(const String& macStr) {
    uint8_t mac[6];
    if (!stringToMac(macStr, mac)) {
        return nullptr;
    }
    return findByMac(mac);
}

const DiscoveredNode* DiscoveryManager::findByMacString(const String& macStr) const {
    uint8_t mac[6];
    if (!stringToMac(macStr, mac)) {
        return nullptr;
    }
    return findByMac(mac);
}

bool DiscoveryManager::updateNodeState(const uint8_t* mac, DiscoveryState newState) {
    DiscoveredNode* node = findByMac(mac);
    if (!node) {
        return false;
    }
    
    DiscoveryState oldState = node->state;
    if (oldState == newState) {
        return true; // No change needed
    }
    
    node->state = newState;
    
    Serial.printf("[DiscoveryManager] Node %s state: %d -> %d\n",
                  node->macString().c_str(),
                  static_cast<int>(oldState),
                  static_cast<int>(newState));
    
    if (onNodeStateChanged) {
        onNodeStateChanged(*node, oldState, newState);
    }
    return true;
}

bool DiscoveryManager::setOfferToken(const uint8_t* mac, uint32_t token) {
    DiscoveredNode* node = findByMac(mac);
    if (!node) {
        return false;
    }
    
    node->offer_token = token;
    Serial.printf("[DiscoveryManager] Set offer token for %s: 0x%08X\n",
                  node->macString().c_str(), token);
    return true;
}

std::vector<const DiscoveredNode*> DiscoveryManager::getDiscoveredNodes() const {
    std::vector<const DiscoveredNode*> result;
    uint32_t now = millis();
    
    for (const auto& node : nodes) {
        if (!node.isExpired(now)) {
            result.push_back(&node);
        }
    }
    return result;
}

std::vector<const DiscoveredNode*> DiscoveryManager::getNodesByState(DiscoveryState state) const {
    std::vector<const DiscoveredNode*> result;
    uint32_t now = millis();
    
    for (const auto& node : nodes) {
        if (node.state == state && !node.isExpired(now)) {
            result.push_back(&node);
        }
    }
    return result;
}

size_t DiscoveryManager::getDiscoveredCount() const {
    size_t count = 0;
    uint32_t now = millis();
    
    for (const auto& node : nodes) {
        if (!node.isExpired(now)) {
            count++;
        }
    }
    return count;
}

bool DiscoveryManager::hasCapacity() const {
    return getDiscoveredCount() < MAX_DISCOVERED_NODES;
}

size_t DiscoveryManager::clearExpired() {
    uint32_t now = millis();
    size_t removed = 0;
    
    for (auto it = nodes.begin(); it != nodes.end(); ) {
        if (it->isExpired(now)) {
            Serial.printf("[DiscoveryManager] Node %s expired\n", it->macString().c_str());
            
            if (onNodeExpired) {
                onNodeExpired(*it);
            }
            
            it = nodes.erase(it);
            removed++;
        } else {
            ++it;
        }
    }
    return removed;
}

void DiscoveryManager::clearAll() {
    // Keep BOUND nodes, clear everything else
    for (auto it = nodes.begin(); it != nodes.end(); ) {
        if (it->state != DiscoveryState::BOUND) {
            Serial.printf("[DiscoveryManager] Cleared node %s\n", it->macString().c_str());
            it = nodes.erase(it);
        } else {
            ++it;
        }
    }
}

bool DiscoveryManager::removeNode(const uint8_t* mac) {
    int idx = findNodeIndex(mac);
    if (idx >= 0) {
        Serial.printf("[DiscoveryManager] Removed node %s\n", nodes[idx].macString().c_str());
        nodes.erase(nodes.begin() + idx);
        return true;
    }
    return false;
}

int DiscoveryManager::findNodeIndex(const uint8_t* mac) const {
    for (size_t i = 0; i < nodes.size(); i++) {
        if (compareMac(nodes[i].mac, mac)) {
            return static_cast<int>(i);
        }
    }
    return -1;
}

bool DiscoveryManager::compareMac(const uint8_t* mac1, const uint8_t* mac2) const {
    return memcmp(mac1, mac2, 6) == 0;
}
