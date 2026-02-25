#include "ConfigStore.h"

// Static member initialization
Preferences ConfigStore::prefs;
bool ConfigStore::initialized = false;
Config ConfigStore::cachedConfig;
bool ConfigStore::cacheValid = false;

bool ConfigStore::initialize() {
    if (initialized) {
        return true;
    }
    
    Serial.println("[ConfigStore] Initializing unified config storage...");
    
    // Try to open our namespace
    if (!prefs.begin(NAMESPACE, false)) {
        Serial.printf("[ConfigStore] Failed to open namespace '%s', attempting recovery...\n", NAMESPACE);
        
        // Try to create the namespace
        prefs.begin(NAMESPACE, false);
        prefs.end();
        
        if (!prefs.begin(NAMESPACE, false)) {
            Serial.println("[ConfigStore] ERROR: Could not create config namespace!");
            return false;
        }
    }
    prefs.end();
    
    initialized = true;
    Serial.println("[ConfigStore] ✓ Initialized successfully");
    
    // Check if we need to migrate from legacy config
    if (hasLegacyConfig()) {
        Serial.println("[ConfigStore] Detected legacy config - attempting migration...");
        if (migrateFromLegacy()) {
            Serial.println("[ConfigStore] ✓ Legacy config migrated successfully");
        } else {
            Serial.println("[ConfigStore] WARNING: Legacy config migration failed");
        }
    }
    
    return true;
}

bool ConfigStore::isInitialized() {
    return initialized;
}

Config ConfigStore::load() {
    if (!initialized) {
        Serial.println("[ConfigStore] ERROR: Not initialized! Returning default config.");
        return loadDefault();
    }
    
    // Return cached config if valid
    if (cacheValid) {
        return cachedConfig;
    }
    
    // Open preferences read-only
    if (!openPreferences(true)) {
        Serial.println("[ConfigStore] ERROR: Could not open preferences for reading");
        return loadDefault();
    }
    
    // Read JSON string from NVS
    String json = prefs.getString(KEY_CONFIG_JSON, "");
    closePreferences();
    
    if (json.isEmpty()) {
        Serial.println("[ConfigStore] No saved config found - using defaults");
        cachedConfig = loadDefault();
        cacheValid = true;
        return cachedConfig;
    }
    
    // Deserialize from JSON
    cachedConfig = Config::fromJson(json);
    
    // Validate config
    if (!cachedConfig.isValid()) {
        Serial.println("[ConfigStore] WARNING: Loaded config is invalid! Using defaults.");
        cachedConfig = loadDefault();
    }
    
    cacheValid = true;
    Serial.println("[ConfigStore] ✓ Config loaded from NVS");
    return cachedConfig;
}

bool ConfigStore::save(const Config& config) {
    if (!initialized) {
        Serial.println("[ConfigStore] ERROR: Not initialized! Cannot save.");
        return false;
    }
    
    // Validate before saving
    if (!config.isValid()) {
        Serial.println("[ConfigStore] ERROR: Config validation failed! Not saving.");
        Serial.printf("  WiFi valid: %d\n", config.wifi.isValid());
        Serial.printf("  MQTT valid: %d\n", config.mqtt.isValid());
        Serial.printf("  ESP-NOW valid: %d\n", config.espnow.isValid());
        Serial.printf("  System valid: %d\n", config.system.isValid());
        Serial.printf("  Channels match: %d\n", config.wifi.channel == config.espnow.channel);
        return false;
    }
    
    // Serialize to JSON
    String json = config.toJson();
    
    // Open preferences for writing
    if (!openPreferences(false)) {
        Serial.println("[ConfigStore] ERROR: Could not open preferences for writing");
        return false;
    }
    
    // Save JSON string to NVS
    size_t written = prefs.putString(KEY_CONFIG_JSON, json);
    closePreferences();
    
    if (written == 0) {
        Serial.println("[ConfigStore] ERROR: Failed to write config to NVS");
        return false;
    }
    
    // Update cache
    cachedConfig = config;
    cacheValid = true;
    
    Serial.printf("[ConfigStore] ✓ Config saved (%d bytes)\n", written);
    return true;
}

bool ConfigStore::reset() {
    if (!initialized) {
        Serial.println("[ConfigStore] ERROR: Not initialized!");
        return false;
    }
    
    Serial.println("[ConfigStore] Performing factory reset...");
    
    // Open preferences for writing
    if (!openPreferences(false)) {
        Serial.println("[ConfigStore] ERROR: Could not open preferences");
        return false;
    }
    
    // Clear all keys
    bool success = prefs.clear();
    closePreferences();
    
    if (success) {
        // Invalidate cache
        cacheValid = false;
        Serial.println("[ConfigStore] ✓ Factory reset complete");
        return true;
    } else {
        Serial.println("[ConfigStore] ERROR: Factory reset failed");
        return false;
    }
}

String ConfigStore::exportJson() {
    Config config = load();
    return config.toJson();
}

bool ConfigStore::importJson(const String& json) {
    Config config = Config::fromJson(json);
    if (!config.isValid()) {
        Serial.println("[ConfigStore] ERROR: Imported config is invalid");
        return false;
    }
    return save(config);
}

bool ConfigStore::migrateFromLegacy() {
    Config config = loadDefault();
    bool migrated = false;
    
    // Migrate WiFi config
    Preferences wifiPrefs;
    if (wifiPrefs.begin("wifi", true)) {
        String ssid = wifiPrefs.getString("ssid", "");
        String password = wifiPrefs.getString("password", "");
        if (!ssid.isEmpty()) {
            config.wifi.ssid = ssid;
            config.wifi.password = password;
            migrated = true;
            Serial.printf("[ConfigStore] Migrated WiFi: %s\n", ssid.c_str());
        }
        wifiPrefs.end();
    }
    
    // Migrate MQTT config
    Preferences mqttPrefs;
    if (mqttPrefs.begin("mqtt", true)) {
        String broker = mqttPrefs.getString("broker_host", "");
        if (!broker.isEmpty()) {
            config.mqtt.broker_host = broker;
            config.mqtt.broker_port = mqttPrefs.getInt("broker_port", 1883);
            config.mqtt.username = mqttPrefs.getString("broker_user", "user1");
            config.mqtt.password = mqttPrefs.getString("broker_pass", "user1");
            config.mqtt.farm_id = mqttPrefs.getString("farm_id", "farm001");
            config.mqtt.coordinator_id = mqttPrefs.getString("coord_id", "");
            
            // Check if using defaults
            config.mqtt.use_defaults = (config.mqtt.username == "user1" && config.mqtt.password == "user1");
            
            migrated = true;
            Serial.printf("[ConfigStore] Migrated MQTT: %s\n", broker.c_str());
        }
        mqttPrefs.end();
    }
    
    if (migrated) {
        return save(config);
    }
    
    return false;
}

bool ConfigStore::hasLegacyConfig() {
    // Check if old "wifi" namespace exists and has data
    Preferences testPrefs;
    bool hasWifi = false;
    if (testPrefs.begin("wifi", true)) {
        hasWifi = !testPrefs.getString("ssid", "").isEmpty();
        testPrefs.end();
    }
    return hasWifi;
}

bool ConfigStore::isFirstBoot() {
    Config config = load();
    return config.isFirstBoot();
}

bool ConfigStore::openPreferences(bool readOnly) {
    return prefs.begin(NAMESPACE, readOnly);
}

void ConfigStore::closePreferences() {
    prefs.end();
}

Config ConfigStore::loadDefault() {
    Config config;
    // All defaults are set in the Config struct definitions
    Serial.println("[ConfigStore] Using default configuration");
    return config;
}
