/**
 * @file TowerConfig.h
 * @brief Tower-specific configuration wrapper for hydroponic tower nodes
 * 
 * Provides a clean interface for tower identity, pairing state, and
 * firmware version management. Uses ConfigManager for persistence.
 */
#pragma once

#include <Arduino.h>
#include "ConfigManager.h"

// Firmware version for tower nodes
#define TOWER_FIRMWARE_VERSION "tower-1.0.0"

/**
 * @brief Tower-specific configuration manager
 * 
 * Wraps ConfigManager with tower-specific methods for:
 * - Tower identity (tower_id, coord_id, farm_id)
 * - Coordinator MAC address for ESP-NOW
 * - Pairing state tracking
 * - Telemetry and operational parameters
 */
class TowerConfig {
public:
    /**
     * @brief Construct a new TowerConfig
     * @param configNamespace Preferences namespace (default "tower")
     */
    explicit TowerConfig(const String& configNamespace = "tower");
    ~TowerConfig();

    /**
     * @brief Initialize the configuration system
     * @return true if successful
     */
    bool begin();

    /**
     * @brief Close the configuration system
     */
    void end();

    // ========================================================================
    // Identity Management
    // ========================================================================

    /**
     * @brief Get the tower ID
     * @return Tower ID string, empty if not set
     */
    String getTowerId() const;

    /**
     * @brief Set the tower ID
     * @param towerId Tower identifier assigned by coordinator
     * @return true if saved successfully
     */
    bool setTowerId(const String& towerId);

    /**
     * @brief Get the coordinator ID
     * @return Coordinator ID string, empty if not set
     */
    String getCoordId() const;

    /**
     * @brief Set the coordinator ID
     * @param coordId Coordinator identifier
     * @return true if saved successfully
     */
    bool setCoordId(const String& coordId);

    /**
     * @brief Get the farm ID
     * @return Farm ID string, empty if not set
     */
    String getFarmId() const;

    /**
     * @brief Set the farm ID
     * @param farmId Farm identifier for MQTT topic hierarchy
     * @return true if saved successfully
     */
    bool setFarmId(const String& farmId);

    // ========================================================================
    // Coordinator MAC Address
    // ========================================================================

    /**
     * @brief Get the coordinator MAC address
     * @param mac Output buffer for 6-byte MAC address
     * @return true if MAC is stored and valid
     */
    bool getCoordMac(uint8_t mac[6]) const;

    /**
     * @brief Set the coordinator MAC address
     * @param mac 6-byte MAC address
     * @return true if saved successfully
     */
    bool setCoordMac(const uint8_t mac[6]);

    /**
     * @brief Get coordinator MAC as hex string
     * @return MAC address in "AA:BB:CC:DD:EE:FF" format, empty if not set
     */
    String getCoordMacString() const;

    /**
     * @brief Set coordinator MAC from hex string
     * @param macStr MAC address in "AA:BB:CC:DD:EE:FF" or "AABBCCDDEEFF" format
     * @return true if parsed and saved successfully
     */
    bool setCoordMacString(const String& macStr);

    // ========================================================================
    // Pairing State
    // ========================================================================

    /**
     * @brief Check if tower is paired with a coordinator
     * @return true if tower_id, coord_id, and coordinator MAC are all set
     */
    bool isPaired() const;

    /**
     * @brief Clear all pairing data (factory reset pairing state)
     */
    void clearPairing();

    // ========================================================================
    // ESP-NOW Security
    // ========================================================================

    /**
     * @brief Get the ESP-NOW Link Master Key (LMK)
     * @return LMK as hex string (32 chars for 16 bytes), empty if not set
     */
    String getLmk() const;

    /**
     * @brief Set the ESP-NOW Link Master Key
     * @param lmkHex LMK as hex string (32 chars for 16 bytes)
     * @return true if saved successfully
     */
    bool setLmk(const String& lmkHex);

    /**
     * @brief Get the WiFi channel coordinator is using
     * @return WiFi channel (1-13), 0 if not set
     */
    uint8_t getWifiChannel() const;

    /**
     * @brief Set the WiFi channel
     * @param channel WiFi channel (1-13)
     * @return true if saved successfully
     */
    bool setWifiChannel(uint8_t channel);

    // ========================================================================
    // Operational Parameters
    // ========================================================================

    /**
     * @brief Get telemetry interval
     * @return Telemetry interval in milliseconds
     */
    uint32_t getTelemetryIntervalMs() const;

    /**
     * @brief Set telemetry interval
     * @param intervalMs Interval in milliseconds
     * @return true if saved successfully
     */
    bool setTelemetryIntervalMs(uint32_t intervalMs);

    /**
     * @brief Get pump maximum duration (safety limit)
     * @return Max pump duration in seconds
     */
    uint16_t getPumpMaxDurationS() const;

    /**
     * @brief Set pump maximum duration
     * @param maxSeconds Maximum pump on time for safety
     * @return true if saved successfully
     */
    bool setPumpMaxDurationS(uint16_t maxSeconds);

    // ========================================================================
    // Firmware Version
    // ========================================================================

    /**
     * @brief Get firmware version string
     * @return Firmware version (e.g., "tower-1.0.0")
     */
    String getFirmwareVersion() const;

    // ========================================================================
    // Full Configuration Save/Load
    // ========================================================================

    /**
     * @brief Save full join accept configuration
     * @param towerId Assigned tower ID
     * @param coordId Coordinator ID
     * @param farmId Farm ID
     * @param coordMac Coordinator MAC address (6 bytes)
     * @param lmkHex ESP-NOW LMK as hex string
     * @param wifiChannel WiFi channel
     * @param telemetryMs Telemetry interval
     * @param pumpMaxS Pump max duration
     * @return true if all saved successfully
     */
    bool saveJoinAccept(
        const String& towerId,
        const String& coordId,
        const String& farmId,
        const uint8_t coordMac[6],
        const String& lmkHex,
        uint8_t wifiChannel,
        uint32_t telemetryMs,
        uint16_t pumpMaxS
    );

    /**
     * @brief Perform factory reset - clear all configuration
     * @return true if successful
     */
    bool factoryReset();

private:
    ConfigManager _config;
    bool _initialized;

    // Internal keys for coordinator MAC storage
    static constexpr const char* KEY_COORD_MAC = "coord_mac";
    static constexpr const char* KEY_WIFI_CHANNEL = "wifi_ch";

    /**
     * @brief Parse MAC string to bytes
     * @param macStr MAC in "AA:BB:CC:DD:EE:FF" or "AABBCCDDEEFF" format
     * @param mac Output 6-byte buffer
     * @return true if parsed successfully
     */
    static bool parseMacString(const String& macStr, uint8_t mac[6]);

    /**
     * @brief Format MAC bytes to string
     * @param mac 6-byte MAC address
     * @return MAC in "AA:BB:CC:DD:EE:FF" format
     */
    static String formatMacString(const uint8_t mac[6]);
};

// ============================================================================
// INLINE IMPLEMENTATION
// ============================================================================

inline TowerConfig::TowerConfig(const String& configNamespace)
    : _config(configNamespace)
    , _initialized(false) {
}

inline TowerConfig::~TowerConfig() {
    end();
}

inline bool TowerConfig::begin() {
    if (_initialized) return true;
    _initialized = _config.begin();
    return _initialized;
}

inline void TowerConfig::end() {
    if (_initialized) {
        _config.end();
        _initialized = false;
    }
}

// Identity
inline String TowerConfig::getTowerId() const {
    return _config.getString(ConfigKeys::TOWER_ID, "");
}

inline bool TowerConfig::setTowerId(const String& towerId) {
    return _config.setString(ConfigKeys::TOWER_ID, towerId);
}

inline String TowerConfig::getCoordId() const {
    return _config.getString(ConfigKeys::COORD_ID, "");
}

inline bool TowerConfig::setCoordId(const String& coordId) {
    return _config.setString(ConfigKeys::COORD_ID, coordId);
}

inline String TowerConfig::getFarmId() const {
    return _config.getString(ConfigKeys::FARM_ID, "");
}

inline bool TowerConfig::setFarmId(const String& farmId) {
    return _config.setString(ConfigKeys::FARM_ID, farmId);
}

// Coordinator MAC
inline bool TowerConfig::getCoordMac(uint8_t mac[6]) const {
    String macStr = _config.getString(KEY_COORD_MAC, "");
    if (macStr.isEmpty()) return false;
    return parseMacString(macStr, mac);
}

inline bool TowerConfig::setCoordMac(const uint8_t mac[6]) {
    return _config.setString(KEY_COORD_MAC, formatMacString(mac));
}

inline String TowerConfig::getCoordMacString() const {
    return _config.getString(KEY_COORD_MAC, "");
}

inline bool TowerConfig::setCoordMacString(const String& macStr) {
    uint8_t mac[6];
    if (!parseMacString(macStr, mac)) return false;
    return setCoordMac(mac);
}

// Pairing state
inline bool TowerConfig::isPaired() const {
    return !getTowerId().isEmpty() && 
           !getCoordId().isEmpty() && 
           !getCoordMacString().isEmpty();
}

inline void TowerConfig::clearPairing() {
    _config.remove(ConfigKeys::TOWER_ID);
    _config.remove(ConfigKeys::COORD_ID);
    _config.remove(ConfigKeys::FARM_ID);
    _config.remove(KEY_COORD_MAC);
    _config.remove(ConfigKeys::LMK);
    _config.remove(KEY_WIFI_CHANNEL);
}

// ESP-NOW security
inline String TowerConfig::getLmk() const {
    return _config.getString(ConfigKeys::LMK, "");
}

inline bool TowerConfig::setLmk(const String& lmkHex) {
    return _config.setString(ConfigKeys::LMK, lmkHex);
}

inline uint8_t TowerConfig::getWifiChannel() const {
    return (uint8_t)_config.getInt(KEY_WIFI_CHANNEL, 0);
}

inline bool TowerConfig::setWifiChannel(uint8_t channel) {
    return _config.setInt(KEY_WIFI_CHANNEL, channel);
}

// Operational parameters
inline uint32_t TowerConfig::getTelemetryIntervalMs() const {
    return (uint32_t)_config.getInt(ConfigKeys::TELEMETRY_INTERVAL_MS, 
                                     Defaults::TELEMETRY_INTERVAL_MS);
}

inline bool TowerConfig::setTelemetryIntervalMs(uint32_t intervalMs) {
    return _config.setInt(ConfigKeys::TELEMETRY_INTERVAL_MS, (int)intervalMs);
}

inline uint16_t TowerConfig::getPumpMaxDurationS() const {
    return (uint16_t)_config.getInt(ConfigKeys::PUMP_MAX_DURATION_S,
                                     Defaults::PUMP_MAX_DURATION_S);
}

inline bool TowerConfig::setPumpMaxDurationS(uint16_t maxSeconds) {
    return _config.setInt(ConfigKeys::PUMP_MAX_DURATION_S, maxSeconds);
}

// Firmware version
inline String TowerConfig::getFirmwareVersion() const {
    return TOWER_FIRMWARE_VERSION;
}

// Full configuration save
inline bool TowerConfig::saveJoinAccept(
    const String& towerId,
    const String& coordId,
    const String& farmId,
    const uint8_t coordMac[6],
    const String& lmkHex,
    uint8_t wifiChannel,
    uint32_t telemetryMs,
    uint16_t pumpMaxS
) {
    bool ok = true;
    ok &= setTowerId(towerId);
    ok &= setCoordId(coordId);
    ok &= setFarmId(farmId);
    ok &= setCoordMac(coordMac);
    if (!lmkHex.isEmpty()) ok &= setLmk(lmkHex);
    ok &= setWifiChannel(wifiChannel);
    ok &= setTelemetryIntervalMs(telemetryMs);
    ok &= setPumpMaxDurationS(pumpMaxS);
    return ok;
}

inline bool TowerConfig::factoryReset() {
    _config.clear();
    return true;
}

// Static helpers
inline bool TowerConfig::parseMacString(const String& macStr, uint8_t mac[6]) {
    String cleanMac = macStr;
    cleanMac.replace(":", "");
    cleanMac.replace("-", "");
    cleanMac.toUpperCase();
    
    if (cleanMac.length() != 12) return false;
    
    for (int i = 0; i < 6; i++) {
        char hex[3] = {cleanMac.charAt(i * 2), cleanMac.charAt(i * 2 + 1), 0};
        mac[i] = (uint8_t)strtol(hex, nullptr, 16);
    }
    return true;
}

inline String TowerConfig::formatMacString(const uint8_t mac[6]) {
    char buf[18];
    snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    return String(buf);
}
