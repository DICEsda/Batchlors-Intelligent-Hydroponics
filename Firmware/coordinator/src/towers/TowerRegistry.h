#pragma once

#include <Arduino.h>
#include <map>
#include <vector>
#include <functional>
#include <Preferences.h>
#include "../Models.h"
#include "../../shared/src/utils/SafeTimer.h"

class TowerRegistry {
public:
    TowerRegistry();
    ~TowerRegistry();

    bool begin();
    void loop();

    // Tower registration
    bool registerTower(const String& towerId, const String& lightId);
    bool unregisterTower(const String& towerId);
    void clearAllTowers();
    
    // Pairing
    void startPairing(uint32_t durationMs = 30000);
    void stopPairing();
    bool isPairingActive() const;
    bool processPairingRequest(const uint8_t* mac, const String& towerId);
    // Notification callback when a tower is successfully registered
    void setTowerRegisteredCallback(std::function<void(const String& towerId, const String& lightId)> callback);
    
    // Tower status
    void updateTowerStatus(const String& towerId, uint8_t duty);
    TowerInfo getTowerStatus(const String& towerId) const;
    std::vector<TowerInfo> getAllTowers() const;
    
    // Tower-Light mapping
    String getTowerForLight(const String& lightId) const;
    String getLightForTower(const String& towerId) const;
    
    // Get all stored tower MAC addresses (for re-pairing on boot)
    std::vector<String> getAllTowerMacs() const;

    // Backward compatibility aliases
    inline bool registerNode(const String& nodeId, const String& lightId) { return registerTower(nodeId, lightId); }
    inline bool unregisterNode(const String& nodeId) { return unregisterTower(nodeId); }
    inline void clearAllNodes() { clearAllTowers(); }
    inline void updateNodeStatus(const String& nodeId, uint8_t duty) { updateTowerStatus(nodeId, duty); }
    inline TowerInfo getNodeStatus(const String& nodeId) const { return getTowerStatus(nodeId); }
    inline std::vector<TowerInfo> getAllNodes() const { return getAllTowers(); }
    inline String getNodeForLight(const String& lightId) const { return getTowerForLight(lightId); }
    inline String getLightForNode(const String& nodeId) const { return getLightForTower(nodeId); }
    inline std::vector<String> getAllNodeMacs() const { return getAllTowerMacs(); }
    inline void setNodeRegisteredCallback(std::function<void(const String& nodeId, const String& lightId)> cb) { setTowerRegisteredCallback(cb); }

private:
    std::map<String, TowerInfo> towers;
    std::map<String, String> lightToTower;  // lightId -> towerId
    Preferences prefs;
    bool prefsInitialized;
    
    bool pairingActive;
    Deadline pairingDl;
    
    void loadFromStorage();
    void saveToStorage();
    void cleanupStaleTowers();
    std::function<void(const String& towerId, const String& lightId)> towerRegisteredCallback = nullptr;
    
    static const char* STORAGE_NAMESPACE;
    static const uint32_t TOWER_TIMEOUT_MS = 300000; // 5 minutes
};

// Backward compatibility alias
using NodeRegistry = TowerRegistry;
