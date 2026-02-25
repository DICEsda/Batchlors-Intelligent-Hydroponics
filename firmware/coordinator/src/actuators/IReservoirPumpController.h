/**
 * @file IReservoirPumpController.h
 * @brief Abstract interface for reservoir pump control in hydroponic coordinator
 * 
 * This interface defines the contract for reservoir pump control implementations.
 * The coordinator manages:
 * - Main circulation pump
 * - pH adjustment dosing pump
 * - Nutrient dosing pump
 * 
 * All pumps have safety interlocks based on water level.
 */
#pragma once

#include <Arduino.h>

class IReservoirPumpController {
public:
    virtual ~IReservoirPumpController() = default;
    
    /**
     * @brief Initialize pump controller hardware
     * @return true if initialization successful
     */
    virtual bool begin() = 0;
    
    /**
     * @brief Update pump state (call in main loop for timer handling)
     * Must be called frequently to handle auto-off timers and safety checks
     */
    virtual void loop() = 0;
    
    // =========================================================================
    // Main Circulation Pump
    // =========================================================================
    
    /**
     * @brief Turn main circulation pump on or off
     * @param on true to turn on, false to turn off
     */
    virtual void setMainPump(bool on) = 0;
    
    /**
     * @brief Check if main circulation pump is currently running
     * @return true if pump is on
     */
    virtual bool isMainPumpOn() const = 0;
    
    /**
     * @brief Get main pump runtime since last turnOn
     * @return Runtime in seconds (0 if pump is off)
     */
    virtual uint32_t getMainPumpRuntimeSeconds() const = 0;
    
    // =========================================================================
    // pH Dosing Pump
    // =========================================================================
    
    /**
     * @brief Control pH adjustment dosing pump
     * @param on true to turn on, false to turn off
     * @param durationMs Auto-off duration in milliseconds (0 = manual control)
     */
    virtual void setPhDosingPump(bool on, uint32_t durationMs = 0) = 0;
    
    /**
     * @brief Check if pH dosing pump is currently running
     * @return true if pump is on
     */
    virtual bool isPhDosingPumpOn() const = 0;
    
    // =========================================================================
    // Nutrient Dosing Pump
    // =========================================================================
    
    /**
     * @brief Control nutrient dosing pump
     * @param on true to turn on, false to turn off
     * @param durationMs Auto-off duration in milliseconds (0 = manual control)
     */
    virtual void setNutrientDosingPump(bool on, uint32_t durationMs = 0) = 0;
    
    /**
     * @brief Check if nutrient dosing pump is currently running
     * @return true if pump is on
     */
    virtual bool isNutrientDosingPumpOn() const = 0;
    
    // =========================================================================
    // Safety Interlocks
    // =========================================================================
    
    /**
     * @brief Update water level status for safety interlock
     * @param ok true if water level is above minimum threshold
     */
    virtual void setWaterLevelOk(bool ok) = 0;
    
    /**
     * @brief Get current water level status
     * @return true if water level is above minimum threshold
     */
    virtual bool isWaterLevelOk() const = 0;
    
    /**
     * @brief Check if it's safe to run any pump
     * Considers: water level, cooldown period, max runtime
     * @return true if pumps can safely operate
     */
    virtual bool isSafeToRun() const = 0;
    
    /**
     * @brief Check if main pump is in cooldown period
     * @return true if pump cannot be started yet
     */
    virtual bool isInCooldown() const = 0;
    
    // =========================================================================
    // Error Handling
    // =========================================================================
    
    /**
     * @brief Get the last error code (0 = no error)
     * @return Error code
     */
    virtual uint8_t getLastError() const = 0;
    
    /**
     * @brief Clear any error state
     */
    virtual void clearError() = 0;
    
    /**
     * @brief Emergency stop all pumps
     */
    virtual void emergencyStop() = 0;
};

// Error codes for reservoir pump controllers
namespace ReservoirPumpError {
    constexpr uint8_t NONE = 0;
    constexpr uint8_t LOW_WATER_LEVEL = 1;
    constexpr uint8_t MAX_RUNTIME_EXCEEDED = 2;
    constexpr uint8_t COOLDOWN_ACTIVE = 3;
    constexpr uint8_t HARDWARE_FAULT = 4;
    constexpr uint8_t EMERGENCY_STOP = 5;
}
