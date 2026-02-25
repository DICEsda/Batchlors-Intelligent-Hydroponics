// PWM-based grow light controller implementation for tower nodes
// Uses a single PWM GPIO to control LED brightness
#pragma once

#include "IGrowLightController.h"

/**
 * @brief PWM-based grow light controller for hydroponic tower nodes
 * 
 * Dimmable LED grow light control via PWM. Includes:
 * - Smooth brightness fading
 * - Auto-off after configurable duration
 * - Non-blocking operation (uses millis-based timers)
 */
class PWMGrowLightController : public IGrowLightController {
public:
    /**
     * @brief Construct a PWM grow light controller
     * @param pwmPin GPIO pin for PWM output
     * @param pwmChannel LEDC channel (0-15 on ESP32)
     * @param pwmFrequency PWM frequency in Hz (default 5000)
     * @param pwmResolution PWM resolution in bits (default 8 for 0-255 range)
     */
    explicit PWMGrowLightController(uint8_t pwmPin, uint8_t pwmChannel = 0,
                                     uint32_t pwmFrequency = 5000, uint8_t pwmResolution = 8)
        : _pwmPin(pwmPin)
        , _pwmChannel(pwmChannel)
        , _pwmFrequency(pwmFrequency)
        , _pwmResolution(pwmResolution)
        , _brightness(0)
        , _targetBrightness(0)
        , _isOn(false)
        , _turnOnTime(0)
        , _autoOffDuration(0)
        , _fadeStartTime(0)
        , _fadeDuration(0)
        , _fadeStartBrightness(0)
        , _lastError(GrowLightError::NONE)
    {}

    bool begin() override {
        // Configure LEDC for PWM
        ledcSetup(_pwmChannel, _pwmFrequency, _pwmResolution);
        ledcAttachPin(_pwmPin, _pwmChannel);
        ledcWrite(_pwmChannel, 0);  // Start with light off
        _brightness = 0;
        _isOn = false;
        return true;
    }

    void turnOn(uint8_t brightness = 255, uint16_t durationMinutes = 0) override {
        _autoOffDuration = durationMinutes;
        _turnOnTime = millis();
        _isOn = true;
        _lastError = GrowLightError::NONE;
        
        setBrightness(brightness, 0);  // Instant on
    }

    void turnOff() override {
        _isOn = false;
        _autoOffDuration = 0;
        setBrightness(0, 0);  // Instant off
    }

    bool isOn() const override {
        return _isOn && _brightness > 0;
    }

    uint8_t getBrightness() const override {
        return _brightness;
    }

    void setBrightness(uint8_t brightness, uint16_t fadeMs = 0) override {
        if (fadeMs == 0) {
            // Instant change
            _brightness = brightness;
            _targetBrightness = brightness;
            _fadeDuration = 0;
            ledcWrite(_pwmChannel, brightness);
        } else {
            // Start fade
            _fadeStartTime = millis();
            _fadeDuration = fadeMs;
            _fadeStartBrightness = _brightness;
            _targetBrightness = brightness;
        }
    }

    uint32_t getRuntimeSeconds() const override {
        if (!_isOn) return 0;
        return (millis() - _turnOnTime) / 1000;
    }

    void loop() override {
        // Handle fading
        if (_fadeDuration > 0) {
            uint32_t elapsed = millis() - _fadeStartTime;
            if (elapsed >= _fadeDuration) {
                // Fade complete
                _brightness = _targetBrightness;
                _fadeDuration = 0;
                ledcWrite(_pwmChannel, _brightness);
            } else {
                // Interpolate brightness
                int32_t delta = (int32_t)_targetBrightness - (int32_t)_fadeStartBrightness;
                int32_t current = _fadeStartBrightness + (delta * elapsed / _fadeDuration);
                _brightness = constrain(current, 0, 255);
                ledcWrite(_pwmChannel, _brightness);
            }
        }
        
        // Handle auto-off
        if (_isOn && _autoOffDuration > 0) {
            uint32_t runtimeMin = getRuntimeSeconds() / 60;
            if (runtimeMin >= _autoOffDuration) {
                turnOff();
            }
        }
    }

    uint8_t getLastError() const override {
        return _lastError;
    }

    void clearError() override {
        _lastError = GrowLightError::NONE;
    }

    // Check if currently fading
    bool isFading() const {
        return _fadeDuration > 0 && _brightness != _targetBrightness;
    }

private:
    uint8_t _pwmPin;
    uint8_t _pwmChannel;
    uint32_t _pwmFrequency;
    uint8_t _pwmResolution;
    
    uint8_t _brightness;
    uint8_t _targetBrightness;
    bool _isOn;
    uint32_t _turnOnTime;
    uint16_t _autoOffDuration;  // in minutes
    
    // Fading state
    uint32_t _fadeStartTime;
    uint16_t _fadeDuration;
    uint8_t _fadeStartBrightness;
    
    uint8_t _lastError;
};
