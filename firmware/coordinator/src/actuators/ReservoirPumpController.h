/**
 * @file ReservoirPumpController.h
 * @brief Relay-based implementation of reservoir pump control
 * 
 * Controls pumps via GPIO-driven relays with safety features:
 * - Water level interlock (won't run if water low)
 * - Maximum runtime auto-shutoff
 * - Cooldown period between cycles
 * - Timed dosing pump operation
 */
#pragma once

#include "IReservoirPumpController.h"
#include <Arduino.h>

class ReservoirPumpController : public IReservoirPumpController {
public:
    /**
     * @brief Construct reservoir pump controller
     * @param mainPumpPin GPIO pin for main circulation pump relay
     * @param phDosingPin GPIO pin for pH dosing pump relay
     * @param nutrientDosingPin GPIO pin for nutrient dosing pump relay
     * @param relayActiveHigh true if relay activates on HIGH signal
     */
    ReservoirPumpController(uint8_t mainPumpPin, uint8_t phDosingPin, 
                            uint8_t nutrientDosingPin, bool relayActiveHigh = true);
    
    ~ReservoirPumpController() override = default;
    
    // IReservoirPumpController interface
    bool begin() override;
    void loop() override;
    
    // Main pump
    void setMainPump(bool on) override;
    bool isMainPumpOn() const override;
    uint32_t getMainPumpRuntimeSeconds() const override;
    
    // pH dosing pump
    void setPhDosingPump(bool on, uint32_t durationMs = 0) override;
    bool isPhDosingPumpOn() const override;
    
    // Nutrient dosing pump
    void setNutrientDosingPump(bool on, uint32_t durationMs = 0) override;
    bool isNutrientDosingPumpOn() const override;
    
    // Safety
    void setWaterLevelOk(bool ok) override;
    bool isWaterLevelOk() const override;
    bool isSafeToRun() const override;
    bool isInCooldown() const override;
    
    // Error handling
    uint8_t getLastError() const override;
    void clearError() override;
    void emergencyStop() override;
    
    // Configuration
    void setMaxRuntime(uint16_t maxSeconds);
    void setCooldownPeriod(uint16_t cooldownSeconds);
    void setDosingMaxDuration(uint32_t maxMs);
    
private:
    // Pin configuration
    uint8_t _mainPumpPin;
    uint8_t _phDosingPin;
    uint8_t _nutrientDosingPin;
    bool _relayActiveHigh;
    
    // Pump states
    bool _mainPumpOn;
    bool _phDosingOn;
    bool _nutrientDosingOn;
    
    // Timing for main pump
    uint32_t _mainPumpStartTime;
    uint32_t _mainPumpStopTime;
    uint16_t _maxRuntimeSeconds;
    uint16_t _cooldownSeconds;
    
    // Timing for dosing pumps
    uint32_t _phDosingStartTime;
    uint32_t _phDosingDurationMs;
    uint32_t _nutrientDosingStartTime;
    uint32_t _nutrientDosingDurationMs;
    uint32_t _dosingMaxDurationMs;
    
    // Safety
    bool _waterLevelOk;
    uint8_t _lastError;
    bool _emergencyStopped;
    
    // Helper methods
    void writeRelay(uint8_t pin, bool on);
    void checkAutoOff();
    void checkSafetyInterlocks();
    void logPumpAction(const char* pump, bool on, const char* reason = nullptr);
};
