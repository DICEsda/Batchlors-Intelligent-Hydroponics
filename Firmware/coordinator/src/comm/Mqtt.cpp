#include "Mqtt.h"
#include "MqttLogger.h"
#include "../utils/Logger.h"
#include "../utils/SystemWatchdog.h"
#include <ArduinoJson.h>
#include <Preferences.h>

#ifndef FIRMWARE_VERSION
#define FIRMWARE_VERSION "1.0.0"
#endif

// Static instance pointer for callback
static Mqtt* mqttInstance = nullptr;

namespace {
    constexpr uint16_t DEFAULT_MQTT_PORT = 1883;

    bool waitForConsole(uint32_t timeoutMs = 0) {
        if (Serial) {
            return true;
        }
        uint32_t start = millis();
        while (!Serial) {
            if (timeoutMs > 0 && (millis() - start) > timeoutMs) {
                return false;
            }
            delay(10);
        }
        return true;
    }

    void flushSerialInput() {
        while (Serial.available()) {
            Serial.read();
        }
    }

    String promptLine(const String& prompt, bool allowEmpty, const String& defaultValue = "") {
        if (!waitForConsole()) {
            return defaultValue;
        }
        while (true) {
            if (defaultValue.length() > 0) {
                Serial.printf("%s [%s]: ", prompt.c_str(), defaultValue.c_str());
            } else {
                Serial.printf("%s: ", prompt.c_str());
            }
            Serial.flush();
            while (!Serial.available()) {
                delay(10);
            }
            String line = Serial.readStringUntil('\n');
            line.trim();
            if (line.isEmpty() && defaultValue.length() > 0) {
                line = defaultValue;
            }
            if (!line.isEmpty() || allowEmpty) {
                return line;
            }
            Serial.println("Value required. Please try again.");
        }
    }

    bool promptYesNo(const String& prompt, bool defaultYes = true) {
        String suffix = defaultYes ? "Y/n" : "y/N";
        while (true) {
            String answer = promptLine(prompt + " (" + suffix + ")", true, defaultYes ? "y" : "n");
            answer.toLowerCase();
            if (answer == "y" || answer == "yes") return true;
            if (answer == "n" || answer == "no") return false;
            Serial.println("Please answer with 'y' or 'n'.");
        }
    }

    bool isLoopbackHost(String host) {
        host.trim();
        host.toLowerCase();
        return host == "localhost" || host == "127.0.0.1" || host == "::1";
    }
}

Mqtt::Mqtt() 
    : mqttClient(wifiClient)
    , brokerPort(DEFAULT_MQTT_PORT)
    , wifiManager(nullptr) {
    mqttInstance = this;
}

Mqtt::~Mqtt() {
    mqttClient.disconnect();
    WiFi.disconnect();
}

bool Mqtt::begin() {
    Logger::info("Initializing MQTT client...");

    // Always derive coordId from WiFi MAC address (unique, deterministic)
    coordId = WiFi.macAddress();
    Logger::info("Coordinator ID set from MAC: %s", coordId.c_str());

    // Load farmId from NVS; fall back to "unregistered" if not yet assigned
    {
        Preferences prefs;
        prefs.begin("mqtt", true); // read-only
        farmId = prefs.getString("farm_id", "");
        prefs.end();
    }
    if (farmId.isEmpty()) {
        farmId = "unregistered";
        Logger::warn("No farm_id in NVS, using '%s' (awaiting backend registration)", farmId.c_str());
    } else {
        Logger::info("Farm ID loaded from NVS: %s", farmId.c_str());
    }

    if (!ensureConfigLoaded()) {
        if (brokerHost.isEmpty()) {
            brokerHost = "192.168.1.100";
        }
        Logger::warn("Using fallback MQTT endpoint %s:%u (update via provisioning)", brokerHost.c_str(), brokerPort);
    }

    warnIfLoopbackHost();

    // Setup MQTT client
    mqttClient.setServer(brokerHost.c_str(), brokerPort);
    mqttClient.setCallback(handleMqttMessage);
    Logger::info("MQTT broker target set to %s:%u", brokerHost.c_str(), brokerPort);
    
    // Connect to MQTT broker
    if (!connectMqtt()) {
        Logger::warn("Failed initial MQTT connection (will retry)");
        // Non-fatal - will retry in loop()
    }
    
    Logger::info("MQTT initialization complete");
    return true;
}

void Mqtt::loop() {
    bool wifiReady = wifiManager ? wifiManager->isConnected() : (WiFi.status() == WL_CONNECTED);

    if (!wifiReady) {
        if (mqttClient.connected()) {
            mqttClient.disconnect();
            MqttLogger::logDisconnect(-1); // WiFi lost
        }
        return;
    }

    if (!mqttClient.connected()) {
        static uint32_t lastReconnect = 0;
        static uint32_t failedAttempts = 0;
        uint32_t now = millis();
        if (now - lastReconnect > 5000) {
            lastReconnect = now;
            if (!connectMqtt()) {
                failedAttempts++;
                // After 6 failed attempts (30 seconds), try rediscovery
                if (failedAttempts >= 6) {
                    Logger::info("Multiple MQTT failures - attempting rediscovery");
                    discoveryAttempted = false; // Reset flag to allow rediscovery
                    failedAttempts = 0;
                }
            } else {
                failedAttempts = 0; // Reset on success
            }
        }
    }

    mqttClient.loop();
    
    // Periodic heartbeat logging (every 60 seconds)
    MqttLogger::logHeartbeat(mqttClient.connected(), 60000);
}

bool Mqtt::isConnected() {
    return mqttClient.connected();
}

void Mqtt::publishLightState(const String& lightId, uint8_t brightness) {
    if (!mqttClient.connected()) return;
    
    StaticJsonDocument<256> doc;
    doc["ts"] = millis() / 1000;
    doc["light_id"] = lightId;
    doc["brightness"] = brightness;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = nodeTelemetryTopic(lightId);
    mqttClient.publish(topic.c_str(), payload.c_str());
}

// PRD-compliant: farm/{farmId}/node/{nodeId}/telemetry (legacy smart tile)
void Mqtt::publishThermalEvent(const String& nodeId, const NodeThermalData& data) {
    if (!mqttClient.connected()) return;
    
    StaticJsonDocument<512> doc;
    doc["ts"] = millis() / 1000;
    doc["node_id"] = nodeId;
    doc["temp_c"] = data.temperature;
    doc["is_derated"] = data.isDerated;
    doc["deration_level"] = data.derationLevel;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = nodeTelemetryTopic(nodeId);
    mqttClient.publish(topic.c_str(), payload.c_str());
    
    Logger::info("Published thermal event for node %s", nodeId.c_str());
}

void Mqtt::publishNodeStatus(const NodeStatusMessage& status) {
    if (!mqttClient.connected()) {
        MqttLogger::logPublish("node_telemetry", "", false, 0);
        return;
    }
    
    uint32_t startMs = millis();
    
    StaticJsonDocument<1024> doc;
    doc["ts"] = startMs / 1000;
    doc["node_id"] = status.node_id.c_str();
    doc["light_id"] = status.light_id.c_str();
    doc["avg_r"] = status.avg_r;
    doc["avg_g"] = status.avg_g;
    doc["avg_b"] = status.avg_b;
    doc["avg_w"] = status.avg_w;
    doc["status_mode"] = status.status_mode.length() > 0 ? status.status_mode.c_str() : "idle";
    doc["temp_c"] = status.temperature;
    doc["button_pressed"] = status.button_pressed;
    doc["vbat_mv"] = status.vbat_mv;
    doc["fw"] = status.fw.length() > 0 ? status.fw.c_str() : "";
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = nodeTelemetryTopic(status.node_id);
    bool success = mqttClient.publish(topic.c_str(), payload.c_str());
    
    // Detailed logging
    MqttLogger::logPublish(topic, payload, success, payload.length());
    MqttLogger::logLatency("NodeStatus", startMs);
}

void Mqtt::setBrokerConfig(const char* host, uint16_t port, const char* username, const char* password) {
    brokerHost = host;
    brokerPort = port;
    brokerUsername = username;
    brokerPassword = password;
    loopbackHintPrinted = false;
    warnIfLoopbackHost();
    persistConfig();
}

void Mqtt::setWifiManager(WifiManager* manager) {
    wifiManager = manager;
}

void Mqtt::setCommandCallback(std::function<void(const String& topic, const String& payload)> callback) {
    commandCallback = callback;
}

bool Mqtt::connectMqtt() {
    if (mqttClient.connected()) {
        return true;
    }
    bool wifiReady = wifiManager ? wifiManager->ensureConnected() : (WiFi.status() == WL_CONNECTED);
    if (!wifiReady) {
        Logger::warn("MQTT connect skipped - Wi-Fi unavailable");
        return false;
    }
    
    // Check if stored broker IP is on same subnet, if not trigger rediscovery
    if (!brokerHost.isEmpty() && configLoaded) {
        IPAddress brokerIP;
        if (brokerIP.fromString(brokerHost)) {
            IPAddress local = WiFi.localIP();
            IPAddress mask = WiFi.subnetMask();
            uint32_t localNet = (uint32_t)local & (uint32_t)mask;
            uint32_t brokerNet = (uint32_t)brokerIP & (uint32_t)mask;
            if (localNet != brokerNet) {
                Logger::warn("Broker %s not on current subnet - triggering rediscovery", brokerHost.c_str());
                discoveryAttempted = false;
            }
        }
    }
    
    warnIfLoopbackHost();
    
    // coordId is always set to WiFi.macAddress() in begin(); guard just in case
    if (coordId.isEmpty()) {
        coordId = WiFi.macAddress();
    }
    String clientId = "coord-" + coordId;
    bool connected = false;
    
    if (brokerUsername.length() > 0 && brokerPassword.length() > 0) {
        connected = mqttClient.connect(clientId.c_str(), brokerUsername.c_str(), brokerPassword.c_str());
    } else {
        connected = mqttClient.connect(clientId.c_str());
    }
    
    // Log connection result with detailed info
    MqttLogger::logConnect(brokerHost, brokerPort, clientId, connected);
    
    if (connected) {
        // Subscribe to coordinator registration response
        String regTopic = coordinatorRegisteredTopic();
        bool regSubSuccess = mqttClient.subscribe(regTopic.c_str());
        MqttLogger::logSubscribe(regTopic, regSubSuccess);
        
        // Subscribe to coordinator commands (Hydroponic topics)
        String cmdTopic = coordinatorCmdTopic();
        bool subSuccess = mqttClient.subscribe(cmdTopic.c_str());
        MqttLogger::logSubscribe(cmdTopic, subSuccess);
        
        // Subscribe to tower commands (wildcard) for forwarding to tower nodes
        String towerCmd = "farm/" + farmId + "/coord/" + coordId + "/tower/+/cmd";
        bool towerSubSuccess = mqttClient.subscribe(towerCmd.c_str());
        MqttLogger::logSubscribe(towerCmd, towerSubSuccess);
        
        // Subscribe to node commands (wildcard) for light control forwarding (legacy)
        String nodeCmd = "farm/" + farmId + "/node/+/cmd";
        bool nodeSubSuccess = mqttClient.subscribe(nodeCmd.c_str());
        MqttLogger::logSubscribe(nodeCmd, nodeSubSuccess);
        
        // Publish coordinator announce so the backend can register/recognize us
        publishAnnounce();
        
        // Publish initial telemetry
        CoordinatorSensorSnapshot snapshot;
        snapshot.timestampMs = millis();
        snapshot.wifiConnected = true;
        snapshot.wifiRssi = WiFi.RSSI();
        publishCoordinatorTelemetry(snapshot);
        
        return true;
    }
    
    int8_t state = mqttClient.state();
    logConnectionFailureDetail(state);
    if (!discoveryAttempted && autoDiscoverBroker()) {
        Logger::info("Retrying MQTT connection using %s:%u", brokerHost.c_str(), brokerPort);
        mqttClient.setServer(brokerHost.c_str(), brokerPort);
        return connectMqtt();
    }
    return false;
}

bool Mqtt::ensureConfigLoaded() {
    configLoaded = loadConfigFromStore();
    if (configLoaded) {
        return true;
    }

    if (!discoveryAttempted && autoDiscoverBroker()) {
        Logger::info("Discovered MQTT broker at %s", brokerHost.c_str());
        // farmId is now managed via NVS registration flow; don't overwrite
        persistConfig();
        configLoaded = true;
        return true;
    }

    Serial.println();
    Serial.println("===========================================");
    Serial.println("MQTT broker settings not found in NVS.");
    Serial.println("The coordinator needs the Docker host IP to reach MQTT.");
    Serial.println("===========================================");
    if (!promptYesNo("Configure MQTT broker now?", true)) {
        Logger::warn("MQTT provisioning skipped by operator");
        return false;
    }

    if (!runProvisioningWizard()) {
        Logger::error("MQTT provisioning failed (using defaults)");
        return false;
    }

    configLoaded = loadConfigFromStore();
    return configLoaded;
}

bool Mqtt::loadConfigFromStore() {
    // Load from unified config store
    Config config = ConfigStore::load();
    
    brokerHost = config.mqtt.broker_host;
    brokerHost.trim();
    brokerPort = config.mqtt.broker_port;
    if (brokerPort == 0) {
        brokerPort = DEFAULT_MQTT_PORT;
    }
    
    // Use credentials from config, but apply defaults if empty (for migrated configs)
    bool needsUpdate = false;
    brokerUsername = config.mqtt.username;
    brokerPassword = config.mqtt.password;
    
    if (brokerUsername.isEmpty()) {
        brokerUsername = "user1";  // Default for Docker mosquitto
        config.mqtt.username = "user1";
        needsUpdate = true;
        Logger::info("MQTT username empty, applying default: user1");
    }
    if (brokerPassword.isEmpty()) {
        brokerPassword = "user1";  // Default for Docker mosquitto
        config.mqtt.password = "user1";
        needsUpdate = true;
        Logger::info("MQTT password empty, applying default: user1");
    }
    
    // Save updated config if we applied defaults
    if (needsUpdate) {
        Logger::info("Saving updated MQTT credentials to config");
        ConfigStore::save(config);
    }
    
    // farmId and coordId are now managed by the MAC/NVS registration flow
    // in begin(). Only load them from ConfigStore if they haven't been set yet
    // (e.g., if loadConfigFromStore is called before begin sets them).
    if (coordId.isEmpty()) {
        coordId = config.mqtt.coordinator_id;
        coordId.trim();
    }
    // Don't overwrite farmId if already loaded from NVS registration
    if (farmId.isEmpty()) {
        String storedFarm = config.mqtt.farm_id;
        storedFarm.trim();
        if (!storedFarm.isEmpty()) {
            farmId = storedFarm;
        }
    }

    bool ready = !brokerHost.isEmpty();
    return ready;
}

bool Mqtt::runProvisioningWizard() {
    if (!waitForConsole(2000)) {
        Logger::warn("Serial console not available for MQTT provisioning");
        return false;
    }

    flushSerialInput();
    Serial.println();
    Serial.println("=== MQTT Broker Setup (Hydroponic System) ===");
    Serial.println("Enter the IP of the machine running docker-compose (ex. 10.0.0.42).");
    Serial.println("Do NOT enter 'localhost' because the coordinator is on Wi-Fi.");

    String host;
    while (true) {
        host = promptLine("Broker host/IP", false, brokerHost.isEmpty() ? "192.168.1.100" : brokerHost);
        if (isLoopbackHost(host)) {
            Serial.println("Loopback addresses won't work. Please enter the LAN IP of the Docker host.");
            continue;
        }
        break;
    }

    String portStr = promptLine("Port", true, String(brokerPort == 0 ? DEFAULT_MQTT_PORT : brokerPort));
    uint16_t portCandidate = portStr.isEmpty() ? DEFAULT_MQTT_PORT : static_cast<uint16_t>(portStr.toInt());
    if (portCandidate == 0) {
        portCandidate = DEFAULT_MQTT_PORT;
    }

    String user = promptLine("Username (leave empty for anonymous)", true, brokerUsername);
    String pass = promptLine("Password", true, brokerPassword);

    String farm = promptLine("Farm ID", false, farmId.isEmpty() ? "farm001" : farmId);
    String coord = promptLine("Coordinator ID (blank = use MAC)", true, coordId);

    brokerHost = host;
    brokerPort = portCandidate;
    brokerUsername = user;
    brokerPassword = pass;
    farmId = farm;
    coordId = coord;

    loopbackHintPrinted = false;
    warnIfLoopbackHost();

    persistConfig();
    Serial.println("MQTT settings saved to NVS.");
    Serial.println();
    return true;
}

void Mqtt::persistConfig() {
    // Load current config, update MQTT settings, and save
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

bool Mqtt::autoDiscoverBroker() {
    discoveryAttempted = true;
    bool wifiReady = wifiManager ? wifiManager->ensureConnected() : (WiFi.status() == WL_CONNECTED);
    if (!wifiReady) {
        Logger::warn("MQTT autodiscovery skipped - Wi-Fi unavailable");
        return false;
    }

    IPAddress local = WiFi.localIP();
    IPAddress subnet = WiFi.subnetMask();
    IPAddress gateway = WiFi.gatewayIP();

    if ((uint32_t)local == 0 || (uint32_t)subnet == 0) {
        Logger::warn("MQTT autodiscovery aborted - invalid IP context");
        return false;
    }

    // Priority 1: Try gateway (often the mobile hotspot host running Docker)
    if (gateway && (uint32_t)gateway != 0) {
        Logger::info("Trying gateway %s as MQTT broker...", gateway.toString().c_str());
        SystemWatchdog::feed();  // Feed watchdog before network operation
        if (tryBrokerCandidate(gateway)) {
            brokerHost = gateway.toString();
            persistConfig();
            Logger::info("MQTT broker found at gateway: %s", brokerHost.c_str());
            return true;
        }
    }

    // Priority 2: Scan local subnet (limit to reasonable range)
    // We iterate the last octet (1-254) to avoid endianness issues with uint32_t arithmetic on ESP32
    const uint32_t hostCount = 254;

    Logger::info("Scanning %u nearby hosts for MQTT (this may take 15-30s)...", hostCount);
    Logger::info("Feeding watchdog during scan to prevent timeout...");
    
    for (uint32_t i = 1; i <= hostCount; ++i) {
        IPAddress candidate = local;
        candidate[3] = i; // Iterate the last octet
        
        if (candidate == local || candidate == gateway) {
            continue;
        }
        
        // Feed watchdog every 10 hosts to prevent timeout during long scan
        if (i % 10 == 0) {
            SystemWatchdog::feed();
        }
        
        // Provide visual feedback every 20 hosts
        if (i % 20 == 0) {
             Logger::debug("Scanning... %s (watchdog fed)", candidate.toString().c_str());
        }
        
        if (tryBrokerCandidate(candidate)) {
            brokerHost = candidate.toString();
            persistConfig();
            Logger::info("Auto-discovered MQTT broker at %s", brokerHost.c_str());
            return true;
        }
    }

    Logger::warn("No MQTT broker found on network");
    return false;
}

bool Mqtt::tryBrokerCandidate(const IPAddress& candidate) {
    WiFiClient probe;
    constexpr uint32_t timeoutMs = 100; // Reduced from 250ms for faster full-subnet scan
    if (!probe.connect(candidate, brokerPort, timeoutMs)) {
        return false;
    }
    probe.stop();
    return true;
}

void Mqtt::logConnectionFailureDetail(int8_t state) {
    const char* description = describeMqttState(state);
    Logger::error("MQTT connection failed, rc=%d (%s)", state, description);
    warnIfLoopbackHost();

    uint32_t now = millis();
    if (state == lastFailureState && (now - lastDiagPrintMs) < 30000) {
        return;
    }
    lastFailureState = state;
    lastDiagPrintMs = now;

    switch (state) {
        case MQTT_CONNECT_FAILED:
        case MQTT_CONNECTION_TIMEOUT:
        case MQTT_CONNECTION_LOST:
            runReachabilityProbe();
            break;
        case MQTT_CONNECT_BAD_CREDENTIALS:
        case MQTT_CONNECT_UNAUTHORIZED:
            Logger::warn("MQTT broker rejected credentials. Update ConfigManager 'mqtt' user/pass or adjust mosquitto ACLs.");
            break;
        case MQTT_CONNECT_BAD_CLIENT_ID:
            Logger::warn("MQTT broker rejected coordinator ID. Set a unique Coordinator ID during provisioning.");
            break;
        case MQTT_CONNECT_UNAVAILABLE:
            Logger::warn("MQTT broker reported itself unavailable. Ensure the Mosquitto container is running and listening on 0.0.0.0:%u.", brokerPort);
            break;
        default:
            break;
    }
}

const char* Mqtt::describeMqttState(int8_t state) const {
    switch (state) {
        case MQTT_CONNECTION_TIMEOUT: return "connection timeout";
        case MQTT_CONNECTION_LOST: return "connection lost";
        case MQTT_CONNECT_FAILED: return "TCP connection failed";
        case MQTT_DISCONNECTED: return "disconnected";
        case MQTT_CONNECTED: return "connected";
        case MQTT_CONNECT_BAD_PROTOCOL: return "bad protocol";
        case MQTT_CONNECT_BAD_CLIENT_ID: return "client ID rejected";
        case MQTT_CONNECT_UNAVAILABLE: return "server unavailable";
        case MQTT_CONNECT_BAD_CREDENTIALS: return "bad credentials";
        case MQTT_CONNECT_UNAUTHORIZED: return "unauthorized";
        default: return "unknown";
    }
}

void Mqtt::warnIfLoopbackHost() {
    if (!brokerHost.length()) {
        return;
    }
    if (isLoopbackHost(brokerHost)) {
        if (!loopbackHintPrinted) {
            Logger::warn("MQTT host %s is a loopback address. Use the LAN IP of the Docker host (ex. 192.168.x.x).", brokerHost.c_str());
            loopbackHintPrinted = true;
        }
        return;
    }
    loopbackHintPrinted = false;
}

void Mqtt::runReachabilityProbe() {
    if (brokerHost.isEmpty()) {
        return;
    }

    Logger::info("Probing TCP reachability to %s:%u...", brokerHost.c_str(), brokerPort);
    WiFiClient probe;
    probe.setTimeout(1000);
    
    // Check WiFi status first (guard against null pointer)
    if (wifiManager) {
        WifiManager::Status status = wifiManager->getStatus();
        if (!status.connected) {
            Logger::error("Cannot probe: WiFi not connected");
            return;
        }
    }
    
    if (!probe.connect(brokerHost.c_str(), brokerPort)) {
        Logger::error("Unable to open TCP socket to %s:%u. Ensure docker-compose exposes Mosquitto on 0.0.0.0:%u and Windows firewall permits inbound connections.",
                      brokerHost.c_str(), brokerPort, brokerPort);
        if (wifiManager) {
            WifiManager::Status status = wifiManager->getStatus();
            String ip = status.ip.toString();
            Logger::info("Wi-Fi context: SSID=%s ip=%s", status.ssid.c_str(), ip.c_str());
        }
        probe.stop();
        return;
    }

    Logger::info("TCP port responded but MQTT handshake still failed. Confirm mosquitto.conf allows the configured credentials or enable anonymous access for testing.");
    probe.stop();
}

void Mqtt::handleMqttMessage(char* topic, uint8_t* payload, unsigned int length) {
    // DEBUG: Print ALL MQTT messages to serial
    Serial.printf("\n[MQTT_RX] Topic: %s\n", topic);
    Serial.printf("[MQTT_RX] Length: %d bytes\n", length);
    Serial.printf("[MQTT_RX] Payload: %.*s\n", length, (char*)payload);
    
    // Log incoming message with detailed info
    MqttLogger::logReceive(String(topic), payload, length);
    
    if (mqttInstance) {
        String topicStr = String(topic);
        String payloadStr = String((char*)payload, length);
        mqttInstance->processMessage(topicStr, payloadStr);
    }
}

void Mqtt::processMessage(const String& topic, const String& payload) {
    uint32_t startMs = millis();
    
    // Check if this is a registration response from the backend
    String regTopic = coordinatorRegisteredTopic();
    if (topic == regTopic) {
        handleRegistrationMessage(payload);
        MqttLogger::logProcess(topic, "Registration processed", true);
        MqttLogger::logLatency("ProcessMessage", startMs);
        return;
    }
    
    if (commandCallback) {
        commandCallback(topic, payload);
        MqttLogger::logProcess(topic, "Command processed", true);
    } else {
        MqttLogger::logProcess(topic, "No callback", false, "callback not registered");
    }
    
    MqttLogger::logLatency("ProcessMessage", startMs);
}

void Mqtt::publishCoordinatorTelemetry(const CoordinatorSensorSnapshot& snapshot) {
    if (!mqttClient.connected()) return;
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
    mqttClient.publish(coordinatorTelemetryTopic().c_str(), payload.c_str());
}

void Mqtt::publishSerialLog(const String& message, const String& level, const String& tag) {
    if (!mqttClient.connected()) return;
    StaticJsonDocument<512> doc;
    doc["ts"] = millis() / 1000;
    doc["message"] = message;
    doc["level"] = level;
    if (tag.length() > 0) {
        doc["tag"] = tag;
    }
    String payload;
    serializeJson(doc, payload);
    mqttClient.publish(coordinatorSerialTopic().c_str(), payload.c_str());
}

// ============================================================================
// Hydroponic System Publishing Methods
// ============================================================================

void Mqtt::publishTowerTelemetry(const TowerTelemetryMessage& telemetry) {
    if (!mqttClient.connected()) {
        MqttLogger::logPublish("tower_telemetry", "", false, 0);
        return;
    }
    
    uint32_t startMs = millis();
    
    StaticJsonDocument<512> doc;
    doc["ts"] = telemetry.ts / 1000;
    doc["farm_id"] = farmId;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    doc["tower_id"] = telemetry.tower_id;
    
    // Environmental sensors
    doc["air_temp_c"] = telemetry.air_temp_c;
    doc["humidity_pct"] = telemetry.humidity_pct;
    doc["light_lux"] = telemetry.light_lux;
    
    // Actuator states
    doc["pump_on"] = telemetry.pump_on;
    doc["light_on"] = telemetry.light_on;
    doc["light_brightness"] = telemetry.light_brightness;
    
    // System status
    doc["status_mode"] = telemetry.status_mode.length() > 0 ? telemetry.status_mode : "idle";
    doc["vbat_mv"] = telemetry.vbat_mv;
    doc["fw"] = telemetry.fw.length() > 0 ? telemetry.fw : "";
    doc["uptime_s"] = telemetry.uptime_s;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = towerTelemetryTopic(telemetry.tower_id);
    bool success = mqttClient.publish(topic.c_str(), payload.c_str());
    
    MqttLogger::logPublish(topic, payload, success, payload.length());
    MqttLogger::logLatency("TowerTelemetry", startMs);
    
    if (success) {
        Logger::debug("Published tower telemetry for %s", telemetry.tower_id.c_str());
    }
}

void Mqtt::publishReservoirTelemetry(const ReservoirTelemetryMessage& telemetry) {
    if (!mqttClient.connected()) {
        MqttLogger::logPublish("reservoir_telemetry", "", false, 0);
        return;
    }
    
    uint32_t startMs = millis();
    
    StaticJsonDocument<512> doc;
    doc["ts"] = telemetry.ts / 1000;
    doc["farm_id"] = farmId;
    doc["coord_id"] = coordId.length() ? coordId : WiFi.macAddress();
    
    // Water quality sensors
    doc["ph"] = telemetry.ph;
    doc["ec_ms_cm"] = telemetry.ec_ms_cm;
    doc["tds_ppm"] = telemetry.tds_ppm;
    doc["water_temp_c"] = telemetry.water_temp_c;
    
    // Water level
    doc["water_level_pct"] = telemetry.water_level_pct;
    doc["water_level_cm"] = telemetry.water_level_cm;
    doc["low_water_alert"] = telemetry.low_water_alert;
    
    // Actuator states
    doc["main_pump_on"] = telemetry.main_pump_on;
    doc["dosing_pump_ph_on"] = telemetry.dosing_pump_ph_on;
    doc["dosing_pump_nutrient_on"] = telemetry.dosing_pump_nutrient_on;
    
    // System status
    doc["status_mode"] = telemetry.status_mode.length() > 0 ? telemetry.status_mode : "operational";
    doc["uptime_s"] = telemetry.uptime_s;
    
    String payload;
    serializeJson(doc, payload);
    
    String topic = reservoirTelemetryTopic();
    bool success = mqttClient.publish(topic.c_str(), payload.c_str());
    
    MqttLogger::logPublish(topic, payload, success, payload.length());
    MqttLogger::logLatency("ReservoirTelemetry", startMs);
    
    if (success) {
        Logger::debug("Published reservoir telemetry");
    }
}

// ============================================================================
// Topic Builders - Hydroponic structure: farm/{farmId}/coord/{coordId}/...
// ============================================================================

String Mqtt::nodeTelemetryTopic(const String& nodeId) const {
    // Legacy smart tile topic (backward compatibility)
    return "farm/" + farmId + "/node/" + nodeId + "/telemetry";
}

String Mqtt::coordinatorTelemetryTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/telemetry";
}

String Mqtt::coordinatorSerialTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/serial";
}

String Mqtt::coordinatorCmdTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/cmd";
}

// Hydroponic-specific topic builders
String Mqtt::towerTelemetryTopic(const String& towerId) const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/tower/" + towerId + "/telemetry";
}

String Mqtt::reservoirTelemetryTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/reservoir/telemetry";
}

String Mqtt::towerCmdTopic(const String& towerId) const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/tower/" + towerId + "/cmd";
}

String Mqtt::coordinatorOtaStatusTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/ota/status";
}

// ============================================================================
// OTA Status Publishing
// ============================================================================

void Mqtt::publishOtaStatus(const String& status, int progress, const String& message, const String& error) {
    if (!mqttClient.connected()) {
        Logger::warn("Cannot publish OTA status: MQTT not connected");
        return;
    }
    
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
    
    String topic = coordinatorOtaStatusTopic();
    
    uint32_t startMs = millis();
    bool success = mqttClient.publish(topic.c_str(), payload.c_str());
    MqttLogger::logPublish(topic, payload, success, payload.length());
    MqttLogger::logLatency("OtaStatus", startMs);
    
    if (success) {
        Logger::debug("Published OTA status: %s %d%%", status.c_str(), progress);
    } else {
        Logger::warn("Failed to publish OTA status");
    }
}

// ============================================================================
// Connection Status Topic & Event Publishing
// ============================================================================

String Mqtt::connectionStatusTopic() const {
    String id = coordId.length() ? coordId : WiFi.macAddress();
    return "farm/" + farmId + "/coord/" + id + "/status/connection";
}

void Mqtt::publishConnectionEvent(const String& event, const String& reason) {
    if (!mqttClient.connected()) {
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
    
    uint32_t startMs = millis();
    bool success = mqttClient.publish(topic.c_str(), payload.c_str(), true);  // retained=true
    MqttLogger::logPublish(topic, payload, success, payload.length());
    MqttLogger::logLatency("ConnectionEvent", startMs);
    
    if (success) {
        Logger::info("📡 Published connection event: %s", event.c_str());
    } else {
        Logger::warn("Failed to publish connection event: %s", event.c_str());
    }
}

// ============================================================================
// Coordinator Registration / Announce
// ============================================================================

void Mqtt::publishAnnounce() {
    if (!isConnected()) return;

    StaticJsonDocument<512> doc;
    doc["mac"] = WiFi.macAddress();
    doc["fw_version"] = FIRMWARE_VERSION;
    doc["chip_model"] = ESP.getChipModel();
    doc["free_heap"] = ESP.getFreeHeap();
    doc["wifi_rssi"] = WiFi.RSSI();
    doc["ip"] = WiFi.localIP().toString();
    doc["farm_id"] = farmId;  // current farm_id (may be "unregistered")

    String payload;
    serializeJson(doc, payload);

    String topic = coordinatorAnnounceTopic();
    bool success = mqttClient.publish(topic.c_str(), payload.c_str());

    if (success) {
        announcePublished = true;
        Logger::info("Published coordinator announce to %s", topic.c_str());
    } else {
        Logger::warn("Failed to publish coordinator announce");
    }
}

void Mqtt::saveFarmId(const String& newFarmId) {
    farmId = newFarmId;

    // Persist to dedicated NVS namespace for fast boot-time retrieval
    Preferences prefs;
    prefs.begin("mqtt", false); // read-write
    prefs.putString("farm_id", farmId);
    prefs.end();

    // Also update the unified ConfigStore so provisioning wizard stays in sync
    Config config = ConfigStore::load();
    config.mqtt.farm_id = farmId;
    ConfigStore::save(config);

    Logger::info("Farm ID saved to NVS: %s", farmId.c_str());
}

void Mqtt::handleRegistrationMessage(const String& payload) {
    StaticJsonDocument<256> doc;
    DeserializationError error = deserializeJson(doc, payload);

    if (error) {
        Logger::error("Failed to parse registration response: %s", error.c_str());
        return;
    }

    const char* newFarmId = doc["farm_id"] | (const char*)nullptr;
    if (!newFarmId || strlen(newFarmId) == 0) {
        Logger::warn("Registration response missing 'farm_id' field");
        return;
    }

    Logger::info("Received registration: farm_id=%s", newFarmId);
    saveFarmId(String(newFarmId));

    // Re-subscribe to farm-scoped topics with the new farmId
    // Unsubscribe old topics first (PubSubClient doesn't track, but re-subscribing is safe)
    String cmdTopic = coordinatorCmdTopic();
    mqttClient.subscribe(cmdTopic.c_str());
    MqttLogger::logSubscribe(cmdTopic, true);

    String towerCmd = "farm/" + farmId + "/coord/" + coordId + "/tower/+/cmd";
    mqttClient.subscribe(towerCmd.c_str());
    MqttLogger::logSubscribe(towerCmd, true);

    String nodeCmd = "farm/" + farmId + "/node/+/cmd";
    mqttClient.subscribe(nodeCmd.c_str());
    MqttLogger::logSubscribe(nodeCmd, true);

    Logger::info("Re-subscribed to topics with new farm_id: %s", farmId.c_str());
}

// ============================================================================
// Topic Builders - Registration / Announce
// ============================================================================

String Mqtt::coordinatorAnnounceTopic() const {
    return "coordinator/" + coordId + "/announce";
}

String Mqtt::coordinatorRegisteredTopic() const {
    return "coordinator/" + coordId + "/registered";
}
