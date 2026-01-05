#pragma once

#include <Arduino.h>
#include <vector>
#include <functional>
#include "EspNowMessage.h"

// Maximum number of discovered nodes to track
constexpr size_t MAX_DISCOVERED_NODES = 32;

// TTL for discovered nodes (30 seconds as per spec)
constexpr uint32_t DISCOVERY_TTL_MS = 30000;

/**
 * State of a discovered node in the pairing process
 */
enum class DiscoveryState : uint8_t {
    DISCOVERED = 0,     // Seen but not acted upon
    PENDING = 1,        // Awaiting frontend decision
    OFFER_SENT = 2,     // PAIRING_OFFER sent, awaiting accept
    BINDING = 3,        // PAIRING_ACCEPT received, confirming
    BOUND = 4,          // Successfully paired
    REJECTED = 5,       // User rejected
    FAILED = 6          // Binding failed
};

/**
 * Information about a discovered node
 */
struct DiscoveredNode {
    uint8_t mac[6];                     // Node MAC address
    DeviceType device_type;             // Tower, sensor, etc.
    uint32_t firmware_version;          // Packed firmware version
    uint16_t capabilities;              // Capability flags
    uint32_t last_nonce;                // Last seen nonce
    uint16_t last_sequence;             // Last sequence number
    int8_t rssi;                        // Signal strength (from coordinator's perspective)
    uint32_t first_seen_ms;             // Timestamp first discovered
    uint32_t last_seen_ms;              // Timestamp last advertisement
    DiscoveryState state;               // Current state in pairing process
    uint32_t offer_token;               // Token if offer sent
    
    // Constructor
    DiscoveredNode() :
        device_type(DeviceType::TOWER),
        firmware_version(0),
        capabilities(0),
        last_nonce(0),
        last_sequence(0),
        rssi(0),
        first_seen_ms(0),
        last_seen_ms(0),
        state(DiscoveryState::DISCOVERED),
        offer_token(0) {
        memset(mac, 0, 6);
    }
    
    // Check if this node has expired (TTL exceeded)
    bool isExpired(uint32_t now_ms) const {
        // BOUND nodes don't expire
        if (state == DiscoveryState::BOUND) return false;
        return (now_ms - last_seen_ms) > DISCOVERY_TTL_MS;
    }
    
    // Get MAC as string
    String macString() const {
        char buf[18];
        snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
                 mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
        return String(buf);
    }
    
    // Get firmware version as string (major.minor.patch)
    String firmwareString() const {
        uint8_t major = (firmware_version >> 16) & 0xFF;
        uint8_t minor = (firmware_version >> 8) & 0xFF;
        uint8_t patch = firmware_version & 0xFF;
        char buf[16];
        snprintf(buf, sizeof(buf), "%d.%d.%d", major, minor, patch);
        return String(buf);
    }
};

/**
 * Callback types for discovery events
 */
using NodeDiscoveredCallback = std::function<void(const DiscoveredNode& node)>;
using NodeUpdatedCallback = std::function<void(const DiscoveredNode& node)>;
using NodeExpiredCallback = std::function<void(const DiscoveredNode& node)>;
using NodeStateChangedCallback = std::function<void(const DiscoveredNode& node, DiscoveryState oldState, DiscoveryState newState)>;

/**
 * DiscoveryManager: Tracks discovered nodes during permit-join window
 * 
 * Responsibilities:
 * - Maintain in-memory table of discovered nodes (max 32)
 * - Handle TTL expiration (30s for non-bound nodes)
 * - Deduplicate advertisements by nonce
 * - Publish discovery events for MQTT relay
 */
class DiscoveryManager {
public:
    DiscoveryManager();
    ~DiscoveryManager() = default;
    
    /**
     * Initialize the discovery manager
     */
    void begin();
    
    /**
     * Process tick - handle TTL expiration
     * Call this from main loop
     */
    void tick();
    
    /**
     * Process a received PAIRING_ADVERTISEMENT message
     * @param msg The advertisement message
     * @param rssi Signal strength (measured by coordinator)
     * @return true if this was a new or updated node
     */
    bool onAdvertisementReceived(const PairingAdvertisementMessage& msg, int8_t rssi);
    
    /**
     * Find a discovered node by MAC address
     * @param mac 6-byte MAC address
     * @return Pointer to node if found, nullptr otherwise
     */
    DiscoveredNode* findByMac(const uint8_t* mac);
    const DiscoveredNode* findByMac(const uint8_t* mac) const;
    
    /**
     * Find a discovered node by MAC string
     * @param macStr MAC address as string (e.g., "AA:BB:CC:DD:EE:FF")
     * @return Pointer to node if found, nullptr otherwise
     */
    DiscoveredNode* findByMacString(const String& macStr);
    const DiscoveredNode* findByMacString(const String& macStr) const;
    
    /**
     * Update a node's state
     * @param mac 6-byte MAC address
     * @param newState New discovery state
     * @return true if node found and updated
     */
    bool updateNodeState(const uint8_t* mac, DiscoveryState newState);
    
    /**
     * Set offer token for a node (when PAIRING_OFFER is sent)
     * @param mac 6-byte MAC address
     * @param token Offer token
     * @return true if node found and updated
     */
    bool setOfferToken(const uint8_t* mac, uint32_t token);
    
    /**
     * Get all discovered nodes (excluding expired)
     */
    std::vector<const DiscoveredNode*> getDiscoveredNodes() const;
    
    /**
     * Get discovered nodes by state
     */
    std::vector<const DiscoveredNode*> getNodesByState(DiscoveryState state) const;
    
    /**
     * Get count of discovered nodes (excluding expired)
     */
    size_t getDiscoveredCount() const;
    
    /**
     * Check if we can accept more discovered nodes
     */
    bool hasCapacity() const;
    
    /**
     * Clear expired nodes from the table
     * @return Number of nodes removed
     */
    size_t clearExpired();
    
    /**
     * Clear all discovered nodes (except BOUND ones)
     */
    void clearAll();
    
    /**
     * Clear a specific node by MAC
     * @param mac 6-byte MAC address
     * @return true if node found and removed
     */
    bool removeNode(const uint8_t* mac);
    
    // Event callbacks
    void setNodeDiscoveredCallback(NodeDiscoveredCallback cb) { onNodeDiscovered = cb; }
    void setNodeUpdatedCallback(NodeUpdatedCallback cb) { onNodeUpdated = cb; }
    void setNodeExpiredCallback(NodeExpiredCallback cb) { onNodeExpired = cb; }
    void setNodeStateChangedCallback(NodeStateChangedCallback cb) { onNodeStateChanged = cb; }
    
private:
    std::vector<DiscoveredNode> nodes;
    
    // Callbacks
    NodeDiscoveredCallback onNodeDiscovered;
    NodeUpdatedCallback onNodeUpdated;
    NodeExpiredCallback onNodeExpired;
    NodeStateChangedCallback onNodeStateChanged;
    
    // Internal helpers
    int findNodeIndex(const uint8_t* mac) const;
    bool compareMac(const uint8_t* mac1, const uint8_t* mac2) const;
};
