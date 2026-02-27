#pragma once

#include <Arduino.h>
#include <map>
#include <vector>
#include "../comm/EspNow.h"
#include "../comm/Mqtt.h"
#include "../towers/TowerRegistry.h"
#include "../zones/ZoneControl.h"
#include "../input/ButtonControl.h"
#include "../sensors/ThermalControl.h"
#include "../utils/StatusLed.h"
#include "../../shared/src/utils/SafeTimer.h"

class WifiManager;
class AmbientLightSensor;
struct NodeStatusMessage;
struct NodeThermalData;

class Reservoir {
public:
    Reservoir();
    ~Reservoir();

    bool begin();
    void loop();

private:
    EspNow* espNow;
    Mqtt* mqtt;
    TowerRegistry* towers;
    ZoneControl* zones;
    ButtonControl* buttons;
    ThermalControl* thermal;
    WifiManager* wifi;
    AmbientLightSensor* ambientLight;
    // Onboard status LED helper
    StatusLed statusLed;
    struct BootStatusEntry {
        String name;
        bool ok;
        String detail;
    };
    std::vector<BootStatusEntry> bootStatus;

    struct TowerTelemetrySnapshot {
        uint8_t avgR = 0;
        uint8_t avgG = 0;
        uint8_t avgB = 0;
        uint8_t avgW = 0;
        float temperatureC = 0.0f;
        bool buttonPressed = false;
        uint32_t lastUpdateMs = 0;
    };
    std::map<String, TowerTelemetrySnapshot> towerTelemetry;
    ReservoirSensorSnapshot reservoirSensors;
    bool zoneOccupiedState = false;
    uint32_t lastSensorSampleMs = 0;
    uint32_t lastSerialPrintMs = 0;

    // Per-tower LED group mapping (4 pixels per group)
    std::map<String, int> towerToGroup;         // towerId -> group index (0..groups-1)
    std::vector<String> groupToTower;           // size = NUM_PIXELS/4
    std::vector<bool> groupConnected;           // true if connected
    std::vector<Deadline> groupFlashDl;          // activity flash deadlines

    // Helpers for LED mapping and updates
    void rebuildLedMappingFromRegistry();
    int getGroupIndexForTower(const String& towerId);
    int assignGroupForTower(const String& towerId);
    void updateLeds();
    void flashLedForTower(const String& towerId, uint32_t durationMs);
    void logConnectedTowers();
    void checkStaleConnections();
    void sendHealthPings();

    // Button/flash state
    bool buttonDown = false;
    bool longPressActive = false;
    
    // Helper to publish important logs to MQTT
    void publishLog(const String& message, const String& level = "INFO", const String& tag = "");
    bool flashAllActive = false;
    bool flashOn = false;
    uint32_t lastFlashTick = 0;
    uint32_t buttonPressedAt = 0;
    
    // Manual LED control
    bool manualLedMode = false;
    uint8_t manualR = 0;
    uint8_t manualG = 0;
    uint8_t manualB = 0;
    Deadline manualLedTimeoutDl;
    
    // Zone presence mode
    bool zonePresenceMode = true;  // When enabled, towers glow green when presence detected
    bool lastPresenceState = false;
    
    // Light control mode
    bool lightControlEnabled = true;  // When enabled, listen for frontend light commands

    void startFlashAll();
    void stopFlashAll();
    void flashAllTick(uint32_t now);
    
    // Event handlers
    void onThermalEvent(const String& towerId, const NodeThermalData& data);
    void onButtonEvent(const String& buttonId, bool pressed);
    void handleTowerMessage(const String& towerId, const uint8_t* data, size_t len);
    void triggerTowerWaveTest();
    void handleMqttCommand(const String& topic, const String& payload);
    void startPairingWindow(uint32_t durationMs, const char* reason);
    void updateTowerTelemetryCache(const String& towerId, const NodeStatusMessage& statusMsg);
    void refreshReservoirSensors();
    void printSerialTelemetry();
    void recordBootStatus(const char* name, bool ok, const String& detail);
    void printBootSummary();
    void handleSerialCommands();
};

// Backward compatibility alias
using Coordinator = Reservoir;
