#pragma once

#include <Arduino.h>
#include <map>
#include <vector>
#include "../comm/EspNow.h"
#include "../comm/Mqtt.h"
#include "../sensors/MmWave.h"
#include "../nodes/NodeRegistry.h"
#include "../zones/ZoneControl.h"
#include "../input/ButtonControl.h"
#include "../sensors/ThermalControl.h"
#include "../utils/StatusLed.h"
#include "BootManager.h"
#include "SerialConsole.h"
#include "LedController.h"

class WifiManager;
class AmbientLightSensor;
struct NodeStatusMessage;

class Coordinator {
public:
    Coordinator();
    ~Coordinator();

    bool begin();
    void loop();

private:
    EspNow* espNow;
    Mqtt* mqtt;
    MmWave* mmWave;
    NodeRegistry* nodes;
    ZoneControl* zones;
    ButtonControl* buttons;
    ThermalControl* thermal;
    WifiManager* wifi;
    AmbientLightSensor* ambientLight;
    // Onboard status LED helper
    StatusLed statusLed;
    
    // Boot status tracking (extracted to BootManager)
    BootManager bootManager;
    
    // Serial command handler (extracted to SerialConsole)
    SerialConsole serialConsole;
    
    // LED visualization (extracted to LedController)
    LedController* ledController;

    struct NodeTelemetrySnapshot {
        uint8_t avgR = 0;
        uint8_t avgG = 0;
        uint8_t avgB = 0;
        uint8_t avgW = 0;
        float temperatureC = 0.0f;
        bool buttonPressed = false;
        uint32_t lastUpdateMs = 0;
    };
    std::map<String, NodeTelemetrySnapshot> nodeTelemetry;
    CoordinatorSensorSnapshot coordinatorSensors;
    
    // Tower ID to MAC address mapping for ESP-NOW command routing
    // Key: tower_id (e.g., "TCCDDEEFF"), Value: MAC string (e.g., "AA:BB:CC:DD:EE:FF")
    std::map<String, String> towerIdToMac;
    MmWaveEvent lastMmWaveEvent;
    bool haveMmWaveSample = false;
    bool zoneOccupiedState = false;
    uint32_t lastSensorSampleMs = 0;
    uint32_t lastSerialPrintMs = 0;

    // Helpers for node connection status
    void logConnectedNodes();
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
    
    // Zone presence mode
    bool zonePresenceMode = true;  // When enabled, nodes glow green when presence detected
    bool lastPresenceState = false;
    
    // Light control mode
    bool lightControlEnabled = true;  // When enabled, listen for frontend light commands

    void startFlashAll();
    void stopFlashAll();
    void flashAllTick(uint32_t now);
    
    // Event handlers
    void onMmWaveEvent(const MmWaveEvent& event);
    void onThermalEvent(const String& nodeId, const NodeThermalData& data);
    void onButtonEvent(const String& buttonId, bool pressed);
    void handleNodeMessage(const String& nodeId, const uint8_t* data, size_t len);
    void triggerNodeWaveTest();
    void handleMqttCommand(const String& topic, const String& payload);
    void startPairingWindow(uint32_t durationMs, const char* reason);
    void updateNodeTelemetryCache(const String& nodeId, const NodeStatusMessage& statusMsg);
    void refreshCoordinatorSensors();
    void printSerialTelemetry();
};
