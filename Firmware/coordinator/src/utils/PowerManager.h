#pragma once

#include <Arduino.h>
#include "esp_pm.h"
#include "esp_sleep.h"

/**
 * @brief ESP32 Power Management Wrapper
 * 
 * Provides automatic light sleep and dynamic frequency scaling (DFS)
 * to reduce power consumption while maintaining responsiveness.
 * 
 * Benefits:
 * - 30-50% power reduction with automatic light sleep
 * - Dynamic CPU frequency scaling (80MHz idle, 240MHz active)
 * - Wake locks for critical operations (ESP-NOW TX/RX)
 * - Modem sleep for WiFi when not transmitting
 * 
 * Power Modes:
 * - HIGH_PERFORMANCE: No power saving, max frequency (240MHz)
 * - BALANCED: Light sleep + DFS (80-240MHz)
 * - LOW_POWER: Aggressive light sleep + low freq (80MHz)
 * 
 * Usage:
 *   PowerManager::init(PowerMode::BALANCED);
 *   
 *   // Critical operation
 *   PowerManager::acquireLock();
 *   performCriticalTask();
 *   PowerManager::releaseLock();
 */
class PowerManager {
public:
    enum PowerMode {
        HIGH_PERFORMANCE,  // No power saving, 240MHz constant
        BALANCED,          // Light sleep + DFS (default)
        LOW_POWER          // Aggressive power saving
    };
    
    /**
     * @brief Initialize power management
     * 
     * @param mode Power mode to use (default: BALANCED)
     * @return true if initialization successful
     */
    static bool init(PowerMode mode = BALANCED);
    
    /**
     * @brief Change power mode at runtime
     * 
     * @param mode New power mode
     * @return true if mode changed successfully
     */
    static bool setMode(PowerMode mode);
    
    /**
     * @brief Get current power mode
     * 
     * @return Current PowerMode
     */
    static PowerMode getMode();
    
    /**
     * @brief Acquire a power lock (prevent sleep)
     * 
     * Call before critical operations that require max CPU frequency
     * (e.g., ESP-NOW transmission, sensor reading). Must be paired
     * with releaseLock().
     * 
     * @return true if lock acquired
     */
    static bool acquireLock();
    
    /**
     * @brief Release power lock (allow sleep)
     * 
     * @return true if lock released
     */
    static bool releaseLock();
    
    /**
     * @brief Check if power lock is currently held
     * 
     * @return true if locked
     */
    static bool isLocked();
    
    /**
     * @brief Disable power management
     * 
     * Returns to default ESP32 power settings.
     */
    static void deinit();
    
    /**
     * @brief Get power statistics
     * 
     * @param activeTimeMs Output: time spent at high frequency
     * @param sleepTimeMs Output: time spent in light sleep
     */
    static void getStats(uint32_t& activeTimeMs, uint32_t& sleepTimeMs);
    
    /**
     * @brief Print power management status
     * 
     * Shows current mode, lock status, and power statistics.
     */
    static void printStatus();

private:
    static bool initialized;
    static PowerMode currentMode;
#if __has_include("esp_pm.h") && defined(CONFIG_PM_ENABLE)
    static esp_pm_lock_handle_t pmLock;
    static esp_pm_config_t getModeConfig(PowerMode mode);
#else
    static void* pmLock;
#endif
    static bool lockHeld;
    static uint32_t activeTime;
    static uint32_t sleepTime;
    static uint32_t lastStateChange;
};
