#pragma once

#include <Arduino.h>

// ESP32-C6-WROOM-1 Pin Configuration
// Note: ESP32-C6 has different GPIO capabilities than ESP32-C3
namespace Pins {
    // LED Configuration - SK6812B RGBW strip
    // ESP32-C6 strapping pins: GPIO4, GPIO5, GPIO8, GPIO9
    // Safe GPIO pins for NeoPixel: GPIO0, GPIO1, GPIO2, GPIO3, GPIO6, GPIO7, GPIO10-GPIO15
    constexpr uint8_t LED_DATA_1 = 6;    // SK6812B data pin (safe GPIO)
    constexpr uint8_t LED_DATA_2 = 7;    // WS2812/SK6812 data pin strip 2 (optional)
    constexpr uint8_t LED_DATA_3 = 10;   // WS2812/SK6812 data pin strip 3 (optional)
    constexpr uint8_t STATUS_LED = 15;   // Built-in RGB LED or external status LED
    
    // Button Input
    constexpr uint8_t BUTTON = 0;        // GPIO0 for button input (safe, boot button)
    
    // Debug UART (Optional)
    // ESP32-C6 USB-Serial-JTAG uses GPIO12/GPIO13 by default
    constexpr uint8_t DEBUG_TX = 16;     // UART0 TX (alternative pins)
    constexpr uint8_t DEBUG_RX = 17;     // UART0 RX
    
    // I2C for TMP117 Temperature Sensor
    // ESP32-C6 I2C can be on any GPIO, these are suggested defaults
    constexpr uint8_t I2C_SDA = 1;       // I2C SDA for TMP117
    constexpr uint8_t I2C_SCL = 2;       // I2C SCL for TMP117
    
    // ESP32-C6 Built-in LED control (if using RGB LED module)
    struct RgbLed {
        static constexpr uint8_t PIN = 15;
        static constexpr uint8_t CHANNEL = 0;
        static constexpr uint8_t NUM_PIXELS = 1;
    };
}

// ESP32-C6 Specific Notes:
// - GPIO4, GPIO5: Strapping pins (SPI boot mode detection)
// - GPIO8: Strapping pin (chip boot mode)
// - GPIO9: Strapping pin (ROM message printing enable)
// - GPIO12/GPIO13: USB-Serial-JTAG (leave for debugging unless repurposed)
// - All other GPIOs (0-3, 6-7, 10-11, 14-23) are safe for general use
// - GPIO18-23: ADC2 channel (WiFi conflicts if using ADC)
// - Total usable GPIOs: 23 (GPIO0-GPIO23, excluding strapping pins during boot)
