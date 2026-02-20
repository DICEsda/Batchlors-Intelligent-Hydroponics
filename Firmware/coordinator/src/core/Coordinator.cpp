#include "Coordinator.h"
#include "../utils/Logger.h"
#include "../utils/LogStreamer.h"
#include "../utils/SystemWatchdog.h"
#include "../../shared/src/EspNowMessage.h"
#include "../../shared/src/ConfigStore.h"
#include <WiFi.h>
#include <ArduinoJson.h>

Coordinator::Coordinator()
    : espNow(nullptr)
    , wifi(nullptr)
    , mqtt(nullptr)
    , nodes(nullptr)
    , lastHealthPingMs(0)
    , lastStatusLogMs(0)
    , lastMqttPublishMs(0)
    , pairingActive(false)
    , pairingEndTime(0) {}

Coordinator::~Coordinator() {
    if (nodes) { delete nodes; nodes = nullptr; }
    if (mqtt) { delete mqtt; mqtt = nullptr; }
    if (wifi) { delete wifi; wifi = nullptr; }
    if (espNow) { delete espNow; espNow = nullptr; }
}

bool Coordinator::begin() {
    Logger::info("=== COORDINATOR WITH PAIRING INIT ===");
    Logger::info("Using unified ConfigStore for all settings");
    
    // Initialize Watchdog Timer (30 second timeout)
    if (SystemWatchdog::init(30, true)) {
        SystemWatchdog::addCurrentTask();
        Logger::info("✓ Watchdog enabled for coordinator task");
    } else {
        Logger::warn("Watchdog initialization failed - continuing without protection");
    }
    
    // Load configuration
    Config config = ConfigStore::load();
    if (config.isFirstBoot()) {
        Logger::warn("First boot detected - WiFi not configured");
        Logger::info("Please configure via serial or MQTT after boot");
    }
    
    // Initialize Node Registry first
    Logger::info("Initializing Node Registry...");
    nodes = new NodeRegistry();
    nodes->begin();
    nodes->loadFromStorage();
    Logger::info("Node Registry initialized - %d nodes loaded", nodes->getAllNodes().size());
    
    // STEP 1: Initialize WiFi Manager (discovers channel)
    Logger::info("STEP 1/4: Initializing WiFi...");
    wifi = new WifiManager();
    
    // ⭐ NEW: Wire up WiFi connection status callback
    wifi->setConnectionStatusCallback([this](const String& event, const String& detail) {
        this->handleConnectionStatusChange(event, detail);
    });
    
    if (!wifi->begin()) {
        Logger::error("WiFi initialization failed!");
        return false;
    }
    
    // Give WiFi time to connect
    delay(2000);
    
    if (wifi->isConnected()) {
        Logger::info("✓ WiFi connected: %s (RSSI: %d dBm)", 
            wifi->getStatus().ssid.c_str(), 
            wifi->getStatus().rssi);
        Logger::info("  IP Address: %s", wifi->getStatus().ip.toString().c_str());
        Logger::info("  WiFi Channel: %d", WiFi.channel());
    } else {
        Logger::warn("WiFi not connected - running in offline mode");
    }
    
    // STEP 2: Initialize ESP-NOW (will briefly disconnect WiFi to set channel)
    Logger::info("STEP 2/4: Initializing ESP-NOW...");
    espNow = new EspNow();
    if (!espNow->begin()) {
        Logger::error("ESP-NOW initialization failed!");
        return false;
    }
    Logger::info("✓ ESP-NOW initialized on channel %d", WiFi.channel());
    
    // Link WiFi manager with ESP-NOW
    if (wifi) {
        wifi->setEspNow(espNow);
    }
    
    // STEP 3: Wait for WiFi to stabilize after ESP-NOW channel change
    Logger::info("STEP 3/4: Waiting for WiFi to stabilize after ESP-NOW init...");
    uint32_t wifiWaitStart = millis();
    bool wifiRestored = false;
    
    while (millis() - wifiWaitStart < 10000) {  // 10 second timeout
        if (WiFi.status() == WL_CONNECTED) {
            wifiRestored = true;
            break;
        }
        delay(100);
    }
    
    if (wifiRestored) {
        Logger::info("✓ WiFi reconnected and stable on channel %d", WiFi.channel());
        Logger::info("  IP Address: %s", WiFi.localIP().toString().c_str());
    } else {
        Logger::warn("WiFi did not reconnect after ESP-NOW init");
        Logger::warn("MQTT will retry connection in background");
    }
    
    // STEP 4: Initialize MQTT (now that WiFi is stable)
    Logger::info("STEP 4/4: Initializing MQTT...");
    mqtt = new Mqtt();
    mqtt->setWifiManager(wifi);
    if (!mqtt->begin()) {
        Logger::warn("MQTT initialization failed - will retry in background");
    } else {
        Logger::info("✓ MQTT initialized");
        Logger::info("  Broker: %s:%d", mqtt->getBrokerHost().c_str(), mqtt->getBrokerPort());
        Logger::info("  Farm ID: %s", mqtt->getFarmId().c_str());
        Logger::info("  Coordinator ID: %s", mqtt->getCoordinatorId().c_str());
        
        // Set MQTT command callback
        mqtt->setCommandCallback([this](const String& topic, const String& payload) {
            this->handleMqttCommand(topic, payload);
        });
        
        // Initialize log streaming to MQTT
        gLogStreamer.setEnabled(true);
        gLogStreamer.setMinLevel(Logger::INFO);  // Stream INFO and above (not DEBUG)
        gLogStreamer.setRateLimit(5);  // Max 5 messages per second
        gLogStreamer.setPublishCallback([this](const String& level, const String& message, unsigned long timestamp) {
            if (mqtt && mqtt->isConnected()) {
                mqtt->publishSerialLog(message, level);
            }
        });
        Logger::info("✓ Log streaming enabled (INFO level, 5 msg/sec)");
    }
    
    // Set up ESP-NOW callbacks
    espNow->setMessageCallback([this](const String& nodeId, const uint8_t* data, size_t len) {
        this->handleNodeMessage(nodeId, data, len);
    });
    
    espNow->setPairingCallback([this](const uint8_t* mac, const uint8_t* data, size_t len) {
        this->handlePairingRequest(mac, data, len);
    });
    
    espNow->setSendErrorCallback([this](const String& nodeId) {
        this->handleSendError(nodeId);
    });
    
    // Start pairing mode for 5 minutes
    startPairing(300000);
    
    Logger::info("=== COORDINATOR READY ===");
    Logger::info("Pairing mode active for 5 minutes");
    Logger::info("Waiting for pairing requests from frontend...");
    
    // Publish initial status to MQTT
    if (mqtt && mqtt->isConnected()) {
        publishPairingStatus();
        publishNodeList();
    }
    
    return true;
}

void Coordinator::loop() {
    uint32_t now = millis();
    
    // Feed the watchdog timer to prevent reset
    SystemWatchdog::feed();
    
    // WiFi loop
    if (wifi) {
        wifi->loop();
    }
    
    // MQTT loop
    if (mqtt) {
        mqtt->loop();
    }
    
    // ESP-NOW loop
    if (espNow) {
        espNow->loop();
    }
    
    // Check if pairing should end
    if (pairingActive && now >= pairingEndTime) {
        stopPairing();
    }
    
    // Send health pings every 5 seconds
    if (now - lastHealthPingMs >= 5000) {
        lastHealthPingMs = now;
        sendHealthPings();
    }
    
    // Log status every 10 seconds
    if (now - lastStatusLogMs >= 10000) {
        lastStatusLogMs = now;
        logConnectedNodes();
    }
    
    // Publish status to MQTT every 30 seconds
    if (mqtt && mqtt->isConnected() && now - lastMqttPublishMs >= 30000) {
        lastMqttPublishMs = now;
        publishNodeList();
    }
}

void Coordinator::handleNodeMessage(const String& nodeId, const uint8_t* data, size_t len) {
    Logger::info("Message from node %s (%d bytes)", nodeId.c_str(), (int)len);
    
    // Update node registry
    if (nodes) {
        nodes->updateNodeStatus(nodeId, 100);
    }
    
    // Parse message
    String payload((const char*)data, len);
    EspNowMessage* msg = MessageFactory::createMessage(payload);
    
    if (msg) {
        if (msg->type == MessageType::NODE_STATUS) {
            NodeStatusMessage* status = static_cast<NodeStatusMessage*>(msg);
            Logger::info("  Status: R=%d G=%d B=%d W=%d Temp=%.1fC Button=%s",
                status->avg_r, status->avg_g, status->avg_b, status->avg_w,
                status->temperature,
                status->button_pressed ? "PRESSED" : "Released");
                
            // Publish to MQTT
            if (mqtt && mqtt->isConnected()) {
                mqtt->publishNodeStatus(*status);
            }
        }
        delete msg;
    }
}

void Coordinator::handlePairingRequest(const uint8_t* mac, const uint8_t* data, size_t len) {
    char macStr[18];
    snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    
    Logger::info("Pairing request from %s", macStr);
    
    if (!pairingActive) {
        Logger::warn("  Pairing not active - ignoring (start pairing via MQTT or wait for auto-enable)");
        return;
    }
    
    // Parse join request
    String payload((const char*)data, len);
    EspNowMessage* msg = MessageFactory::createMessage(payload);
    
    if (msg && msg->type == MessageType::JOIN_REQUEST) {
        JoinRequestMessage* joinReq = static_cast<JoinRequestMessage*>(msg);
        
        Logger::info("  Join Request:");
        Logger::info("    MAC: %s", joinReq->mac.c_str());
        Logger::info("    FW: %s", joinReq->fw.c_str());
        Logger::info("    RGBW: %s", joinReq->caps.rgbw ? "Yes" : "No");
        Logger::info("    LEDs: %d", joinReq->caps.led_count);
        
        // Generate node ID and light ID
        String nodeId = joinReq->mac;
        nodeId.replace(":", "");
        nodeId = "N" + nodeId.substring(nodeId.length() - 6);
        
        String lightId = "L" + nodeId.substring(1);
        
        // Notify via MQTT that a node wants to pair
        if (mqtt && mqtt->isConnected()) {
            StaticJsonDocument<512> doc;
            doc["event"] = "pairing_request";
            doc["mac"] = joinReq->mac;
            doc["node_id"] = nodeId;
            doc["light_id"] = lightId;
            doc["firmware"] = joinReq->fw;
            doc["rssi"] = espNow->getPeerRssi(joinReq->mac);
            doc["capabilities"]["rgbw"] = joinReq->caps.rgbw;
            doc["capabilities"]["led_count"] = joinReq->caps.led_count;
            doc["capabilities"]["temp_sensor"] = joinReq->caps.temp_i2c;
            doc["capabilities"]["button"] = joinReq->caps.button;
            
            String topicBase = mqtt->getFarmId() + "/" + mqtt->getCoordinatorId();
            String topic = topicBase + "/pairing/events";
            
            String jsonStr;
            serializeJson(doc, jsonStr);
            // Note: We can't publish directly here without PubSubClient access
            // The MQTT class would need a publishJson() method
            Logger::info("  Pairing request detected - waiting for frontend approval via MQTT");
        }
        
        // For now, auto-accept (frontend can control via MQTT commands)
        Logger::info("  Auto-accepting pairing request...");
        
        // Add to registry
        if (nodes) {
            nodes->registerNode(nodeId, lightId);
            nodes->saveToStorage();
        }
        
        // Add peer
        uint8_t macBytes[6];
        if (EspNow::macStringToBytes(joinReq->mac, macBytes)) {
            espNow->addPeer(macBytes);
            espNow->savePeersToStorage();
        }
        
        // Send acceptance
        JoinAcceptMessage accept;
        accept.node_id = nodeId;
        accept.light_id = lightId;
        accept.wifi_channel = WiFi.channel();
        accept.cfg.pwm_freq = 5000;
        accept.cfg.rx_window_ms = 100;
        accept.cfg.rx_period_ms = 1000;
        
        String acceptJson = accept.toJson();
        espNow->sendToMac(macBytes, acceptJson);
        
        Logger::info("  ✓ Paired: %s -> %s", nodeId.c_str(), lightId.c_str());
        
        // Publish updated node list
        if (mqtt && mqtt->isConnected()) {
            publishNodeList();
        }
        
        delete msg;
    }
}

void Coordinator::handleSendError(const String& nodeId) {
    Logger::warn("Send error to node %s", nodeId.c_str());
}

void Coordinator::handleMqttCommand(const String& topic, const String& payload) {
    Logger::info("MQTT command: %s", topic.c_str());
    Logger::info("  Payload: %s", payload.c_str());
    
    // Parse command
    StaticJsonDocument<512> doc;
    DeserializationError error = deserializeJson(doc, payload);
    
    if (error) {
        Logger::error("Failed to parse MQTT command: %s", error.c_str());
        return;
    }
    
    String cmd = doc["command"] | "";
    
    if (cmd == "start_pairing") {
        uint32_t durationMs = doc["duration_ms"] | 60000;
        startPairing(durationMs);
        publishPairingStatus();
    }
    else if (cmd == "stop_pairing") {
        stopPairing();
        publishPairingStatus();
    }
    else if (cmd == "list_nodes") {
        publishNodeList();
    }
    else if (cmd == "unpair_node") {
        String nodeId = doc["node_id"] | "";
        if (!nodeId.isEmpty() && nodes) {
            nodes->unregisterNode(nodeId);
            nodes->saveToStorage();
            Logger::info("Unpaired node: %s", nodeId.c_str());
            publishNodeList();
        }
    }
    else if (cmd == "send_light_command") {
        String nodeId = doc["node_id"] | "";
        uint8_t r = doc["r"] | 0;
        uint8_t g = doc["g"] | 0;
        uint8_t b = doc["b"] | 0;
        uint8_t w = doc["w"] | 0;
        uint16_t fadeMs = doc["fade_ms"] | 0;
        
        if (!nodeId.isEmpty() && espNow) {
            espNow->sendColorCommand(nodeId, r, g, b, w, fadeMs);
            Logger::info("Sent color command to %s: R=%d G=%d B=%d W=%d", 
                nodeId.c_str(), r, g, b, w);
        }
    }
}

void Coordinator::logConnectedNodes() {
    if (!nodes) return;
    
    auto allNodes = nodes->getAllNodes();
    uint32_t now = millis();
    int onlineCount = 0;
    
    for (const auto& node : allNodes) {
        bool online = (node.lastSeenMs > 0 && (now - node.lastSeenMs) <= 10000);
        if (online) onlineCount++;
    }
    
    // Enhanced status log with more coordinator details
    uint32_t uptimeSeconds = now / 1000;
    uint32_t freeHeap = ESP.getFreeHeap();
    uint32_t heapSize = ESP.getHeapSize();
    int8_t rssi = wifi && wifi->isConnected() ? WiFi.RSSI() : 0;
    String ipAddr = wifi && wifi->isConnected() ? WiFi.localIP().toString() : "0.0.0.0";
    
    Logger::info("📊 Coordinator Status: Nodes=%d/%d online | Uptime=%lus | Mem=%lu/%luKB free | WiFi=%ddBm (%s) | MQTT=%s | IP=%s", 
        onlineCount, allNodes.size(),
        uptimeSeconds,
        freeHeap / 1024, heapSize / 1024,
        rssi,
        wifi && wifi->isConnected() ? "OK" : "Disconnected",
        mqtt && mqtt->isConnected() ? "Connected" : "Disconnected",
        ipAddr.c_str());
    
    if (allNodes.size() > 0) {
        for (const auto& node : allNodes) {
            bool online = (node.lastSeenMs > 0 && (now - node.lastSeenMs) <= 10000);
            uint32_t ago = (node.lastSeenMs > 0) ? (now - node.lastSeenMs) / 1000 : 999;
            Logger::info("  🌱 Tower %s -> Light %s [%s] (last seen %ds ago)",
                node.towerId.c_str(),
                node.lightId.c_str(),
                online ? "ONLINE" : "OFFLINE",
                ago);
        }
    }
}

void Coordinator::sendHealthPings() {
    if (!nodes || !espNow) return;
    
    auto allNodes = nodes->getAllNodes();
    for (const auto& node : allNodes) {
        // Send a simple ping
        String ping = "{\"msg\":\"ping\",\"ts\":" + String(millis()) + "}";
        uint8_t mac[6];
        if (EspNow::macStringToBytes(node.towerId, mac)) {
            espNow->sendToMac(mac, ping);
        }
    }
}

void Coordinator::startPairing(uint32_t durationMs) {
    pairingActive = true;
    pairingEndTime = millis() + durationMs;
    
    if (espNow) {
        espNow->enablePairingMode(durationMs);
    }
    
    Logger::info("Pairing mode enabled for %d seconds", durationMs / 1000);
}

void Coordinator::stopPairing() {
    pairingActive = false;
    
    if (espNow) {
        espNow->disablePairingMode();
    }
    
    Logger::info("Pairing mode disabled");
}

void Coordinator::publishPairingStatus() {
    if (!mqtt || !mqtt->isConnected()) return;
    
    StaticJsonDocument<256> doc;
    doc["pairing_active"] = pairingActive;
    if (pairingActive) {
        doc["time_remaining_ms"] = pairingEndTime - millis();
    } else {
        doc["time_remaining_ms"] = 0;
    }
    
    String topicBase = mqtt->getFarmId() + "/" + mqtt->getCoordinatorId();
    String topic = topicBase + "/pairing/status";
    
    String jsonStr;
    serializeJson(doc, jsonStr);
    
    // We need to add a publishRaw() method to Mqtt class
    // For now, log it
    Logger::info("Pairing status: %s", jsonStr.c_str());
}

void Coordinator::publishNodeList() {
    if (!mqtt || !mqtt->isConnected() || !nodes) return;
    
    auto allNodes = nodes->getAllNodes();
    uint32_t now = millis();
    
    DynamicJsonDocument doc(2048);
    JsonArray nodesArray = doc["nodes"].to<JsonArray>();
    
    for (const auto& node : allNodes) {
        JsonObject nodeObj = nodesArray.createNestedObject();
        nodeObj["node_id"] = node.towerId;
        nodeObj["light_id"] = node.lightId;
        nodeObj["last_duty"] = node.lastDuty;
        
        bool online = (node.lastSeenMs > 0 && (now - node.lastSeenMs) <= 10000);
        nodeObj["online"] = online;
        nodeObj["last_seen_ms"] = node.lastSeenMs;
        
        if (espNow) {
            nodeObj["rssi"] = espNow->getPeerRssi(node.towerId);
        }
    }
    
    doc["count"] = allNodes.size();
    doc["timestamp"] = now;
    
    String topicBase = mqtt->getFarmId() + "/" + mqtt->getCoordinatorId();
    String topic = topicBase + "/nodes/list";
    
    String jsonStr;
    serializeJson(doc, jsonStr);
    
    Logger::info("Node list: %s", jsonStr.c_str());
}

// ============================================================================
// Connection Status Change Handler - Real-Time Event Publishing
// ============================================================================

void Coordinator::handleConnectionStatusChange(const String& event, const String& detail) {
    Logger::info("🔄 Connection status changed: %s (detail: %s)", event.c_str(), detail.c_str());
    
    // Publish to MQTT if available (will fail gracefully if MQTT is down)
    if (mqtt && mqtt->isConnected()) {
        mqtt->publishConnectionEvent(event, detail);
    }
    
    // Also send as serial log for visibility in /logs page
    String logMessage = "Connection event: " + event;
    if (detail.length() > 0) {
        logMessage += " (" + detail + ")";
    }
    
    // Log at appropriate level based on event type
    if (event == "wifi_disconnected" || event == "mqtt_disconnected") {
        Logger::error("📡 %s", logMessage.c_str());
    } else if (event == "wifi_connected" || event == "mqtt_connected" || event == "wifi_got_ip") {
        Logger::info("📡 %s", logMessage.c_str());
    } else {
        Logger::info("📡 %s", logMessage.c_str());
    }
}
