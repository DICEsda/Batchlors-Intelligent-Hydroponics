#include "LedController.h"
#include "../nodes/NodeRegistry.h"
#include "Logger.h"
#include <algorithm>

LedController::LedController(StatusLed& statusLed)
    : statusLed_(statusLed)
{
}

void LedController::begin() {
    groupCount_ = Pins::RgbLed::NUM_PIXELS / 4;
    groupToNode_.assign(groupCount_, String());
    groupConnected_.assign(groupCount_, false);
    groupFlashDl_.assign(groupCount_, Deadline());
    nodeToGroup_.clear();
}

void LedController::rebuildMapping(const std::vector<NodeInfo>& nodeList) {
    nodeToGroup_.clear();
    std::fill(groupToNode_.begin(), groupToNode_.end(), String());
    std::fill(groupConnected_.begin(), groupConnected_.end(), false);
    for (auto& dl : groupFlashDl_) dl.clear();

    // Sort by towerId for deterministic assignment
    std::vector<NodeInfo> sorted = nodeList;
    std::sort(sorted.begin(), sorted.end(), [](const NodeInfo& a, const NodeInfo& b) {
        return a.towerId < b.towerId;
    });

    uint32_t now = millis();
    int idx = 0;
    for (const auto& n : sorted) {
        if (idx >= groupCount_) break;
        nodeToGroup_[n.towerId] = idx;
        groupToNode_[idx] = n.towerId;
        // Mark as connected if recently seen (within last 6 seconds)
        groupConnected_[idx] = (n.lastSeenMs > 0 && (now - n.lastSeenMs) <= 6000U);
        idx++;
    }
}

int LedController::getGroupIndex(const String& nodeId) const {
    auto it = nodeToGroup_.find(nodeId);
    if (it != nodeToGroup_.end()) return it->second;
    return -1;
}

int LedController::assignGroup(const String& nodeId) {
    // Already assigned?
    int cur = getGroupIndex(nodeId);
    if (cur >= 0) return cur;
    
    // Find first free group slot
    for (int i = 0; i < groupCount_; ++i) {
        if (groupToNode_[i].length() == 0) {
            groupToNode_[i] = nodeId;
            nodeToGroup_[nodeId] = i;
            return i;
        }
    }
    Logger::warn("No free LED group available for node %s", nodeId.c_str());
    return -1;
}

void LedController::setConnected(const String& nodeId, bool connected) {
    int idx = getGroupIndex(nodeId);
    if (idx >= 0) {
        groupConnected_[idx] = connected;
    }
}

bool LedController::isConnected(const String& nodeId) const {
    int idx = getGroupIndex(nodeId);
    if (idx >= 0) {
        return groupConnected_[idx];
    }
    return false;
}

void LedController::flash(const String& nodeId, uint32_t durationMs) {
    int idx = getGroupIndex(nodeId);
    if (idx >= 0) {
        groupFlashDl_[idx].set(durationMs);
    }
}

void LedController::setManualMode(uint8_t r, uint8_t g, uint8_t b, uint32_t timeoutMs) {
    manualMode_ = true;
    manualR_ = r;
    manualG_ = g;
    manualB_ = b;
    if (timeoutMs > 0) { manualTimeoutDl_.set(timeoutMs); } else { manualTimeoutDl_.clear(); }
}

void LedController::clearManualMode() {
    manualMode_ = false;
}

void LedController::update() {
    // Check for manual LED override timeout
    if (manualMode_ && manualTimeoutDl_.expired()) {
        manualMode_ = false;
        Logger::info("Manual LED override timed out");
    }

    for (int g = 0; g < groupCount_; ++g) {
        uint8_t r = 0, gc = 0, b = 0;
        
        if (manualMode_) {
            // Manual override active
            r = manualR_;
            gc = manualG_;
            b = manualB_;
        } else if (groupFlashDl_[g].running()) {
            // Bright green flash (activity) at 50%
            r = 0; gc = 128; b = 0;
        } else if (groupToNode_[g].length() > 0) {
            if (groupConnected_[g]) {
                // Solid green (dim) at 50%
                r = 0; gc = 45; b = 0;
            } else {
                // Red for disconnected/stale node at 50%
                r = 90; gc = 0; b = 0;
            }
        } else {
            r = 0; gc = 0; b = 0;
        }
        
        // Paint the 4-pixel group
        int base = g * 4;
        for (int k = 0; k < 4; k++) {
            statusLed_.setPixel(base + k, r, gc, b);
        }
    }
    statusLed_.show();
}

String LedController::getNodeAtGroup(int groupIndex) const {
    if (groupIndex >= 0 && groupIndex < groupCount_) {
        return groupToNode_[groupIndex];
    }
    return String();
}

std::vector<String> LedController::getConnectedNodes() const {
    std::vector<String> result;
    for (int i = 0; i < groupCount_; ++i) {
        if (groupToNode_[i].length() > 0 && groupConnected_[i]) {
            result.push_back(groupToNode_[i]);
        }
    }
    return result;
}
