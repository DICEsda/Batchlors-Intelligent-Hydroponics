#pragma once

#include <Arduino.h>
#include <functional>

/**
 * @brief Serial command handler for coordinator debug/config menu
 * 
 * Extracted from Coordinator to handle single responsibility:
 * - Reading and buffering serial input
 * - Parsing and dispatching commands
 * - Displaying help menu
 * 
 * Uses callbacks to invoke coordinator actions without tight coupling.
 * 
 * Usage:
 * @code
 *   SerialConsole console;
 *   console.onWifiConfig([]() { wifi->reconfigureWifi(); });
 *   console.onMqttConfig([]() { mqtt->runProvisioningWizard(); });
 *   console.onStatus([]() { printStatus(); });
 *   console.onPair([]() { startPairing(); });
 *   console.onReboot([]() { ESP.restart(); });
 *   
 *   // In loop:
 *   console.process();
 * @endcode
 */
class SerialConsole {
public:
    using Callback = std::function<void()>;

    SerialConsole() = default;
    ~SerialConsole() = default;

    /**
     * @brief Process any available serial input
     * 
     * Call this from loop() to handle incoming commands.
     * Reads characters, buffers until newline, then dispatches.
     */
    void process();

    /**
     * @brief Print the help menu to Serial
     */
    void printHelp() const;

    // Callback setters for each command
    void onWifiConfig(Callback cb) { wifiConfigCb_ = cb; }
    void onMqttConfig(Callback cb) { mqttConfigCb_ = cb; }
    void onStatus(Callback cb) { statusCb_ = cb; }
    void onPair(Callback cb) { pairCb_ = cb; }
    void onReboot(Callback cb) { rebootCb_ = cb; }

    /**
     * @brief Set maximum command buffer size (default 64)
     */
    void setMaxCommandLength(size_t len) { maxCmdLen_ = len; }

private:
    void executeCommand(const String& cmd);

    String cmdBuffer_;
    size_t maxCmdLen_ = 64;

    Callback wifiConfigCb_;
    Callback mqttConfigCb_;
    Callback statusCb_;
    Callback pairCb_;
    Callback rebootCb_;
};
