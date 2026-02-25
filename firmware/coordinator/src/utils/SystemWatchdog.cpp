#include "SystemWatchdog.h"
#include "Logger.h"

// Static member initialization
bool SystemWatchdog::initialized = false;
uint32_t SystemWatchdog::timeoutSeconds = 30;
bool SystemWatchdog::panicEnabled = true;

bool SystemWatchdog::init(uint32_t timeoutSec, bool panicOnTimeout) {
    if (initialized) {
        Logger::warn("Watchdog already initialized");
        return true;
    }
    
    timeoutSeconds = timeoutSec;
    panicEnabled = panicOnTimeout;
    
    // Configure watchdog timer with version-specific API
    esp_err_t err;
    
#if ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
    // ESP-IDF 5.x API (ESP32-S3)
    esp_task_wdt_config_t wdt_config = {
        .timeout_ms = timeoutSeconds * 1000,  // Convert to milliseconds
        .idle_core_mask = 0,                   // Don't watch idle tasks
        .trigger_panic = panicEnabled          // Trigger panic on timeout
    };
    err = esp_task_wdt_init(&wdt_config);
#else
    // ESP-IDF 4.x API (ESP32-C3)
    err = esp_task_wdt_init(timeoutSeconds, panicEnabled);
#endif
    
    if (err != ESP_OK) {
        Logger::error("Failed to initialize watchdog: %d", err);
        return false;
    }
    
    initialized = true;
    Logger::info("✓ Watchdog initialized (timeout: %d seconds, panic: %s)", 
                 timeoutSeconds, panicEnabled ? "enabled" : "disabled");
    
    return true;
}

bool SystemWatchdog::addCurrentTask() {
    if (!initialized) {
        Logger::warn("Watchdog not initialized, call init() first");
        return false;
    }
    
    esp_err_t err = esp_task_wdt_add(NULL);  // NULL = current task
    if (err != ESP_OK) {
        Logger::error("Failed to add task to watchdog: %d", err);
        return false;
    }
    
    Logger::info("Task '%s' added to watchdog monitoring", pcTaskGetName(NULL));
    return true;
}

bool SystemWatchdog::removeCurrentTask() {
    if (!initialized) {
        return false;
    }
    
    esp_err_t err = esp_task_wdt_delete(NULL);  // NULL = current task
    if (err != ESP_OK) {
        Logger::error("Failed to remove task from watchdog: %d", err);
        return false;
    }
    
    Logger::info("Task '%s' removed from watchdog monitoring", pcTaskGetName(NULL));
    return true;
}

void SystemWatchdog::feed() {
    if (!initialized) {
        return;  // Silently ignore if not initialized
    }
    
    // Reset the watchdog timer for the current task
    esp_task_wdt_reset();
}

void SystemWatchdog::deinit() {
    if (!initialized) {
        return;
    }
    
    esp_task_wdt_deinit();
    initialized = false;
    Logger::info("Watchdog deinitialized");
}

bool SystemWatchdog::isInitialized() {
    return initialized;
}

uint32_t SystemWatchdog::getTimeout() {
    return timeoutSeconds;
}

void SystemWatchdog::printStatus() {
    if (!initialized) {
        Serial.println("Watchdog: NOT INITIALIZED");
        return;
    }
    
    Serial.println("=== Watchdog Status ===");
    Serial.printf("Timeout: %d seconds\n", timeoutSeconds);
    Serial.printf("Panic on timeout: %s\n", panicEnabled ? "YES" : "NO");
    Serial.printf("Current task: %s\n", pcTaskGetName(NULL));
    Serial.println("======================");
}
