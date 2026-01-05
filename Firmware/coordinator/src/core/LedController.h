#pragma once

#include <Arduino.h>
#include <map>
#include <vector>
#include <functional>
#include "../utils/StatusLed.h"
#include "../config/PinConfig.h"

// Forward declaration
struct NodeInfo;

/**
 * @brief Manages LED visualization for node status on the coordinator
 * 
 * Extracted from Coordinator to handle single responsibility:
 * - Maps nodes to LED pixel groups (4 pixels per node)
 * - Shows connection state (green=connected, red=disconnected)
 * - Flashes on activity (bright green pulse)
 * - Supports manual override mode
 * 
 * Each node is assigned a "group" of 4 consecutive pixels.
 * Groups are assigned deterministically by sorted nodeId.
 */
class LedController {
public:
    /**
     * @brief Construct a new LedController
     * @param statusLed Reference to the StatusLed driver
     */
    explicit LedController(StatusLed& statusLed);
    ~LedController() = default;

    /**
     * @brief Initialize LED groups based on pixel count
     * Must be called before using other methods.
     */
    void begin();

    /**
     * @brief Rebuild LED mapping from node registry
     * @param nodeList List of all registered nodes (sorted by nodeId)
     * 
     * Call this when nodes are added/removed or at startup.
     */
    void rebuildMapping(const std::vector<NodeInfo>& nodeList);

    /**
     * @brief Update all LED pixels based on current state
     * Call this in loop() to update display.
     */
    void update();

    /**
     * @brief Get the group index for a node
     * @param nodeId The node's ID (MAC string)
     * @return Group index (0..N-1) or -1 if not assigned
     */
    int getGroupIndex(const String& nodeId) const;

    /**
     * @brief Assign a group to a node (finds first free slot)
     * @param nodeId The node's ID (MAC string)
     * @return Group index or -1 if no free slot
     */
    int assignGroup(const String& nodeId);

    /**
     * @brief Mark a node as connected (recently seen)
     * @param nodeId The node's ID
     * @param connected true if connected/active
     */
    void setConnected(const String& nodeId, bool connected);

    /**
     * @brief Check if a node is marked as connected
     * @param nodeId The node's ID
     * @return true if connected
     */
    bool isConnected(const String& nodeId) const;

    /**
     * @brief Trigger a brief activity flash for a node
     * @param nodeId The node's ID
     * @param durationMs Flash duration in milliseconds
     */
    void flash(const String& nodeId, uint32_t durationMs);

    /**
     * @brief Enable manual LED override (all pixels same color)
     * @param r Red (0-255)
     * @param g Green (0-255)
     * @param b Blue (0-255)
     * @param timeoutMs Auto-disable after this duration (0 = no timeout)
     */
    void setManualMode(uint8_t r, uint8_t g, uint8_t b, uint32_t timeoutMs = 0);

    /**
     * @brief Disable manual LED override
     */
    void clearManualMode();

    /**
     * @brief Check if manual mode is active
     */
    bool isManualMode() const { return manualMode_; }

    /**
     * @brief Get the node ID assigned to a group
     * @param groupIndex The group index
     * @return Node ID or empty string if unassigned
     */
    String getNodeAtGroup(int groupIndex) const;

    /**
     * @brief Get total number of groups available
     */
    int groupCount() const { return groupCount_; }

    /**
     * @brief Get list of connected node IDs
     */
    std::vector<String> getConnectedNodes() const;

private:
    StatusLed& statusLed_;
    int groupCount_ = 0;

    // Node-to-group mapping
    std::map<String, int> nodeToGroup_;
    std::vector<String> groupToNode_;
    std::vector<bool> groupConnected_;
    std::vector<uint32_t> groupFlashUntilMs_;

    // Manual override state
    bool manualMode_ = false;
    uint8_t manualR_ = 0;
    uint8_t manualG_ = 0;
    uint8_t manualB_ = 0;
    uint32_t manualTimeoutMs_ = 0;
};
