#pragma once

#include <Arduino.h>
#include <ArduinoJson.h>

/**
 * Unified Configuration System
 * Single source of truth for all coordinator settings
 * Replaces scattered configs across multiple namespaces
 */

namespace ConfigVersion {
    constexpr uint16_t CURRENT = 1;
}

struct WiFiConfig {
    String ssid;
    String password;
    uint8_t channel = 1;  // Auto-discovered and locked with ESP-NOW
    bool enabled = true;
    
    bool isValid() const {
        return !ssid.isEmpty() && enabled;
    }
};

struct MqttConfig {
    // Default credentials that match docker-compose mosquitto config
    static constexpr const char* DEFAULT_USER = "user1";
    static constexpr const char* DEFAULT_PASS = "user1";
    static constexpr uint16_t DEFAULT_PORT = 1883;
    
    // TEMPORARY: Hardcoded broker for initial setup
    String broker_host = "192.168.1.49";
    uint16_t broker_port = DEFAULT_PORT;
    String username = DEFAULT_USER;
    String password = DEFAULT_PASS;
    String farm_id = "farm001";
    String coordinator_id;  // Empty = use MAC address
    bool enabled = true;
    bool use_defaults = true;  // Flag to indicate using default credentials
    
    bool isValid() const {
        // Broker host can be empty if auto-discovery will be used
        // Username/password can be empty for anonymous connection
        return enabled;
    }
};

struct EspNowConfig {
    uint8_t channel = 1;  // Must match WiFi channel
    bool pairing_enabled = true;
    uint32_t pairing_duration_ms = 300000;  // 5 minutes default
    
    bool isValid() const {
        return channel >= 1 && channel <= 13 && pairing_duration_ms > 0;
    }
};

struct SystemConfig {
    String device_name;  // User-friendly name (e.g., "Living Room Coordinator")
    uint32_t telemetry_interval_ms = 5000;
    bool debug_mode = false;
    
    bool isValid() const {
        return telemetry_interval_ms >= 1000;  // Min 1 second
    }
};

struct Config {
    // Configuration sections
    WiFiConfig wifi;
    MqttConfig mqtt;
    EspNowConfig espnow;
    SystemConfig system;
    
    // Version for future migrations
    uint16_t version = ConfigVersion::CURRENT;
    
    // Validation
    bool isValid() const {
        return wifi.isValid() && 
               mqtt.isValid() && 
               espnow.isValid() && 
               system.isValid() &&
               wifi.channel == espnow.channel;  // Channels must match!
    }
    
    // JSON serialization
    String toJson() const;
    static Config fromJson(const String& json);
    
    // Helper to check if this is first boot (no WiFi configured)
    bool isFirstBoot() const {
        return wifi.ssid.isEmpty();
    }
};

// JSON Serialization Implementation
inline String Config::toJson() const {
    DynamicJsonDocument doc(2048);
    
    // WiFi
    doc["wifi"]["ssid"] = wifi.ssid;
    doc["wifi"]["password"] = wifi.password;
    doc["wifi"]["channel"] = wifi.channel;
    doc["wifi"]["enabled"] = wifi.enabled;
    
    // MQTT
    doc["mqtt"]["broker_host"] = mqtt.broker_host;
    doc["mqtt"]["broker_port"] = mqtt.broker_port;
    doc["mqtt"]["username"] = mqtt.username;
    doc["mqtt"]["password"] = mqtt.password;
    doc["mqtt"]["farm_id"] = mqtt.farm_id;
    doc["mqtt"]["coordinator_id"] = mqtt.coordinator_id;
    doc["mqtt"]["enabled"] = mqtt.enabled;
    doc["mqtt"]["use_defaults"] = mqtt.use_defaults;
    
    // ESP-NOW
    doc["espnow"]["channel"] = espnow.channel;
    doc["espnow"]["pairing_enabled"] = espnow.pairing_enabled;
    doc["espnow"]["pairing_duration_ms"] = espnow.pairing_duration_ms;
    
    // System
    doc["system"]["device_name"] = system.device_name;
    doc["system"]["telemetry_interval_ms"] = system.telemetry_interval_ms;
    doc["system"]["debug_mode"] = system.debug_mode;
    
    // Version
    doc["version"] = version;
    
    String json;
    serializeJson(doc, json);
    return json;
}

inline Config Config::fromJson(const String& json) {
    Config config;
    DynamicJsonDocument doc(2048);
    
    DeserializationError error = deserializeJson(doc, json);
    if (error) {
        Serial.printf("Config::fromJson error: %s\n", error.c_str());
        return config;  // Return default config
    }
    
    // WiFi
    config.wifi.ssid = doc["wifi"]["ssid"] | "";
    config.wifi.password = doc["wifi"]["password"] | "";
    config.wifi.channel = doc["wifi"]["channel"] | 1;
    config.wifi.enabled = doc["wifi"]["enabled"] | true;
    
    // MQTT
    config.mqtt.broker_host = doc["mqtt"]["broker_host"] | "";
    config.mqtt.broker_port = doc["mqtt"]["broker_port"] | MqttConfig::DEFAULT_PORT;
    config.mqtt.username = doc["mqtt"]["username"] | MqttConfig::DEFAULT_USER;
    config.mqtt.password = doc["mqtt"]["password"] | MqttConfig::DEFAULT_PASS;
    config.mqtt.farm_id = doc["mqtt"]["farm_id"] | "farm001";
    config.mqtt.coordinator_id = doc["mqtt"]["coordinator_id"] | "";
    config.mqtt.enabled = doc["mqtt"]["enabled"] | true;
    config.mqtt.use_defaults = doc["mqtt"]["use_defaults"] | true;
    
    // ESP-NOW
    config.espnow.channel = doc["espnow"]["channel"] | 1;
    config.espnow.pairing_enabled = doc["espnow"]["pairing_enabled"] | true;
    config.espnow.pairing_duration_ms = doc["espnow"]["pairing_duration_ms"] | 300000;
    
    // System
    config.system.device_name = doc["system"]["device_name"] | "";
    config.system.telemetry_interval_ms = doc["system"]["telemetry_interval_ms"] | 5000;
    config.system.debug_mode = doc["system"]["debug_mode"] | false;
    
    // Version
    config.version = doc["version"] | ConfigVersion::CURRENT;
    
    return config;
}
