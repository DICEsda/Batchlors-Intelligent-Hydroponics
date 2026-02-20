# ESP32-C6-WROOM-1 Setup Guide

This guide explains how to upload firmware to the ESP32-C6-WROOM-1 module.

## Hardware Differences: ESP32-C6 vs ESP32-C3

| Feature | ESP32-C3 | ESP32-C6 |
|---------|----------|----------|
| **CPU** | RISC-V single-core 160MHz | RISC-V single-core 160MHz |
| **WiFi** | WiFi 4 (802.11n) | WiFi 6 (802.11ax) |
| **Bluetooth** | BLE 5.0 | BLE 5.3 + 802.15.4 (Zigbee/Thread) |
| **GPIO Count** | 22 | 30 |
| **RAM** | 400KB | 512KB |
| **Flash** | External (varies) | External (varies) |
| **USB** | USB Serial/JTAG | USB Serial/JTAG |
| **Strapping Pins** | GPIO2, GPIO8, GPIO9 | GPIO4, GPIO5, GPIO8, GPIO9 |

## Pin Configuration

The firmware uses different pin mappings for ESP32-C6. See `src/config/PinConfig_C6.h` for the complete configuration.

### Default ESP32-C6 Pin Assignments:

```cpp
LED_DATA_1:   GPIO6  (SK6812B RGBW LED strip)
LED_DATA_2:   GPIO7  (Optional second strip)
LED_DATA_3:   GPIO10 (Optional third strip)
STATUS_LED:   GPIO15 (Built-in RGB LED or status indicator)

BUTTON:       GPIO0  (Pairing/mode button, also BOOT button)

I2C_SDA:      GPIO1  (TMP117 temperature sensor)
I2C_SCL:      GPIO2  (TMP117 temperature sensor)

DEBUG_TX:     GPIO16 (Alternative UART TX)
DEBUG_RX:     GPIO17 (Alternative UART RX)
```

### ⚠️ **Important: Strapping Pins on ESP32-C6**

Avoid using these pins for external peripherals during boot:
- **GPIO4**: SPI boot mode detection
- **GPIO5**: SPI boot mode detection
- **GPIO8**: Chip boot mode selection
- **GPIO9**: ROM message printing enable

These pins have pull-up/pull-down resistors that determine boot mode. You can use them after boot, but they should not have conflicting external pull resistors.

## PlatformIO Configuration

The `platformio.ini` file includes a dedicated environment for ESP32-C6:

```ini
[env:esp32-c6-wroom-1]
platform = espressif32 @ ^6.5.0
board = esp32-c6-devkitc-1
framework = arduino
board_build.mcu = esp32c6
board_build.variant = esp32c6
```

## How to Upload Firmware

### Method 1: Using PlatformIO (Recommended)

1. **Connect your ESP32-C6 to your computer via USB**

2. **Identify the COM port:**
   - Windows: Check Device Manager → Ports (COM & LPT)
   - Linux: Check `ls /dev/ttyUSB*` or `ls /dev/ttyACM*`
   - macOS: Check `ls /dev/cu.usbserial-*` or `ls /dev/cu.usbmodem-*`

3. **Update the COM port in `platformio.ini`:**
   ```ini
   upload_port = COM6  ; Change to your port
   monitor_port = COM6
   ```

4. **Set ESP32-C6 as the default environment:**
   ```ini
   [platformio]
   default_envs = esp32-c6-wroom-1
   ```

5. **Build and upload:**
   ```bash
   # In VS Code with PlatformIO extension:
   # Click "PlatformIO: Upload" button

   # Or from command line:
   pio run -e esp32-c6-wroom-1 -t upload
   ```

6. **Monitor serial output:**
   ```bash
   # In VS Code: Click "PlatformIO: Serial Monitor"
   
   # Or from command line:
   pio device monitor -e esp32-c6-wroom-1
   ```

### Method 2: Manual Upload with esptool

If PlatformIO doesn't auto-detect the board:

1. **Build the firmware:**
   ```bash
   pio run -e esp32-c6-wroom-1
   ```

2. **Put ESP32-C6 into download mode:**
   - Hold the **BOOT** button (GPIO0)
   - Press and release the **RESET** button
   - Release the **BOOT** button

3. **Upload using esptool:**
   ```bash
   esptool.py --chip esp32c6 --port COM6 --baud 921600 \
     --before default_reset --after hard_reset write_flash \
     -z 0x0 .pio/build/esp32-c6-wroom-1/firmware.bin
   ```

## Troubleshooting

### Issue: Board not detected

**Solution:**
- Install CP210x or CH340 USB-to-serial drivers
- Try a different USB cable (must support data, not just charging)
- Check Device Manager for unknown devices

### Issue: Upload fails with "Timed out waiting for packet header"

**Solution:**
- Manually enter download mode (hold BOOT, tap RESET)
- Reduce baud rate: `upload_speed = 460800` in `platformio.ini`
- Check USB cable quality

### Issue: ESP32-C6 reboots continuously

**Solution:**
- Check power supply (USB should provide at least 500mA)
- Verify pin connections (no shorts to GND/VCC)
- Check strapping pin states during boot

### Issue: WiFi/ESP-NOW not working

**Solution:**
- Verify antenna is connected (if using external antenna)
- Check that WiFi region is configured correctly
- ESP-NOW requires WiFi to be initialized (even without connecting to AP)

### Issue: "Arduino.h not found" or LSP errors

**Solution:**
These are normal IDE errors before building. They resolve after:
```bash
pio run -e esp32-c6-wroom-1
```

## Verifying Upload Success

After uploading, you should see:

```
Connecting........_____
Chip is ESP32-C6 (revision v0.0)
...
Hard resetting via RTS pin...
```

Open serial monitor (115200 baud) to see boot messages:

```
ESP32-S3 SMART TILE NODE
===========================================

*** BOOT START ***
Initializing Logger...
✓ NVS initialized successfully
*** SETUP START ***
...
*** SETUP COMPLETE - System Ready ***
```

## ESP32-C6 Specific Features

### WiFi 6 Support
The ESP32-C6 supports 802.11ax (WiFi 6), but the current firmware uses ESP-NOW which works on all WiFi standards.

### 802.15.4 Radio
The ESP32-C6 has a dedicated 802.15.4 radio for Zigbee/Thread. This is NOT used in the current firmware (only ESP-NOW via WiFi radio).

### USB Serial/JTAG
The ESP32-C6 has built-in USB support (no external USB-to-serial chip needed):
- **GPIO12/GPIO13**: USB D-/D+ (hardware USB, not UART-over-USB)
- Appears as native USB CDC device on PC

## Pin Change Summary from ESP32-C3

If you're migrating from ESP32-C3, these pins changed:

| Function | ESP32-C3 | ESP32-C6 |
|----------|----------|----------|
| LED_DATA_1 | GPIO4 | GPIO6 |
| LED_DATA_2 | GPIO5 | GPIO7 |
| LED_DATA_3 | GPIO6 | GPIO10 |
| STATUS_LED | GPIO8 | GPIO15 |
| BUTTON | GPIO3 | GPIO0 |

**Reason:** ESP32-C6 has different strapping pins, so we avoid GPIO4, GPIO5, GPIO8, GPIO9 during boot.

## Additional Resources

- [ESP32-C6 Datasheet](https://www.espressif.com/sites/default/files/documentation/esp32-c6_datasheet_en.pdf)
- [ESP32-C6 Technical Reference Manual](https://www.espressif.com/sites/default/files/documentation/esp32-c6_technical_reference_manual_en.pdf)
- [Arduino-ESP32 Documentation](https://docs.espressif.com/projects/arduino-esp32/en/latest/)
- [PlatformIO ESP32 Platform](https://docs.platformio.org/en/latest/platforms/espressif32.html)

## Next Steps

1. **Test LED strip:** Upload `esp32-c6-wroom-1-standalone` environment to test SK6812B LEDs
2. **Test button:** Press GPIO0 button to cycle LED modes
3. **Test I2C sensor:** Check serial output for TMP117 temperature readings
4. **Test pairing:** Long-press button to enter pairing mode (blue blinking LED)

## Support

If you encounter issues specific to ESP32-C6, check:
- PlatformIO platform version: `espressif32 @ ^6.5.0` or newer
- Arduino-ESP32 core: `3.0.0` or newer (supports ESP32-C6)
- Bootloader version: May need updating for very early ESP32-C6 chips
