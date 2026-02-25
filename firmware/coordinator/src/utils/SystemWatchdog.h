#pragma once

#include <Arduino.h>
#include "esp_task_wdt.h"

/**
 * @brief System Watchdog Timer Wrapper
 * 
 * Provides a clean interface to the ESP32 Task Watchdog Timer (TWDT).
 * Monitors critical tasks and automatically reboots the system if a task
 * becomes unresponsive or locked up.
 * 
 * Benefits:
 * - Automatic recovery from firmware lockups
 * - Per-task monitoring with custom timeouts
 * - Panic handler with stack trace on timeout
 * - Configurable timeout periods
 * 
 * Usage:
 *   SystemWatchdog::init(30);  // 30 second timeout
 *   SystemWatchdog::addCurrentTask();
 *   
 *   void loop() {
 *       SystemWatchdog::feed();  // Reset watchdog timer
 *       // ... critical operations ...
 *   }
 */
class SystemWatchdog {
public:
    /**
     * @brief Initialize the watchdog timer
     * 
     * @param timeoutSeconds Watchdog timeout in seconds (default: 30)
     * @param panicOnTimeout If true, trigger panic and reboot on timeout (default: true)
     * @return true if initialization successful
     */
    static bool init(uint32_t timeoutSeconds = 30, bool panicOnTimeout = true);
    
    /**
     * @brief Add the current task to watchdog monitoring
     * 
     * Call this from any task that should be monitored.
     * The task must call feed() periodically or it will trigger a watchdog reset.
     * 
     * @return true if task added successfully
     */
    static bool addCurrentTask();
    
    /**
     * @brief Remove the current task from watchdog monitoring
     * 
     * @return true if task removed successfully
     */
    static bool removeCurrentTask();
    
    /**
     * @brief Feed the watchdog (reset the timer)
     * 
     * Must be called periodically from all monitored tasks to prevent
     * watchdog timeout. Should be called at least once per timeout period.
     */
    static void feed();
    
    /**
     * @brief Deinitialize the watchdog timer
     * 
     * Stops watchdog monitoring and frees resources.
     */
    static void deinit();
    
    /**
     * @brief Check if watchdog is initialized
     * 
     * @return true if watchdog is active
     */
    static bool isInitialized();
    
    /**
     * @brief Get the configured timeout in seconds
     * 
     * @return Watchdog timeout in seconds
     */
    static uint32_t getTimeout();
    
    /**
     * @brief Print watchdog status to serial
     * 
     * Useful for debugging - shows which tasks are monitored
     * and current watchdog configuration.
     */
    static void printStatus();

private:
    static bool initialized;
    static uint32_t timeoutSeconds;
    static bool panicEnabled;
};
