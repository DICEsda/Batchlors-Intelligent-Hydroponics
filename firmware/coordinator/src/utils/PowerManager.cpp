#include "PowerManager.h"
#include "Logger.h"

// Check if power management is available (ESP-IDF 4.0+)
#if __has_include("esp_pm.h") && defined(CONFIG_PM_ENABLE)
#define PM_AVAILABLE 1
#else
#define PM_AVAILABLE 0
#endif

// Static member initialization
bool PowerManager::initialized = false;
PowerManager::PowerMode PowerManager::currentMode = PowerManager::BALANCED;
#if PM_AVAILABLE
esp_pm_lock_handle_t PowerManager::pmLock = nullptr;
#else
void* PowerManager::pmLock = nullptr;
#endif
bool PowerManager::lockHeld = false;
uint32_t PowerManager::activeTime = 0;
uint32_t PowerManager::sleepTime = 0;
uint32_t PowerManager::lastStateChange = 0;

#if PM_AVAILABLE
esp_pm_config_t PowerManager::getModeConfig(PowerMode mode) {
    esp_pm_config_t config;
    
    switch (mode) {
        case HIGH_PERFORMANCE:
            config.max_freq_mhz = 240;
            config.min_freq_mhz = 240;  // Always max frequency
            config.light_sleep_enable = false;
            break;
            
        case BALANCED:
            config.max_freq_mhz = 240;
            config.min_freq_mhz = 80;   // Scale down when idle
            config.light_sleep_enable = true;
            break;
            
        case LOW_POWER:
            config.max_freq_mhz = 160;
            config.min_freq_mhz = 80;
            config.light_sleep_enable = true;
            break;
    }
    
    return config;
}
#endif

bool PowerManager::init(PowerMode mode) {
    if (initialized) {
        Logger::warn("Power manager already initialized");
        return true;
    }
    
    currentMode = mode;
    
#if PM_AVAILABLE
    esp_pm_config_t config = getModeConfig(mode);
    
    esp_err_t err = esp_pm_configure(&config);
    if (err != ESP_OK) {
        Logger::error("Failed to configure power management: %d", err);
        return false;
    }
    
    // Create power lock for critical sections
    err = esp_pm_lock_create(ESP_PM_CPU_FREQ_MAX, 0, "critical", &pmLock);
    if (err != ESP_OK) {
        Logger::error("Failed to create power lock: %d", err);
        return false;
    }
    
    initialized = true;
    lastStateChange = millis();
    
    const char* modeStr = (mode == HIGH_PERFORMANCE) ? "HIGH_PERFORMANCE" :
                          (mode == BALANCED) ? "BALANCED" : "LOW_POWER";
    Logger::info("✓ Power management initialized (mode: %s)", modeStr);
    Logger::info("  Max freq: %d MHz, Min freq: %d MHz, Light sleep: %s",
                 config.max_freq_mhz, config.min_freq_mhz,
                 config.light_sleep_enable ? "enabled" : "disabled");
#else
    initialized = true;
    Logger::warn("Power management not available on this platform (requires ESP-IDF 4.0+)");
    Logger::info("  Running at fixed frequency - power saving disabled");
#endif
    
    return true;
}

bool PowerManager::setMode(PowerMode mode) {
    if (!initialized) {
        Logger::warn("Power manager not initialized");
        return false;
    }
    
    if (mode == currentMode) {
        return true;  // Already in this mode
    }
    
    currentMode = mode;
    
#if PM_AVAILABLE
    esp_pm_config_t config = getModeConfig(mode);
    
    esp_err_t err = esp_pm_configure(&config);
    if (err != ESP_OK) {
        Logger::error("Failed to change power mode: %d", err);
        return false;
    }
    
    const char* modeStr = (mode == HIGH_PERFORMANCE) ? "HIGH_PERFORMANCE" :
                          (mode == BALANCED) ? "BALANCED" : "LOW_POWER";
    Logger::info("Power mode changed to: %s", modeStr);
#else
    Logger::warn("Power mode change requested but PM not available on this platform");
#endif
    
    return true;
}

PowerManager::PowerMode PowerManager::getMode() {
    return currentMode;
}

bool PowerManager::acquireLock() {
    if (!initialized || !pmLock) {
        return false;
    }
    
    if (lockHeld) {
        return true;  // Already locked
    }
    
#if PM_AVAILABLE
    esp_err_t err = esp_pm_lock_acquire(pmLock);
    if (err != ESP_OK) {
        Logger::error("Failed to acquire power lock: %d", err);
        return false;
    }
#endif
    
    lockHeld = true;
    uint32_t now = millis();
    if (lastStateChange > 0) {
        sleepTime += (now - lastStateChange);
    }
    lastStateChange = now;
    
    return true;
}

bool PowerManager::releaseLock() {
    if (!initialized || !pmLock) {
        return false;
    }
    
    if (!lockHeld) {
        return true;  // Already released
    }
    
#if PM_AVAILABLE
    esp_err_t err = esp_pm_lock_release(pmLock);
    if (err != ESP_OK) {
        Logger::error("Failed to release power lock: %d", err);
        return false;
    }
#endif
    
    lockHeld = false;
    uint32_t now = millis();
    if (lastStateChange > 0) {
        activeTime += (now - lastStateChange);
    }
    lastStateChange = now;
    
    return true;
}

bool PowerManager::isLocked() {
    return lockHeld;
}

void PowerManager::deinit() {
    if (!initialized) {
        return;
    }
    
#if PM_AVAILABLE
    if (pmLock) {
        if (lockHeld) {
            esp_pm_lock_release(pmLock);
        }
        esp_pm_lock_delete(pmLock);
        pmLock = nullptr;
    }
#endif
    
    initialized = false;
    Logger::info("Power management deinitialized");
}

void PowerManager::getStats(uint32_t& activeMs, uint32_t& sleepMs) {
    activeMs = activeTime;
    sleepMs = sleepTime;
}

void PowerManager::printStatus() {
    if (!initialized) {
        Serial.println("Power Management: NOT INITIALIZED");
        return;
    }
    
    const char* modeStr = (currentMode == HIGH_PERFORMANCE) ? "HIGH_PERFORMANCE" :
                          (currentMode == BALANCED) ? "BALANCED" : "LOW_POWER";
    
    Serial.println("=== Power Management Status ===");
    Serial.printf("Mode: %s\n", modeStr);
    Serial.printf("Lock held: %s\n", lockHeld ? "YES" : "NO");
    Serial.printf("Active time: %lu ms\n", activeTime);
    Serial.printf("Sleep time: %lu ms\n", sleepTime);
    
    if (activeTime + sleepTime > 0) {
        float activePct = (float)activeTime / (activeTime + sleepTime) * 100.0f;
        Serial.printf("Active: %.1f%%\n", activePct);
    }
    
    Serial.println("==============================");
}
