/**
 * @file TowerCommandHandler.h
 * @brief Command handler for hydroponic tower nodes
 * 
 * Receives TowerCommandMessage from coordinator and dispatches to
 * appropriate actuators (pump, grow light). Handles command validation
 * and provides feedback.
 */
#pragma once

#include <Arduino.h>
#include "EspNowMessage.h"
#include "config/TowerConfig.h"
#include "actuators/IPumpController.h"
#include "actuators/IGrowLightController.h"

// Command types as string constants (matching TowerCommandMessage.command)
namespace TowerCommands {
    constexpr const char* SET_PUMP = "set_pump";
    constexpr const char* SET_LIGHT = "set_light";
    constexpr const char* REBOOT = "reboot";
    constexpr const char* OTA = "ota";
    constexpr const char* STATUS = "status";       // Request immediate status
    constexpr const char* CONFIGURE = "configure"; // Update configuration
}

// Command result codes
namespace CommandResult {
    constexpr uint8_t SUCCESS = 0;
    constexpr uint8_t INVALID_COMMAND = 1;
    constexpr uint8_t INVALID_TOWER_ID = 2;
    constexpr uint8_t ACTUATOR_NOT_AVAILABLE = 3;
    constexpr uint8_t ACTUATOR_ERROR = 4;
    constexpr uint8_t COMMAND_EXPIRED = 5;
    constexpr uint8_t COOLDOWN_ACTIVE = 6;
    constexpr uint8_t PARAMETER_ERROR = 7;
}

/**
 * @brief Callback type for command completion notification
 * @param cmdId Command ID that was processed
 * @param result Result code from CommandResult namespace
 * @param message Human-readable result message
 */
using CommandCallback = void (*)(const String& cmdId, uint8_t result, const String& message);

/**
 * @brief Tower command handler for processing coordinator commands
 * 
 * Receives commands via processCommand(), validates them, and dispatches
 * to the appropriate actuators. Supports:
 * - Pump control (on/off with optional duration)
 * - Grow light control (on/off/brightness with optional duration)
 * - System commands (reboot, OTA, status request)
 * 
 * Usage:
 *   TowerCommandHandler handler(config, pump, light);
 *   handler.begin();
 *   handler.setOnCommandComplete(myCallback);
 *   
 *   // When command received from ESP-NOW:
 *   handler.processCommand(cmdMsg);
 */
class TowerCommandHandler {
public:
    /**
     * @brief Construct command handler
     * @param config Tower configuration for ID validation
     * @param pump Pump controller (can be nullptr if pump not available)
     * @param light Grow light controller (can be nullptr if light not available)
     */
    TowerCommandHandler(
        TowerConfig& config,
        IPumpController* pump = nullptr,
        IGrowLightController* light = nullptr
    );

    ~TowerCommandHandler() = default;

    /**
     * @brief Initialize the command handler
     * @return true if successful
     */
    bool begin();

    /**
     * @brief Process a received command
     * @param cmd TowerCommandMessage received from coordinator
     * @return Result code from CommandResult namespace
     */
    uint8_t processCommand(const TowerCommandMessage& cmd);

    /**
     * @brief Process a command from raw JSON string
     * @param json JSON string containing TowerCommandMessage
     * @return Result code from CommandResult namespace
     */
    uint8_t processCommandJson(const String& json);

    /**
     * @brief Set callback for command completion notification
     * @param callback Function to call when command completes
     */
    void setOnCommandComplete(CommandCallback callback);

    /**
     * @brief Get the last command ID processed
     * @return Command ID string
     */
    String getLastCommandId() const { return _lastCmdId; }

    /**
     * @brief Get the last command result
     * @return Result code from CommandResult namespace
     */
    uint8_t getLastResult() const { return _lastResult; }

    /**
     * @brief Get count of commands processed since begin()
     * @return Command count
     */
    uint32_t getCommandCount() const { return _cmdCount; }

    /**
     * @brief Get count of failed commands since begin()
     * @return Failure count
     */
    uint32_t getFailureCount() const { return _failCount; }

    /**
     * @brief Check if a reboot has been requested
     * @return true if reboot is pending
     */
    bool isRebootPending() const { return _rebootPending; }

    /**
     * @brief Clear the reboot pending flag (after handling reboot)
     */
    void clearRebootPending() { _rebootPending = false; }

    /**
     * @brief Get OTA URL if OTA command was received
     * @return OTA URL or empty string
     */
    String getOtaUrl() const { return _otaUrl; }

    /**
     * @brief Get OTA checksum if OTA command was received
     * @return OTA checksum or empty string
     */
    String getOtaChecksum() const { return _otaChecksum; }

    /**
     * @brief Clear OTA request data
     */
    void clearOtaRequest() { _otaUrl = ""; _otaChecksum = ""; }

private:
    TowerConfig& _config;
    IPumpController* _pump;
    IGrowLightController* _light;
    CommandCallback _callback;

    String _lastCmdId;
    uint8_t _lastResult;
    uint32_t _cmdCount;
    uint32_t _failCount;

    bool _rebootPending;
    String _otaUrl;
    String _otaChecksum;

    // Command handlers
    uint8_t handleSetPump(const TowerCommandMessage& cmd);
    uint8_t handleSetLight(const TowerCommandMessage& cmd);
    uint8_t handleReboot(const TowerCommandMessage& cmd);
    uint8_t handleOta(const TowerCommandMessage& cmd);

    /**
     * @brief Validate command is for this tower and not expired
     * @param cmd Command to validate
     * @return SUCCESS if valid, error code otherwise
     */
    uint8_t validateCommand(const TowerCommandMessage& cmd);

    /**
     * @brief Notify callback of command completion
     * @param cmdId Command ID
     * @param result Result code
     * @param message Result message
     */
    void notifyComplete(const String& cmdId, uint8_t result, const String& message);

    /**
     * @brief Log message to serial
     * @param level Log level
     * @param message Message text
     */
    void log(const String& level, const String& message) const;
};

// ============================================================================
// INLINE IMPLEMENTATION
// ============================================================================

inline TowerCommandHandler::TowerCommandHandler(
    TowerConfig& config,
    IPumpController* pump,
    IGrowLightController* light
)
    : _config(config)
    , _pump(pump)
    , _light(light)
    , _callback(nullptr)
    , _lastCmdId("")
    , _lastResult(CommandResult::SUCCESS)
    , _cmdCount(0)
    , _failCount(0)
    , _rebootPending(false)
    , _otaUrl("")
    , _otaChecksum("")
{
}

inline bool TowerCommandHandler::begin() {
    _cmdCount = 0;
    _failCount = 0;
    _rebootPending = false;
    _otaUrl = "";
    _otaChecksum = "";
    log("INFO", "Command handler initialized");
    return true;
}

inline uint8_t TowerCommandHandler::processCommand(const TowerCommandMessage& cmd) {
    _lastCmdId = cmd.cmd_id;
    _cmdCount++;

    log("INFO", String("Processing command: ") + cmd.command + " (id: " + cmd.cmd_id + ")");

    // Validate command
    uint8_t validationResult = validateCommand(cmd);
    if (validationResult != CommandResult::SUCCESS) {
        _lastResult = validationResult;
        _failCount++;
        notifyComplete(cmd.cmd_id, validationResult, "Command validation failed");
        return validationResult;
    }

    // Dispatch to appropriate handler
    uint8_t result;
    if (cmd.command == TowerCommands::SET_PUMP) {
        result = handleSetPump(cmd);
    } else if (cmd.command == TowerCommands::SET_LIGHT) {
        result = handleSetLight(cmd);
    } else if (cmd.command == TowerCommands::REBOOT) {
        result = handleReboot(cmd);
    } else if (cmd.command == TowerCommands::OTA) {
        result = handleOta(cmd);
    } else {
        result = CommandResult::INVALID_COMMAND;
        log("WARN", String("Unknown command: ") + cmd.command);
    }

    _lastResult = result;
    if (result != CommandResult::SUCCESS) {
        _failCount++;
    }

    return result;
}

inline uint8_t TowerCommandHandler::processCommandJson(const String& json) {
    TowerCommandMessage cmd;
    if (!cmd.fromJson(json)) {
        log("ERROR", "Failed to parse command JSON");
        return CommandResult::INVALID_COMMAND;
    }
    return processCommand(cmd);
}

inline void TowerCommandHandler::setOnCommandComplete(CommandCallback callback) {
    _callback = callback;
}

inline uint8_t TowerCommandHandler::validateCommand(const TowerCommandMessage& cmd) {
    // Check tower ID matches
    String myTowerId = _config.getTowerId();
    if (!myTowerId.isEmpty() && cmd.tower_id != myTowerId) {
        log("WARN", String("Command for different tower: ") + cmd.tower_id);
        return CommandResult::INVALID_TOWER_ID;
    }

    // Check TTL (time-to-live)
    if (cmd.ttl_ms > 0) {
        // Note: This assumes cmd.ts is in the same time reference as millis()
        // In practice, coordinator would need to sync time or use relative TTL
        uint32_t now = millis();
        uint32_t cmdAge = now - cmd.ts;
        if (cmdAge > cmd.ttl_ms) {
            log("WARN", String("Command expired, age: ") + String(cmdAge) + "ms, TTL: " + String(cmd.ttl_ms) + "ms");
            return CommandResult::COMMAND_EXPIRED;
        }
    }

    return CommandResult::SUCCESS;
}

inline uint8_t TowerCommandHandler::handleSetPump(const TowerCommandMessage& cmd) {
    if (!_pump) {
        log("ERROR", "Pump controller not available");
        notifyComplete(cmd.cmd_id, CommandResult::ACTUATOR_NOT_AVAILABLE, "Pump not available");
        return CommandResult::ACTUATOR_NOT_AVAILABLE;
    }

    // Check cooldown
    if (cmd.pump_on && _pump->isInCooldown()) {
        log("WARN", "Pump in cooldown period");
        notifyComplete(cmd.cmd_id, CommandResult::COOLDOWN_ACTIVE, "Pump in cooldown");
        return CommandResult::COOLDOWN_ACTIVE;
    }

    if (cmd.pump_on) {
        // Enforce max duration from config if command duration is too long
        uint16_t maxDuration = _config.getPumpMaxDurationS();
        uint16_t duration = cmd.pump_duration_s;
        if (duration > maxDuration || duration == 0) {
            duration = maxDuration;
        }

        log("INFO", String("Turning pump ON for ") + String(duration) + "s");
        _pump->turnOn(duration);
    } else {
        log("INFO", "Turning pump OFF");
        _pump->turnOff();
    }

    // Check for errors
    uint8_t pumpError = _pump->getLastError();
    if (pumpError != PumpError::NONE) {
        log("ERROR", String("Pump error: ") + String(pumpError));
        notifyComplete(cmd.cmd_id, CommandResult::ACTUATOR_ERROR, String("Pump error: ") + String(pumpError));
        return CommandResult::ACTUATOR_ERROR;
    }

    notifyComplete(cmd.cmd_id, CommandResult::SUCCESS, cmd.pump_on ? "Pump started" : "Pump stopped");
    return CommandResult::SUCCESS;
}

inline uint8_t TowerCommandHandler::handleSetLight(const TowerCommandMessage& cmd) {
    if (!_light) {
        log("ERROR", "Light controller not available");
        notifyComplete(cmd.cmd_id, CommandResult::ACTUATOR_NOT_AVAILABLE, "Light not available");
        return CommandResult::ACTUATOR_NOT_AVAILABLE;
    }

    if (cmd.light_on) {
        log("INFO", String("Turning light ON, brightness: ") + String(cmd.light_brightness) + 
                    ", duration: " + String(cmd.light_duration_m) + "m");
        _light->turnOn(cmd.light_brightness, cmd.light_duration_m);
    } else {
        log("INFO", "Turning light OFF");
        _light->turnOff();
    }

    // Check for errors
    uint8_t lightError = _light->getLastError();
    if (lightError != GrowLightError::NONE) {
        log("ERROR", String("Light error: ") + String(lightError));
        notifyComplete(cmd.cmd_id, CommandResult::ACTUATOR_ERROR, String("Light error: ") + String(lightError));
        return CommandResult::ACTUATOR_ERROR;
    }

    notifyComplete(cmd.cmd_id, CommandResult::SUCCESS, cmd.light_on ? "Light started" : "Light stopped");
    return CommandResult::SUCCESS;
}

inline uint8_t TowerCommandHandler::handleReboot(const TowerCommandMessage& cmd) {
    log("INFO", "Reboot command received");
    _rebootPending = true;
    notifyComplete(cmd.cmd_id, CommandResult::SUCCESS, "Reboot scheduled");
    return CommandResult::SUCCESS;
}

inline uint8_t TowerCommandHandler::handleOta(const TowerCommandMessage& cmd) {
    if (cmd.ota_url.isEmpty()) {
        log("ERROR", "OTA command missing URL");
        notifyComplete(cmd.cmd_id, CommandResult::PARAMETER_ERROR, "Missing OTA URL");
        return CommandResult::PARAMETER_ERROR;
    }

    log("INFO", String("OTA command received, URL: ") + cmd.ota_url);
    _otaUrl = cmd.ota_url;
    _otaChecksum = cmd.ota_checksum;

    notifyComplete(cmd.cmd_id, CommandResult::SUCCESS, "OTA request accepted");
    return CommandResult::SUCCESS;
}

inline void TowerCommandHandler::notifyComplete(const String& cmdId, uint8_t result, const String& message) {
    if (_callback) {
        _callback(cmdId, result, message);
    }
}

inline void TowerCommandHandler::log(const String& level, const String& message) const {
    Serial.print("[CommandHandler][");
    Serial.print(level);
    Serial.print("] ");
    Serial.println(message);
}
