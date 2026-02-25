#pragma once

#include <Arduino.h>
#include <vector>
#include <map>

struct TowerInfo {
    String towerId;
    String lightId;
    uint8_t lastDuty;
    uint32_t lastSeenMs;
    float temperature;      // Current temperature in Celsius
    bool isDerated;         // Whether the tower is currently derated
    uint8_t derationLevel; // Current deration level (100 = no deration)
};

struct ZoneMapping {
    String zoneId;
    std::vector<String> lightIds;
};

struct ThermalEvent {
    String towerId;
    float temperature;
    bool isDerated;
    uint8_t derationLevel;
    uint32_t timestampMs;
};

struct SensorData {
    String towerId;
    float temperature;
    uint32_t timestampMs;
};

struct ReservoirSensorSnapshot {
    float lightLux = 0.0f;
    float tempC = 0.0f;
    int16_t wifiRssi = -127;
    bool wifiConnected = false;
    uint32_t timestampMs = 0;
};

// Backward compatibility aliases
using CoordinatorSensorSnapshot = ReservoirSensorSnapshot;
using NodeInfo = TowerInfo;  // NodeInfo is an alias for TowerInfo

// Forward declaration for TowerStatusMessage from shared messages
struct NodeStatusMessage;
using TowerStatusMessage = NodeStatusMessage;  // TowerStatusMessage is an alias for NodeStatusMessage

