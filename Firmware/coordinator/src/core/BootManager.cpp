#include "BootManager.h"

void BootManager::clear() {
    entries_.clear();
}

void BootManager::record(const char* name, bool ok, const String& detail) {
    BootStatusEntry entry;
    entry.name = name ? name : "Subsystem";
    entry.ok = ok;
    entry.detail = detail;
    entries_.push_back(entry);
}

bool BootManager::allOk() const {
    for (const auto& entry : entries_) {
        if (!entry.ok) {
            return false;
        }
    }
    return true;
}

void BootManager::printSummary() const {
    if (entries_.empty()) {
        return;
    }
    Serial.println();
    Serial.println("\u250C\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u252C\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2510");
    Serial.println("\u2502 Subsystem  \u2502 Status                       \u2502");
    Serial.println("\u251C\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u253C\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2524");
    for (const auto& entry : entries_) {
        String detail = entry.detail;
        if (detail.isEmpty()) {
            detail = entry.ok ? "OK" : "See logs";
        }
        if (detail.length() > 28) {
            detail = detail.substring(0, 25) + "...";
        }
        String status = String(entry.ok ? "\u2713 " : "! ") + detail;
        Serial.printf("\u2502 %-10s \u2502 %-30s \u2502\n", entry.name.c_str(), status.c_str());
    }
    Serial.println("\u2514\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2534\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2518");
    Serial.println();
}
