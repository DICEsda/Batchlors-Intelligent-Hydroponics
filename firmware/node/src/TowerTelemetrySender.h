/**
 * @file TowerTelemetrySender.h
 * @brief Periodic telemetry sender for hydroponic tower nodes
 * 
 * Sends TowerTelemetryMessage to coordinator via ESP-NOW at configurable
 * intervals. Non-blocking implementation using millis()-based timing.
 */
#pragma once

#include <Arduino.h>
#include <esp_now.h>
#include "EspNowMessage.h"
#include "config/TowerConfig.h"
#include "actuators/IPumpController.h"
#include "actuators/IGrowLightController.h"

// Forward declaration for DHT sensor (optional)
class DHT;

/**
 * @brief Tower telemetry sender for periodic status updates
 * 
 * Collects sensor data and actuator states, packages into TowerTelemetryMessage,
 * and sends to coordinator via ESP-NOW at configurable intervals.
 * 
 * Usage:
 *   TowerTelemetrySender sender(config, pump, light);
 *   sender.begin();
 *   // In loop:
 *   sender.loop();  // Non-blocking, sends when interval elapsed
 */
class TowerTelemetrySender {
public:
    /**
     * @brief Construct telemetry sender
     * @param config Tower configuration for ID and interval
     * @param pump Pump controller for state reporting (can be nullptr)
     * @param light Grow light controller for state reporting (can be nullptr)
     */
    TowerTelemetrySender(
        TowerConfig& config,
        IPumpController* pump = nullptr,
        IGrowLightController* light = nullptr
    );

    ~TowerTelemetrySender() = default;

    /**
     * @brief Initialize the telemetry sender
     * @return true if successful
     */
    bool begin();

    /**
     * @brief Main loop - call frequently, sends telemetry when interval elapsed
     * Non-blocking implementation using millis()
     */
    void loop();

    /**
     * @brief Force immediate telemetry send (bypasses interval check)
     * @return true if message was sent successfully
     */
    bool sendNow();

    /**
     * @brief Set the coordinator MAC address for ESP-NOW sends
     * @param mac 6-byte MAC address
     */
    void setCoordinatorMac(const uint8_t mac[6]);

    /**
     * @brief Set optional DHT sensor for temperature/humidity readings
     * @param dhtSensor Pointer to initialized DHT sensor
     */
    void setDhtSensor(DHT* dhtSensor);

    /**
     * @brief Set ambient light reading (from external sensor)
     * @param lux Light level in lux
     */
    void setAmbientLight(float lux);

    /**
     * @brief Set battery/supply voltage reading
     * @param millivolts Voltage in millivolts
     */
    void setBatteryVoltage(uint16_t millivolts);

    /**
     * @brief Set node status mode
     * @param mode Status string: "operational", "pairing", "ota", "error", "idle"
     */
    void setStatusMode(const String& mode);

    /**
     * @brief Get count of telemetry messages sent since begin()
     * @return Send count
     */
    uint32_t getSendCount() const { return _sendCount; }

    /**
     * @brief Get count of failed sends since begin()
     * @return Failure count
     */
    uint32_t getFailCount() const { return _failCount; }

    /**
     * @brief Get milliseconds since last successful send
     * @return Time since last send, or UINT32_MAX if never sent
     */
    uint32_t getTimeSinceLastSend() const;

    /**
     * @brief Check if telemetry is due to be sent
     * @return true if interval has elapsed since last send
     */
    bool isDue() const;

    /**
     * @brief Temporarily pause telemetry sending
     * @param paused true to pause, false to resume
     */
    void setPaused(bool paused) { _paused = paused; }

    /**
     * @brief Check if telemetry is paused
     * @return true if paused
     */
    bool isPaused() const { return _paused; }

private:
    TowerConfig& _config;
    IPumpController* _pump;
    IGrowLightController* _light;
    DHT* _dht;

    uint8_t _coordMac[6];
    bool _coordMacSet;

    // Timing
    uint32_t _lastSendTime;
    uint32_t _sendCount;
    uint32_t _failCount;
    bool _paused;

    // External sensor values (set by caller)
    float _ambientLux;
    uint16_t _batteryMv;
    String _statusMode;

    // Startup time for uptime calculation
    uint32_t _startTime;

    /**
     * @brief Build telemetry message from current sensor/actuator states
     * @return Populated TowerTelemetryMessage
     */
    TowerTelemetryMessage buildMessage() const;

    /**
     * @brief Send message via ESP-NOW to coordinator
     * @param msg Message to send
     * @return true if send was successful
     */
    bool sendMessage(const TowerTelemetryMessage& msg);

    /**
     * @brief Read temperature from DHT sensor
     * @return Temperature in Celsius, or NAN if unavailable
     */
    float readTemperature() const;

    /**
     * @brief Read humidity from DHT sensor
     * @return Relative humidity percentage, or NAN if unavailable
     */
    float readHumidity() const;

    /**
     * @brief Log message to serial
     * @param level Log level (INFO, WARN, ERROR)
     * @param message Message text
     */
    void log(const String& level, const String& message) const;
};

// ============================================================================
// INLINE IMPLEMENTATION
// ============================================================================

inline TowerTelemetrySender::TowerTelemetrySender(
    TowerConfig& config,
    IPumpController* pump,
    IGrowLightController* light
)
    : _config(config)
    , _pump(pump)
    , _light(light)
    , _dht(nullptr)
    , _coordMacSet(false)
    , _lastSendTime(0)
    , _sendCount(0)
    , _failCount(0)
    , _paused(false)
    , _ambientLux(0.0f)
    , _batteryMv(0)
    , _statusMode("idle")
    , _startTime(0)
{
    memset(_coordMac, 0, 6);
}

inline bool TowerTelemetrySender::begin() {
    _startTime = millis();
    _lastSendTime = 0;  // Force immediate first send
    _sendCount = 0;
    _failCount = 0;
    _paused = false;

    // Try to load coordinator MAC from config
    if (_config.getCoordMac(_coordMac)) {
        _coordMacSet = true;
        log("INFO", "Coordinator MAC loaded from config");
    } else {
        log("WARN", "No coordinator MAC configured, telemetry disabled until paired");
    }

    return true;
}

inline void TowerTelemetrySender::loop() {
    if (_paused || !_coordMacSet) {
        return;
    }

    if (isDue()) {
        sendNow();
    }
}

inline bool TowerTelemetrySender::sendNow() {
    if (!_coordMacSet) {
        log("WARN", "Cannot send telemetry: coordinator MAC not set");
        return false;
    }

    TowerTelemetryMessage msg = buildMessage();
    bool success = sendMessage(msg);

    if (success) {
        _lastSendTime = millis();
        _sendCount++;
        log("INFO", String("Telemetry sent #") + String(_sendCount));
    } else {
        _failCount++;
        log("ERROR", String("Telemetry send failed, total failures: ") + String(_failCount));
    }

    return success;
}

inline void TowerTelemetrySender::setCoordinatorMac(const uint8_t mac[6]) {
    memcpy(_coordMac, mac, 6);
    _coordMacSet = true;
    log("INFO", String("Coordinator MAC set: ") + _config.getCoordMacString());
}

inline void TowerTelemetrySender::setDhtSensor(DHT* dhtSensor) {
    _dht = dhtSensor;
}

inline void TowerTelemetrySender::setAmbientLight(float lux) {
    _ambientLux = lux;
}

inline void TowerTelemetrySender::setBatteryVoltage(uint16_t millivolts) {
    _batteryMv = millivolts;
}

inline void TowerTelemetrySender::setStatusMode(const String& mode) {
    _statusMode = mode;
}

inline uint32_t TowerTelemetrySender::getTimeSinceLastSend() const {
    if (_lastSendTime == 0) return UINT32_MAX;
    return millis() - _lastSendTime;
}

inline bool TowerTelemetrySender::isDue() const {
    uint32_t interval = _config.getTelemetryIntervalMs();
    return (millis() - _lastSendTime) >= interval;
}

inline TowerTelemetryMessage TowerTelemetrySender::buildMessage() const {
    TowerTelemetryMessage msg;

    msg.tower_id = _config.getTowerId();
    msg.ts = millis();

    // Environmental sensors
    msg.air_temp_c = readTemperature();
    msg.humidity_pct = readHumidity();
    msg.light_lux = _ambientLux;

    // Handle NaN values (sensor not available)
    if (isnan(msg.air_temp_c)) msg.air_temp_c = 0.0f;
    if (isnan(msg.humidity_pct)) msg.humidity_pct = 0.0f;

    // Actuator states
    if (_pump) {
        msg.pump_on = _pump->isOn();
    } else {
        msg.pump_on = false;
    }

    if (_light) {
        msg.light_on = _light->isOn();
        msg.light_brightness = _light->getBrightness();
    } else {
        msg.light_on = false;
        msg.light_brightness = 0;
    }

    // System status
    msg.status_mode = _statusMode;
    msg.vbat_mv = _batteryMv;
    msg.fw = _config.getFirmwareVersion();
    msg.uptime_s = (millis() - _startTime) / 1000;

    return msg;
}

inline bool TowerTelemetrySender::sendMessage(const TowerTelemetryMessage& msg) {
    String json = msg.toJson();

    // Check if we have the coordinator as a peer
    esp_now_peer_info_t peerInfo;
    if (esp_now_get_peer(_coordMac, &peerInfo) != ESP_OK) {
        // Need to add peer first
        memset(&peerInfo, 0, sizeof(peerInfo));
        memcpy(peerInfo.peer_addr, _coordMac, 6);
        peerInfo.channel = _config.getWifiChannel();
        peerInfo.encrypt = false;  // Use encryption if LMK is set

        String lmk = _config.getLmk();
        if (!lmk.isEmpty() && lmk.length() == 32) {
            peerInfo.encrypt = true;
            // Convert hex LMK to bytes
            for (int i = 0; i < 16; i++) {
                char hex[3] = {lmk.charAt(i * 2), lmk.charAt(i * 2 + 1), 0};
                peerInfo.lmk[i] = (uint8_t)strtol(hex, nullptr, 16);
            }
        }

        if (esp_now_add_peer(&peerInfo) != ESP_OK) {
            log("ERROR", "Failed to add coordinator as ESP-NOW peer");
            return false;
        }
    }

    // Send the message
    esp_err_t result = esp_now_send(_coordMac, (uint8_t*)json.c_str(), json.length() + 1);
    return (result == ESP_OK);
}

inline float TowerTelemetrySender::readTemperature() const {
    if (!_dht) return NAN;
    // DHT library typically has readTemperature() method
    // This will be implemented based on actual DHT library used
    return NAN;  // Placeholder - actual implementation depends on DHT library
}

inline float TowerTelemetrySender::readHumidity() const {
    if (!_dht) return NAN;
    // DHT library typically has readHumidity() method
    return NAN;  // Placeholder - actual implementation depends on DHT library
}

inline void TowerTelemetrySender::log(const String& level, const String& message) const {
    Serial.print("[TelemetrySender][");
    Serial.print(level);
    Serial.print("] ");
    Serial.println(message);
}
