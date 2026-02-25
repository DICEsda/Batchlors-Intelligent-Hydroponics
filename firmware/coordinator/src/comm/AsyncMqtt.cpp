#include "AsyncMqtt.h"
#include "MqttLogger.h"
#include "../utils/Logger.h"
#include <ArduinoJson.h>

namespace {
    constexpr uint16_t DEFAULT_MQTT_PORT = 1883;
    constexpr uint32_t MIN_RECONNECT_DELAY = 5000;     // 5 seconds
    constexpr uint32_t MAX_RECONNECT_DELAY = 300000;   // 5 minutes
    constexpr float RECONNECT_BACKOFF_FACTOR = 1.5;     // Exponential backoff

    bool isLoopbackHost(String host) {
        host.trim();
        host.toLowerCase();
        return host == "localhost" || host == "127.0.0.1" || host == "::1";
    }
}

// Static member initialization
AsyncMqtt* AsyncMqtt::instance = nullptr;

AsyncMqtt::AsyncMqtt()
    : brokerPort(DEFAULT_MQTT_PORT)
    , wifiManager(nullptr)
    , connected(false)
    , reconnectAttempts(0)
    , reconnectDelay(MIN_RECONNECT_DELAY) {
    
    // Set static instance for Ticker callback
    instance = this;
    
    // Set up MQTT callbacks
    mqttClient.onConnect([this](bool sessionPresent) {
        this->onMqttConnect(sessionPresent);
    });
    
    mqttClient.onDisconnect([this](AsyncMqttClientDisconnectReason reason) {
        this->onMqttDisconnect(reason);
    });
    
    mqttClient.onMessage([this](char* topic, char* payload,
                               AsyncMqttClientMessageProperties properties,
                               size_t len, size_t index, size_t total) {
        this->onMqttMessage(topic, payload, properties, len, index, total);
    });
    
    mqttClient.onPublish([this](uint16_t packetId) {
        this->onMqttPublish(packetId);
    });
    
    mqttClient.onSubscribe([this](uint16_t packetId, uint8_t qos) {
        this->onMqttSubscribe(packetId, qos);
    });
}

AsyncMqtt::~AsyncMqtt() {
    mqttClient.disconnect(true);  // Force disconnect
    mqttReconnectTimer.detach();
    
    // Clear static instance
    if (instance == this) {
        instance = nullptr;
    }
}

bool AsyncMqtt::begin() {
    Logger::info("Initializing Async MQTT client...");

    if (!ensureConfigLoaded()) {
        if (brokerHost.isEmpty()) {
            brokerHost = "192.168.1.100";
        }
        if (farmId.isEmpty()) {
            farmId = "farm001";
        }
        Logger::warn("Using fallback MQTT endpoint %s:%u", brokerHost.c_str(), brokerPort);
    }

    warnIfLoopbackHost();

    if (coordId.isEmpty()) {
        coordId = "coord001";
        Logger::info("No coordinator ID set, using default: coord001");
    }

    // Configure MQTT client
    String clientId = "coord-" + coordId;
    mqttClient.setServer(brokerHost.c_str(), brokerPort);
    mqttClient.setClientId(clientId.c_str());
    
    if (brokerUsername.length() > 0 && brokerPassword.length() > 0) {
        mqttClient.setCredentials(brokerUsername.c_str(), brokerPassword.c_str());
    }
    
    // Configure connection parameters
    mqttClient.setKeepAlive(15);          // 15 second keepalive
    mqttClient.setCleanSession(true);     // Start fresh each time
    
    // ⭐ ENHANCED: Configure Last Will & Testament with detailed connection status
    String lwtTopic = connectionStatusTopic();
    StaticJsonDocument<256> lwtDoc;
    lwtDoc["ts"] = 0;  // Will be updated by broker
    lwtDoc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    lwtDoc["farm_id"] = farmId;
    lwtDoc["event"] = "mqtt_disconnected";
    lwtDoc["wifi_connected"] = false;
    lwtDoc["mqtt_connected"] = false;
    lwtDoc["reason"] = "unclean_disconnect";  // Crash, power loss, or network failure
    
    String lwtPayload;
    serializeJson(lwtDoc, lwtPayload);
    
    mqttClient.setWill(lwtTopic.c_str(), 1, true, lwtPayload.c_str());  // QoS=1, retained=true
    
    Logger::info("MQTT broker target set to %s:%u", brokerHost.c_str(), brokerPort);
    Logger::info("Client ID: %s", clientId.c_str());
    Logger::info("MQTT LWT configured: %s", lwtTopic.c_str());
    
    // Initiate first connection
    connectToMqtt();
    
    Logger::info("Async MQTT initialization complete");
    return true;
}

void AsyncMqtt::loop() {
    // AsyncMqttClient is fully asynchronous - no blocking work needed!
    // This is called for compatibility with synchronous Mqtt interface
    
    // Periodic heartbeat logging (every 60 seconds)
    MqttLogger::logHeartbeat(connected, 60000);
}

bool AsyncMqtt::isConnected() {
    return connected && mqttClient.connected();
}

void AsyncMqtt::connectToMqtt() {
    bool wifiReady = wifiManager ? wifiManager->ensureConnected() : (WiFi.status() == WL_CONNECTED);
    if (!wifiReady) {
        Logger::warn("MQTT connect skipped - Wi-Fi unavailable");
        scheduleReconnect();
        return;
    }
    
    if (mqttClient.connected()) {
        Logger::debug("MQTT already connected");
        return;
    }
    
    warnIfLoopbackHost();
    
    Logger::info("Connecting to MQTT broker at %s:%u (attempt %d)...", 
                 brokerHost.c_str(), brokerPort, reconnectAttempts + 1);
    
    mqttClient.connect();
}

void AsyncMqtt::scheduleReconnect() {
    uint32_t now = millis();
    if (now - lastReconnectAttempt < reconnectDelay) {
        return;  // Too soon
    }
    
    lastReconnectAttempt = now;
    reconnectAttempts++;
    
    // Exponential backoff with cap
    reconnectDelay = min((uint32_t)(reconnectDelay * RECONNECT_BACKOFF_FACTOR), MAX_RECONNECT_DELAY);
    
    Logger::info("Scheduling MQTT reconnect in %d ms (attempt %d)", reconnectDelay, reconnectAttempts);
    
    // Use Ticker for non-blocking delayed reconnect (use static callback for compatibility)
    mqttReconnectTimer.once_ms(reconnectDelay, staticReconnectCallback);
}

void AsyncMqtt::staticReconnectCallback() {
    if (instance) {
        instance->connectToMqtt();
    }
}

void AsyncMqtt::onMqttConnect(bool sessionPresent) {
    connected = true;
    reconnectAttempts = 0;
    reconnectDelay = MIN_RECONNECT_DELAY;  // Reset backoff
    
    Logger::info("✓ MQTT connected! (session present: %s)", sessionPresent ? "yes" : "no");
    MqttLogger::logConnect(brokerHost, brokerPort, "coord-" + coordId, true);
    
    // ⭐ NEW: Publish connection status event
    publishConnectionEvent("mqtt_connected");
    
    // Subscribe to all relevant topics
    subscribeToTopics();
    
    // Publish initial telemetry
    CoordinatorSensorSnapshot snapshot;
    snapshot.timestampMs = millis();
    snapshot.wifiConnected = true;
    snapshot.wifiRssi = WiFi.RSSI();
    publishCoordinatorTelemetry(snapshot);
}

void AsyncMqtt::onMqttDisconnect(AsyncMqttClientDisconnectReason reason) {
    connected = false;
    
    const char* reasonStr = describeDisconnectReason(reason);
    Logger::warn("MQTT disconnected: %s", reasonStr);
    Logger::error("📡 MQTT connection lost: %s", reasonStr);  // ⭐ NEW: Visible in serial logs
    MqttLogger::logDisconnect((int)reason);
    
    logConnectionFailureDetail(reason);
    
    // Auto-reconnect if WiFi is still up
    bool wifiReady = wifiManager ? wifiManager->isConnected() : (WiFi.status() == WL_CONNECTED);
    if (wifiReady) {
        scheduleReconnect();
    }
}

void AsyncMqtt::onMqttMessage(char* topic, char* payload,
                              AsyncMqttClientMessageProperties properties,
                              size_t len, size_t index, size_t total) {
    // Handle message fragments (for large messages)
    static String assembledPayload;
    
    if (index == 0) {
        assembledPayload = "";
    }
    
    assembledPayload += String(payload, len);
    
    // Only process when we have the complete message
    if (index + len == total) {
        // DEBUG: Print all MQTT messages
        Serial.printf("\n[ASYNC_MQTT_RX] Topic: %s\n", topic);
        Serial.printf("[ASYNC_MQTT_RX] Length: %d bytes\n", total);
        Serial.printf("[ASYNC_MQTT_RX] Payload: %s\n", assembledPayload.c_str());
        
        MqttLogger::logReceive(String(topic), (uint8_t*)assembledPayload.c_str(), total);
        
        if (commandCallback) {
            uint32_t startMs = millis();
            commandCallback(String(topic), assembledPayload);
            MqttLogger::logProcess(String(topic), "Command processed", true);
            MqttLogger::logLatency("ProcessMessage", startMs);
        }
        
        assembledPayload = "";  // Clear for next message
    }
}

void AsyncMqtt::onMqttPublish(uint16_t packetId) {
    Logger::debug("MQTT publish acknowledged (packet %d)", packetId);
}

void AsyncMqtt::onMqttSubscribe(uint16_t packetId, uint8_t qos) {
    Logger::debug("MQTT subscription acknowledged (packet %d, QoS %d)", packetId, qos);
}

void AsyncMqtt::subscribeToTopics() {
    // Subscribe to coordinator commands
    String cmdTopic = coordinatorCmdTopic();
    uint16_t packetId = mqttClient.subscribe(cmdTopic.c_str(), 1);
    Logger::info("Subscribed to: %s (packet %d)", cmdTopic.c_str(), packetId);
    
    // Subscribe to tower commands (wildcard)
    String towerCmd = "farm/" + farmId + "/coord/" + coordId + "/tower/+/cmd";
    packetId = mqttClient.subscribe(towerCmd.c_str(), 1);
    Logger::info("Subscribed to: %s (packet %d)", towerCmd.c_str(), packetId);
    
    // Subscribe to node commands (wildcard, legacy)
    String nodeCmd = "farm/" + farmId + "/node/+/cmd";
    packetId = mqttClient.subscribe(nodeCmd.c_str(), 1);
    Logger::info("Subscribed to: %s (packet %d)", nodeCmd.c_str(), packetId);
}

// Configuration management
void AsyncMqtt::setBrokerConfig(const char* host, uint16_t port, const char* username, const char* password) {
    brokerHost = host;
    brokerPort = port;
    brokerUsername = username;
    brokerPassword = password;
    warnIfLoopbackHost();
    persistConfig();
}

void AsyncMqtt::setWifiManager(WifiManager* manager) {
    wifiManager = manager;
}

void AsyncMqtt::setCommandCallback(std::function<void(const String& topic, const String& payload)> callback) {
    commandCallback = callback;
}

bool AsyncMqtt::ensureConfigLoaded() {
    configLoaded = loadConfigFromStore();
    if (configLoaded) {
        return true;
    }

    if (!discoveryAttempted && autoDiscoverBroker()) {
        Logger::info("Discovered MQTT broker at %s", brokerHost.c_str());
        if (farmId.isEmpty()) {
            farmId = "farm001";
        }
        persistConfig();
        configLoaded = true;
        return true;
    }

    return false;
}

bool AsyncMqtt::loadConfigFromStore() {
    Config config = ConfigStore::load();
    
    brokerHost = config.mqtt.broker_host;
    brokerHost.trim();
    brokerPort = config.mqtt.broker_port;
    if (brokerPort == 0) {
        brokerPort = DEFAULT_MQTT_PORT;
    }
    
    brokerUsername = config.mqtt.username;
    brokerPassword = config.mqtt.password;
    
    if (brokerUsername.isEmpty()) {
        brokerUsername = "user1";
    }
    if (brokerPassword.isEmpty()) {
        brokerPassword = "user1";
    }
    
    farmId = config.mqtt.farm_id;
    farmId.trim();
    coordId = config.mqtt.coordinator_id;
    coordId.trim();

    bool ready = !brokerHost.isEmpty() && !farmId.isEmpty();
    return ready;
}

void AsyncMqtt::persistConfig() {
    Config config = ConfigStore::load();
    
    config.mqtt.broker_host = brokerHost;
    config.mqtt.broker_port = brokerPort;
    config.mqtt.username = brokerUsername;
    config.mqtt.password = brokerPassword;
    config.mqtt.farm_id = farmId;
    config.mqtt.coordinator_id = coordId;
    
    if (!ConfigStore::save(config)) {
        Logger::error("Failed to persist MQTT config to store");
    }
}

bool AsyncMqtt::autoDiscoverBroker() {
    discoveryAttempted = true;
    bool wifiReady = wifiManager ? wifiManager->ensureConnected() : (WiFi.status() == WL_CONNECTED);
    if (!wifiReady) {
        Logger::warn("MQTT autodiscovery skipped - Wi-Fi unavailable");
        return false;
    }

    IPAddress gateway = WiFi.gatewayIP();
    if (gateway && (uint32_t)gateway != 0) {
        Logger::info("Trying gateway %s as MQTT broker...", gateway.toString().c_str());
        if (tryBrokerCandidate(gateway)) {
            brokerHost = gateway.toString();
            persistConfig();
            return true;
        }
    }

    Logger::warn("No MQTT broker found");
    return false;
}

bool AsyncMqtt::tryBrokerCandidate(const IPAddress& candidate) {
    WiFiClient probe;
    constexpr uint32_t timeoutMs = 100;
    if (!probe.connect(candidate, brokerPort, timeoutMs)) {
        return false;
    }
    probe.stop();
    return true;
}

void AsyncMqtt::logConnectionFailureDetail(AsyncMqttClientDisconnectReason reason) {
    const char* description = describeDisconnectReason(reason);
    Logger::error("MQTT connection issue: %s", description);
    warnIfLoopbackHost();
}

const char* AsyncMqtt::describeDisconnectReason(AsyncMqttClientDisconnectReason reason) const {
    switch (reason) {
        case AsyncMqttClientDisconnectReason::TCP_DISCONNECTED: return "TCP disconnected";
        case AsyncMqttClientDisconnectReason::MQTT_UNACCEPTABLE_PROTOCOL_VERSION: return "unacceptable protocol version";
        case AsyncMqttClientDisconnectReason::MQTT_IDENTIFIER_REJECTED: return "identifier rejected";
        case AsyncMqttClientDisconnectReason::MQTT_SERVER_UNAVAILABLE: return "server unavailable";
        case AsyncMqttClientDisconnectReason::MQTT_MALFORMED_CREDENTIALS: return "malformed credentials";
        case AsyncMqttClientDisconnectReason::MQTT_NOT_AUTHORIZED: return "not authorized";
        case AsyncMqttClientDisconnectReason::TLS_BAD_FINGERPRINT: return "TLS bad fingerprint";
        default: return "unknown";
    }
}

void AsyncMqtt::warnIfLoopbackHost() {
    if (!brokerHost.length()) return;
    if (isLoopbackHost(brokerHost)) {
        Logger::warn("MQTT host %s is a loopback address. Use the LAN IP of the Docker host.", brokerHost.c_str());
    }
}

// Publishing methods (same interface as synchronous Mqtt class)
void AsyncMqtt::publishCoordinatorTelemetry(const CoordinatorSensorSnapshot& snapshot) {
    if (!isConnected()) return;
    
    StaticJsonDocument<256> doc;
    uint32_t ts = snapshot.timestampMs ? snapshot.timestampMs : millis();
    doc["ts"] = ts / 1000;
    doc["farm_id"] = farmId;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    doc["light_lux"] = snapshot.lightLux;
    doc["temp_c"] = snapshot.tempC;
    doc["wifi_rssi"] = snapshot.wifiConnected ? snapshot.wifiRssi : -127;
    doc["wifi_connected"] = snapshot.wifiConnected;
    
    String payload;
    serializeJson(doc, payload);
    
    uint16_t packetId = mqttClient.publish(coordinatorTelemetryTopic().c_str(), 1, false, payload.c_str());
    Logger::debug("Published coordinator telemetry (packet %d)", packetId);
}

void AsyncMqtt::publishTowerTelemetry(const TowerTelemetryMessage& telemetry) {
    if (!isConnected()) return;
    
    StaticJsonDocument<512> doc;
    doc["ts"] = telemetry.ts / 1000;
    doc["farm_id"] = farmId;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    doc["tower_id"] = telemetry.tower_id;
    doc["air_temp_c"] = telemetry.air_temp_c;
    doc["humidity_pct"] = telemetry.humidity_pct;
    doc["light_lux"] = telemetry.light_lux;
    doc["pump_on"] = telemetry.pump_on;
    doc["light_on"] = telemetry.light_on;
    doc["light_brightness"] = telemetry.light_brightness;
    doc["status_mode"] = telemetry.status_mode.length() > 0 ? telemetry.status_mode : "idle";
    doc["vbat_mv"] = telemetry.vbat_mv;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = towerTelemetryTopic(telemetry.tower_id);
    uint16_t packetId = mqttClient.publish(topic.c_str(), 1, false, payload.c_str());
    MqttLogger::logPublish(topic, payload, true, payload.length());
}

void AsyncMqtt::publishReservoirTelemetry(const ReservoirTelemetryMessage& telemetry) {
    if (!isConnected()) return;
    
    StaticJsonDocument<512> doc;
    doc["ts"] = telemetry.ts / 1000;
    doc["farm_id"] = farmId;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    doc["ph"] = telemetry.ph;
    doc["ec_ms_cm"] = telemetry.ec_ms_cm;
    doc["water_temp_c"] = telemetry.water_temp_c;
    doc["water_level_pct"] = telemetry.water_level_pct;
    doc["main_pump_on"] = telemetry.main_pump_on;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = reservoirTelemetryTopic();
    uint16_t packetId = mqttClient.publish(topic.c_str(), 1, false, payload.c_str());
    MqttLogger::logPublish(topic, payload, true, payload.length());
}

void AsyncMqtt::publishSerialLog(const String& message, const String& level, const String& tag) {
    if (!isConnected()) return;
    
    StaticJsonDocument<512> doc;
    doc["ts"] = millis() / 1000;
    doc["message"] = message;
    doc["level"] = level;
    if (tag.length() > 0) {
        doc["tag"] = tag;
    }
    
    String payload;
    serializeJson(doc, payload);
    
    mqttClient.publish(coordinatorSerialTopic().c_str(), 0, false, payload.c_str());
}

void AsyncMqtt::publishOtaStatus(const String& status, int progress, const String& message, const String& error) {
    if (!isConnected()) return;
    
    StaticJsonDocument<256> doc;
    doc["status"] = status;
    doc["progress"] = progress;
    doc["message"] = message;
    if (error.length() > 0) {
        doc["error"] = error;
    }
    doc["timestamp"] = millis();
    
    String payload;
    serializeJson(doc, payload);
    
    mqttClient.publish(coordinatorOtaStatusTopic().c_str(), 1, false, payload.c_str());
}

// Stub implementations for legacy compatibility
void AsyncMqtt::publishLightState(const String& lightId, uint8_t brightness) {
    if (!isConnected()) return;
    StaticJsonDocument<128> doc;
    doc["light_id"] = lightId;
    doc["brightness"] = brightness;
    String payload;
    serializeJson(doc, payload);
    mqttClient.publish(nodeTelemetryTopic(lightId).c_str(), 0, false, payload.c_str());
}

void AsyncMqtt::publishThermalEvent(const String& nodeId, const NodeThermalData& data) {
    if (!isConnected()) return;
    StaticJsonDocument<256> doc;
    doc["node_id"] = nodeId;
    doc["temp_c"] = data.temperature;
    doc["is_derated"] = data.isDerated;
    String payload;
    serializeJson(doc, payload);
    mqttClient.publish(nodeTelemetryTopic(nodeId).c_str(), 0, false, payload.c_str());
}

void AsyncMqtt::publishMmWaveEvent(const MmWaveEvent& event) {
    if (!isConnected()) return;
    StaticJsonDocument<512> doc;
    doc["sensor_id"] = event.sensorId;
    doc["presence"] = event.presence;
    String payload;
    serializeJson(doc, payload);
    mqttClient.publish(coordinatorMmwaveTopic().c_str(), 0, false, payload.c_str());
}

void AsyncMqtt::publishNodeStatus(const NodeStatusMessage& status) {
    if (!isConnected()) return;
    StaticJsonDocument<512> doc;
    doc["node_id"] = status.node_id;
    doc["temp_c"] = status.temperature;
    String payload;
    serializeJson(doc, payload);
    mqttClient.publish(nodeTelemetryTopic(status.node_id).c_str(), 0, false, payload.c_str());
}

// Topic builders
String AsyncMqtt::coordinatorTelemetryTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/telemetry";
}

String AsyncMqtt::coordinatorSerialTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/serial";
}

String AsyncMqtt::coordinatorCmdTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/cmd";
}

String AsyncMqtt::coordinatorMmwaveTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/mmwave";
}

String AsyncMqtt::towerTelemetryTopic(const String& towerId) const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/tower/" + towerId + "/telemetry";
}

String AsyncMqtt::reservoirTelemetryTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/reservoir/telemetry";
}

String AsyncMqtt::towerCmdTopic(const String& towerId) const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/tower/" + towerId + "/cmd";
}

String AsyncMqtt::coordinatorOtaStatusTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/ota/status";
}

String AsyncMqtt::nodeTelemetryTopic(const String& nodeId) const {
    return "farm/" + farmId + "/node/" + nodeId + "/telemetry";
}

String AsyncMqtt::connectionStatusTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/status/connection";
}

bool AsyncMqtt::runProvisioningWizard() {
    // TODO: Implement interactive provisioning
    Logger::warn("Interactive provisioning not yet implemented for AsyncMqtt");
    return false;
}

// ============================================================================
// Connection Event Publishing - Real-Time Status Updates
// ============================================================================

void AsyncMqtt::publishConnectionEvent(const String& event, const String& reason) {
    if (!isConnected()) {
        Logger::warn("Cannot publish connection event '%s': MQTT not connected", event.c_str());
        return;
    }
    
    StaticJsonDocument<384> doc;
    doc["ts"] = millis() / 1000;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    doc["farm_id"] = farmId;
    doc["event"] = event;
    doc["wifi_connected"] = (WiFi.status() == WL_CONNECTED);
    doc["wifi_rssi"] = WiFi.RSSI();
    doc["mqtt_connected"] = true;  // Must be true if we're publishing
    doc["uptime_ms"] = millis();
    doc["free_heap"] = ESP.getFreeHeap();
    
    if (reason.length() > 0) {
        doc["reason"] = reason;
    }
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = connectionStatusTopic();
    uint16_t packetId = mqttClient.publish(topic.c_str(), 1, true, payload.c_str());  // QoS=1, retained=true
    
    Logger::info("📡 Published connection event: %s (packet %d)", event.c_str(), packetId);
}
