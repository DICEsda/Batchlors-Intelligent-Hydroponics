// Abstract interface for tower pump control
// Implementations: relay-based pump, peristaltic pump, etc.
#pragma once

#include <Arduino.h>

/**
 * @brief Abstract interface for pump controllers in hydroponic tower nodes
 * 
 * This interface defines the contract for pump control implementations.
 * Concrete implementations might include:
 * - RelayPumpController: Simple on/off relay control
 * - PWMPumpController: Variable speed via PWM (peristaltic pumps)
 * - DosingPumpController: Precise volume dispensing
 */
class IPumpController {
public:
    virtual ~IPumpController() = default;

    /**
     * @brief Initialize the pump controller hardware
     * @return true if initialization successful
     */
    virtual bool begin() = 0;

    /**
     * @brief Turn the pump on
     * @param durationSeconds Auto-off after N seconds (0 = manual control, no auto-off)
     */
    virtual void turnOn(uint16_t durationSeconds = 0) = 0;

    /**
     * @brief Turn the pump off immediately
     */
    virtual void turnOff() = 0;

    /**
     * @brief Check if pump is currently running
     * @return true if pump is on
     */
    virtual bool isOn() const = 0;

    /**
     * @brief Get total runtime since last turnOn()
     * @return Runtime in seconds (0 if pump is off)
     */
    virtual uint32_t getRuntimeSeconds() const = 0;

    /**
     * @brief Update pump state (call in loop for auto-off and safety checks)
     * Should be called frequently to handle auto-off timers
     */
    virtual void loop() = 0;

    /**
     * @brief Get the last error code (0 = no error)
     * @return Error code specific to implementation
     */
    virtual uint8_t getLastError() const = 0;

    /**
     * @brief Clear any error state and reset the controller
     */
    virtual void clearError() = 0;

    // Safety features
    /**
     * @brief Set maximum continuous run time (safety limit)
     * @param maxSeconds Maximum seconds before forced shutoff
     */
    virtual void setMaxRuntime(uint16_t maxSeconds) = 0;

    /**
     * @brief Set minimum cooldown period between pump cycles
     * @param cooldownSeconds Minimum seconds pump must be off before restarting
     */
    virtual void setCooldownPeriod(uint16_t cooldownSeconds) = 0;

    /**
     * @brief Check if pump is in cooldown period
     * @return true if pump cannot be started yet
     */
    virtual bool isInCooldown() const = 0;
};

// Error codes for pump controllers
namespace PumpError {
    constexpr uint8_t NONE = 0;
    constexpr uint8_t MAX_RUNTIME_EXCEEDED = 1;
    constexpr uint8_t COOLDOWN_ACTIVE = 2;
    constexpr uint8_t HARDWARE_FAULT = 3;
    constexpr uint8_t LOW_WATER = 4;
    constexpr uint8_t OVERTEMP = 5;
}
