#include "Reservoir.h"
#include "../utils/Logger.h"
#include "../../shared/src/EspNowMessage.h"
#include "../../shared/src/ConfigManager.h"
#include "../comm/WifiManager.h"
#include "../sensors/AmbientLightSensor.h"
#include <algorithm>
#include <ArduinoJson.h>
#if defined(ARDUINO_ARCH_ESP32)
#include <esp32-hal.h>
#include <esp_wifi.h>
#endif

Reservoir::Reservoir()
    : espNow(nullptr)
    , mqtt(nullptr)
    , mmWave(nullptr)
    , towers(nullptr)
    , zones(nullptr)
    , buttons(nullptr)
    , thermal(nullptr)
    , wifi(nullptr)
    , ambientLight(nullptr)
    , manualLedMode(false)
    , manualR(0)
    , manualG(0)
    , manualB(0)
    , manualLedTimeoutMs(0) {}


Reservoir::~Reservoir() {
    // Clean up in reverse order of initialization
    if (thermal) { delete thermal; thermal = nullptr; }
    if (buttons) { delete buttons; buttons = nullptr; }
    if (zones) { delete zones; zones = nullptr; }
    if (towers) { delete towers; towers = nullptr; }
    if (mmWave) { delete mmWave; mmWave = nullptr; }
    if (ambientLight) { delete ambientLight; ambientLight = nullptr; }
    if (mqtt) { delete mqtt; mqtt = nullptr; }
    if (wifi) { delete wifi; wifi = nullptr; }
    if (espNow) { delete espNow; espNow = nullptr; }
}

bool Reservoir::begin() {
    // Don't call Logger::begin here - it's already called in main.cpp
    Logger::setMinLevel(Logger::INFO); // Reduce noise: default to INFO
    delay(500);
    bootStatus.clear();
    
    Logger::info("Smart Hydroponic Reservoir starting...");
    publishLog("Smart Hydroponic Reservoir starting...", "INFO", "setup");

    espNow = new EspNow();
    mqtt = new Mqtt();
    mmWave = new MmWave();
    towers = new TowerRegistry();
    zones = new ZoneControl();
    buttons = new ButtonControl();
    thermal = new ThermalControl();
    wifi = new WifiManager();
    ambientLight = new AmbientLightSensor();

    Logger::info("Objects created, starting initialization...");

    // Initialize ESP-NOW first (before WiFi connects)
    Logger::info("Initializing ESP-NOW...");
    bool espNowOk = espNow->begin();
    recordBootStatus("ESP-NOW", espNowOk, espNowOk ? "Radio ready" : "init failed");
    if (!espNowOk) {
        Logger::error("Failed to initialize ESP-NOW");
        return false;
    }
    Logger::info("ESP-NOW initialized successfully");
    publishLog("ESP-NOW initialized successfully", "INFO", "setup");

    // Link EspNow to WiFi so channels sync on connection
    wifi->setEspNow(espNow);

    bool wifiReady = wifi && wifi->begin();
    WifiManager::Status wifiState;
    if (wifi) {
        wifiState = wifi->getStatus();
    }
    String wifiDetail;
    if (wifiReady && wifiState.connected) {
        wifiDetail = wifiState.ssid + " @ " + wifiState.ip.toString();
    } else if (wifiState.offlineMode) {
        wifiDetail = "Offline mode";
    } else {
        wifiDetail = "Needs setup";
    }
    recordBootStatus("Wi-Fi", wifiReady, wifiDetail);
    if (!wifiReady) {
        Logger::warn("Wi-Fi not connected at boot; continuing with offline fallback");
    }

    // Register message callback for regular tower messages
    espNow->setMessageCallback([this](const String& towerId, const uint8_t* data, size_t len) {
        if (data && len > 0) {
            this->handleTowerMessage(towerId, data, len);
        }
    });

    // Register send error callback for visual feedback
    espNow->setSendErrorCallback([this](const String& towerId) {
        // Flash red on all LEDs to indicate send failure
        statusLed.pulse(180, 0, 0, 200); // Red flash for 200ms
        Logger::warn("ESP-NOW send failed to tower %s - showing red flash", towerId.c_str());
    });
    
    // Register pairing callback to handle join requests coming from towers
    espNow->setPairingCallback([this](const uint8_t* mac, const uint8_t* data, size_t len) {
        if (!mac || !data || len == 0) {
            Logger::warn("Invalid pairing callback parameters");
            return;
        }
        String payload((const char*)data, len);
        MessageType mt = MessageFactory::getMessageType(payload);
        if (mt != MessageType::JOIN_REQUEST) {
            Logger::warn("Pairing callback: unexpected message type");
            return;
        }

        // Format MAC string
        char macStr[18];
        snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X",
                 mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
        String towerId(macStr);

        if (!towers->isPairingActive()) {
            Logger::warn("Rejecting pairing from %s: pairing not active", macStr);
            return;
        }

        bool regOk = towers->processPairingRequest(mac, towerId);
        if (!regOk) {
            Logger::warn("Failed to register tower %s during pairing", macStr);
            return;
        }

        // Add as ESP-NOW peer (unencrypted) - this now handles duplicates gracefully
        espNow->addPeer(mac);

        // Get current WiFi channel
        uint8_t currentChannel = 1;
        wifi_second_chan_t second = WIFI_SECOND_CHAN_NONE;
        esp_wifi_get_channel(&currentChannel, &second);
        
        JoinAcceptMessage accept;
        accept.node_id = towerId;  // Keep wire format field name for compatibility
        accept.light_id = towers->getLightForTower(towerId);
        accept.lmk = ""; // Unencrypted for now
        accept.wifi_channel = currentChannel; // Tell tower which channel to use
        accept.cfg.pwm_freq = 0; // Not used but set explicitly
        accept.cfg.rx_window_ms = 20;
        accept.cfg.rx_period_ms = 100;
        String json = accept.toJson();
        
        // Debug: log the message we're sending
        Logger::info("JOIN_ACCEPT message (%d bytes): %s", json.length(), json.c_str());

        // send back to tower mac (will auto-add peer if missing)
        if (!espNow->sendToMac(mac, json)) {
            Logger::warn("Failed to send join_accept to %s", macStr);
        } else {
            Logger::info("Sent join_accept to %s", macStr);
        }

        // Assign LED and give brief green flash for connection feedback
        int idx = assignGroupForTower(towerId);
        if (idx >= 0) {
            groupConnected[idx] = true; // mark active connection
            flashLedForTower(towerId, 400); // Longer flash for pairing success
        }
        
        // Show "OK" confirmation: all LEDs green pulse for 300ms
        statusLed.pulse(0, 150, 0, 300); // Green confirmation flash
        Logger::info("Pairing successful - OK confirmation shown");
    });

    if (mqtt && wifi) {
        mqtt->setWifiManager(wifi);
    }

    Logger::info("Initializing MQTT...");
    bool mqttInitOk = mqtt->begin();
    bool mqttConnected = mqtt->isConnected();
    String brokerLabel = mqtt->getBrokerHost().isEmpty() ? String("auto") : mqtt->getBrokerHost();
    recordBootStatus("MQTT", mqttConnected, mqttConnected ? String("Connected ") + brokerLabel : String("Waiting on ") + brokerLabel);
    if (!mqttInitOk) {
        Logger::error("Failed to initialize MQTT");
        return false;
    }
    Logger::info("MQTT initialized successfully");
    mqtt->setCommandCallback([this](const String& topic, const String& payload) {
        this->handleMqttCommand(topic, payload);
    });

    Logger::info("Initializing mmWave sensor...");
    bool mmWaveOk = mmWave->begin();
    bool mmWaveOnline = mmWave->isOnline();
    recordBootStatus("mmWave", mmWaveOnline, mmWaveOnline ? "LD2450 streaming" : "will retry");
    if (!mmWaveOk) {
        Logger::warn("Failed to initialize mmWave sensor - continuing without it");
    } else if (!mmWaveOnline) {
        Logger::warn("mmWave sensor initialized but no stream detected - will retry in background");
    } else {
        Logger::info("mmWave initialized successfully");
    }

    Logger::info("Initializing tower registry...");
    bool towersOk = towers->begin();
    recordBootStatus("Towers", towersOk, towersOk ? "registry ready" : "init failed");
    if (!towersOk) {
        Logger::error("Failed to initialize tower registry");
        return false;
    }
    Logger::info("Tower registry initialized successfully");
    publishLog("Tower registry initialized successfully", "INFO", "setup");
    
    // Auto-pair all stored towers with ESP-NOW on boot
    auto storedTowers = towers->getAllTowerMacs();
    if (storedTowers.size() > 0) {
        Logger::info("Auto-pairing %d stored tower(s) with ESP-NOW...", storedTowers.size());
        for (const auto& macStr : storedTowers) {
            uint8_t mac[6];
            if (EspNow::macStringToBytes(macStr, mac)) {
                if (espNow->addPeer(mac)) {
                    Logger::info("  Re-paired tower: %s", macStr.c_str());
                } else {
                    Logger::warn("  Failed to re-pair tower: %s", macStr.c_str());
                }
            } else {
                Logger::error("  Invalid MAC format: %s", macStr.c_str());
            }
        }
        Logger::info("Auto-pairing complete: %d/%d towers paired", storedTowers.size(), storedTowers.size());
    } else {
        Logger::info("No stored towers to auto-pair");
    }
    
    towers->setTowerRegisteredCallback([this](const String& towerId, const String& lightId) {
        Logger::info("Tower %s paired to light %s", towerId.c_str(), lightId.c_str());
        if (espNow) {
            espNow->disablePairingMode();
        }
        if (towers && towers->isPairingActive()) {
            towers->stopPairing();
        }
        statusLed.pulse(0, 150, 0, 400);
    });

    // Initialize onboard status LED
    statusLed.begin();
    delay(50);  // Give RMT driver time to initialize
    
    // Test pattern: cycle through all pixels briefly to verify SK6812B strip is working
    // Temporarily disabled due to ESP32-S3 RMT/WiFi conflict
    // Logger::info("Testing SK6812B strip (%d pixels)...", Pins::RgbLed::NUM_PIXELS);
    // for (uint8_t i = 0; i < Pins::RgbLed::NUM_PIXELS; ++i) {
    //     statusLed.clear();
    //     statusLed.setPixel(i, 0, 100, 0); // green
    //     statusLed.show();
    //     delay(100);
    // }
    // statusLed.clear();
    Logger::info("SK6812B strip initialized (test pattern skipped)");;
    
    // Initialize LED group mapping containers (4 pixels per group)
    int groupCount = Pins::RgbLed::NUM_PIXELS / 4;
    groupToTower.assign(groupCount, String());
    groupConnected.assign(groupCount, false);
    groupFlashUntilMs.assign(groupCount, 0);
    rebuildLedMappingFromRegistry();

    bool zonesOk = zones->begin();
    recordBootStatus("Zones", zonesOk, zonesOk ? "control ready" : "init failed");
    if (!zonesOk) {
        Logger::error("Failed to initialize zone control");
        return false;
    }

    // Disable idle breathing; LEDs indicate tower connections per requirements
    statusLed.setIdleBreathing(false);

    bool buttonsOk = buttons->begin();
    recordBootStatus("Button", buttonsOk, buttonsOk ? "GPIO ready" : "init failed");
    if (!buttonsOk) {
        Logger::error("Failed to initialize button control");
        return false;
    }

    bool thermalOk = thermal->begin();
    recordBootStatus("Thermal", thermalOk, thermalOk ? "monitoring" : "init failed");
    if (!thermalOk) {
        Logger::error("Failed to initialize thermal control");
        return false;
    }

    bool ambientOk = true;
    if (ambientLight && !ambientLight->begin()) {
        Logger::warn("TSL2561 ambient light sensor init failed (continuing)");
        ambientOk = false;
    }
    recordBootStatus("Ambient", ambientOk, ambientOk ? "TSL2561 ready" : "sensor offline");

    // Register event handlers
    mmWave->setEventCallback([this](const MmWaveEvent& event) {
        this->onMmWaveEvent(event);
    });

    thermal->registerThermalAlertCallback([this](const String& towerId, const TowerThermalData& data) {
        this->onThermalEvent(towerId, data);
    });

    buttons->setEventCallback([this](const String& buttonId, bool pressed) {
        this->onButtonEvent(buttonId, pressed);
    });
    
    buttons->setLongPressCallback([this]() {
        // Long press: flash all connected towers on white channel while held
        this->longPressActive = true;
        this->startFlashAll();
    });
    
    buttons->setVeryLongPressCallback([this]() {
        // Very long press (10s): clear all known towers
        Logger::info("===========================================");
        Logger::info("CLEARING ALL TOWERS (10s hold detected)");
        Logger::info("===========================================");
        if (towers) {
            towers->clearAllTowers();
        }
        if (espNow) {
            espNow->clearAllPeers(); // Clear ESP-NOW peers
        }
        // Rebuild LED mapping to clear visual indicators
        rebuildLedMappingFromRegistry();
        updateLeds();
        Logger::info("All towers cleared. Release button to continue.");
        Logger::info("===========================================");
    });

printBootSummary();
Logger::info("Reservoir initialization complete");
Logger::info("==============================================");
Logger::info("System ready! Press BOOT button to pair towers.");
Logger::info("Hold 4s to run wave test on paired towers.");
logConnectedTowers();
Logger::info("==============================================");
return true;
}

void Reservoir::loop() {
    // Handle serial commands (must be before other loops to respond quickly)
    handleSerialCommands();
    
    if (wifi) {
        wifi->loop();
    }
    // Use guard checks to prevent null pointer crashes
    if (espNow) espNow->loop();
    if (mqtt) mqtt->loop();
    if (mmWave) mmWave->loop();
    if (towers) towers->loop();
    if (zones) zones->loop();
    if (buttons) buttons->loop();
    if (thermal) thermal->loop();
    
    // Update status LED pulse (pairing) if active
    statusLed.loop();

    // While flashing mode is active and button is held, tick the flash
    if (flashAllActive && buttonDown) {
        flashAllTick(millis());
    }

    // Always update per-tower LEDs (show connection state)
    if (!statusLed.isPulsing()) {
        updateLeds();
    }

    // Periodically ping and check staleness
    static uint32_t lastPing = 0;
    static uint32_t lastStaleCheck = 0;
    uint32_t now = millis();
    if (now - lastPing > 2000) {
        sendHealthPings();
        lastPing = now;
    }
    if (now - lastStaleCheck > 5000) {
        checkStaleConnections();
        lastStaleCheck = now;
    }

    refreshReservoirSensors();
    printSerialTelemetry();
}

void Reservoir::onMmWaveEvent(const MmWaveEvent& event) {
    lastMmWaveEvent = event;
    haveMmWaveSample = true;
    // Guard against null pointers
    if (!zones || !towers || !thermal || !espNow || !mqtt) {
        Logger::error("Cannot process mmWave event: components not initialized");
        return;
    }
    
    // Publish raw mmWave frame to MQTT for backend/frontend consumption
    mqtt->publishMmWaveEvent(event);

    // Only send lighting commands when zone state CHANGES (not every frame)
    // This prevents flickering and allows manual control when out of zone
    if (event.zoneOccupied != zoneOccupiedState) {
        zoneOccupiedState = event.zoneOccupied;
        
        auto allTowers = towers->getAllTowers();
        for (const auto& towerInfo : allTowers) {
            if (zoneOccupiedState) {
                // Just entered zone: turn all tower LEDs GREEN
                espNow->sendColorCommand(towerInfo.towerId, 0, 255, 0, 0, 200); // Green, 200ms fade
                Logger::info("ENTERED ZONE - sending GREEN to tower %s", towerInfo.towerId.c_str());
            } else {
                // Just left zone: turn off LEDs (available for manual control)
                espNow->sendColorCommand(towerInfo.towerId, 0, 0, 0, 0, 200); // Off, 200ms fade
                Logger::info("LEFT ZONE - turning off tower %s (manual control available)", towerInfo.towerId.c_str());
            }
            
            // Publish state change to MQTT
            mqtt->publishLightState(towerInfo.lightId, zoneOccupiedState ? 255 : 0);
        }
    }
}

void Reservoir::onThermalEvent(const String& towerId, const TowerThermalData& data) {
    // Guard against null pointers
    if (!mqtt || !towers || !zones || !espNow) {
        Logger::error("Cannot process thermal event: components not initialized");
        return;
    }
    
    // Handle thermal alerts
    Logger::warning("Thermal alert for tower %s: %.1f C, deration: %d%%",
                   towerId.c_str(), data.temperature, data.derationLevel);
                   
    // Publish thermal event to MQTT
    mqtt->publishThermalEvent(towerId, data);
    
    // If tower is currently active, update its brightness with deration
    auto lightId = towers->getLightForTower(towerId);
    if (!lightId.isEmpty() && zones->isLightActive(lightId)) {
        espNow->sendLightCommand(towerId, data.derationLevel);
    }
}

void Reservoir::onButtonEvent(const String& buttonId, bool pressed) {
    // Guard against null pointers
    if (!towers || !espNow) {
        Logger::error("Cannot process button event: components not initialized");
        return;
    }

    if (pressed) {
        // Begin press: track state, do not enter pairing yet
        buttonDown = true;
        buttonPressedAt = millis();
        longPressActive = false;
        return;
    }

    // On release
    buttonDown = false;
    if (longPressActive) {
        // Stop flashing and suppress pairing
        stopFlashAll();
        longPressActive = false;
        return;
    }

    const uint32_t windowMs = 60000; // 60s pairing window
    startPairingWindow(windowMs, "button");
}

void Reservoir::handleTowerMessage(const String& towerId, const uint8_t* data, size_t len) {
    // Parse the JSON payload to detect message type
    String payload((const char*)data, len);
    MessageType mt = MessageFactory::getMessageType(payload);

    // Auto-accept any tower JOIN_REQUEST (no formal pairing required)
    if (mt == MessageType::JOIN_REQUEST && towers) {
        // towerId is the MAC string - add as peer first
        uint8_t mac[6];
        if (EspNow::macStringToBytes(towerId, mac)) {
            espNow->addPeer(mac);
        }
        
        // Auto-register if not already known
        String existingLight = towers->getLightForTower(towerId);
        if (existingLight.length() == 0) {
            // Generate light ID from MAC last 3 bytes
            char lightIdBuf[16];
            snprintf(lightIdBuf, sizeof(lightIdBuf), "L%s", towerId.substring(towerId.length() - 8).c_str());
            // Remove colons
            String lightId = String(lightIdBuf);
            lightId.replace(":", "");
            towers->registerTower(towerId, lightId);
            existingLight = lightId;
            Logger::info("Auto-registered tower %s as %s", towerId.c_str(), lightId.c_str());
        }
        
        // Always respond with join_accept
        JoinAcceptMessage accept;
        accept.node_id = towerId;  // Keep wire format field name for compatibility
        accept.light_id = existingLight;
        accept.lmk = "";
        // CRITICAL: Include current WiFi channel so tower can switch
        uint8_t currentChannel = 1;
        wifi_second_chan_t second = WIFI_SECOND_CHAN_NONE;
        esp_wifi_get_channel(&currentChannel, &second);
        accept.wifi_channel = currentChannel;
        accept.cfg.rx_window_ms = 20;
        accept.cfg.rx_period_ms = 100;
        String json = accept.toJson();
        
        uint8_t mac2[6];
        if (EspNow::macStringToBytes(towerId, mac2)) {
            if (!espNow->sendToMac(mac2, json)) {
                Logger::warn("Failed to send join_accept to %s", towerId.c_str());
            } else {
                Logger::info("Sent join_accept to %s", towerId.c_str());
            }
        }
    }

    // Update last-seen for any message first
    if (towers) {
        towers->updateTowerStatus(towerId, 0);
    }
    
    // Ensure we have a group assigned for this tower
    int idx = getGroupIndexForTower(towerId);
    if (idx < 0) {
        idx = assignGroupForTower(towerId);
        Logger::info("Assigned group %d to tower %s", idx + 1, towerId.c_str());
    }
    if (idx >= 0) {
        if (!groupConnected[idx]) {
            Logger::info("[Tower %d] %s CONNECTED", idx + 1, towerId.c_str());
        }
        groupConnected[idx] = true; // mark as active connection
        flashLedForTower(towerId, 150); // brief activity flash on each message
    }

    // Improved logging per tower index with MAC
    Logger::info("[Tower %d] %s %s | %d bytes", 
                 idx >= 0 ? idx + 1 : 0,
                 towerId.c_str(),
                 mt == MessageType::TOWER_STATUS ? "STATUS" : "MESSAGE", 
                 (int)len);

    // Mark last seen on status and log sensor data
    if (mt == MessageType::TOWER_STATUS && towers) {
        towers->updateTowerStatus(towerId, 0);
        
        // Parse and log sensor data from telemetry
        EspNowMessage* msg = MessageFactory::createMessage(payload);
        if (msg && msg->type == MessageType::TOWER_STATUS) {
            TowerStatusMessage* statusMsg = static_cast<TowerStatusMessage*>(msg);
            updateTowerTelemetryCache(towerId, *statusMsg);
            
            // Log temperature if available
            if (statusMsg->temperature > -50.0f && statusMsg->temperature < 150.0f) {
                Logger::info("  [Tower %d] Temperature: %.2f C", 
                             idx >= 0 ? idx + 1 : 0, 
                             statusMsg->temperature);
            }
            
            // Log button state
            Logger::info("  [Tower %d] Button: %s, RGBW: (%d,%d,%d,%d)", 
                         idx >= 0 ? idx + 1 : 0,
                         statusMsg->button_pressed ? "PRESSED" : "Released",
                         statusMsg->avg_r, statusMsg->avg_g, statusMsg->avg_b, statusMsg->avg_w);
            
            delete msg;
        }
        
        // Send ACK back to tower to keep connection alive
        uint8_t mac[6];
        if (EspNow::macStringToBytes(towerId, mac)) {
            AckMessage ack;
            ack.cmd_id = "telemetry_ack";
            String ackJson = ack.toJson();
            if (!espNow->sendToMac(mac, ackJson)) {
                Logger::debug("Failed to send telemetry ACK to %s", towerId.c_str());
            }
        }
    }

    // ===== Hydroponic Tower Telemetry Forwarding =====
    // Forward tower telemetry from ESP-NOW to MQTT for backend/dashboard consumption
    if (mt == MessageType::TOWER_TELEMETRY && mqtt) {
        EspNowMessage* msg = MessageFactory::createMessage(payload);
        if (msg && msg->type == MessageType::TOWER_TELEMETRY) {
            TowerTelemetryMessage* telemetry = static_cast<TowerTelemetryMessage*>(msg);
            
            // Log tower environmental data
            Logger::info("[Tower %s] Air: %.1f C, Humidity: %.1f%%, Light: %.0f lux", 
                         telemetry->tower_id.c_str(),
                         telemetry->air_temp_c, 
                         telemetry->humidity_pct,
                         telemetry->light_lux);
            Logger::info("[Tower %s] Pump: %s, Light: %s (brightness: %d)", 
                         telemetry->tower_id.c_str(),
                         telemetry->pump_on ? "ON" : "OFF",
                         telemetry->light_on ? "ON" : "OFF",
                         telemetry->light_brightness);
            
            // Forward to MQTT broker
            mqtt->publishTowerTelemetry(*telemetry);
            
            // Send ACK back to tower
            uint8_t mac[6];
            if (EspNow::macStringToBytes(towerId, mac)) {
                AckMessage ack;
                ack.cmd_id = "tower_telemetry_ack";
                String ackJson = ack.toJson();
                if (!espNow->sendToMac(mac, ackJson)) {
                    Logger::debug("Failed to send tower telemetry ACK to %s", towerId.c_str());
                }
            }
            
            delete msg;
        }
    }

    // ===== Hydroponic Tower Join Request =====
    // Handle tower-specific join requests (different from legacy join)
    if (mt == MessageType::TOWER_JOIN_REQUEST && mqtt) {
        EspNowMessage* msg = MessageFactory::createMessage(payload);
        if (msg && msg->type == MessageType::TOWER_JOIN_REQUEST) {
            TowerJoinRequestMessage* joinReq = static_cast<TowerJoinRequestMessage*>(msg);
            
            // Add as ESP-NOW peer
            uint8_t mac[6];
            if (EspNow::macStringToBytes(towerId, mac)) {
                espNow->addPeer(mac);
            }
            
            // Generate tower ID from MAC
            char towerIdBuf[24];
            snprintf(towerIdBuf, sizeof(towerIdBuf), "T%s", towerId.substring(towerId.length() - 8).c_str());
            String assignedTowerId = String(towerIdBuf);
            assignedTowerId.replace(":", "");
            
            Logger::info("Tower join request from %s (FW: %s), assigning ID: %s", 
                         towerId.c_str(), joinReq->fw.c_str(), assignedTowerId.c_str());
            Logger::info("  Capabilities: DHT=%d, Light=%d, Pump=%d, GrowLight=%d, Slots=%d",
                         joinReq->caps.dht_sensor, joinReq->caps.light_sensor,
                         joinReq->caps.pump_relay, joinReq->caps.grow_light,
                         joinReq->caps.slot_count);
            
            // Send join accept response
            TowerJoinAcceptMessage accept;
            accept.tower_id = assignedTowerId;
            accept.coord_id = mqtt->getReservoirId();  // Use new method name
            accept.farm_id = mqtt->getFarmId();
            accept.lmk = "";  // TODO: implement secure pairing with LMK
            
            // Get current WiFi channel
            uint8_t currentChannel = 1;
            wifi_second_chan_t second = WIFI_SECOND_CHAN_NONE;
            esp_wifi_get_channel(&currentChannel, &second);
            accept.wifi_channel = currentChannel;
            
            // Default configuration
            accept.cfg.telemetry_interval_ms = 30000;  // 30 seconds
            accept.cfg.pump_max_duration_s = 300;      // 5 minutes max
            
            String json = accept.toJson();
            if (EspNow::macStringToBytes(towerId, mac)) {
                if (!espNow->sendToMac(mac, json)) {
                    Logger::warn("Failed to send tower_join_accept to %s", towerId.c_str());
                } else {
                    Logger::info("Sent tower_join_accept to %s (tower_id: %s)", 
                                 towerId.c_str(), assignedTowerId.c_str());
                }
            }
            
            delete msg;
        }
    }
}

// ===== LED mapping helpers =====
void Reservoir::rebuildLedMappingFromRegistry() {
    towerToGroup.clear();
    std::fill(groupToTower.begin(), groupToTower.end(), String());
    std::fill(groupConnected.begin(), groupConnected.end(), false);
    std::fill(groupFlashUntilMs.begin(), groupFlashUntilMs.end(), 0);

    if (!towers) return;
    // Assign deterministically by sorted towerId up to available groups
    auto list = towers->getAllTowers();
    std::sort(list.begin(), list.end(), [](const TowerInfo& a, const TowerInfo& b){ return a.towerId < b.towerId; });
    int maxGroups = Pins::RgbLed::NUM_PIXELS / 4;
    int idx = 0;
    uint32_t now = millis();
    for (const auto& t : list) {
        if (idx >= maxGroups) break;
        towerToGroup[t.towerId] = idx;
        groupToTower[idx] = t.towerId;
        // Mark as connected if recently seen (within last 6 seconds)
        groupConnected[idx] = (t.lastSeenMs > 0 && (now - t.lastSeenMs) <= 6000U);
        idx++;
    }
}

int Reservoir::getGroupIndexForTower(const String& towerId) {
    auto it = towerToGroup.find(towerId);
    if (it != towerToGroup.end()) return it->second;
    return -1;
}

int Reservoir::assignGroupForTower(const String& towerId) {
    // Already assigned?
    int cur = getGroupIndexForTower(towerId);
    if (cur >= 0) return cur;
    // Find first free group slot
    int groupCount = Pins::RgbLed::NUM_PIXELS / 4;
    for (int i = 0; i < groupCount; ++i) {
        if (groupToTower[i].length() == 0) {
            groupToTower[i] = towerId;
            towerToGroup[towerId] = i;
            return i;
        }
    }
    Logger::warn("No free LED group available for tower %s", towerId.c_str());
    return -1;
}

void Reservoir::flashLedForTower(const String& towerId, uint32_t durationMs) {
    int idx = getGroupIndexForTower(towerId);
    if (idx < 0) return;
    groupFlashUntilMs[idx] = millis() + durationMs;
}

void Reservoir::updateLeds() {
    uint32_t now = millis();
    
    // Check for manual LED override timeout
    if (manualLedMode && manualLedTimeoutMs > 0 && now > manualLedTimeoutMs) {
        manualLedMode = false;
        Logger::info("Manual LED override timed out");
    }

    int groupCount = Pins::RgbLed::NUM_PIXELS / 4;
    for (int g = 0; g < groupCount; ++g) {
        uint8_t r=0,gc=0,b=0;
        
        if (manualLedMode) {
            // Manual override active
            r = manualR;
            gc = manualG;
            b = manualB;
        } else if (groupFlashUntilMs[g] > now) {
            // Bright green flash (activity) at 50%
            r=0; gc=128; b=0;
        } else if (groupToTower[g].length() > 0) {
            if (groupConnected[g]) {
                // Solid green (dim) at 50%
                r=0; gc=45; b=0;
            } else {
                // Red for disconnected/stale tower at 50%
                r=90; gc=0; b=0;
            }
        } else {
            r=0; gc=0; b=0;
        }
        // Paint the 4-pixel group
        int base = g * 4;
        for (int k=0;k<4;k++) {
            statusLed.setPixel(base+k, r, gc, b);
        }
    }
    statusLed.show();
}

void Reservoir::logConnectedTowers() {
    if (!towers) return;
    auto allTowers = towers->getAllTowers();
    if (allTowers.empty()) {
        Logger::info("Connected towers: 0");
        return;
    }

    std::sort(allTowers.begin(), allTowers.end(), [](const TowerInfo& a, const TowerInfo& b) {
        return a.towerId < b.towerId;
    });

    Logger::info("Connected towers: %d", allTowers.size());
    for (const auto& tower : allTowers) {
        int idx = getGroupIndexForTower(tower.towerId);
        bool alive = (idx >= 0) ? groupConnected[idx] : false;
        Logger::info("  [Tower %d] %s -> %s [%s]",
                     idx >= 0 ? idx + 1 : 0,
                     tower.towerId.c_str(),
                     tower.lightId.c_str(),
                     alive ? "ONLINE" : "OFFLINE");
    }
}

void Reservoir::checkStaleConnections() {
    if (!towers) return;
    auto allTowers = towers->getAllTowers();
    uint32_t now = millis();

    for (const auto& tower : allTowers) {
        int idx = getGroupIndexForTower(tower.towerId);
        if (idx >= 0 && groupConnected[idx]) {
            // Mark disconnected if last seen > 6s
            if (tower.lastSeenMs > 0 && (now - tower.lastSeenMs) > 6000U) {
                groupConnected[idx] = false;
                Logger::warn("[Tower %d] DISCONNECTED (timeout)", idx + 1);
            }
        }
    }
}

void Reservoir::sendHealthPings() {
    if (!espNow) return;
    int groupCount = Pins::RgbLed::NUM_PIXELS / 4;
    for (int g = 0; g < groupCount; ++g) {
        // Only ping towers that currently appear connected
        if (groupToTower[g].length() == 0 || !groupConnected[g]) continue;
        uint8_t mac[6];
        if (!EspNow::macStringToBytes(groupToTower[g], mac)) continue;
        const String ping = "{\"msg\":\"ping\"}";
        espNow->sendToMac(mac, ping);
    }
}

void Reservoir::startFlashAll() {
    // Build list of connected towers
    if (!towers || !espNow) return;
    auto all = towers->getAllTowers();
    bool any = false;
    for (const auto& t : all) {
        int gi = getGroupIndexForTower(t.towerId);
        if (gi >= 0 && groupConnected[gi]) { any = true; break; }
    }
    if (!any) {
        Logger::info("No connected towers - flash-all suppressed");
        flashAllActive = false;
        return;
    }
    flashAllActive = true;
    flashOn = false; // will toggle to ON on first tick
    lastFlashTick = 0;
    Logger::info("Flash-all: ACTIVE (hold button to keep flashing)");
    // trigger first tick immediately for instant feedback
    flashAllTick(millis());
}

void Reservoir::stopFlashAll() {
    flashAllActive = false;
    flashOn = false;
    lastFlashTick = 0;
    Logger::info("Flash-all: STOPPED");
}

void Reservoir::flashAllTick(uint32_t now) {
    const uint32_t intervalMs = 350; // toggle cadence
    if (now - lastFlashTick < intervalMs) return;
    lastFlashTick = now;
    flashOn = !flashOn;

    // Send white on/off to all connected towers with short TTL and override_status
    auto all = towers->getAllTowers();
    for (const auto& t : all) {
        int gi = getGroupIndexForTower(t.towerId);
        if (gi < 0 || !groupConnected[gi]) continue;
        uint8_t level = flashOn ? 128 : 0; // 50% brightness
        // quick fade for nicer blink
        espNow->sendLightCommand(t.towerId, level, 60 /*fadeMs*/, true /*override*/, 500 /*ttl*/);
    }
}

void Reservoir::triggerTowerWaveTest() {
    if (!towers || !espNow) {
        Logger::warn("Cannot run tower wave test: components not initialized");
        return;
    }

    // Build list of currently connected towers only
    auto allTowers = towers->getAllTowers();
    std::vector<TowerInfo> connected;
    connected.reserve(allTowers.size());
    for (const auto& t : allTowers) {
        int gi = getGroupIndexForTower(t.towerId);
        if (gi >= 0 && groupConnected[gi]) connected.push_back(t);
    }
    if (connected.empty()) {
        Logger::info("No connected towers - wave test skipped");
        return;
    }

    Logger::info("Starting wave on %d connected tower(s)...", connected.size());

    // Deterministic order and synchronized start time across towers
    std::sort(connected.begin(), connected.end(), [](const TowerInfo& a, const TowerInfo& b) {
        return a.towerId < b.towerId;
    });

    const uint32_t now = millis();
    const uint32_t startAt = now + 300; // 300ms in the future to allow delivery jitter
    const uint16_t periodMs = 1200;
    const uint16_t durationMs = 4000;

    for (const auto& tower : connected) {
        uint8_t mac[6];
        if (!EspNow::macStringToBytes(tower.towerId, mac)) continue;
        // Include start_at to coordinate across towers
        String wave = String("{\"msg\":\"wave\",\"period_ms\":") + String(periodMs) +
                      ",\"duration_ms\":" + String(durationMs) +
                      ",\"start_at\":" + String(startAt) + "}";
        espNow->sendToMac(mac, wave);
    }

    Logger::info("Wave command sent");
}

void Reservoir::handleMqttCommand(const String& topic, const String& payload) {
    StaticJsonDocument<256> doc;
    DeserializationError err = deserializeJson(doc, payload);
    if (err) {
        Logger::warn("Failed to parse MQTT command (%s)", err.c_str());
        return;
    }

    String cmd = doc["cmd"] | "";
    cmd.toLowerCase();
    
    // Extract towerId from topic if it's a tower command (site/{siteId}/node/{nodeId}/cmd)
    // Note: MQTT topic format kept for backward compatibility
    String towerId = "";
    if (topic.indexOf("/node/") >= 0) {
        int towerStart = topic.indexOf("/node/") + 6;
        int towerEnd = topic.indexOf("/", towerStart);
        if (towerEnd > towerStart) {
            towerId = topic.substring(towerStart, towerEnd);
        }
    }
    
    if (cmd == "pair" || cmd == "pairing.start" || cmd == "enter_pairing_mode") {
        uint32_t windowMs = doc["duration_ms"] | 60000;
        startPairingWindow(windowMs, "mqtt");
    } else if (cmd == "pairing.stop") {
        if (towers) towers->stopPairing();
        if (espNow) espNow->disablePairingMode();
        Logger::info("Pairing window closed via MQTT command");
        Serial.println("Pairing window closed via MQTT command");
    } else if (cmd == "set_light" && towerId.length() > 0) {
        // Forward set_light command to tower via ESP-NOW
        uint8_t r = doc["r"] | 0;
        uint8_t g = doc["g"] | 0;
        uint8_t b = doc["b"] | 0;
        uint8_t w = doc["w"] | 0;
        uint16_t fadeMs = doc["fade_ms"] | 200;
        int8_t pixel = doc["pixel"] | -1;
        bool overrideStatus = doc["override"] | false;
        uint16_t ttlMs = doc["ttl_ms"] | 1500;
        
        Logger::info("set_light -> tower=%s RGBW(%d,%d,%d,%d) pixel=%d fade=%dms",
                     towerId.c_str(), r, g, b, w, pixel, fadeMs);
        
        if (espNow) {
            bool sent = espNow->sendColorCommand(towerId, r, g, b, w, fadeMs, overrideStatus, ttlMs, pixel);
            if (sent) {
                Logger::info("  ESP-NOW sent to %s", towerId.c_str());
            } else {
                Logger::warn("  ESP-NOW failed to %s", towerId.c_str());
            }
        } else {
            Logger::error("ESP-NOW not initialized, cannot send to tower");
        }
    } else if (cmd == "led.set") {
        manualR = doc["r"] | 0;
        manualG = doc["g"] | 0;
        manualB = doc["b"] | 0;
        uint32_t duration = doc["duration_ms"] | 0;
        
        manualLedMode = true;
        if (duration > 0) {
            manualLedTimeoutMs = millis() + duration;
        } else {
            manualLedTimeoutMs = 0; // Indefinite
        }
        Logger::info("Manual LED override: RGB(%d,%d,%d)", manualR, manualG, manualB);
        updateLeds();
    } else if (cmd == "led.reset") {
        manualLedMode = false;
        Logger::info("Manual LED override cleared");
        updateLeds();
    } else if (cmd == "update_config") {
        // Handle configuration updates from frontend
        JsonObject configObj = doc["config"];
        if (!configObj.isNull()) {
            ConfigManager config("reservoir");  // Updated namespace
            if (!config.begin()) {
                Logger::error("Failed to open config namespace");
                publishLog("Config update failed: namespace error", "ERROR", "config");
                return;
            }
            
            int updateCount = 0;
            
            // Update each key from the config object
            for (JsonPair kv : configObj) {
                String key = kv.key().c_str();
                
                if (kv.value().is<int>()) {
                    if (config.setInt(key, kv.value().as<int>())) {
                        updateCount++;
                        Logger::info("Updated config: %s = %d", key.c_str(), kv.value().as<int>());
                    }
                } else if (kv.value().is<float>()) {
                    if (config.setFloat(key, kv.value().as<float>())) {
                        updateCount++;
                        Logger::info("Updated config: %s = %.2f", key.c_str(), kv.value().as<float>());
                    }
                } else if (kv.value().is<bool>()) {
                    if (config.setBool(key, kv.value().as<bool>())) {
                        updateCount++;
                        Logger::info("Updated config: %s = %s", key.c_str(), kv.value().as<bool>() ? "true" : "false");
                    }
                } else if (kv.value().is<const char*>()) {
                    if (config.setString(key, kv.value().as<String>())) {
                        updateCount++;
                        Logger::info("Updated config: %s = %s", key.c_str(), kv.value().as<String>().c_str());
                    }
                }
            }
            
            config.end();
            
            String msg = "Configuration updated: " + String(updateCount) + " parameters changed";
            publishLog(msg, "INFO", "config");
            Logger::info("%s", msg.c_str());
            
            // Note: A reboot may be required for some parameters to take effect
            if (updateCount > 0) {
                Logger::warn("Some config changes may require restart to take effect");
            }
        } else {
            Logger::warn("update_config command received with empty config object");
        }
    }
}

void Reservoir::startPairingWindow(uint32_t durationMs, const char* reason) {
    if (!towers || !espNow) {
        return;
    }
    towers->startPairing(durationMs);
    espNow->enablePairingMode(durationMs);
    const char* origin = reason ? reason : "manual";
    Logger::info("Pairing window (%s) open for %u ms", origin, durationMs);
    Serial.printf("PAIRING MODE (%s) OPEN for %lu ms\n", origin, (unsigned long)durationMs);
    String msg = "Pairing window (" + String(origin) + ") open for " + String(durationMs) + " ms";
    publishLog(msg, "INFO", "pairing");
    statusLed.pulse(0, 0, 180, 500);
}

void Reservoir::updateTowerTelemetryCache(const String& towerId, const TowerStatusMessage& statusMsg) {
    TowerTelemetrySnapshot snapshot;
    snapshot.avgR = statusMsg.avg_r;
    snapshot.avgG = statusMsg.avg_g;
    snapshot.avgB = statusMsg.avg_b;
    snapshot.avgW = statusMsg.avg_w;
    snapshot.temperatureC = statusMsg.temperature;
    snapshot.buttonPressed = statusMsg.button_pressed;
    snapshot.lastUpdateMs = millis();
    towerTelemetry[towerId] = snapshot;

    if (mqtt) {
        mqtt->publishTowerStatus(statusMsg);
    }
}

void Reservoir::refreshReservoirSensors() {
    uint32_t now = millis();
    if (now - lastSensorSampleMs < 2000) {
        return;
    }
    lastSensorSampleMs = now;

    if (ambientLight) {
        reservoirSensors.lightLux = ambientLight->readLux();
    }
    
    // Read ESP32 internal temperature sensor (if available)
    #ifdef SOC_TEMP_SENSOR_SUPPORTED
    reservoirSensors.tempC = temperatureRead();
    #else
    reservoirSensors.tempC = 0.0f;
    #endif
    
    reservoirSensors.timestampMs = now;
    bool mmWaveOnline = mmWave && mmWave->isOnline();
    reservoirSensors.mmWaveOnline = mmWaveOnline;
    if (haveMmWaveSample && mmWaveOnline) {
        reservoirSensors.mmWavePresence = lastMmWaveEvent.presence;
        reservoirSensors.mmWaveConfidence = lastMmWaveEvent.confidence;
    } else if (!mmWaveOnline) {
        reservoirSensors.mmWavePresence = false;
        reservoirSensors.mmWaveConfidence = 0.0f;
    }

    if (wifi) {
        WifiManager::Status wifiStatus = wifi->getStatus();
        reservoirSensors.wifiConnected = wifiStatus.connected && !wifiStatus.offlineMode;
        reservoirSensors.wifiRssi = wifiStatus.connected ? wifiStatus.rssi : -127;
    } else {
        reservoirSensors.wifiConnected = WiFi.status() == WL_CONNECTED;
        reservoirSensors.wifiRssi = reservoirSensors.wifiConnected ? WiFi.RSSI() : -127;
    }

    if (mqtt) {
        mqtt->publishReservoirTelemetry(reservoirSensors);
    }
}

void Reservoir::printSerialTelemetry() {
    uint32_t now = millis();
    if (now - lastSerialPrintMs < 3000) {
        return;
    }
    lastSerialPrintMs = now;

    WifiManager::Status wifiStatus;
    if (wifi) {
        wifiStatus = wifi->getStatus();
    } else {
        wifiStatus.connected = (WiFi.status() == WL_CONNECTED);
        wifiStatus.ssid = wifiStatus.connected ? WiFi.SSID() : "";
        wifiStatus.rssi = wifiStatus.connected ? WiFi.RSSI() : -127;
        wifiStatus.offlineMode = false;
    }

    bool mqttConnected = mqtt && mqtt->isConnected();
    String brokerHost = mqtt ? mqtt->getBrokerHost() : String("n/a");
    uint16_t brokerPort = mqtt ? mqtt->getBrokerPort() : 0;
    const char* pairingState = (towers && towers->isPairingActive()) ? "OPEN" : "IDLE";
    const char* mmStatus;
    if (!reservoirSensors.mmWaveOnline) {
        mmStatus = "OFFLINE";
    } else {
        mmStatus = reservoirSensors.mmWavePresence ? "PRESENT" : "CLEAR";
    }
    uint16_t mmRestarts = mmWave ? mmWave->getRestartCount() : 0;

    size_t activeTowers = 0;
    for (const auto& entry : towerTelemetry) {
        if (now - entry.second.lastUpdateMs <= 30000) {
            activeTowers++;
        }
    }

    Serial.println();
    Serial.println("========== Reservoir Snapshot ==========");
    Serial.printf("Sensors   | Lux %5.1f\n",
                  reservoirSensors.lightLux);
    Serial.printf("mmWave    | %-8s  conf=%.2f restarts=%u\n",
                  mmStatus,
                  reservoirSensors.mmWaveConfidence,
                  static_cast<unsigned>(mmRestarts));
    if (!reservoirSensors.mmWaveOnline) {
        Serial.println("           | sensor offline - verify LD2450 wiring (RX=GPIO44, TX=GPIO43, 3V3, GND)");
    }
    Serial.printf("Wi-Fi     | %-10s ssid=%s rssi=%d dBm offline=%s\n",
                  wifiStatus.connected ? "CONNECTED" : "DISCONNECTED",
                  wifiStatus.ssid.c_str(),
                  wifiStatus.rssi,
                  wifiStatus.offlineMode ? "true" : "false");
    Serial.printf("MQTT      | %-10s %s:%u\n",
                  mqttConnected ? "CONNECTED" : "RETRYING",
                  brokerHost.c_str(),
                  brokerPort);
    Serial.printf("Pairing   | %s\n", pairingState);
    if (activeTowers == 0) {
        Serial.println("Towers    | none paired (mmWave + ambient-only mode)");
    } else {
        Serial.printf("Towers    | %u active\n", static_cast<unsigned>(activeTowers));
        for (const auto& entry : towerTelemetry) {
            const auto& data = entry.second;
            uint32_t age = now - data.lastUpdateMs;
            if (age > 30000) {
                continue;
            }
            Serial.printf("           - %s -> RGBW(%d,%d,%d,%d) temp=%.1f C btn=%s age=%lus\n",
                          entry.first.c_str(),
                          data.avgR,
                          data.avgG,
                          data.avgB,
                          data.avgW,
                          data.temperatureC,
                          data.buttonPressed ? "DOWN" : "up",
                          static_cast<unsigned long>(age / 1000));
        }
    }
    Serial.println("==========================================");
}

void Reservoir::recordBootStatus(const char* name, bool ok, const String& detail) {
    BootStatusEntry entry;
    entry.name = name ? name : "Subsystem";
    entry.ok = ok;
    entry.detail = detail;
    bootStatus.push_back(entry);
}

void Reservoir::publishLog(const String& message, const String& level, const String& tag) {
    if (mqtt && mqtt->isConnected()) {
        mqtt->publishSerialLog(message, level, tag);
    }
}

void Reservoir::printBootSummary() {
    if (bootStatus.empty()) {
        return;
    }
    Serial.println();
    Serial.println("+------------+------------------------------+");
    Serial.println("| Subsystem  | Status                       |");
    Serial.println("+------------+------------------------------+");
    for (const auto& entry : bootStatus) {
        String detail = entry.detail;
        if (detail.isEmpty()) {
            detail = entry.ok ? "OK" : "See logs";
        }
        if (detail.length() > 28) {
            detail = detail.substring(0, 25) + "...";
        }
        String status = String(entry.ok ? "OK " : "!  ") + detail;
        Serial.printf("| %-10s | %-28s |\n", entry.name.c_str(), status.c_str());
    }
    Serial.println("+------------+------------------------------+");
    Serial.println();
}


void Reservoir::handleSerialCommands() {
    static String commandBuffer;
    
    while (Serial.available()) {
        char c = Serial.read();
        
        if (c == '\n' || c == '\r') {
            if (commandBuffer.length() > 0) {
                commandBuffer.trim();
                commandBuffer.toLowerCase();
                
                // Process command
                if (commandBuffer == "help" || commandBuffer == "?") {
                    Serial.println();
                    Serial.println("=======================================");
                    Serial.println("  RESERVOIR SERIAL MENU");
                    Serial.println("=======================================");
                    Serial.println("  help, ?       - Show this menu");
                    Serial.println("  wifi          - Reconfigure Wi-Fi");
                    Serial.println("  mqtt          - Reconfigure MQTT");
                    Serial.println("  status        - Show system status");
                    Serial.println("  pair          - Start pairing mode (60s)");
                    Serial.println("  reboot        - Restart reservoir");
                    Serial.println("=======================================");
                    Serial.println();
                    
                } else if (commandBuffer == "wifi") {
                    if (wifi) {
                        Serial.println();
                        wifi->reconfigureWifi();
                    } else {
                        Serial.println("X WiFi manager not available");
                    }
                    
                } else if (commandBuffer == "mqtt") {
                    if (mqtt) {
                        Serial.println();
                        mqtt->runProvisioningWizard();
                    } else {
                        Serial.println("X MQTT not available");
                    }
                    
                } else if (commandBuffer == "status") {
                    Serial.println();
                    printSerialTelemetry();
                    
                } else if (commandBuffer == "pair") {
                    Serial.println();
                    startPairingWindow(60000, "serial command");
                    Serial.println("OK Pairing mode activated for 60 seconds");
                    
                } else if (commandBuffer == "reboot") {
                    Serial.println();
                    Serial.println("Rebooting reservoir...");
                    delay(500);
                    ESP.restart();
                    
                } else if (commandBuffer.length() > 0) {
                    Serial.printf("X Unknown command: '%s' (type 'help' for menu)\n", commandBuffer.c_str());
                }
                
                commandBuffer = "";
            }
        } else {
            commandBuffer += c;
            // Limit buffer size to prevent overflow
            if (commandBuffer.length() > 64) {
                commandBuffer = "";
                Serial.println("X Command too long, cleared");
            }
        }
    }
}
