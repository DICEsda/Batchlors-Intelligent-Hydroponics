#pragma once

#include <Arduino.h>
#include <Preferences.h>
#include <ArduinoJson.h>

namespace ConfigKeys {
    // Coordinator
    static const char* const PRESENCE_DEBOUNCE_MS = "presence_debounce_ms";
    static const char* const OCCUPANCY_HOLD_MS   = "occupancy_hold_ms";
    static const char* const FADE_IN_MS          = "fade_in_ms";
    static const char* const FADE_OUT_MS         = "fade_out_ms";
    static const char* const PAIRING_WINDOW_S    = "pairing_window_s";

    // Node
    static const char* const NODE_ID             = "node_id";
    static const char* const LIGHT_ID            = "light_id";
    static const char* const LMK                 = "lmk"; // ESP-NOW LMK key
    static const char* const PWM_FREQ_HZ         = "pwm_freq_hz";
    static const char* const PWM_RESOLUTION_BITS = "pwm_res_bits";
    static const char* const TELEMETRY_INTERVAL_S= "telemetry_s";
    static const char* const RX_WINDOW_MS        = "rx_window_ms";
    static const char* const RX_PERIOD_MS        = "rx_period_ms";
    static const char* const DERATE_START_C      = "derate_start_c";
    static const char* const DERATE_MIN_DUTY_PCT = "derate_min_duty_pct";
    static const char* const RETRY_COUNT         = "retry_count";
    static const char* const CMD_TTL_MS          = "cmd_ttl_ms";
    
    // Hydroponic System - Identifiers
    static const char* const FARM_ID             = "farm_id";
    static const char* const COORD_ID            = "coord_id";
    static const char* const TOWER_ID            = "tower_id";
    
    // Hydroponic System - Water Quality Thresholds
    static const char* const PH_MIN              = "ph_min";
    static const char* const PH_MAX              = "ph_max";
    static const char* const EC_MIN_MS_CM        = "ec_min_ms_cm";
    static const char* const EC_MAX_MS_CM        = "ec_max_ms_cm";
    static const char* const WATER_TEMP_MIN_C    = "water_temp_min_c";
    static const char* const WATER_TEMP_MAX_C    = "water_temp_max_c";
    
    // Hydroponic System - Water Level
    static const char* const WATER_LEVEL_MIN_PCT = "water_level_min_pct";
    static const char* const WATER_LEVEL_MAX_CM  = "water_level_max_cm";
    
    // Hydroponic System - Pump Control
    static const char* const PUMP_MAX_DURATION_S = "pump_max_duration_s";
    static const char* const PUMP_COOLDOWN_S     = "pump_cooldown_s";
    
    // Hydroponic System - Telemetry
    static const char* const TELEMETRY_INTERVAL_MS = "telemetry_ms";
    
    // Hydroponic System - Grow Light
    static const char* const LIGHT_ON_HOUR       = "light_on_hour";
    static const char* const LIGHT_OFF_HOUR      = "light_off_hour";
    static const char* const LIGHT_INTENSITY_PCT = "light_intensity_pct";
}

namespace Defaults {
    // Coordinator defaults (from PRD)
    static constexpr int PRESENCE_DEBOUNCE_MS = 150;
    static constexpr int OCCUPANCY_HOLD_MS   = 5000;
    static constexpr int FADE_IN_MS          = 150;
    static constexpr int FADE_OUT_MS         = 1000;
    static constexpr int PAIRING_WINDOW_S    = 120;

    // Node defaults (from PRD)
    static constexpr int PWM_FREQ_HZ         = 1000;
    static constexpr int PWM_RESOLUTION_BITS = 12;
    static constexpr int TELEMETRY_INTERVAL_S= 1;
    static constexpr int RX_WINDOW_MS        = 20;
    static constexpr int RX_PERIOD_MS        = 100;
    static constexpr float DERATE_START_C    = 70.0f;
    static constexpr int DERATE_MIN_DUTY_PCT = 30;
    static constexpr int RETRY_COUNT         = 3;
    static constexpr int CMD_TTL_MS          = 1500;
    
    // Hydroponic System - Water Quality (optimal ranges for leafy greens)
    static constexpr float PH_MIN            = 5.5f;
    static constexpr float PH_MAX            = 6.5f;
    static constexpr float EC_MIN_MS_CM      = 1.0f;   // mS/cm
    static constexpr float EC_MAX_MS_CM      = 2.5f;   // mS/cm
    static constexpr float WATER_TEMP_MIN_C  = 18.0f;
    static constexpr float WATER_TEMP_MAX_C  = 24.0f;
    
    // Hydroponic System - Water Level
    static constexpr float WATER_LEVEL_MIN_PCT = 20.0f;  // alert threshold
    static constexpr float WATER_LEVEL_MAX_CM  = 30.0f;  // reservoir depth
    
    // Hydroponic System - Pump Control
    static constexpr int PUMP_MAX_DURATION_S   = 300;    // 5 minute safety limit
    static constexpr int PUMP_COOLDOWN_S       = 60;     // 1 minute between cycles
    
    // Hydroponic System - Telemetry
    static constexpr int TELEMETRY_INTERVAL_MS = 30000;  // 30 seconds
    
    // Hydroponic System - Grow Light (default 16/8 light cycle)
    static constexpr int LIGHT_ON_HOUR         = 6;      // 6 AM
    static constexpr int LIGHT_OFF_HOUR        = 22;     // 10 PM
    static constexpr int LIGHT_INTENSITY_PCT   = 80;     // 80% brightness
}

class ConfigManager {
public:
    explicit ConfigManager(const String& ns);
    ~ConfigManager();

    bool begin();
    void end();

    String getString(const String& key, const String& defaultValue = "");
    bool setString(const String& key, const String& value);

    int getInt(const String& key, int defaultValue = 0);
    bool setInt(const String& key, int value);

    float getFloat(const String& key, float defaultValue = 0.0f);
    bool setFloat(const String& key, float value);

    bool getBool(const String& key, bool defaultValue = false);
    bool setBool(const String& key, bool value);

    JsonObject getJson(const String& key);
    bool setJson(const String& key, const JsonObject& obj);

    bool exists(const String& key);
    bool remove(const String& key);
    void clear();

    bool factoryReset();
    bool validateConfig();
    void loadDefaults();

private:
    Preferences preferences;
    String namespace_name;
    bool initialized;
};
