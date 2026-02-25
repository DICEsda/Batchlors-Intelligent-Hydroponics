#pragma once

#include <Arduino.h>
#include <WiFi.h>
#include <vector>
#include <functional>
#include "../../shared/src/ConfigStore.h"

class WifiManager {
public:
    struct Status {
        bool connected = false;
        bool offlineMode = false;
        String ssid;
        int32_t rssi = -127;
        IPAddress ip;
    };

    WifiManager();
    ~WifiManager() = default;

    bool begin();
    void loop();
    bool ensureConnected();
    bool reconfigureWifi(); // Interactive WiFi reconfiguration at runtime

    bool isConnected() const { return status.connected; }
    bool isOffline() const { return status.offlineMode; }
    Status getStatus() const { return status; }
    
    void setEspNow(class EspNow* espNowPtr) { espNow = espNowPtr; }
    
    // Connection status callback for real-time event notifications
    void setConnectionStatusCallback(std::function<void(const String& event, const String& detail)> callback) {
        connectionStatusCallback = callback;
    }

private:
    String storedSsid;
    String storedPassword;
    Status status;
    uint32_t lastReconnectAttempt;
    class EspNow* espNow = nullptr;
    std::function<void(const String& event, const String& detail)> connectionStatusCallback;

    bool attemptConnect(const String& ssid, const String& password, bool verbose = true);
    bool interactiveSetup();
    bool promptYesNo(const String& prompt);
    String promptLine(const String& prompt, bool allowEmpty = false, bool hide = false);
    bool selectNetwork(String& ssidOut);
    void updateStatusCache();
    
    // WiFi event handlers
    void handleWiFiEvent(WiFiEvent_t event, WiFiEventInfo_t info);
    void onWiFiConnected();
    void onWiFiDisconnected(uint8_t reason);
    void onWiFiGotIP();
    String getDisconnectReasonString(uint8_t reason);
};
