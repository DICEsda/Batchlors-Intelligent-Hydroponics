#include "TowerRegistry.h"
#include "../utils/Logger.h"

const char* TowerRegistry::STORAGE_NAMESPACE = "towers";

TowerRegistry::TowerRegistry()
    : prefsInitialized(false)
    , pairingActive(false) {
}

TowerRegistry::~TowerRegistry() {
    prefs.end();
}

bool TowerRegistry::begin() {
    prefsInitialized = prefs.begin(STORAGE_NAMESPACE, false);
    if (!prefsInitialized) {
        Logger::info("No saved tower data found - starting with empty registry");
        Logger::info("(This is normal on first boot or after flash erase)");
        return true; // Continue anyway, just without persistence
    }
    
    loadFromStorage();
    Logger::info("Tower registry initialized with %d towers", towers.size());
    return true;
}

void TowerRegistry::loop() {
    uint32_t now = millis();
    
    // Check pairing timeout
    if (pairingActive && pairingDl.expired()) {
        pairingActive = false;
        Logger::info("Pairing window closed");
    }
    
    // Periodically clean up stale towers
    static uint32_t lastCleanup = 0;
    if (now - lastCleanup >= 60000) { // Every minute
        cleanupStaleTowers();
        lastCleanup = now;
    }
}

bool TowerRegistry::registerTower(const String& towerId, const String& lightId) {
    if (towers.find(towerId) != towers.end()) {
        Logger::warning("Tower %s already registered", towerId.c_str());
        return false;
    }
    
    TowerInfo info;
    info.towerId = towerId;
    info.lightId = lightId;
    info.lastDuty = 0;
    info.lastSeenMs = millis();
    info.temperature = 0;
    info.isDerated = false;
    info.derationLevel = 100;
    
    towers[towerId] = info;
    lightToTower[lightId] = towerId;
    
    saveToStorage();
    Logger::info("Registered tower %s with light %s", towerId.c_str(), lightId.c_str());
    // Notify any listener about registration
    if (towerRegisteredCallback) {
        towerRegisteredCallback(towerId, lightId);
    }
    return true;
}

bool TowerRegistry::unregisterTower(const String& towerId) {
    auto it = towers.find(towerId);
    if (it == towers.end()) {
        return false;
    }
    
    String lightId = it->second.lightId;
    towers.erase(it);
    lightToTower.erase(lightId);
    
    saveToStorage();
    Logger::info("Unregistered tower %s", towerId.c_str());
    return true;
}

void TowerRegistry::clearAllTowers() {
    towers.clear();
    lightToTower.clear();
    saveToStorage();
    Logger::info("Cleared all towers from registry");
}

void TowerRegistry::startPairing(uint32_t durationMs) {
    pairingActive = true;
    pairingDl.set(durationMs);
    Logger::info("Started pairing window for %d ms", durationMs);
}

void TowerRegistry::stopPairing() {
    pairingActive = false;
    pairingDl.clear();
    Logger::info("Pairing window closed manually");
}

void TowerRegistry::setTowerRegisteredCallback(std::function<void(const String& towerId, const String& lightId)> callback) {
    towerRegisteredCallback = callback;
}

bool TowerRegistry::isPairingActive() const {
    return pairingActive && pairingDl.running();
}

bool TowerRegistry::processPairingRequest(const uint8_t* mac, const String& towerId) {
    if (!isPairingActive()) {
        Logger::warning("Rejected pairing request from %s: pairing not active", towerId.c_str());
        return false;
    }
    // Generate a stable light ID from MAC last 3 bytes
    char lightIdBuf[16];
    snprintf(lightIdBuf, sizeof(lightIdBuf), "L%02X%02X%02X", mac[3], mac[4], mac[5]);
    String lightId(lightIdBuf);
    
    if (registerTower(towerId, lightId)) {
        pairingActive = false; // Close window after successful pairing
        return true;
    }
    return false;
}

void TowerRegistry::updateTowerStatus(const String& towerId, uint8_t duty) {
    auto it = towers.find(towerId);
    if (it != towers.end()) {
        it->second.lastDuty = duty;
        it->second.lastSeenMs = millis();
    }
}

TowerInfo TowerRegistry::getTowerStatus(const String& towerId) const {
    auto it = towers.find(towerId);
    return it != towers.end() ? it->second : TowerInfo();
}

std::vector<TowerInfo> TowerRegistry::getAllTowers() const {
    std::vector<TowerInfo> result;
    result.reserve(towers.size());
    for (const auto& pair : towers) {
        result.push_back(pair.second);
    }
    return result;
}

String TowerRegistry::getTowerForLight(const String& lightId) const {
    auto it = lightToTower.find(lightId);
    return it != lightToTower.end() ? it->second : String();
}

String TowerRegistry::getLightForTower(const String& towerId) const {
    auto it = towers.find(towerId);
    return it != towers.end() ? it->second.lightId : String();
}

std::vector<String> TowerRegistry::getAllTowerMacs() const {
    std::vector<String> macs;
    macs.reserve(towers.size());
    for (const auto& pair : towers) {
        macs.push_back(pair.first); // towerId is the MAC address
    }
    return macs;
}

void TowerRegistry::loadFromStorage() {
    towers.clear();
    lightToTower.clear();
    
    size_t towerCount = prefs.getUInt("count", 0);
    for (size_t i = 0; i < towerCount; i++) {
        String key = "tower" + String(i);
        String data = prefs.getString(key.c_str());
        
        // Parse tower data (format: "towerId,lightId,lastDuty")
        int comma1 = data.indexOf(',');
        int comma2 = data.indexOf(',', comma1 + 1);
        if (comma1 > 0 && comma2 > comma1) {
            String towerId = data.substring(0, comma1);
            String lightId = data.substring(comma1 + 1, comma2);
            uint8_t lastDuty = data.substring(comma2 + 1).toInt();
            
            TowerInfo info;
            info.towerId = towerId;
            info.lightId = lightId;
            info.lastDuty = lastDuty;
            info.lastSeenMs = 0; // Mark as not seen in this session
            
            towers[towerId] = info;
            lightToTower[lightId] = towerId;
        }
    }
    
    // Also try to load legacy "node" format for backward compatibility
    if (towers.empty()) {
        size_t nodeCount = prefs.getUInt("count", 0);
        for (size_t i = 0; i < nodeCount; i++) {
            String key = "node" + String(i);
            String data = prefs.getString(key.c_str());
            
            if (data.length() == 0) continue;
            
            int comma1 = data.indexOf(',');
            int comma2 = data.indexOf(',', comma1 + 1);
            if (comma1 > 0 && comma2 > comma1) {
                String towerId = data.substring(0, comma1);
                String lightId = data.substring(comma1 + 1, comma2);
                uint8_t lastDuty = data.substring(comma2 + 1).toInt();
                
                TowerInfo info;
                info.towerId = towerId;
                info.lightId = lightId;
                info.lastDuty = lastDuty;
                info.lastSeenMs = 0;
                
                towers[towerId] = info;
                lightToTower[lightId] = towerId;
            }
        }
        
        // If we loaded legacy data, save in new format
        if (!towers.empty()) {
            Logger::info("Migrated %d towers from legacy storage format", towers.size());
            saveToStorage();
        }
    }
}

void TowerRegistry::saveToStorage() {
    if (!prefsInitialized) {
        return; // Skip saving if preferences not available
    }
    prefs.clear();
    prefs.putUInt("count", towers.size());
    
    size_t i = 0;
    for (const auto& pair : towers) {
        const TowerInfo& info = pair.second;
        String data = info.towerId + "," + info.lightId + "," + String(info.lastDuty);
        prefs.putString(("tower" + String(i)).c_str(), data);
        i++;
    }
}

void TowerRegistry::cleanupStaleTowers() {
    uint32_t now = millis();
    std::vector<String> staleTowers;
    staleTowers.reserve(4); // Pre-allocate for typical case
    
    for (const auto& pair : towers) {
        // Skip towers that have never been seen (lastSeenMs == 0)
        if (pair.second.lastSeenMs > 0 && now - pair.second.lastSeenMs >= TOWER_TIMEOUT_MS) {
            staleTowers.push_back(pair.first);
        }
    }
    
    for (const String& towerId : staleTowers) {
        Logger::warning("Removing stale tower %s", towerId.c_str());
        unregisterTower(towerId);
    }
}
