#include "SerialConsole.h"

void SerialConsole::process() {
    while (Serial.available()) {
        char c = Serial.read();
        
        if (c == '\n' || c == '\r') {
            if (cmdBuffer_.length() > 0) {
                cmdBuffer_.trim();
                cmdBuffer_.toLowerCase();
                executeCommand(cmdBuffer_);
                cmdBuffer_ = "";
            }
        } else {
            cmdBuffer_ += c;
            // Limit buffer size to prevent overflow
            if (cmdBuffer_.length() > maxCmdLen_) {
                cmdBuffer_ = "";
                Serial.println("✗ Command too long, cleared");
            }
        }
    }
}

void SerialConsole::printHelp() const {
    Serial.println();
    Serial.println("═══════════════════════════════════════");
    Serial.println("  COORDINATOR SERIAL MENU");
    Serial.println("═══════════════════════════════════════");
    Serial.println("  help, ?       - Show this menu");
    Serial.println("  wifi          - Reconfigure Wi-Fi");
    Serial.println("  mqtt          - Reconfigure MQTT");
    Serial.println("  status        - Show system status");
    Serial.println("  pair          - Start pairing mode (60s)");
    Serial.println("  reboot        - Restart coordinator");
    Serial.println("═══════════════════════════════════════");
    Serial.println();
}

void SerialConsole::executeCommand(const String& cmd) {
    if (cmd == "help" || cmd == "?") {
        printHelp();
        
    } else if (cmd == "wifi") {
        if (wifiConfigCb_) {
            Serial.println();
            wifiConfigCb_();
        } else {
            Serial.println("✗ WiFi manager not available");
        }
        
    } else if (cmd == "mqtt") {
        if (mqttConfigCb_) {
            Serial.println();
            mqttConfigCb_();
        } else {
            Serial.println("✗ MQTT not available");
        }
        
    } else if (cmd == "status") {
        if (statusCb_) {
            Serial.println();
            statusCb_();
        } else {
            Serial.println("✗ Status not available");
        }
        
    } else if (cmd == "pair") {
        if (pairCb_) {
            Serial.println();
            pairCb_();
            Serial.println("✓ Pairing mode activated for 60 seconds");
        } else {
            Serial.println("✗ Pairing not available");
        }
        
    } else if (cmd == "reboot") {
        Serial.println();
        Serial.println("Rebooting coordinator...");
        delay(500);
        if (rebootCb_) {
            rebootCb_();
        } else {
            ESP.restart();
        }
        
    } else if (cmd.length() > 0) {
        Serial.printf("✗ Unknown command: '%s' (type 'help' for menu)\n", cmd.c_str());
    }
}
