// Abstract interface for grow light control
// Implementations: relay-based, PWM dimmable, addressable LED strips
#pragma once

#include <Arduino.h>

/**
 * @brief Abstract interface for grow light controllers in hydroponic tower nodes
 * 
 * This interface defines the contract for grow light control implementations.
 * Concrete implementations might include:
 * - RelayGrowLightController: Simple on/off relay control
 * - PWMGrowLightController: Dimmable via PWM (LED drivers)
 * - AddressableLightController: SK6812/WS2812 addressable strips
 */
class IGrowLightController {
public:
    virtual ~IGrowLightController() = default;

    /**
     * @brief Initialize the light controller hardware
     * @return true if initialization successful
     */
    virtual bool begin() = 0;

    /**
     * @brief Turn the grow light on at specified brightness
     * @param brightness Light intensity (0-255, where 255 is full brightness)
     * @param durationMinutes Auto-off after N minutes (0 = manual control)
     */
    virtual void turnOn(uint8_t brightness = 255, uint16_t durationMinutes = 0) = 0;

    /**
     * @brief Turn the grow light off immediately
     */
    virtual void turnOff() = 0;

    /**
     * @brief Check if grow light is currently on
     * @return true if light is on (any brightness > 0)
     */
    virtual bool isOn() const = 0;

    /**
     * @brief Get current brightness level
     * @return Brightness 0-255
     */
    virtual uint8_t getBrightness() const = 0;

    /**
     * @brief Set brightness without changing on/off state
     * @param brightness Light intensity (0-255)
     * @param fadeMs Fade duration in milliseconds (0 = instant)
     */
    virtual void setBrightness(uint8_t brightness, uint16_t fadeMs = 0) = 0;

    /**
     * @brief Get total runtime since last turnOn()
     * @return Runtime in seconds (0 if light is off)
     */
    virtual uint32_t getRuntimeSeconds() const = 0;

    /**
     * @brief Update light state (call in loop for auto-off and fading)
     * Should be called frequently to handle auto-off timers and smooth fading
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

    // Light spectrum control (for multi-channel grow lights)
    /**
     * @brief Check if controller supports spectrum adjustment
     * @return true if setSpectrum() is available
     */
    virtual bool supportsSpectrum() const { return false; }

    /**
     * @brief Set light spectrum mix (for multi-channel lights)
     * @param red Red channel intensity (0-255) - typically 630-660nm
     * @param blue Blue channel intensity (0-255) - typically 450-470nm
     * @param white White/full spectrum channel intensity (0-255)
     * @param uv UV channel intensity (0-255) - typically 380-400nm (if supported)
     * @param ir IR channel intensity (0-255) - typically 720-740nm (if supported)
     */
    virtual void setSpectrum(uint8_t red, uint8_t blue, uint8_t white, 
                             uint8_t uv = 0, uint8_t ir = 0) {
        // Default implementation: ignore spectrum, use setBrightness instead
        (void)red; (void)blue; (void)white; (void)uv; (void)ir;
    }

    // Schedule support
    /**
     * @brief Set daily light schedule
     * @param onHour Hour to turn on (0-23)
     * @param onMinute Minute to turn on (0-59)
     * @param durationHours How many hours to stay on
     * @param brightness Brightness during scheduled on period
     */
    virtual void setSchedule(uint8_t onHour, uint8_t onMinute, 
                             uint8_t durationHours, uint8_t brightness = 255) {
        // Default: no scheduling support
        (void)onHour; (void)onMinute; (void)durationHours; (void)brightness;
    }

    /**
     * @brief Enable or disable scheduled operation
     * @param enabled true to enable schedule
     */
    virtual void enableSchedule(bool enabled) { (void)enabled; }

    /**
     * @brief Check if schedule is enabled
     * @return true if scheduled operation is active
     */
    virtual bool isScheduleEnabled() const { return false; }
};

// Error codes for grow light controllers
namespace GrowLightError {
    constexpr uint8_t NONE = 0;
    constexpr uint8_t HARDWARE_FAULT = 1;
    constexpr uint8_t OVERTEMP = 2;
    constexpr uint8_t OVERCURRENT = 3;
    constexpr uint8_t DRIVER_FAULT = 4;
    constexpr uint8_t COMMUNICATION_ERROR = 5;
}
