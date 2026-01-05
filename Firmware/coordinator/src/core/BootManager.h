#pragma once

#include <Arduino.h>
#include <vector>

/**
 * @brief Boot status entry for tracking subsystem initialization results
 * 
 * Used by BootManager to record which subsystems initialized successfully
 * and display a summary table at boot completion.
 */
struct BootStatusEntry {
    String name;      ///< Subsystem name (e.g., "Wi-Fi", "MQTT", "ESP-NOW")
    bool ok;          ///< true if initialization succeeded
    String detail;    ///< Human-readable detail (e.g., "Connected 192.168.1.5")
};

/**
 * @brief Manages boot status tracking and summary display
 * 
 * Extracted from Coordinator to handle single responsibility:
 * - Recording initialization status of each subsystem
 * - Printing a formatted boot summary table to Serial
 * 
 * Usage:
 * @code
 *   BootManager boot;
 *   boot.record("ESP-NOW", true, "Radio ready");
 *   boot.record("Wi-Fi", false, "Connection failed");
 *   boot.printSummary();
 * @endcode
 */
class BootManager {
public:
    BootManager() = default;
    ~BootManager() = default;

    /**
     * @brief Clear all recorded boot status entries
     * 
     * Call this at the start of begin() to reset from any previous boot attempt.
     */
    void clear();

    /**
     * @brief Record the initialization status of a subsystem
     * 
     * @param name    Subsystem name (e.g., "ESP-NOW", "Wi-Fi", "MQTT")
     * @param ok      true if initialization succeeded, false otherwise
     * @param detail  Human-readable status detail
     */
    void record(const char* name, bool ok, const String& detail);

    /**
     * @brief Print a formatted summary table of all boot status entries
     * 
     * Outputs a ASCII-art table to Serial showing all recorded subsystems
     * with their status (checkmark or exclamation mark) and detail.
     * 
     * Example output:
     * @code
     * ┌────────────┬──────────────────────────────┐
     * │ Subsystem  │ Status                       │
     * ├────────────┼──────────────────────────────┤
     * │ ESP-NOW    │ ✓ Radio ready                │
     * │ Wi-Fi      │ ! Connection failed          │
     * └────────────┴──────────────────────────────┘
     * @endcode
     */
    void printSummary() const;

    /**
     * @brief Get the number of recorded boot status entries
     * @return Number of entries
     */
    size_t count() const { return entries_.size(); }

    /**
     * @brief Check if all recorded subsystems initialized successfully
     * @return true if all entries have ok=true, false otherwise
     */
    bool allOk() const;

    /**
     * @brief Get read-only access to all boot status entries
     * @return Const reference to the entries vector
     */
    const std::vector<BootStatusEntry>& entries() const { return entries_; }

private:
    std::vector<BootStatusEntry> entries_;
};
