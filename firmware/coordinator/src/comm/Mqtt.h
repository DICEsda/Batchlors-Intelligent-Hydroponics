#pragma once

#include <Arduino.h>
#include <PubSubClient.h>
#include <WiFi.h>
#include <map>
#include <functional>
#include "../Models.h"
#include "../sensors/ThermalControl.h" // for NodeThermalData
#include "WifiManager.h"
#include "../../shared/src/EspNowMessage.h"
#include "../../shared/src/ConfigStore.h"

class Mqtt {
public:
    Mqtt();
    ~Mqtt();

    bool begin();
    void loop();
    bool isConnected();

    // Publishing methods - Smart Tile (legacy)
    void publishLightState(const String& lightId, uint8_t brightness);
    void publishThermalEvent(const String& nodeId, const NodeThermalData& data);
    void publishMmWaveEvent(const MmWaveEvent& event);
    void publishNodeStatus(const NodeStatusMessage& status);
    void publishCoordinatorTelemetry(const CoordinatorSensorSnapshot& snapshot);
    void publishSerialLog(const String& message, const String& level = "INFO", const String& tag = "");
    
    // Publishing methods - Hydroponic System
    void publishTowerTelemetry(const TowerTelemetryMessage& telemetry);
    void publishReservoirTelemetry(const ReservoirTelemetryMessage& telemetry);
    void publishOtaStatus(const String& status, int progress, const String& message, const String& error = "");
    
    // Publishing methods - Pairing events (coordinator -> backend)
    void publishPairingRequest(const String& towerId, const String& macAddress, int rssi, const String& fwVersion);
    void publishPairingStatus(const String& status, int durationMs, int nodesDiscovered, int nodesPaired);
    void publishPairingComplete(const String& towerId, const String& macAddress, bool success, const String& reason);
    
    // Connection event publishing (real-time status updates)
    void publishConnectionEvent(const String& event, const String& reason = "");
    
    // Coordinator registration / announce
    void publishAnnounce();
    void saveFarmId(const String& newFarmId);
    
    // Configuration
    void setBrokerConfig(const char* host, uint16_t port, const char* username, const char* password);
    void setWifiManager(WifiManager* manager);
    
    // Subscription handling
    void setCommandCallback(std::function<void(const String& topic, const String& payload)> callback);

    // Read-only broker info for telemetry/log formatting
    String getBrokerHost() const { return brokerHost; }
    uint16_t getBrokerPort() const { return brokerPort; }
    String getFarmId() const { return farmId; }
    String getCoordinatorId() const { return coordId; }
    
    // Legacy compatibility - maps to farmId
    String getSiteId() const { return farmId; }
    
    // Interactive configuration
    bool runProvisioningWizard();

private:
    WiFiClient wifiClient;
    PubSubClient mqttClient;
    
    // Configuration
    String brokerHost;
    uint16_t brokerPort;
    String brokerUsername;
    String brokerPassword;
    String farmId;      // Hydroponic farm identifier (replaces siteId)
    String coordId;     // Coordinator identifier
    bool configLoaded = false;
    bool discoveryAttempted = false;
    
    WifiManager* wifiManager;
    std::function<void(const String& topic, const String& payload)> commandCallback;
    int8_t lastFailureState = 0;
    uint32_t lastDiagPrintMs = 0;
    bool loopbackHintPrinted = false;
    bool announcePublished = false;
    
    bool connectMqtt();
    bool ensureConfigLoaded();
    bool loadConfigFromStore();
    void persistConfig();
    static void handleMqttMessage(char* topic, uint8_t* payload, unsigned int length);
    void processMessage(const String& topic, const String& payload);
    void handleRegistrationMessage(const String& payload);
    bool autoDiscoverBroker();
    bool tryBrokerCandidate(const IPAddress& candidate);
    void logConnectionFailureDetail(int8_t state);
    const char* describeMqttState(int8_t state) const;
    void warnIfLoopbackHost();
    void runReachabilityProbe();

    // Topic builders - Hydroponic structure: farm/{farmId}/coord/{coordId}/...
    String towerTelemetryTopic(const String& towerId) const;
    String reservoirTelemetryTopic() const;
    String coordinatorTelemetryTopic() const;
    String coordinatorCmdTopic() const;
    String coordinatorSerialTopic() const;
    String coordinatorMmwaveTopic() const;
    String coordinatorOtaStatusTopic() const;
    String towerCmdTopic(const String& towerId) const;
    String connectionStatusTopic() const;
    String coordinatorAnnounceTopic() const;
    String coordinatorRegisteredTopic() const;
    String coordinatorConfigTopic() const;
    String coordinatorDirectCmdTopic() const;
    String reservoirCmdTopic() const;
    String coordinatorOtaStartTopic() const;
    String coordinatorOtaCancelTopic() const;
    
    // Pairing topic builders
    String pairingRequestTopic() const;
    String pairingStatusTopic() const;
    String pairingCompleteTopic() const;
    
    // Legacy topic builders (for backward compatibility during migration)
    String nodeTelemetryTopic(const String& nodeId) const;
};
