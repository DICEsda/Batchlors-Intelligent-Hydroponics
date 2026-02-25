# Configuration System Refactor - Sprint 1 Status

## Completed ✅

### 1. Created Unified Configuration Architecture
- **File:** `firmware/shared/src/Config.h`
  - Defined `WiFiConfig`, `MqttConfig`, `EspNowConfig`, `SystemConfig` structs
  - Unified `Config` struct with all settings
  - JSON serialization/deserialization built-in
  - Validation methods for each section
  - Version field for future migrations

### 2. Created ConfigStore Class
- **Files:** `firmware/shared/src/ConfigStore.h` and `ConfigStore.cpp`
  - Single namespace: `coordinator_v1` (versioned)
  - Load/save/reset operations
  - JSON export/import for backup
  - Auto-migration from legacy ConfigManager
  - Caching for performance

## Next Steps 🚧

### Phase 1 Remaining Tasks:

1. **Update main.cpp to initialize ConfigStore** 
   - Add `ConfigStore::initialize()` after NVS init
   - Remove old ConfigManager usage

2. **Update Coordinator.cpp initialization order**
   - WiFi → ESP-NOW → wait for reconnect → MQTT
   - Use ConfigStore::load() instead of scattered configs

3. **Update WifiManager.cpp**
   - Use ConfigStore for WiFi credentials
   - Auto-discover and lock WiFi channel
   - Update ESP-NOW channel in config

4. **Update EspNow.cpp**
   - Read channel from ConfigStore
   - Lock to WiFi channel (no hardcoded channel 1)

5. **Update Mqtt.cpp**
   - Use default credentials `user1/user1`
   - Read all settings from ConfigStore
   - Remove old ConfigManager("mqtt") usage

6. **Test end-to-end boot sequence**

## Key Design Decisions 📋

1. **Config Migration:** Auto-migrate from legacy (Option A)
2. **Channel Lock:** Always use router's channel, discover on connect (Option A)
3. **MQTT Passwords:** Plain text in NVS for v1 (Option A)
4. **Frontend Config:** Read-only + factory reset initially (Option A)
5. **Serial vs MQTT Priority:** Not critical for now (frontend primary after initial config)

## Implementation Notes 💡

### Default MQTT Credentials
```cpp
username: "user1"  // Matches docker-compose mosquitto.conf
password: "user1"
```

### Channel Locking Strategy
1. WiFi connects to AP (any channel)
2. Discover actual WiFi channel
3. Save channel to config: `config.wifi.channel = WiFi.channel()`
4. ESP-NOW uses same channel: `config.espnow.channel = config.wifi.channel`
5. Both locked together in Config validation

### Initialization Order
```
main.cpp:
  1. Serial.begin()
  2. Logger::begin()
  3. nvs_flash_init()
  4. ConfigStore::initialize()  ← NEW
  5. coordinator.begin()

coordinator.begin():
  1. WiFi connect (discovers channel)
  2. ESP-NOW init (uses WiFi channel)
  3. Wait 3-5s for WiFi reconnect
  4. MQTT connect (stable WiFi)
```

## Files Modified 📝

### New Files:
- `firmware/shared/src/Config.h` - Unified config structs
- `firmware/shared/src/ConfigStore.h` - Storage manager header
- `firmware/shared/src/ConfigStore.cpp` - Storage manager implementation

### Files To Modify:
- `firmware/coordinator/src/main.cpp` - Add ConfigStore init
- `firmware/coordinator/src/core/Coordinator.cpp` - New init order
- `firmware/coordinator/src/comm/WifiManager.cpp` - Use ConfigStore, discover channel
- `firmware/coordinator/src/comm/EspNow.cpp` - Use stored channel
- `firmware/coordinator/src/comm/Mqtt.cpp` - Use ConfigStore, default credentials

### Files To Keep (Deprecated):
- `firmware/shared/src/ConfigManager.h/cpp` - Keep for 2 releases for rollback

## Testing Checklist ✓

After implementation:
- [ ] Flash coordinator with new firmware
- [ ] Verify NVS initializes without errors
- [ ] Verify WiFi connects and STAYS connected
- [ ] Verify ESP-NOW initializes without WiFi disconnect
- [ ] Verify MQTT connects with user1/user1
- [ ] Verify all channels match (WiFi == ESP-NOW)
- [ ] Power cycle - verify config persists
- [ ] Factory reset via serial - verify reset works
- [ ] Re-provision - verify settings persist

## Current Errors to Fix 🐛

From logs:
```
[E][Preferences.cpp:96] remove(): nvs_erase_key fail: coord_id NOT_FOUND
[E][Preferences.cpp:50] begin(): nvs_open failed: NOT_FOUND
ERROR | MQTT connection failed, rc=5 (unauthorized)
[W][WiFiGeneric.cpp:1062] _eventCallback(): Reason: 8 - ASSOC_LEAVE
```

**Root Causes:**
1. Multiple config namespaces not coordinated
2. WiFi disconnects when ESP-NOW changes channel
3. MQTT credentials mismatch or missing
4. NVS namespaces not pre-created

**Solutions (ConfigStore):**
1. ✅ Single namespace `coordinator_v1`
2. ✅ Auto-migration from legacy namespaces
3. ✅ Channel locking (WiFi == ESP-NOW)
4. ✅ Default credentials `user1/user1`

## Build Status 🔨

- Config.h: ✅ Compiles
- ConfigStore.h: ✅ Compiles
- ConfigStore.cpp: ✅ Compiles
- Integration: ⏳ Pending

Next: Continue with Step 4 (Update main.cpp)
