# Quick Start: Upload to ESP32-C6-WROOM-1

## ⚡ Fast Upload Steps

1. **Connect ESP32-C6 via USB**

2. **Check COM port:**
   - Windows: Device Manager → Ports
   - Linux: `ls /dev/ttyUSB*`
   - macOS: `ls /dev/cu.*`

3. **Edit `platformio.ini`:**
   ```ini
   [platformio]
   default_envs = esp32-c6-wroom-1
   
   [env:esp32-c6-wroom-1]
   ...
   upload_port = COM6  # ← Change this to your port
   monitor_port = COM6
   ```

4. **Upload:**
   ```bash
   # VS Code with PlatformIO: Click "Upload" button
   
   # Or command line:
   pio run -e esp32-c6-wroom-1 -t upload
   ```

5. **Monitor:**
   ```bash
   pio device monitor -e esp32-c6-wroom-1
   ```

## 🔧 If Upload Fails

### Method 1: Auto-download mode (should work automatically)
PlatformIO handles this for you.

### Method 2: Manual download mode
1. Hold **BOOT** button (GPIO0)
2. Press **RESET** button briefly
3. Release **BOOT** button
4. Run upload command

### Method 3: Reduce baud rate
Edit `platformio.ini`:
```ini
upload_speed = 460800  # Instead of 921600
```

## 📍 Pin Changes from ESP32-C3

| Function | ESP32-C3 | ESP32-C6 |
|----------|----------|----------|
| LED Strip | GPIO4 | GPIO6 |
| Button | GPIO3 | GPIO0 |
| Status LED | GPIO8 | GPIO15 |
| I2C SDA | GPIO1 | GPIO1 ✓ (same) |
| I2C SCL | GPIO2 | GPIO2 ✓ (same) |

**Wire your hardware to GPIO6** for LED strip on ESP32-C6!

## ✅ Success Indicators

Serial output at 115200 baud should show:
```
ESP32-S3 SMART TILE NODE  # (will say S3 but runs on C6)
===========================================
*** BOOT START ***
✓ NVS initialized successfully
*** SETUP START ***
...
*** SETUP COMPLETE - System Ready ***
```

## 🚨 Common Issues

| Issue | Solution |
|-------|----------|
| "Timed out waiting for packet header" | Manual boot mode (hold BOOT, tap RESET) |
| Board not detected | Check USB cable (needs data pins), install drivers |
| LEDs don't work | Check GPIO6 connection, verify 5V power for LEDs |
| Upload works but no serial output | Check baud rate is 115200 |

## 📚 Full Documentation

See `README_ESP32_C6.md` for complete details on:
- Hardware differences C3 vs C6
- Strapping pins to avoid
- Pin configuration details
- Troubleshooting guide
- ESP32-C6 specific features

## 🎯 Next Steps

1. **Test standalone mode:** Upload and test LEDs
   ```bash
   pio run -e esp32-c6-wroom-1-standalone -t upload
   ```

2. **Test I2C sensor:** Check serial output for TMP117 readings

3. **Test pairing:** Long-press button to enter pairing mode (blue blink)

---

**Need help?** Check the full README or open an issue on GitHub.
