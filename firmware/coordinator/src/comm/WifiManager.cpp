#include "WifiManager.h"
#include "../utils/Logger.h"
#include "EspNow.h"

WifiManager::WifiManager()
    : lastReconnectAttempt(0) {
    status = Status{};
}

bool WifiManager::begin() {
    // Load configuration from unified config store
    Config config = ConfigStore::load();
    storedSsid = config.wifi.ssid;
    storedPassword = config.wifi.password;

    WiFi.mode(WIFI_STA);
    WiFi.setSleep(false);
    
    // Register WiFi event handler for real-time connection status
    WiFi.onEvent([this](WiFiEvent_t event, WiFiEventInfo_t info) {
        this->handleWiFiEvent(event, info);
    });

    // Try stored credentials first
    if (!storedSsid.isEmpty()) {
        Serial.printf("Found stored Wi-Fi: %s\n", storedSsid.c_str());
        if (attemptConnect(storedSsid, storedPassword)) {
            return true;
        }
        // Connection failed with stored credentials
        Serial.println("✗ Stored credentials failed to connect.");
        Serial.println("Would you like to:");
        Serial.println("  1) Retry existing credentials");
        Serial.println("  2) Configure new Wi-Fi network");
        Serial.println("  3) Continue offline");
        Serial.print("Enter choice (1-3): ");
        String choice = promptLine("", false);
        choice.trim();
        
        if (choice == "1") {
            Serial.println("Retrying stored credentials in background...");
            // Will retry in loop()
            return false;
        } else if (choice == "3") {
            status.offlineMode = true;
            Serial.println("Continuing in offline mode. Use serial menu to configure later.");
            return false;
        }
        // Fall through to interactive setup for choice "2" or invalid input
    } else {
        // No stored credentials
        Serial.println("═══════════════════════════════════════");
        Serial.println("  No Wi-Fi credentials configured");
        Serial.println("═══════════════════════════════════════");
    }

    Serial.println("Configure Wi-Fi? (y/n)");
    if (!promptYesNo("") ) {
        status.offlineMode = true;
        Serial.println("Continuing in offline mode. MQTT will retry when Wi-Fi becomes available.");
        return false;
    }

    bool configured = interactiveSetup();
    if (!configured) {
        status.offlineMode = true;
        Serial.println("Wi-Fi setup skipped. Running offline.");
        return false;
    }
    return true;
}

void WifiManager::loop() {
    if (status.offlineMode || storedSsid.isEmpty()) {
        return;
    }

    if (WiFi.status() != WL_CONNECTED) {
        uint32_t now = millis();
        if (now - lastReconnectAttempt > 10000) {
            lastReconnectAttempt = now;
            attemptConnect(storedSsid, storedPassword, false);
        }
    } else {
        updateStatusCache();
    }
}

bool WifiManager::ensureConnected() {
    if (status.offlineMode) {
        return false;
    }
    if (WiFi.status() == WL_CONNECTED) {
        updateStatusCache();
        return true;
    }
    if (storedSsid.isEmpty()) {
        return false;
    }
    return attemptConnect(storedSsid, storedPassword);
}

bool WifiManager::attemptConnect(const String& ssid, const String& password, bool verbose) {
    if (ssid.isEmpty()) {
        return false;
    }

    if (verbose) {
        Serial.printf("Connecting to Wi-Fi SSID '%s'...\n", ssid.c_str());
        Serial.printf("Password length: %d characters\n", password.length());
        Serial.printf("Password (first 3 chars): %c%c%c...\n", 
                     password.length() > 0 ? password[0] : '?',
                     password.length() > 1 ? password[1] : '?',
                     password.length() > 2 ? password[2] : '?');
    }
    Logger::info("Connecting to Wi-Fi: %s", ssid.c_str());
    Logger::info("Password length: %d", password.length());

    // CRITICAL: Use WiFi.disconnect(false, false) to avoid deinitializing ESP-NOW
    // The second parameter (true) would erase WiFi config AND deinit radio, breaking ESP-NOW!
    WiFi.disconnect(false, false);
    delay(100);
    WiFi.begin(ssid.c_str(), password.c_str());

    uint32_t start = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - start) < 20000) {
        delay(500);
        if (verbose) Serial.print('.');
    }
    if (verbose) Serial.println();

    if (WiFi.status() == WL_CONNECTED) {
        updateStatusCache();
        status.offlineMode = false;
        storedSsid = ssid;
        storedPassword = password;
        
        // CRITICAL: Discover actual WiFi channel and save it
        uint8_t actualChannel = WiFi.channel();
        Logger::info("WiFi connected on channel %d", actualChannel);
        
        // Load current config and update WiFi settings
        Config config = ConfigStore::load();
        config.wifi.ssid = storedSsid;
        config.wifi.password = storedPassword;
        
        // Lock WiFi and ESP-NOW to the same channel
        if (config.wifi.channel != actualChannel || config.espnow.channel != actualChannel) {
            config.wifi.channel = actualChannel;
            config.espnow.channel = actualChannel;
            Logger::info("Saved WiFi channel %d to config (locked with ESP-NOW)", actualChannel);
        }
        
        // Save updated config
        if (!ConfigStore::save(config)) {
            Logger::warn("Failed to save WiFi config to store");
        }
        
        Serial.printf("✓ Wi-Fi connected: %s (IP %s, RSSI %d, Channel %d)\n",
                      status.ssid.c_str(), WiFi.localIP().toString().c_str(), WiFi.RSSI(), actualChannel);
        Logger::info("Wi-Fi connected: %s", status.ssid.c_str());
        
        if (espNow) {
            espNow->updatePeerChannels();
        }
        return true;
    }

    Logger::warn("Wi-Fi connection to %s failed", ssid.c_str());
    Serial.println("✗ Failed to connect. Check credentials and retry.");
    return false;
}

bool WifiManager::interactiveSetup() {
    while (true) {
        String chosenSsid;
        if (!selectNetwork(chosenSsid)) {
            Serial.println("No networks selected. Retry? (y/n)");
            if (!promptYesNo("")) {
                return false;
            }
            continue;
        }

        String password = promptLine("Enter password (leave empty for open network): ", true);
        if (attemptConnect(chosenSsid, password)) {
            return true;
        }

        Serial.println("Connection failed. Try a different network? (y/n)");
        if (!promptYesNo("")) {
            return false;
        }
    }
}

bool WifiManager::selectNetwork(String& ssidOut) {
    Serial.println("Scanning for Wi-Fi networks...");
    int count = WiFi.scanNetworks(/*async=*/false, /*hidden=*/true);
    if (count <= 0) {
        Serial.println("No networks found. Enter SSID manually? (y/n)");
        if (promptYesNo("")) {
            ssidOut = promptLine("Enter SSID: ", false);
            return !ssidOut.isEmpty();
        }
        return false;
    }

    for (int i = 0; i < count; ++i) {
        Serial.printf("[%d] %s (RSSI %d dBm)%s\n", i, WiFi.SSID(i).c_str(), WiFi.RSSI(i),
                     WiFi.encryptionType(i) == WIFI_AUTH_OPEN ? " [open]" : "");
    }
    Serial.println("Enter the index of the network to use, or type the SSID manually:");
    String choice = promptLine("> ", false);
    choice.trim();

    bool numeric = true;
    for (size_t i = 0; i < choice.length(); ++i) {
        if (!isDigit(choice[i])) {
            numeric = false;
            break;
        }
    }

    if (numeric && choice.length() > 0) {
        int idx = choice.toInt();
        if (idx >= 0 && idx < count) {
            ssidOut = WiFi.SSID(idx);
            WiFi.scanDelete();
            return true;
        }
    }

    ssidOut = choice;
    WiFi.scanDelete();
    return !ssidOut.isEmpty();
}

bool WifiManager::promptYesNo(const String& prompt) {
    String line = promptLine(prompt.isEmpty() ? "(y/n): " : prompt, false);
    line.toLowerCase();
    return line == "y" || line == "yes";
}

String WifiManager::promptLine(const String& prompt, bool allowEmpty, bool /*hide*/) {
    if (!prompt.isEmpty()) {
        Serial.print(prompt);
    }
    Serial.flush();
    String input;
    while (!Serial.available()) {
        delay(10);
    }
    input = Serial.readStringUntil('\n');
    input.trim();
    if (!allowEmpty) {
        while (input.isEmpty()) {
            Serial.print("> ");
            Serial.flush();
            while (!Serial.available()) {
                delay(10);
            }
            input = Serial.readStringUntil('\n');
            input.trim();
        }
    }
    return input;
}

void WifiManager::updateStatusCache() {
    status.connected = (WiFi.status() == WL_CONNECTED);
    status.ssid = status.connected ? WiFi.SSID() : "";
    status.rssi = status.connected ? WiFi.RSSI() : -127;
    status.ip = status.connected ? WiFi.localIP() : IPAddress();
}

bool WifiManager::reconfigureWifi() {
    Serial.println("═══════════════════════════════════════");
    Serial.println("  Wi-Fi Reconfiguration");
    Serial.println("═══════════════════════════════════════");
    
    if (WiFi.status() == WL_CONNECTED) {
        Serial.printf("Currently connected to: %s\n", WiFi.SSID().c_str());
        Serial.println("This will disconnect and configure a new network.");
        Serial.println("Continue? (y/n)");
        if (!promptYesNo("")) {
            Serial.println("Reconfiguration cancelled.");
            return false;
        }
        WiFi.disconnect(false, false);
        delay(500);
    }
    
    bool configured = interactiveSetup();
    if (configured) {
        status.offlineMode = false;
        Serial.println("✓ Wi-Fi reconfigured successfully!");
        return true;
    } else {
        Serial.println("✗ Wi-Fi reconfiguration failed or cancelled.");
        return false;
    }
}

// ============================================================================
// WiFi Event Handlers - Real-Time Connection Status
// ============================================================================

void WifiManager::handleWiFiEvent(WiFiEvent_t event, WiFiEventInfo_t info) {
    switch(event) {
        case ARDUINO_EVENT_WIFI_STA_CONNECTED:
            Logger::info("📶 WiFi event: STA_CONNECTED (SSID: %s)", WiFi.SSID().c_str());
            onWiFiConnected();
            break;
            
        case ARDUINO_EVENT_WIFI_STA_DISCONNECTED:
            Logger::warn("📶 WiFi event: STA_DISCONNECTED (reason: %d - %s)", 
                        info.wifi_sta_disconnected.reason, 
                        getDisconnectReasonString(info.wifi_sta_disconnected.reason).c_str());
            onWiFiDisconnected(info.wifi_sta_disconnected.reason);
            break;
            
        case ARDUINO_EVENT_WIFI_STA_GOT_IP:
            Logger::info("📶 WiFi event: GOT_IP (%s, RSSI: %d dBm)", 
                        WiFi.localIP().toString().c_str(), WiFi.RSSI());
            onWiFiGotIP();
            break;
            
        case ARDUINO_EVENT_WIFI_STA_LOST_IP:
            Logger::warn("📶 WiFi event: LOST_IP");
            if (connectionStatusCallback) {
                connectionStatusCallback("wifi_lost_ip", "");
            }
            break;
            
        default:
            // Ignore other WiFi events
            break;
    }
}

void WifiManager::onWiFiConnected() {
    status.connected = true;
    updateStatusCache();
    
    // Notify callback about WiFi connection
    if (connectionStatusCallback) {
        connectionStatusCallback("wifi_connected", WiFi.SSID());
    }
}

void WifiManager::onWiFiDisconnected(uint8_t reason) {
    status.connected = false;
    
    // Notify callback about WiFi disconnection with reason
    if (connectionStatusCallback) {
        String reasonStr = getDisconnectReasonString(reason);
        connectionStatusCallback("wifi_disconnected", reasonStr);
    }
}

void WifiManager::onWiFiGotIP() {
    status.connected = true;
    updateStatusCache();
    
    // Notify callback about IP acquisition
    if (connectionStatusCallback) {
        connectionStatusCallback("wifi_got_ip", WiFi.localIP().toString());
    }
}

String WifiManager::getDisconnectReasonString(uint8_t reason) {
    switch(reason) {
        case WIFI_REASON_UNSPECIFIED: return "Unspecified";
        case WIFI_REASON_AUTH_EXPIRE: return "Auth expired";
        case WIFI_REASON_AUTH_LEAVE: return "Auth leave";
        case WIFI_REASON_ASSOC_EXPIRE: return "Association expired";
        case WIFI_REASON_ASSOC_TOOMANY: return "Too many associations";
        case WIFI_REASON_NOT_AUTHED: return "Not authenticated";
        case WIFI_REASON_NOT_ASSOCED: return "Not associated";
        case WIFI_REASON_ASSOC_LEAVE: return "Association leave";
        case WIFI_REASON_ASSOC_NOT_AUTHED: return "Association not authenticated";
        case WIFI_REASON_DISASSOC_PWRCAP_BAD: return "Bad power capability";
        case WIFI_REASON_DISASSOC_SUPCHAN_BAD: return "Bad supported channels";
        case WIFI_REASON_BSS_TRANSITION_DISASSOC: return "BSS transition disassoc";
        case WIFI_REASON_IE_INVALID: return "Invalid IE";
        case WIFI_REASON_MIC_FAILURE: return "MIC failure";
        case WIFI_REASON_4WAY_HANDSHAKE_TIMEOUT: return "4-way handshake timeout";
        case WIFI_REASON_GROUP_KEY_UPDATE_TIMEOUT: return "Group key update timeout";
        case WIFI_REASON_IE_IN_4WAY_DIFFERS: return "IE in 4-way differs";
        case WIFI_REASON_GROUP_CIPHER_INVALID: return "Invalid group cipher";
        case WIFI_REASON_PAIRWISE_CIPHER_INVALID: return "Invalid pairwise cipher";
        case WIFI_REASON_AKMP_INVALID: return "Invalid AKMP";
        case WIFI_REASON_UNSUPP_RSN_IE_VERSION: return "Unsupported RSN IE version";
        case WIFI_REASON_INVALID_RSN_IE_CAP: return "Invalid RSN IE capability";
        case WIFI_REASON_802_1X_AUTH_FAILED: return "802.1x auth failed";
        case WIFI_REASON_CIPHER_SUITE_REJECTED: return "Cipher suite rejected";
        case WIFI_REASON_BEACON_TIMEOUT: return "Beacon timeout";
        case WIFI_REASON_NO_AP_FOUND: return "AP not found";
        case WIFI_REASON_AUTH_FAIL: return "Auth failed";
        case WIFI_REASON_ASSOC_FAIL: return "Association failed";
        case WIFI_REASON_HANDSHAKE_TIMEOUT: return "Handshake timeout";
        case WIFI_REASON_CONNECTION_FAIL: return "Connection failed";
        default: return "Unknown (" + String(reason) + ")";
    }
}
