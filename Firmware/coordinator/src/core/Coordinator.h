#pragma once

#include <Arduino.h>
#include "../comm/EspNow.h"
#include "../comm/WifiManager.h"
#include "../comm/Mqtt.h"
#include "../nodes/NodeRegistry.h"
#include "../../shared/src/utils/SafeTimer.h"

// Coordinator with full pairing functionality (WiFi + MQTT + ESP-NOW)
class Coordinator {
public:
    Coordinator();
    ~Coordinator();

    bool begin();
    void loop();

private:
    // Core components
    EspNow* espNow;
    WifiManager* wifi;
    Mqtt* mqtt;
    NodeRegistry* nodes;
    
    // Timing
    uint32_t lastHealthPingMs;
    uint32_t lastStatusLogMs;
    uint32_t lastMqttPublishMs;
    
    // Pairing
    bool pairingActive;
    Deadline pairingDl;
    
    // Callbacks
    void handleNodeMessage(const String& nodeId, const uint8_t* data, size_t len);
    void handlePairingRequest(const uint8_t* mac, const uint8_t* data, size_t len);
    void handleSendError(const String& nodeId);
    void handleMqttCommand(const String& topic, const String& payload);
    void handleConnectionStatusChange(const String& event, const String& detail);
    
    // Helpers
    void logConnectedNodes();
    void sendHealthPings();
    void startPairing(uint32_t durationMs);
    void stopPairing();
    void publishPairingStatus();
    void publishNodeList();
};
