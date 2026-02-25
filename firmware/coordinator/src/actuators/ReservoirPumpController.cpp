/**
 * @file ReservoirPumpController.cpp
 * @brief Implementation of relay-based reservoir pump control
 */

#include "ReservoirPumpController.h"
#include "ConfigManager.h"

ReservoirPumpController::ReservoirPumpController(uint8_t mainPumpPin, uint8_t phDosingPin,
                                                 uint8_t nutrientDosingPin, bool relayActiveHigh)
    : _mainPumpPin(mainPumpPin)
    , _phDosingPin(phDosingPin)
    , _nutrientDosingPin(nutrientDosingPin)
    , _relayActiveHigh(relayActiveHigh)
    , _mainPumpOn(false)
    , _phDosingOn(false)
    , _nutrientDosingOn(false)
    , _mainPumpStartTime(0)
    , _mainPumpStopTime(0)
    , _maxRuntimeSeconds(Defaults::PUMP_MAX_DURATION_S)
    , _cooldownSeconds(Defaults::PUMP_COOLDOWN_S)
    , _phDosingStartTime(0)
    , _phDosingDurationMs(0)
    , _nutrientDosingStartTime(0)
    , _nutrientDosingDurationMs(0)
    , _dosingMaxDurationMs(30000)  // 30 second max for dosing pumps
    , _waterLevelOk(true)
    , _lastError(ReservoirPumpError::NONE)
    , _emergencyStopped(false)
{
}

bool ReservoirPumpController::begin() {
    // Configure GPIO pins as outputs
    pinMode(_mainPumpPin, OUTPUT);
    pinMode(_phDosingPin, OUTPUT);
    pinMode(_nutrientDosingPin, OUTPUT);
    
    // Initialize all relays to OFF
    writeRelay(_mainPumpPin, false);
    writeRelay(_phDosingPin, false);
    writeRelay(_nutrientDosingPin, false);
    
    Serial.printf("[PUMP] Reservoir pump controller initialized (pins: main=%d, pH=%d, nutrient=%d)\n",
                  _mainPumpPin, _phDosingPin, _nutrientDosingPin);
    
    return true;
}

void ReservoirPumpController::loop() {
    checkSafetyInterlocks();
    checkAutoOff();
}

// =============================================================================
// Main Circulation Pump
// =============================================================================

void ReservoirPumpController::setMainPump(bool on) {
    if (_emergencyStopped) {
        logPumpAction("main", false, "emergency stop active");
        return;
    }
    
    if (on) {
        // Check safety conditions before turning on
        if (!_waterLevelOk) {
            _lastError = ReservoirPumpError::LOW_WATER_LEVEL;
            logPumpAction("main", false, "low water level");
            return;
        }
        
        if (isInCooldown()) {
            _lastError = ReservoirPumpError::COOLDOWN_ACTIVE;
            logPumpAction("main", false, "cooldown active");
            return;
        }
        
        _mainPumpOn = true;
        _mainPumpStartTime = millis();
        writeRelay(_mainPumpPin, true);
        logPumpAction("main", true);
    } else {
        _mainPumpOn = false;
        _mainPumpStopTime = millis();
        writeRelay(_mainPumpPin, false);
        logPumpAction("main", false);
    }
}

bool ReservoirPumpController::isMainPumpOn() const {
    return _mainPumpOn;
}

uint32_t ReservoirPumpController::getMainPumpRuntimeSeconds() const {
    if (!_mainPumpOn) return 0;
    return (millis() - _mainPumpStartTime) / 1000;
}

// =============================================================================
// pH Dosing Pump
// =============================================================================

void ReservoirPumpController::setPhDosingPump(bool on, uint32_t durationMs) {
    if (_emergencyStopped) {
        logPumpAction("pH dosing", false, "emergency stop active");
        return;
    }
    
    if (on) {
        if (!_waterLevelOk) {
            _lastError = ReservoirPumpError::LOW_WATER_LEVEL;
            logPumpAction("pH dosing", false, "low water level");
            return;
        }
        
        _phDosingOn = true;
        _phDosingStartTime = millis();
        _phDosingDurationMs = (durationMs > 0) ? min(durationMs, _dosingMaxDurationMs) : 0;
        writeRelay(_phDosingPin, true);
        logPumpAction("pH dosing", true);
        
        if (_phDosingDurationMs > 0) {
            Serial.printf("[PUMP] pH dosing auto-off in %lu ms\n", _phDosingDurationMs);
        }
    } else {
        _phDosingOn = false;
        _phDosingDurationMs = 0;
        writeRelay(_phDosingPin, false);
        logPumpAction("pH dosing", false);
    }
}

bool ReservoirPumpController::isPhDosingPumpOn() const {
    return _phDosingOn;
}

// =============================================================================
// Nutrient Dosing Pump
// =============================================================================

void ReservoirPumpController::setNutrientDosingPump(bool on, uint32_t durationMs) {
    if (_emergencyStopped) {
        logPumpAction("nutrient dosing", false, "emergency stop active");
        return;
    }
    
    if (on) {
        if (!_waterLevelOk) {
            _lastError = ReservoirPumpError::LOW_WATER_LEVEL;
            logPumpAction("nutrient dosing", false, "low water level");
            return;
        }
        
        _nutrientDosingOn = true;
        _nutrientDosingStartTime = millis();
        _nutrientDosingDurationMs = (durationMs > 0) ? min(durationMs, _dosingMaxDurationMs) : 0;
        writeRelay(_nutrientDosingPin, true);
        logPumpAction("nutrient dosing", true);
        
        if (_nutrientDosingDurationMs > 0) {
            Serial.printf("[PUMP] Nutrient dosing auto-off in %lu ms\n", _nutrientDosingDurationMs);
        }
    } else {
        _nutrientDosingOn = false;
        _nutrientDosingDurationMs = 0;
        writeRelay(_nutrientDosingPin, false);
        logPumpAction("nutrient dosing", false);
    }
}

bool ReservoirPumpController::isNutrientDosingPumpOn() const {
    return _nutrientDosingOn;
}

// =============================================================================
// Safety
// =============================================================================

void ReservoirPumpController::setWaterLevelOk(bool ok) {
    bool wasOk = _waterLevelOk;
    _waterLevelOk = ok;
    
    if (wasOk && !ok) {
        Serial.println("[PUMP] WARNING: Water level dropped below minimum!");
        // Safety shutoff if water drops while pumps are running
        checkSafetyInterlocks();
    }
}

bool ReservoirPumpController::isWaterLevelOk() const {
    return _waterLevelOk;
}

bool ReservoirPumpController::isSafeToRun() const {
    return _waterLevelOk && !_emergencyStopped && !isInCooldown();
}

bool ReservoirPumpController::isInCooldown() const {
    if (_mainPumpStopTime == 0) return false;
    
    uint32_t elapsed = millis() - _mainPumpStopTime;
    return elapsed < (_cooldownSeconds * 1000UL);
}

// =============================================================================
// Error Handling
// =============================================================================

uint8_t ReservoirPumpController::getLastError() const {
    return _lastError;
}

void ReservoirPumpController::clearError() {
    _lastError = ReservoirPumpError::NONE;
    _emergencyStopped = false;
}

void ReservoirPumpController::emergencyStop() {
    Serial.println("[PUMP] EMERGENCY STOP - All pumps off!");
    
    _emergencyStopped = true;
    _lastError = ReservoirPumpError::EMERGENCY_STOP;
    
    // Force all pumps off
    _mainPumpOn = false;
    _phDosingOn = false;
    _nutrientDosingOn = false;
    
    writeRelay(_mainPumpPin, false);
    writeRelay(_phDosingPin, false);
    writeRelay(_nutrientDosingPin, false);
    
    _mainPumpStopTime = millis();
}

// =============================================================================
// Configuration
// =============================================================================

void ReservoirPumpController::setMaxRuntime(uint16_t maxSeconds) {
    _maxRuntimeSeconds = maxSeconds;
    Serial.printf("[PUMP] Max runtime set to %u seconds\n", maxSeconds);
}

void ReservoirPumpController::setCooldownPeriod(uint16_t cooldownSeconds) {
    _cooldownSeconds = cooldownSeconds;
    Serial.printf("[PUMP] Cooldown period set to %u seconds\n", cooldownSeconds);
}

void ReservoirPumpController::setDosingMaxDuration(uint32_t maxMs) {
    _dosingMaxDurationMs = maxMs;
    Serial.printf("[PUMP] Dosing max duration set to %lu ms\n", maxMs);
}

// =============================================================================
// Private Helpers
// =============================================================================

void ReservoirPumpController::writeRelay(uint8_t pin, bool on) {
    // Handle active-high vs active-low relay modules
    bool level = _relayActiveHigh ? on : !on;
    digitalWrite(pin, level ? HIGH : LOW);
}

void ReservoirPumpController::checkAutoOff() {
    uint32_t now = millis();
    
    // Check main pump max runtime
    if (_mainPumpOn) {
        uint32_t runtime = (now - _mainPumpStartTime) / 1000;
        if (runtime >= _maxRuntimeSeconds) {
            _lastError = ReservoirPumpError::MAX_RUNTIME_EXCEEDED;
            setMainPump(false);
            Serial.printf("[PUMP] Main pump auto-off: max runtime %u seconds exceeded\n", _maxRuntimeSeconds);
        }
    }
    
    // Check pH dosing auto-off
    if (_phDosingOn && _phDosingDurationMs > 0) {
        if ((now - _phDosingStartTime) >= _phDosingDurationMs) {
            setPhDosingPump(false);
            Serial.println("[PUMP] pH dosing auto-off: duration complete");
        }
    }
    
    // Check nutrient dosing auto-off
    if (_nutrientDosingOn && _nutrientDosingDurationMs > 0) {
        if ((now - _nutrientDosingStartTime) >= _nutrientDosingDurationMs) {
            setNutrientDosingPump(false);
            Serial.println("[PUMP] Nutrient dosing auto-off: duration complete");
        }
    }
}

void ReservoirPumpController::checkSafetyInterlocks() {
    // If water level drops, shut off all pumps
    if (!_waterLevelOk) {
        if (_mainPumpOn) {
            _mainPumpOn = false;
            _mainPumpStopTime = millis();
            writeRelay(_mainPumpPin, false);
            _lastError = ReservoirPumpError::LOW_WATER_LEVEL;
            Serial.println("[PUMP] Main pump forced off: low water level");
        }
        
        if (_phDosingOn) {
            _phDosingOn = false;
            writeRelay(_phDosingPin, false);
            Serial.println("[PUMP] pH dosing forced off: low water level");
        }
        
        if (_nutrientDosingOn) {
            _nutrientDosingOn = false;
            writeRelay(_nutrientDosingPin, false);
            Serial.println("[PUMP] Nutrient dosing forced off: low water level");
        }
    }
}

void ReservoirPumpController::logPumpAction(const char* pump, bool on, const char* reason) {
    if (reason) {
        Serial.printf("[PUMP] %s pump %s (%s)\n", pump, on ? "ON" : "OFF", reason);
    } else {
        Serial.printf("[PUMP] %s pump %s\n", pump, on ? "ON" : "OFF");
    }
}
