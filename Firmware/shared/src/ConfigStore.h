#pragma once

#include "Config.h"
#include <Preferences.h>

/**
 * ConfigStore - Unified Configuration Storage
 * 
 * Single, robust NVS wrapper that manages all coordinator configuration.
 * Replaces the scattered ConfigManager instances.
 * 
 * Features:
 * - Single namespace "coordinator_v1" for all settings
 * - Atomic save operations
 * - Auto-recovery from corruption
 * - Migration support from old configs
 * - JSON export/import
 */
class ConfigStore {
public:
    // Initialization - call once in main.cpp after nvs_flash_init()
    static bool initialize();
    static bool isInitialized();
    
    // Configuration management
    static Config load();
    static bool save(const Config& config);
    static bool reset();  // Factory reset - clears all config
    
    // JSON export/import for backup/restore
    static String exportJson();
    static bool importJson(const String& json);
    
    // Migration from old ConfigManager namespaces
    static bool migrateFromLegacy();
    static bool hasLegacyConfig();
    
    // Helpers
    static bool isFirstBoot();
    
private:
    static constexpr const char* NAMESPACE = "coordinator_v1";
    static constexpr const char* KEY_CONFIG_JSON = "config_json";
    
    static Preferences prefs;
    static bool initialized;
    static Config cachedConfig;
    static bool cacheValid;
    
    // Internal helpers
    static bool openPreferences(bool readOnly);
    static void closePreferences();
    static Config loadDefault();
};
