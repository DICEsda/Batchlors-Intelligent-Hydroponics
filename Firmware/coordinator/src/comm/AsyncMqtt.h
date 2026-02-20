#pragma once

#include <Arduino.h>
#include <AsyncMqttClient.h>
#include <WiFi.h>
#include <Ticker.h>
#include <map>
#include <functional>
#include "../Models.h"
#include "../sensors/ThermalControl.h"
#include "WifiManager.h"
#include "../../shared/src/EspNowMessage.h"
#include "../../shared/src/ConfigStore.h"

/**
 * @brief Async MQTT Client for ESP32
 * 
 * Non-blocking MQTT implementation using AsyncMqttClient library.
 * Benefits over PubSubClient:
 * - Fully asynchronous (no blocking in loop())
 * - Automatic reconnection with exponential backoff
 * - TLS/SSL support ready
 * - Better QoS handling (up to QoS 2)
 * - Larger message payloads (up to 64 KB)
 * 
 * @note This is a drop-in replacement for the synchronous Mqtt class
 */
class AsyncMqtt {
public:
    AsyncMqtt();
    ~AsyncMqtt();

    bool begin();
    void loop();  // Minimal work, no blocking
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
    String getSiteId() const { return farmId; }  // Legacy compatibility
    
    // Interactive configuration
    bool runProvisioningWizard();

private:
    AsyncMqttClient mqttClient;
    Ticker mqttReconnectTimer;
    
    // Configuration
    String brokerHost;
    uint16_t brokerPort;
    String brokerUsername;
    String brokerPassword;
    String farmId;
    String coordId;
    bool configLoaded = false;
    bool discoveryAttempted = false;
    
    WifiManager* wifiManager;
    std::function<void(const String& topic, const String& payload)> commandCallback;
    
    // Connection state
    bool connected = false;
    uint32_t lastReconnectAttempt = 0;
    uint8_t reconnectAttempts = 0;
    uint32_t reconnectDelay = 5000;  // Start with 5 seconds
    
    // Static instance for Ticker callback (workaround for lambda incompatibility)
    static AsyncMqtt* instance;
    static void staticReconnectCallback();
    
    // Callbacks
    void onMqttConnect(bool sessionPresent);
    void onMqttDisconnect(AsyncMqttClientDisconnectReason reason);
    void onMqttMessage(char* topic, char* payload, 
                       AsyncMqttClientMessageProperties properties,
                       size_t len, size_t index, size_t total);
    void onMqttPublish(uint16_t packetId);
    void onMqttSubscribe(uint16_t packetId, uint8_t qos);
    
    // Connection management
    void connectToMqtt();
    void scheduleReconnect();
    bool ensureConfigLoaded();
    bool loadConfigFromStore();
    void persistConfig();
    
    // Discovery
    bool autoDiscoverBroker();
    bool tryBrokerCandidate(const IPAddress& candidate);
    
    // Helpers
    void subscribeToTopics();
    void logConnectionFailureDetail(AsyncMqttClientDisconnectReason reason);
    const char* describeDisconnectReason(AsyncMqttClientDisconnectReason reason) const;
    void warnIfLoopbackHost();
    
    // Topic builders
    String towerTelemetryTopic(const String& towerId) const;
    String reservoirTelemetryTopic() const;
    String coordinatorTelemetryTopic() const;
    String coordinatorCmdTopic() const;
    String coordinatorSerialTopic() const;
    String coordinatorMmwaveTopic() const;
    String coordinatorOtaStatusTopic() const;
    String towerCmdTopic(const String& towerId) const;
    String nodeTelemetryTopic(const String& nodeId) const;
    String connectionStatusTopic() const;
    
    // Connection event publishing
    void publishConnectionEvent(const String& event, const String& reason = "");
};
