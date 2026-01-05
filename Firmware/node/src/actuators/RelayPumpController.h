// Relay-based pump controller implementation for tower nodes
// Uses a single GPIO to control an on/off relay
#pragma once

#include "IPumpController.h"

/**
 * @brief Relay-based pump controller for hydroponic tower nodes
 * 
 * Simple on/off pump control via GPIO relay. Includes:
 * - Auto-off after configurable duration
 * - Maximum runtime safety limit
 * - Cooldown period enforcement
 * - Non-blocking operation (uses millis-based timers)
 */
class RelayPumpController : public IPumpController {
public:
    /**
     * @brief Construct a relay pump controller
     * @param relayPin GPIO pin connected to relay
     * @param activeHigh true if relay is activated by HIGH signal (default)
     */
    explicit RelayPumpController(uint8_t relayPin, bool activeHigh = true)
        : _relayPin(relayPin)
        , _activeHigh(activeHigh)
        , _isOn(false)
        , _turnOnTime(0)
        , _autoOffDuration(0)
        , _turnOffTime(0)
        , _maxRuntime(300)      // Default 5 minutes max
        , _cooldownPeriod(30)   // Default 30 seconds cooldown
        , _lastError(PumpError::NONE)
    {}

    bool begin() override {
        pinMode(_relayPin, OUTPUT);
        digitalWrite(_relayPin, _activeHigh ? LOW : HIGH);  // Start with pump off
        _isOn = false;
        return true;
    }

    void turnOn(uint16_t durationSeconds = 0) override {
        // Check if in cooldown
        if (isInCooldown()) {
            _lastError = PumpError::COOLDOWN_ACTIVE;
            return;
        }
        
        _autoOffDuration = durationSeconds;
        _turnOnTime = millis();
        _isOn = true;
        _lastError = PumpError::NONE;
        
        digitalWrite(_relayPin, _activeHigh ? HIGH : LOW);
    }

    void turnOff() override {
        _isOn = false;
        _turnOffTime = millis();
        _autoOffDuration = 0;
        
        digitalWrite(_relayPin, _activeHigh ? LOW : HIGH);
    }

    bool isOn() const override {
        return _isOn;
    }

    uint32_t getRuntimeSeconds() const override {
        if (!_isOn) return 0;
        return (millis() - _turnOnTime) / 1000;
    }

    void loop() override {
        if (!_isOn) return;
        
        uint32_t runtimeSec = getRuntimeSeconds();
        
        // Check auto-off duration
        if (_autoOffDuration > 0 && runtimeSec >= _autoOffDuration) {
            turnOff();
            return;
        }
        
        // Check max runtime safety limit
        if (runtimeSec >= _maxRuntime) {
            _lastError = PumpError::MAX_RUNTIME_EXCEEDED;
            turnOff();
            return;
        }
    }

    uint8_t getLastError() const override {
        return _lastError;
    }

    void clearError() override {
        _lastError = PumpError::NONE;
    }

    void setMaxRuntime(uint16_t maxSeconds) override {
        _maxRuntime = maxSeconds;
    }

    void setCooldownPeriod(uint16_t cooldownSeconds) override {
        _cooldownPeriod = cooldownSeconds;
    }

    bool isInCooldown() const override {
        if (_turnOffTime == 0) return false;  // Never ran before
        if (_isOn) return false;  // Currently running, not in cooldown
        
        uint32_t timeSinceOff = (millis() - _turnOffTime) / 1000;
        return timeSinceOff < _cooldownPeriod;
    }

    // Additional helper methods
    uint32_t getCooldownRemaining() const {
        if (!isInCooldown()) return 0;
        uint32_t timeSinceOff = (millis() - _turnOffTime) / 1000;
        return _cooldownPeriod - timeSinceOff;
    }

private:
    uint8_t _relayPin;
    bool _activeHigh;
    bool _isOn;
    uint32_t _turnOnTime;
    uint16_t _autoOffDuration;
    uint32_t _turnOffTime;
    uint16_t _maxRuntime;
    uint16_t _cooldownPeriod;
    uint8_t _lastError;
};
