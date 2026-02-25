# Layer Integration Rapport

## 1. System Layers

| Layer | Technology | Location | Port |
|---|---|---|---|
| Node Firmware | ESP32-C3, Arduino, PlatformIO | `Firmware/node/` | ESP-NOW |
| Coordinator Firmware | ESP32-S3, Arduino, PlatformIO | `Firmware/coordinator/` | ESP-NOW + MQTT |
| MQTT Broker | Eclipse Mosquitto 2.0 | Docker container | 1883 (TCP), 9001 (WS) |
| Backend API | ASP.NET Core 8, C# | `Backend/src/IoT.Backend/` | 8000 |
| Frontend | Angular 19, TypeScript | `Frontend/` | 4200 |
| ML Service | Python, FastAPI | `ML/` | 8001 |
| Database | MongoDB 7.0 | Docker container | 27017 |

## 2. Data Flow Summary

| # | From | To | Protocol | Transport |
|---|---|---|---|---|
| 1 | Node | Coordinator | ESP-NOW (binary + JSON) | 2.4 GHz radio |
| 2 | Coordinator | Node | ESP-NOW (binary + JSON) | 2.4 GHz radio |
| 3 | Coordinator | MQTT Broker | MQTT v3.1.1 (QoS 0) | WiFi → TCP |
| 4 | MQTT Broker | Backend | MQTT v3.1.1 (subscriptions) | TCP |
| 5 | Backend | MQTT Broker | MQTT v3.1.1 (publications) | TCP |
| 6 | Backend | MongoDB | MongoDB Wire Protocol | TCP |
| 7 | Frontend | Backend | HTTP REST + WebSocket | TCP |
| 8 | Backend | ML Service | HTTP REST proxy | TCP |
| 9 | Backend | Frontend | WebSocket broadcast | TCP |

## 3. ESP-NOW Message Types

| Enum Value | JSON `msg` String | Binary Marker | Direction | Protocol | Source |
|---|---|---|---|---|---|
| `JOIN_REQUEST` | `join_request` | — | Node → Coord | V1 JSON | `EspNowMessage.h:9` |
| `JOIN_ACCEPT` | `join_accept` | — | Coord → Node | V1 JSON | `EspNowMessage.h:10` |
| `SET_LIGHT` | `set_light` | — | Coord → Node | V1 JSON | `EspNowMessage.h:11` |
| `NODE_STATUS` | `node_status` | — | Node → Coord | V1 JSON | `EspNowMessage.h:12` |
| `ERROR` | `error` | — | Either | V1 JSON | `EspNowMessage.h:13` |
| `ACK` | `ack` | — | Node → Coord | V1 JSON | `EspNowMessage.h:14` |
| `TOWER_JOIN_REQUEST` | `tower_join_request` | — | Node → Coord | V1 JSON | `EspNowMessage.h:16` |
| `TOWER_JOIN_ACCEPT` | `tower_join_accept` | — | Coord → Node | V1 JSON | `EspNowMessage.h:17` |
| `TOWER_TELEMETRY` | `tower_telemetry` | — | Node → Coord | V1 JSON | `EspNowMessage.h:18` |
| `TOWER_COMMAND` | `tower_command` | — | Coord → Node | V1 JSON | `EspNowMessage.h:19` |
| `RESERVOIR_TELEMETRY` | `reservoir_telemetry` | — | Internal only | V1 JSON | `EspNowMessage.h:20` |
| `PAIRING_ADVERTISEMENT` | `pairing_advertisement` | `0x20` | Node → Broadcast | V2 Binary | `EspNowMessage.h:23` |
| `PAIRING_OFFER` | `pairing_offer` | `0x21` | Coord → Node | V2 Binary | `EspNowMessage.h:24` |
| `PAIRING_ACCEPT` | `pairing_accept` | `0x22` | Node → Coord | V2 Binary | `EspNowMessage.h:25` |
| `PAIRING_CONFIRM` | `pairing_confirm` | `0x23` | Coord → Node | V2 Binary | `EspNowMessage.h:26` |
| `PAIRING_REJECT` | `pairing_reject` | `0x24` | Coord → Node | V2 Binary | `EspNowMessage.h:27` |
| `PAIRING_ABORT` | `pairing_abort` | `0x25` | Node → Coord | V2 Binary | `EspNowMessage.h:28` |
| `OTA_BEGIN` | `ota_begin` | `0x30` | Coord → Node | V2 Binary | `EspNowMessage.h:32` |
| `OTA_CHUNK` | `ota_chunk` | `0x31` | Coord → Node | V2 Binary | `EspNowMessage.h:33` |
| `OTA_CHUNK_ACK` | `ota_chunk_ack` | `0x32` | Node → Coord | V2 Binary | `EspNowMessage.h:34` |
| `OTA_ABORT` | `ota_abort` | `0x33` | Either | V2 Binary | `EspNowMessage.h:35` |
| `OTA_COMPLETE` | `ota_complete` | `0x34` | Node → Coord | V2 Binary | `EspNowMessage.h:36` |

## 4. ESP-NOW Message Payloads

### 4a. V1 JSON Messages (Legacy Smart Tile)

| Message | Direction | Field | Type | Notes |
|---|---|---|---|---|
| **JoinRequest** | Node → Coord | `mac` | String | Station MAC |
| | | `fw` | String | Firmware version |
| | | `caps.rgbw` | bool | SK6812B support |
| | | `caps.led_count` | uint8 | Pixels per node (default 4) |
| | | `caps.temp_i2c` | bool | TMP177 sensor |
| | | `caps.deep_sleep` | bool | Deep sleep capable |
| | | `caps.button` | bool | Button input |
| | | `token` | String | Rotating token |
| **JoinAccept** | Coord → Node | `node_id` | String | Assigned ID |
| | | `light_id` | String | Light group ID |
| | | `lmk` | String | Link master key |
| | | `wifi_channel` | uint8 | WiFi channel |
| | | `cfg.pwm_freq` | int | PWM frequency |
| | | `cfg.rx_window_ms` | int | RX window |
| | | `cfg.rx_period_ms` | int | RX period |
| **SetLight** | Coord → Node | `light_id` | String | Target light |
| | | `r, g, b, w` | uint8 | RGBW (0-255) |
| | | `value` | uint8 | Fallback brightness |
| | | `fade_ms` | uint16 | Fade duration |
| | | `override_status` | bool | Override mode |
| | | `ttl_ms` | uint16 | Time-to-live (default 1500) |
| | | `reason` | String | Command reason |
| | | `pixel` | int8 | -1=all, 0-3=specific |
| **NodeStatus** | Node → Coord | `node_id` | String | |
| | | `light_id` | String | |
| | | `avg_r, avg_g, avg_b, avg_w` | uint8 | Current RGBW |
| | | `status_mode` | String | "operational"/"pairing"/"ota"/"error" |
| | | `vbat_mv` | uint16 | Battery mV |
| | | `temperature` | float | Celsius (TMP177) |
| | | `button_pressed` | bool | |
| | | `fw` | String | Firmware version |

### 4b. V1 JSON Messages (Hydroponic Tower)

| Message | Direction | Field | Type | Notes |
|---|---|---|---|---|
| **TowerJoinRequest** | Node → Coord | `mac` | String | Station MAC |
| | | `fw` | String | Firmware version |
| | | `caps.dht_sensor` | bool | DHT22 present |
| | | `caps.light_sensor` | bool | |
| | | `caps.pump_relay` | bool | |
| | | `caps.grow_light` | bool | |
| | | `caps.slot_count` | uint8 | Default 6 |
| | | `token` | String | Rotating token |
| **TowerJoinAccept** | Coord → Node | `tower_id` | String | Assigned ID |
| | | `coord_id` | String | Coordinator ID |
| | | `farm_id` | String | Farm ID |
| | | `lmk` | String | Link master key |
| | | `wifi_channel` | uint8 | |
| | | `cfg.telemetry_interval_ms` | uint16 | Default 30000 |
| | | `cfg.pump_max_duration_s` | uint16 | Default 300 |
| **TowerTelemetry** | Node → Coord | `tower_id` | String | |
| | | `air_temp_c` | float | DHT22 |
| | | `humidity_pct` | float | DHT22 |
| | | `light_lux` | float | Optional sensor |
| | | `pump_on` | bool | |
| | | `light_on` | bool | |
| | | `light_brightness` | uint8 | 0-255 |
| | | `status_mode` | String | "operational"/"pairing"/"ota"/"error"/"idle" |
| | | `vbat_mv` | uint16 | |
| | | `fw` | String | |
| | | `uptime_s` | uint32 | |
| **TowerCommand** | Coord → Node | `tower_id` | String | |
| | | `command` | String | "set_pump"/"set_light"/"reboot"/"ota" |
| | | `pump_on` | bool | |
| | | `pump_duration_s` | uint16 | |
| | | `light_on` | bool | |
| | | `light_brightness` | uint8 | |
| | | `light_duration_m` | uint16 | |
| | | `ota_url` | String | |
| | | `ota_checksum` | String | |
| | | `ttl_ms` | uint16 | Default 5000 |
| **ReservoirTelemetry** | Coord internal | `coord_id` | String | |
| | | `farm_id` | String | |
| | | `ph` | float | 0-14 |
| | | `ec_ms_cm` | float | mS/cm |
| | | `tds_ppm` | float | Calculated |
| | | `water_temp_c` | float | |
| | | `water_level_pct` | float | 0-100 |
| | | `water_level_cm` | float | |
| | | `low_water_alert` | bool | |
| | | `main_pump_on` | bool | |
| | | `dosing_pump_ph_on` | bool | |
| | | `dosing_pump_nutrient_on` | bool | |
| | | `status_mode` | String | "operational"/"maintenance"/"error" |
| | | `uptime_s` | uint32 | |

### 4c. V2 Binary Messages (Pairing)

| Message | Direction | Size | Field | Type | Offset |
|---|---|---|---|---|---|
| **PairingAdvertisement** | Node → Broadcast | 22B | `protocol_version` | uint8 | 0 |
| | | | `node_mac` | uint8[6] | 1 |
| | | | `device_type` | uint8 (enum) | 7 |
| | | | `firmware_version` | uint32 | 8 |
| | | | `capabilities` | uint16 (flags) | 12 |
| | | | `nonce` | uint32 | 14 |
| | | | `sequence_num` | uint16 | 18 |
| | | | `rssi_request` | int8 | 20 |
| **PairingOffer** | Coord → Node | 23B | `protocol_version` | uint8 | 0 |
| | | | `coord_mac` | uint8[6] | 1 |
| | | | `coord_id` | uint16 | 7 |
| | | | `farm_id` | uint16 | 9 |
| | | | `offered_tower_id` | uint16 | 11 |
| | | | `nonce_echo` | uint32 | 13 |
| | | | `offer_token` | uint32 | 17 |
| | | | `channel` | uint8 | 21 |
| **PairingAccept** | Node → Coord | 13B | `node_mac` | uint8[6] | 0 |
| | | | `offer_token` | uint32 | 6 |
| | | | `accepted_tower_id` | uint16 | 10 |
| **PairingConfirm** | Coord → Node | 26B | `coord_mac` | uint8[6] | 0 |
| | | | `tower_id` | uint16 | 6 |
| | | | `encryption_key` | uint8[16] | 8 |
| | | | `config_flags` | uint8 | 24 |
| **PairingReject** | Coord → Node | 12B | `sender_mac` | uint8[6] | 0 |
| | | | `reason_code` | uint8 (enum) | 6 |
| | | | `offer_token` | uint32 | 7 |
| **PairingAbort** | Node → Coord | 12B | `sender_mac` | uint8[6] | 0 |
| | | | `reason_code` | uint8 (enum) | 6 |
| | | | `offer_token` | uint32 | 7 |

### 4d. V2 Binary Messages (OTA)

| Message | Direction | Size | Field | Type |
|---|---|---|---|---|
| **OtaBegin** | Coord → Node | 45B | `firmware_size` | uint32 |
| | | | `chunk_count` | uint16 |
| | | | `chunk_size` | uint16 |
| | | | `checksum_type` | uint8 (0=none,1=MD5,2=SHA256) |
| | | | `checksum` | uint8[32] |
| | | | `firmware_version` | uint32 |
| **OtaChunk** | Coord → Node | 4B+data | `chunk_index` | uint16 |
| | | | `data_len` | uint8 (1-200) |
| | | | `data` | uint8[200] |
| **OtaChunkAck** | Node → Coord | 5B | `chunk_index` | uint16 |
| | | | `status` | uint8 (0=OK,1=CRC,2=write,3=retry) |
| | | | `next_expected` | uint8 |
| **OtaAbort** | Either | 4B | `reason` | uint8 (enum 0-9) |
| | | | `last_chunk` | uint16 |
| **OtaComplete** | Node → Coord | 3B | `status` | uint8 (0=ok,1=checksum,2=flash) |
| | | | `will_reboot` | uint8 |

## 5. ESP-NOW Enums

| Enum | Value | Meaning | Source |
|---|---|---|---|
| **DeviceType** | 0 | UNKNOWN | `EspNowMessage.h:40` |
| | 1 | TOWER | |
| | 2 | SENSOR | |
| | 3 | LIGHT_NODE | |
| | 4 | COORDINATOR | |
| **CapabilityFlags** | 0x0001 | DHT_SENSOR | `EspNowMessage.h:49` |
| | 0x0002 | LIGHT_SENSOR | |
| | 0x0004 | PUMP_RELAY | |
| | 0x0008 | GROW_LIGHT | |
| | 0x0010 | RGBW_LED | |
| | 0x0020 | DEEP_SLEEP | |
| | 0x0040 | BUTTON | |
| | 0x0080 | TEMP_I2C | |
| | 0x0100 | PRESENCE_SENSOR | |
| | 0x0200 | BATTERY | |
| **PairingRejectReason** | 0 | NONE | `EspNowMessage.h:64` |
| | 1 | PERMIT_JOIN_DISABLED | |
| | 2 | CAPACITY_FULL | |
| | 3 | DUPLICATE_MAC | |
| | 4 | TIMEOUT | |
| | 5 | USER_REJECTED | |
| | 6 | PROTOCOL_MISMATCH | |
| | 7 | INTERNAL_ERROR | |
| | 8 | NODE_CANCELLED | |
| | 9 | INVALID_TOKEN | |
| | 10 | ALREADY_PAIRED | |
| **OtaAbortReason** | 0 | NONE | `EspNowMessage.h:452` |
| | 1 | USER_CANCELLED | |
| | 2 | TIMEOUT | |
| | 3 | CHECKSUM_MISMATCH | |
| | 4 | FLASH_WRITE_ERROR | |
| | 5 | OUT_OF_MEMORY | |
| | 6 | INVALID_FIRMWARE | |
| | 7 | CHUNK_SEQUENCE_ERROR | |
| | 8 | COMMUNICATION_ERROR | |
| | 9 | INTERNAL_ERROR | |

## 6. MQTT Topics — Firmware Publishes

| # | Topic Pattern | QoS | Retained | JSON Fields | Source |
|---|---|---|---|---|---|
| 1 | `farm/{farmId}/node/{lightId}/telemetry` | 0 | No | `ts, light_id, brightness` | `Mqtt.cpp:180` |
| 2 | `farm/{farmId}/node/{nodeId}/telemetry` | 0 | No | `ts, node_id, temp_c, is_derated, deration_level` | `Mqtt.cpp:196` |
| 3 | `farm/{farmId}/coord/{coordId}/mmwave` | 0 | No | `ts, farm_id, coord_id, sensor_id, presence, confidence, events[], targets[]` | `Mqtt.cpp:216` |
| 4 | `farm/{farmId}/node/{nodeId}/telemetry` | 0 | No | `ts, node_id, light_id, avg_r/g/b/w, status_mode, temp_c, button_pressed, vbat_mv, fw` | `Mqtt.cpp:267` |
| 5 | `farm/{farmId}/coord/{coordId}/telemetry` | 0 | No | `ts, farm_id, coord_id, light_lux, temp_c, mmwave_presence, mmwave_confidence, mmwave_online, wifi_rssi, wifi_connected` | `Mqtt.cpp:770` |
| 6 | `farm/{farmId}/coord/{coordId}/serial` | 0 | No | `ts, message, level, tag` | `Mqtt.cpp:789` |
| 7 | `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry` | 0 | No | `ts, farm_id, coord_id, tower_id, air_temp_c, humidity_pct, light_lux, pump_on, light_on, light_brightness, status_mode, vbat_mv, fw, uptime_s` | `Mqtt.cpp:807` |
| 8 | `farm/{farmId}/coord/{coordId}/reservoir/telemetry` | 0 | No | `ts, farm_id, coord_id, ph, ec_ms_cm, tds_ppm, water_temp_c, water_level_pct, water_level_cm, low_water_alert, main_pump_on, dosing_pump_ph_on, dosing_pump_nutrient_on, status_mode, uptime_s` | `Mqtt.cpp:851` |
| 9 | `farm/{farmId}/coord/{coordId}/ota/status` | 0 | No | `status, progress, message, error, timestamp` | `Mqtt.cpp:952` |
| 10 | `farm/{farmId}/coord/{coordId}/status/connection` | 0 | **Yes** | `ts, coord_id, farm_id, event, wifi_connected, wifi_rssi, mqtt_connected, uptime_ms, free_heap, reason` | `Mqtt.cpp:993` |
| 11 | `coordinator/{coordId}/announce` | 0 | No | `mac, fw_version, chip_model, free_heap, wifi_rssi, ip, farm_id` | `Mqtt.cpp:1035` |
| 12 | `farm/{farmId}/coord/{coordId}/pairing/request` | 0 | No | `ts, farm_id, coord_id, tower_id, mac_address, rssi, fw_version` | `Mqtt.cpp:1208` |
| 13 | `farm/{farmId}/coord/{coordId}/pairing/status` | 0 | No | `ts, farm_id, coord_id, status, duration_ms, nodes_discovered, nodes_paired` | `Mqtt.cpp:1241` |
| 14 | `farm/{farmId}/coord/{coordId}/pairing/complete` | 0 | No | `ts, farm_id, coord_id, tower_id, mac_address, success, reason` | `Mqtt.cpp:1274` |

## 7. MQTT Topics — Firmware Subscribes

| # | Topic Pattern | Handler | Action | Source |
|---|---|---|---|---|
| 1 | `coordinator/{coordId}/registered` | `handleRegistrationMessage()` | Parse `farm_id`, save to NVS, re-subscribe farm-scoped topics | `Mqtt.cpp:363` |
| 2 | `farm/{farmId}/coord/{coordId}/cmd` | `commandCallback` | Forward to coordinator command handler | `Mqtt.cpp:368` |
| 3 | `farm/{farmId}/coord/{coordId}/tower/+/cmd` | `commandCallback` | Relay command to tower node via ESP-NOW | `Mqtt.cpp:373` |
| 4 | `farm/{farmId}/node/+/cmd` | `commandCallback` | Legacy: relay to smart tile node via ESP-NOW | `Mqtt.cpp:378` |
| 5 | `coordinator/{coordId}/config` | `commandCallback` | Apply pushed config from backend | `Mqtt.cpp:383` |
| 6 | `coordinator/{coordId}/cmd` | `commandCallback` | Direct commands (restart, etc.) | `Mqtt.cpp:388` |
| 7 | `farm/{farmId}/coord/{coordId}/reservoir/cmd` | `commandCallback` | Reservoir pump/dosing control | `Mqtt.cpp:393` |
| 8 | `farm/{farmId}/coord/{coordId}/ota/start` | `commandCallback` | Start OTA update | `Mqtt.cpp:398` |
| 9 | `farm/{farmId}/coord/{coordId}/ota/cancel` | `commandCallback` | Cancel OTA update | `Mqtt.cpp:401` |

## 8. MQTT Topics — Backend Subscribes

| # | Topic Pattern | Handler Method | Action | Source |
|---|---|---|---|---|
| 1 | `farm/+/coord/+/telemetry` | `HandleFarmCoordinatorTelemetry` | Parse, upsert coordinator, broadcast WS | `TelemetryHandler.cs:76` |
| 2 | `farm/+/coord/+/reservoir/telemetry` | `HandleReservoirTelemetry` | Parse, store in MongoDB, update coordinator twin | `TelemetryHandler.cs:79` |
| 3 | `farm/+/coord/+/tower/+/telemetry` | `HandleTowerTelemetry` | Parse, store in MongoDB, broadcast WS, update tower twin | `TelemetryHandler.cs:82` |
| 4 | `farm/+/coord/+/tower/+/status` | `HandleTowerStatus` | Update tower twin, sync desired state ack | `TelemetryHandler.cs:85` |
| 5 | `farm/+/coord/+/ota/status` | `HandleFarmOtaStatus` | Update OTA job status, broadcast WS | `TelemetryHandler.cs:88` |
| 6 | `farm/+/coord/+/pairing/request` | `HandlePairingRequest` | Store pairing request via PairingService, broadcast WS | `TelemetryHandler.cs:95` |
| 7 | `farm/+/coord/+/pairing/status` | `HandlePairingStatus` | Update pairing session via PairingService, broadcast WS | `TelemetryHandler.cs:98` |
| 8 | `farm/+/coord/+/pairing/complete` | `HandlePairingComplete` | Finalize pairing via PairingService, broadcast WS | `TelemetryHandler.cs:101` |
| 9 | `farm/+/coord/+/serial` | `HandleSerialLog` | Broadcast WS as coordinator_log | `TelemetryHandler.cs:108` |
| 10 | `farm/+/coord/+/status/connection` | `HandleConnectionStatus` | Update coordinator status, broadcast WS | `TelemetryHandler.cs:115` |
| 11 | `coordinator/+/announce` | `HandleCoordinatorAnnounce` | Register pending coordinator via RegistrationService, broadcast WS | `TelemetryHandler.cs:122` |

## 9. MQTT Topics — Backend Publishes

| # | Topic Pattern | Trigger | Payload | Source |
|---|---|---|---|---|
| 1 | `farm/{farmId}/coord/{coordId}/reservoir/cmd` | REST: pump/dosing control | `{cmd, ...params}` | Backend controllers |
| 2 | `farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd` | REST: light/pump control | `{cmd, tower_id, ...params}` | Backend controllers |
| 3 | `farm/{farmId}/coord/{coordId}/cmd` | REST: pairing start/stop/approve/reject/forget | `{cmd, ...params}` | Backend controllers |
| 4 | `farm/{farmId}/coord/{coordId}/ota/start` | REST: start OTA | OTA job payload | Backend controllers |
| 5 | `farm/{farmId}/coord/{coordId}/ota/cancel` | REST: cancel OTA | Cancel payload | Backend controllers |
| 6 | `coordinator/{coordId}/config` | REST: push config | Config object | Backend controllers |
| 7 | `coordinator/{coordId}/cmd` | REST: restart | `{cmd: "restart"}` | Backend controllers |
| 8 | `coordinator/{coordId}/registered` | REST: approve registration | `{farm_id}` | Backend controllers |

## 10. MQTT Topic Match Matrix — Firmware Pub vs Backend Sub

| Firmware Publishes | Backend Subscription | Match? |
|---|---|---|
| `farm/{farmId}/coord/{coordId}/telemetry` | `farm/+/coord/+/telemetry` | Yes |
| `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry` | `farm/+/coord/+/tower/+/telemetry` | Yes |
| `farm/{farmId}/coord/{coordId}/reservoir/telemetry` | `farm/+/coord/+/reservoir/telemetry` | Yes |
| `farm/{farmId}/coord/{coordId}/serial` | `farm/+/coord/+/serial` | Yes |
| `farm/{farmId}/coord/{coordId}/status/connection` | `farm/+/coord/+/status/connection` | Yes |
| `farm/{farmId}/coord/{coordId}/ota/status` | `farm/+/coord/+/ota/status` | Yes |
| `farm/{farmId}/coord/{coordId}/mmwave` | — | **No subscriber** (mmWave data not persisted) |
| `farm/{farmId}/node/{nodeId}/telemetry` | — | **No subscriber** (legacy smart tile node telemetry) |
| `coordinator/{coordId}/announce` | `coordinator/+/announce` | Yes |

## 11. MQTT Topic Match Matrix — Backend Pub vs Firmware Sub

| Backend Publishes | Firmware Subscription | Match? |
|---|---|---|
| `coordinator/{coordId}/registered` | `coordinator/{coordId}/registered` | Yes |
| `coordinator/{coordId}/config` | `coordinator/{coordId}/config` | Yes |
| `coordinator/{coordId}/cmd` | `coordinator/{coordId}/cmd` | Yes |
| `farm/{farmId}/coord/{coordId}/cmd` | `farm/{farmId}/coord/{coordId}/cmd` | Yes |
| `farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd` | `farm/{farmId}/coord/{coordId}/tower/+/cmd` | Yes |
| `farm/{farmId}/coord/{coordId}/ota/start` | `farm/{farmId}/coord/{coordId}/ota/start` | Yes |
| `farm/{farmId}/coord/{coordId}/ota/cancel` | `farm/{farmId}/coord/{coordId}/ota/cancel` | Yes |
| `farm/{farmId}/coord/{coordId}/reservoir/cmd` | `farm/{farmId}/coord/{coordId}/reservoir/cmd` | Yes |

## 12. Backend REST Endpoints

### 12a. Health & System

| # | Method | Route | Request | Response | Source |
|---|---|---|---|---|---|
| 1 | GET | `/health` | — | `HealthStatus` | `HealthController` |
| 2 | GET | `/health/live` | — | Status | `HealthController` |
| 3 | GET | `/health/ready` | — | Status | `HealthController` |
| 4 | GET | `/api/diagnostics` | — | `SystemMetricsSnapshot` | `DiagnosticsController` |
| 5 | GET | `/api/diagnostics/history` | `?minutes=` | `SystemMetricsSnapshot[]` | `DiagnosticsController` |
| 6 | POST | `/api/diagnostics/reset` | `{}` | `{message}` | `DiagnosticsController` |

### 12b. Farms

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 7 | GET | `/api/farms` | — | `Farm[]` |
| 8 | POST | `/api/farms` | `Farm` | `Farm` |
| 9 | PUT | `/api/farms/{farmId}` | `Farm` | `Farm` |
| 10 | DELETE | `/api/farms/{farmId}` | — | 204 |

### 12c. Coordinators

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 11 | GET | `/api/coordinators` | — | `CoordinatorSummary[]` |
| 12 | GET | `/api/coordinators/{coordId}` | — | `Coordinator` |
| 13 | PUT | `/api/coordinators/{coordId}` | metadata | `Coordinator` |
| 14 | PUT | `/api/coordinators/{coordId}/config` | config object | Updated config |
| 15 | PATCH | `/api/coordinators/{coordId}` | `{name?, description?, location?, tags?, color?}` | `Coordinator` |
| 16 | POST | `/api/coordinators/restart` | `{coord_id}` | 200 |
| 17 | POST | `/api/coordinators/wifi` | `{coord_id, ssid, password}` | 200 |

### 12d. Coordinator Registration

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 18 | GET | `/api/coordinators/pending` | — | Pending registrations |
| 19 | POST | `/api/coordinators/register/approve` | `{coordId, farmId, name, description?, color?, tags?, location?}` | Approved coordinator |
| 20 | POST | `/api/coordinators/register/reject` | `{coordId}` | 200 |
| 21 | DELETE | `/api/coordinators/{coordId}` | — | 200 |

### 12e. Towers / Nodes

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 22 | GET | `/api/nodes` | — | `NodeSummary[]` |
| 23 | GET | `/api/nodes/{nodeId}` | — | `Node` |
| 24 | DELETE | `/api/sites/{siteId}/coordinators/{coordId}/nodes/{nodeId}` | — | 204 |
| 25 | PUT | `/api/nodes/name` | `{node_id, name}` | 200 |
| 26 | PUT | `/api/nodes/zone` | `{node_id, zone_id}` | 200 |
| 27 | POST | `/api/nodes/test-color` | `{node_id, color:{r,g,b,w?}, duration_ms?}` | 200 |
| 28 | POST | `/api/nodes/off` | `{nodeId}` | 200 |
| 29 | POST | `/api/nodes/brightness` | `{node_id, brightness}` | 200 |
| 30 | POST | `/api/nodes/light/control` | `{node_id, color?, brightness?, effect?}` | 200 |

### 12f. Zones

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 31 | GET | `/api/zones` | — | `Zone[]` |
| 32 | POST | `/api/zones` | `{name, site_id, coordinator_id}` | `Zone` |
| 33 | GET | `/api/zones/{zoneId}` | — | `Zone` |
| 34 | PUT | `/api/zones/{zoneId}` | `{name?}` | `Zone` |
| 35 | DELETE | `/api/zones/{zoneId}` | — | 204 |

### 12g. Pairing

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 36 | POST | `/api/pairing/start` | `{farmId, coordId, durationSeconds}` | `PairingSession` |
| 37 | POST | `/api/pairing/stop` | `{farmId, coordId}` | `PairingSession` |
| 38 | GET | `/api/pairing/session/{farmId}/{coordId}` | — | `PairingSession?` |
| 39 | GET | `/api/pairing/requests/{farmId}/{coordId}` | — | `TowerPairingRequest[]` |
| 40 | POST | `/api/pairing/approve` | `{farmId, coordId, towerId}` | `Tower` |
| 41 | POST | `/api/pairing/reject` | `{farmId, coordId, towerId}` | 200 |
| 42 | POST | `/api/pairing/forget` | `{farmId, coordId, towerId}` | 200 |

### 12h. Telemetry

| # | Method | Route | Params | Response |
|---|---|---|---|---|
| 43 | GET | `/api/telemetry/coordinator/{coordId}` | — | `CoordinatorTelemetryData` |
| 44 | GET | `/api/telemetry/node/{nodeId}` | — | `NodeTelemetryData` |
| 45 | GET | `/api/telemetry/coordinator/{coordId}/history` | `?start=&end=&interval=` | `CoordinatorHistory` |
| 46 | GET | `/api/telemetry/node/{nodeId}/history` | `?start=&end=&interval=` | `NodeHistory` |
| 47 | GET | `/api/telemetry/reservoir/history` | `?coordId=&farmId=&minutes=` | `ReservoirTelemetry[]` |
| 48 | GET | `/api/telemetry/tower/history` | `?towerId=&farmId=&coordId=&minutes=` | `TowerTelemetry[]` |
| 49 | GET | `/api/telemetry/reservoir/latest` | `?farmId=&coordId=` | `ReservoirTelemetry` |

### 12i. Settings

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 50 | GET | `/api/settings` | — | `SmartTileSettings` |
| 51 | PUT | `/api/settings` | `Partial<SmartTileSettings>` | `SmartTileSettings` |

### 12j. Alerts

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 52 | GET | `/api/alerts` | `?page=&pageSize=&severity=&acknowledged=` | `PaginatedResponse<Alert>` |
| 53 | POST | `/api/alerts/{alertId}/acknowledge` | `{}` | 200 |
| 54 | PUT | `/api/alerts/{alertId}` | `{status?, acknowledgedBy?, resolvedBy?}` | `Alert` |
| 55 | DELETE | `/api/alerts/{alertId}` | — | 204 |

### 12k. OTA

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 56 | GET | `/api/ota/firmware` | `?targetType=` | `FirmwareVersion[]` |
| 57 | GET | `/api/ota/jobs` | `?page=&pageSize=&status=` | `PaginatedResponse<OtaJob>` |
| 58 | GET | `/api/ota/jobs/{jobId}` | — | `OtaJob` |
| 59 | POST | `/api/ota/start` | `{targetType, targetId, version}` | `OtaJob` |
| 60 | POST | `/api/ota/jobs/{jobId}/cancel` | `{}` | 200 |
| 61 | GET | `/api/ota/campaigns` | — | `OtaCampaign[]` |
| 62 | POST | `/api/ota/campaigns` | `CreateCampaignRequest` | `OtaCampaign` |
| 63 | GET | `/api/ota/statistics` | — | `OtaStatistics` |
| 64 | GET | `/api/ota/devices/status` | — | `DeviceFirmwareStatus[]` |

### 12l. ML Predictions

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 65 | GET | `/api/predictions/node/{nodeId}` | — | `HeightPrediction` |
| 66 | GET | `/api/predictions/summary` | — | `FarmPredictionSummary` |
| 67 | POST | `/api/predictions/request` | `{towerId, slotIndex?, horizonDays}` | `HeightPrediction` |
| 68 | POST | `/api/predictions/batch` | `{towerIds?, horizonDays}` | `TowerPredictions[]` |
| 69 | GET | `/api/predictions/analysis/{nodeId}` | — | `GrowthAnalysis` |
| 70 | GET | `/api/predictions/anomalies` | `?page=&pageSize=&severity=&status=` | `PaginatedResponse<GrowthAnomaly>` |
| 71 | POST | `/api/predictions/anomalies/{anomalyId}/acknowledge` | `{}` | 200 |
| 72 | GET | `/api/predictions/models` | — | `MLModelInfo[]` |

### 12m. Digital Twins

| # | Method | Route | Request | Response |
|---|---|---|---|---|
| 73 | GET | `/api/twins/farms/{farmId}` | — | `FarmTwinsResponse` |
| 74 | GET | `/api/twins/towers/{towerId}` | — | `TowerTwin` |
| 75 | GET | `/api/twins/towers` | `?farmId=&coordId=` | `TowerTwin[]` |
| 76 | PUT | `/api/twins/towers/{towerId}/desired` | `Partial<TowerDesiredState>` | 200 |
| 77 | GET | `/api/twins/towers/{towerId}/delta` | — | `TowerDeltaResponse` |
| 78 | GET | `/api/twins/coordinators/{coordId}` | — | `CoordinatorTwin` |
| 79 | GET | `/api/twins/coordinators` | `?farmId=` | `CoordinatorTwin[]` |
| 80 | PUT | `/api/twins/coordinators/{coordId}/desired` | `Partial<CoordinatorDesiredState>` | 200 |
| 81 | GET | `/api/twins/coordinators/{coordId}/delta` | — | `CoordinatorDeltaResponse` |

## 13. WebSocket Messages — Frontend → Backend

| # | Type | Payload | Trigger | Source |
|---|---|---|---|---|
| 1 | `ping` | `{type: "ping"}` | 30s heartbeat | `websocket.service.ts:610` |
| 2 | `subscribe` | `{type: "subscribe", target: "coordinator", id: string}` | Manual | `websocket.service.ts:292` |
| 3 | `subscribe` | `{type: "subscribe", target: "tower", id: string}` | Manual | `websocket.service.ts:303` |
| 4 | `subscribe` | `{type: "subscribe", target: "alerts"}` | Auto on connect | `websocket.service.ts:313` |
| 5 | `subscribe` | `{type: "subscribe", target: "ota", id?: string}` | Manual | `websocket.service.ts:323` |
| 6 | `subscribe` | `{type: "subscribe", target: "digital_twin"}` | Manual | `websocket.service.ts:334` |
| 7 | `unsubscribe` | `{type: "unsubscribe", target: string, id?: string}` | Manual | `websocket.service.ts:344` |

## 14. WebSocket Messages — Backend → Frontend

| # | Type | Payload Fields | Handler | Source |
|---|---|---|---|---|
| 1 | `reservoir_telemetry` | `ReservoirTelemetry` fields | `reservoirTelemetrySubject` | `websocket.service.ts:420` |
| 2 | `tower_telemetry` | `TowerTelemetry` fields | `towerTelemetrySubject` | `websocket.service.ts:428` |
| 3 | `alert` | `Alert` fields | `alertSubject` | `websocket.service.ts:436` |
| 4 | `device_status` | `deviceId, deviceType, status, timestamp` | `deviceStatusSubject` | `websocket.service.ts:443` |
| 5 | `connection_status` | `ts, coordId, farmId, event, wifiConnected, wifiRssi, mqttConnected, uptimeMs, freeHeap, reason?` | `connectionStatusSubject` | `websocket.service.ts:484` |
| 6 | `ota_progress` | `jobId, deviceId, progress, status, error?` | `otaProgressSubject` | `websocket.service.ts:451` |
| 7 | `digital_twin_update` | `changeType, deviceId, farmId?, coordId?, towerTwin?, coordinatorTwin?, timestamp?` | `digitalTwinSubject` | `websocket.service.ts:459` |
| 8 | `prediction_update` | `towerId, plantSlot, predictedHeight, confidence, timestamp` | `predictionSubject` | `websocket.service.ts:467` |
| 9 | `coordinator_log` | `coordId, farmId, timestamp, level, message, tag?` | `coordinatorLogSubject` | `websocket.service.ts:475` |
| 10 | `pong` | — | No action | `websocket.service.ts:492` |
| 11 | `error` | `message?` | `errorSubject` | `websocket.service.ts:495` |
| 12 | `pairing_started` | `coordinatorId, siteId, durationMs, startedAt` | `pairingStartedSubject` | `websocket.service.ts:506` |
| 13 | `pairing_stopped` | `coordinatorId, siteId, reason, nodesDiscovered, nodesPaired` | `pairingStoppedSubject` | `websocket.service.ts:513` |
| 14 | `node_discovered` | `coordinatorId, nodeId, macAddress, rssi, discoveredAt, firmwareVersion?` | `nodeDiscoveredSubject` | `websocket.service.ts:521` |
| 15 | `node_paired` | `coordinatorId, nodeId, macAddress, assignedName?, pairedAt` | `nodePairedSubject` | `websocket.service.ts:529` |
| 16 | `pairing_timeout` | `coordinatorId, siteId, nodesDiscovered, nodesPaired` | `pairingTimeoutSubject` | `websocket.service.ts:536` |
| 17 | `coordinator_registration_request` | `coordId, fwVersion?, chipModel?, wifiRssi, ip?, freeHeap, firstSeenAt` | `coordinatorRegistrationSubject` | `websocket.service.ts:549` |
| 18 | `coordinator_registered` | `coordId, farmId, name, description?, color?, location?, registeredAt` | `coordinatorRegisteredSubject` | `websocket.service.ts:555` |
| 19 | `coordinator_rejected` | — | Logged only | `websocket.service.ts:561` |
| 20 | `coordinator_removed` | — | Logged only | `websocket.service.ts:561` |
| 21 | `telemetry_batch` | `Array<{type, payload}>` | Unbatched into tower/reservoir subjects | `websocket.service.ts:570` |
| 22 | `diagnostics_update` | `SystemMetricsSnapshot` fields | `DiagnosticsService` | `websocket.service.ts:589` |

### Server Acknowledgement Types (No-Op Handlers)

| Type | Handler | Source |
|---|---|---|
| `pong` | No-op (heartbeat response) | `websocket.service.ts:492` |
| `subscribed` | No-op (subscription confirmed) | `websocket.service.ts:493` |
| `unsubscribed` | No-op (unsubscription confirmed) | `websocket.service.ts:494` |
| `heartbeat` | No-op (server heartbeat) | `websocket.service.ts:495` |
| `command_ack` | No-op (command acknowledged) | `websocket.service.ts:496` |

## 15. MongoDB Collections

| Collection | Model | TTL | Key Indexes | Written By | Read By |
|---|---|---|---|---|---|
| `coordinators` | Coordinator | — | `coord_id` (unique) | Backend (MQTT handler, REST) | Backend (REST), Frontend (REST) |
| `towers` | Tower | — | `tower_id` (unique) | Backend (MQTT handler, REST) | Backend (REST), Frontend (REST) |
| `zones` | Zone | — | `zone_id` (unique) | Backend (REST) | Backend (REST), Frontend (REST) |
| `farms` | Farm | — | `farm_id` (unique) | Backend (REST) | Backend (REST), Frontend (REST) |
| `settings` | Settings | — | — | Backend (REST) | Backend (REST), Frontend (REST) |
| `ota_jobs` | OtaJob | — | — | Backend (REST) | Backend (REST), Frontend (REST) |
| `reservoir_telemetry` | ReservoirTelemetry | 7 days | `timestamp` | Backend (MQTT handler) | Backend (REST), Frontend (REST) |
| `tower_telemetry` | TowerTelemetry | 7 days | `timestamp` | Backend (MQTT handler) | Backend (REST), Frontend (REST) |
| `height_measurements` | HeightMeasurement | — | — | Backend (REST) | Backend (REST, ML proxy) |
| `firmware_versions` | FirmwareVersion | — | — | Backend (REST) | Backend (REST), Frontend (REST) |
| `alerts` | Alert | — | `farm_id, status, alert_key, created_at` | Backend (MQTT handler, REST) | Backend (REST), Frontend (REST+WS) |
| `tower_twins` | TowerTwin | — | `farm+coord, farm, sync_status, last_reported` | Backend (MQTT handler, twin sync) | Backend (REST), Frontend (REST+WS) |
| `coordinator_twins` | CoordinatorTwin | — | `farm, sync_status, last_reported` | Backend (MQTT handler, twin sync) | Backend (REST), Frontend (REST+WS) |

## 16. Backend Background Services

| Service | Interval | Action | Data Flow |
|---|---|---|---|
| `TwinSyncBackgroundService` | 5s retry, 30s stale check | Retries pending desired-state commands, marks stale twins | MongoDB ↔ MQTT |
| `MlSchedulerBackgroundService` | 1 hour | Runs growth predictions for all towers with active crops | MongoDB → ML Service → MongoDB |
| `AdtSyncService` | Event-driven | Syncs twin changes to Azure Digital Twins | MongoDB → Azure ADT |
| `PairingBackgroundService` | 5s | Expires timed-out pairing sessions | MongoDB |
| `DiagnosticsPushService` | 2s | Broadcasts system metrics to WebSocket clients | Backend → WebSocket |

## 17. Frontend HTTP Interceptors

| Interceptor | Direction | Transform | Exception |
|---|---|---|---|
| `snakeCaseInterceptor` | Request (POST/PUT/PATCH) | camelCase keys → snake_case keys | Preserves `_id` and `_`-prefixed keys |
| `snakeCaseInterceptor` | Response | snake_case keys → camelCase keys | Preserves `_id` and `_`-prefixed keys |

### 17a. snakeCaseInterceptor Detail

| Property | Value | Source |
|---|---|---|
| File | `core/snake-case.interceptor.ts` | 65 lines |
| Type | Angular `HttpInterceptorFn` (functional) | `snake-case.interceptor.ts:47` |
| Request transform | Applies to POST, PUT, PATCH only; GET/DELETE bodies ignored | `snake-case.interceptor.ts:50` |
| Response transform | Applies to all `HttpResponse` bodies | `snake-case.interceptor.ts:58` |
| Key preservation | Keys starting with `_` (e.g., `_id`) pass through unchanged | `snake-case.interceptor.ts:32` |
| Recursion | Transforms nested objects and arrays recursively | `snake-case.interceptor.ts:24-38` |
| Backend convention | ASP.NET serializes with `SnakeCaseLower` (`Program.cs:26`) | |
| Date handling | `Date` objects excluded from plain-object recursion | `snake-case.interceptor.ts:21` |

**Implication:** Frontend TypeScript uses camelCase (`farmId`, `coordId`, `airTempC`). Backend C# uses snake_case (`farm_id`, `coord_id`, `air_temp_c`). The interceptor silently bridges this gap. WebSocket messages are NOT intercepted — they arrive as snake_case and the frontend handles them directly.

## 18. Frontend API URL Prefix

| Prefix Pattern | Endpoints Using It | Count |
|---|---|---|
| `/api/...` | All endpoints (farms, coordinators, nodes, zones, settings, alerts, OTA, predictions, telemetry, pairing, twins, diagnostics, metrics) | ~77 |
| `/health` | Health check (no `/api/` prefix by convention) | 1 |

## 19. Contract Gaps and Mismatches

### 19a. Resolved Gaps (Phases 1-3)

| # | Layer | Issue | Resolution |
|---|---|---|---|
| 1 | Firmware→Backend | Coordinator telemetry unsubscribed | Backend now subscribes to `farm/+/coord/+/telemetry` via `HandleFarmCoordinatorTelemetry` |
| 3 | Firmware→Backend | OTA status unsubscribed | Backend now subscribes to `farm/+/coord/+/ota/status` via `HandleFarmOtaStatus`; legacy `site/+/ota/progress` removed |
| 5 | Backend→Firmware | Config topic unsubscribed | Firmware now subscribes to `coordinator/{coordId}/config` (`Mqtt.cpp:383`) |
| 6 | Backend→Firmware | Restart command unsubscribed | Firmware now subscribes to `coordinator/{coordId}/cmd` (`Mqtt.cpp:388`) |
| 7 | Backend→Firmware | OTA start/cancel unsubscribed | Firmware now subscribes to `farm/.../ota/start` and `ota/cancel` (`Mqtt.cpp:398-405`) |
| 8 | Backend→Firmware | Reservoir commands unsubscribed | Firmware now subscribes to `farm/.../reservoir/cmd` (`Mqtt.cpp:393`) |
| 9 | Frontend→Backend | `fetch()` bypasses interceptor | Replaced with `HttpClient.patch()` in `iot-data.service.ts:584` |
| 10 | Frontend | Unhandled WS message types | `subscribed`, `unsubscribed`, `heartbeat`, `command_ack` now handled as no-ops (`websocket.service.ts:493-496`) |
| 11 | Frontend | Duplicate `ConnectionStatus` interface | Removed from `websocket.model.ts`; single definition in `common.model.ts` |
| 12 | Frontend | `mqttWsUrl` configured but unused | Removed from `environment.service.ts` |
| 13 | Frontend | `SignalR` interfaces defined but unused | Removed from `common.model.ts` |
| 15 | Frontend | `getFarms()` returns `any[]` | Now typed to `Farm[]` in `api.service.ts` |
| 16 | Frontend | Three URL prefix patterns | All unified to `/api/` prefix; only `/health` remains without prefix |
| 17 | Firmware | V2 binary factory gap | `getMessageTypeFromBinary()` and `createFromBinary()` now handle OTA markers `0x30-0x34` |
| 19 | Backend | Pairing topics unmatched | Firmware now has 3 pairing publish methods (`publishPairingRequest/Status/Complete`) targeting matching topics (not yet wired to FSM) |

### 19b. Resolved Gaps (Phase 4)

| # | Layer | Issue | Resolution |
|---|---|---|---|
| 14 | Frontend | Mixed tower/node naming | Pairing types renamed: `DiscoveredNode` → `DiscoveredTower`, `WSNodeDiscoveredPayload` → `WSTowerDiscoveredPayload`, `WSNodePairedPayload` → `WSTowerPairedPayload`. Methods renamed: `approveNode` → `approveTower`, `rejectNode` → `rejectTower`. Prediction params: `nodeId` → `towerId`. WS subjects: `nodeDiscovered$` → `towerDiscovered$`, `nodePaired$` → `towerPaired$`. Route titles updated. Smart tile `Node` types correctly preserved. |
| 21 | Backend | Legacy `site/` topic in ZonesController | Changed to `farm/` prefix in `ZonesController.cs:225` |
| 22 | Backend | Legacy `site/` topics in CustomizeController | Changed 5 MQTT topic strings from `site/` to `farm/` prefix in `CustomizeController.cs` (lines 57, 88, 119, 150, 182) |

### 19c. Structural Hardening (Phase 5)

| # | Layer | Enhancement | Detail |
|---|---|---|---|
| 25 | Backend | Centralized MQTT topics | Created `MqttTopics` static class with 11 subscription constants + 9 publication builders. All controllers and `TelemetryHandler` now reference `MqttTopics.*` instead of inline strings. Zero inline MQTT topic strings remain. |
| 26 | Firmware | MQTT Last Will Testament | `connectMqtt()` now sets LWT on the `status/connection` topic with `{"event":"disconnected"}` retained payload. On successful connect, publishes `{"event":"connected"}` to overwrite. Broker auto-publishes disconnect on unexpected loss. |
| 27 | Frontend | Exhaustive WS message switch | All 28 `WSMessageType` values handled in switch. Added `node_telemetry` and `coord_telemetry` alias cases. Compile-time `never` exhaustiveness check added — new types added to the union without a switch case will cause a build error. |
| 28 | Firmware | MQTT topic validation | Guard clause in MQTT callback validates topic prefix (`farm/` or `coordinator/`). Unexpected topics logged and discarded. Uses `strncmp` (zero allocation). |
| 29 | Backend | WS message envelope validation | `MqttBridgeHandler` validates incoming WS messages: JSON parse, required `type` field, known type whitelist (`ping`/`subscribe`/`unsubscribe`/`publish`), `topic` field for sub/unsub/pub. Invalid messages get `{"type":"error","message":"..."}` response. 15 unit tests. |
| 30 | Frontend | snakeCaseInterceptor documented | Section 17a added to rapport with full behavioral detail: transforms, exceptions, recursion, WebSocket exclusion. |

### 19d. Remaining Gaps

| # | Layer | Issue | Detail | Severity |
|---|---|---|---|---|
| 2 | Firmware→Backend | mmWave data unsubscribed | Firmware publishes `farm/{farmId}/coord/{coordId}/mmwave` — no backend subscriber; data is not persisted | **Medium** |
| 4 | Firmware→Backend | Legacy node telemetry unsubscribed | Firmware publishes `farm/{farmId}/node/{nodeId}/telemetry` — no backend subscriber (smart tile legacy) | **Low** |
| 18 | Firmware | V1/V2 pairing gap | Node still uses V1 JSON pairing; V2 binary FSM defined but not wired to MQTT publish | **Medium** |
| 20 | Backend | Tower status unmatched | Backend subscribes to `farm/+/coord/+/tower/+/status` but firmware doesn't publish tower status separately (only telemetry with `status_mode` field) | **Medium** |
| 23 | Frontend | `NodesController` shim | Backend exposes `Tower` data at `/api/nodes` via `NodesController` shim; frontend calls this instead of `/api/towers` (`TowersController`). Both controllers return the same `Tower` data from `ITowerRepository`. | **Low** |
| 24 | Frontend | WS message type strings | Backend sends `node_discovered` and `node_paired` as WS message types for tower events; TypeScript types renamed but protocol strings preserved for compatibility | **Low** |

## 20. End-to-End Data Flow: Tower Telemetry

| Step | From | To | Protocol | Topic/Endpoint | Payload Summary |
|---|---|---|---|---|---|
| 1 | Tower Node | Coordinator | ESP-NOW JSON | `TowerTelemetry` message | `tower_id, air_temp_c, humidity_pct, light_lux, pump_on, light_on, ...` |
| 2 | Coordinator | MQTT Broker | MQTT QoS 0 | `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry` | Same fields + `ts, farm_id, coord_id` |
| 3 | MQTT Broker | Backend | MQTT subscription | `farm/+/coord/+/tower/+/telemetry` | JSON parsed by `HandleTowerTelemetry` |
| 4 | Backend | MongoDB | MongoDB insert | `tower_telemetry` collection | Stored with 7-day TTL |
| 5 | Backend | MongoDB | MongoDB upsert | `tower_twins` collection | Reported state updated, version incremented |
| 6 | Backend | WebSocket | WS broadcast | `tower_telemetry` message type | Broadcast to subscribed frontend clients |
| 7 | Frontend WS | Frontend state | Signal update | `towerTelemetrySubject` → `IoTDataService` | Updates `nodes` signal, triggers UI re-render |

## 21. End-to-End Data Flow: Tower Command

| Step | From | To | Protocol | Topic/Endpoint | Payload Summary |
|---|---|---|---|---|---|
| 1 | Frontend | Backend | HTTP POST | `/api/nodes/light/control` | `{node_id, color?, brightness?, effect?}` |
| 2 | Backend | MQTT Broker | MQTT publish | `farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd` | `{cmd, tower_id, ...params}` |
| 3 | MQTT Broker | Coordinator | MQTT subscription | `farm/{farmId}/coord/{coordId}/tower/+/cmd` | JSON parsed by `commandCallback` |
| 4 | Coordinator | Tower Node | ESP-NOW JSON | `TowerCommand` message | `tower_id, command, pump_on, light_on, light_brightness, ...` |

## 22. End-to-End Data Flow: Coordinator Registration

| Step | From | To | Protocol | Topic/Endpoint | Payload Summary |
|---|---|---|---|---|---|
| 1 | Coordinator | MQTT Broker | MQTT publish | `coordinator/{coordId}/announce` | `{mac, fw_version, chip_model, free_heap, wifi_rssi, ip, farm_id}` |
| 2 | MQTT Broker | Backend | MQTT subscription | `coordinator/+/announce` | `HandleCoordinatorAnnounce` |
| 3 | Backend | WebSocket | WS broadcast | `coordinator_registration_request` | `{coordId, fwVersion, chipModel, wifiRssi, ip, freeHeap, firstSeenAt}` |
| 4 | Frontend | Backend | HTTP POST | `/api/coordinators/register/approve` | `{coordId, farmId, name, ...}` |
| 5 | Backend | MQTT Broker | MQTT publish | `coordinator/{coordId}/registered` | `{farm_id}` |
| 6 | MQTT Broker | Coordinator | MQTT subscription | `coordinator/{coordId}/registered` | `handleRegistrationMessage()` saves farm_id to NVS |

## 23. End-to-End Data Flow: Pairing

| Step | From | To | Protocol | Topic/Endpoint | Payload Summary |
|---|---|---|---|---|---|
| 1 | Frontend | Backend | HTTP POST | `/api/pairing/start` | `{farmId, coordId, durationSeconds}` |
| 2 | Backend | MQTT Broker | MQTT publish | `farm/{farmId}/coord/{coordId}/cmd` | `{cmd: "start_pairing", duration_ms}` |
| 3 | MQTT Broker | Coordinator | MQTT subscription | `farm/{farmId}/coord/{coordId}/cmd` | Coordinator enables permit-join |
| 4 | Tower Node | Coordinator | ESP-NOW V2 Binary | `PAIRING_ADVERTISEMENT` (0x20) | 22 bytes: mac, device_type, firmware, capabilities, nonce |
| 5 | Coordinator | Tower Node | ESP-NOW V2 Binary | `PAIRING_OFFER` (0x21) | 23 bytes: coord_mac, tower_id, farm_id, offer_token, channel |
| 6 | Tower Node | Coordinator | ESP-NOW V2 Binary | `PAIRING_ACCEPT` (0x22) | 13 bytes: node_mac, offer_token, accepted_tower_id |
| 7 | Coordinator | Tower Node | ESP-NOW V2 Binary | `PAIRING_CONFIRM` (0x23) | 26 bytes: coord_mac, tower_id, encryption_key, config_flags |
| 8 | Coordinator | MQTT Broker | MQTT publish | `farm/{farmId}/coord/{coordId}/pairing/request\|status\|complete` | Publish methods exist (`Mqtt.cpp:1208-1305`) but not yet wired to pairing FSM |

## 24. Firmware Pairing Constants

| Constant | Value | Unit | Source |
|---|---|---|---|
| `ADV_INTERVAL_MS` | 100 | ms | `EspNowMessage.h:305` |
| `ADV_JITTER_MS` | 20 | ms | `EspNowMessage.h:306` |
| `ADV_TIMEOUT_MS` | 300000 | ms (5 min) | `EspNowMessage.h:307` |
| `NONCE_ROTATION_MS` | 30000 | ms (30s) | `EspNowMessage.h:308` |
| `DISCOVERY_TTL_MS` | 30000 | ms (30s) | `EspNowMessage.h:309` |
| `DEFAULT_PERMIT_JOIN_MS` | 60000 | ms (1 min) | `EspNowMessage.h:310` |
| `MAX_PERMIT_JOIN_MS` | 300000 | ms (5 min) | `EspNowMessage.h:311` |
| `BINDING_TIMEOUT_MS` | 10000 | ms (10s) | `EspNowMessage.h:312` |
| `CONFIRM_TIMEOUT_MS` | 5000 | ms (5s) | `EspNowMessage.h:313` |
| `PROTOCOL_VERSION` | 0x02 | — | `EspNowMessage.h:314` |
| `MAX_DISCOVERED_NODES` | 32 | count | `EspNowMessage.h:315` |

## 25. Firmware OTA Constants

| Constant | Value | Unit | Source |
|---|---|---|---|
| `MAX_CHUNK_SIZE` | 200 | bytes | `EspNowMessage.h:442` |
| `CHUNK_ACK_TIMEOUT_MS` | 1000 | ms | `EspNowMessage.h:443` |
| `MAX_CHUNK_RETRIES` | 3 | count | `EspNowMessage.h:444` |
| `OTA_TOTAL_TIMEOUT_MS` | 600000 | ms (10 min) | `EspNowMessage.h:445` |

## 26. Frontend WebSocket Configuration

| Setting | Default | Override | Source |
|---|---|---|---|
| WebSocket URL | `ws://localhost:8000/ws/broadcast` | `window.env.WS_URL` | `environment.service.ts:16` |
| Heartbeat interval | 30s | — | `websocket.service.ts:610` |
| Reconnect delay | 5000ms | `window.env.WS_RECONNECT_DELAY` | `environment.service.ts:30` |
| Max reconnect attempts | 10 | `window.env.WS_MAX_RECONNECT_ATTEMPTS` | `environment.service.ts:31` |
| Reconnect strategy | Exponential backoff | `delay * min(attempts, 5)` | `websocket.service.ts` |

## 27. Frontend State Management

| Service | State Mechanism | Data Source | Refresh Strategy |
|---|---|---|---|
| `IoTDataService` | Angular Signals | REST + WS | Auto-refresh 30s + WS push |
| `TwinService` | Angular Signals | REST + WS | WS `digital_twin_update` push |
| `OtaService` | Angular Signals | REST + WS | Manual + WS OTA progress |
| `AlertService` | Angular Signals | REST + WS | Manual + WS alert push |
| `DiagnosticsService` | Angular Signals | REST + WS | WS `diagnostics_update` (2s) + REST fallback |
| `NotificationService` | Angular Signals | WS events | WS event-driven via `NotificationListenerService` |
| `LogStorageService` | Angular Signals + localStorage | WS `coordinator_log` | WS push + localStorage persistence |

## 28. Backend WebSocket Broadcast Types (22+)

| Type | Trigger Source | Throttle |
|---|---|---|
| `telemetry_batch` | MQTT telemetry handler | 500ms batch |
| `tower_telemetry` | MQTT tower telemetry | Per-message |
| `reservoir_telemetry` | MQTT reservoir telemetry | Per-message |
| `zone_change` | REST zone CRUD | Immediate |
| `ota_status` | MQTT OTA progress | Per-message |
| `coordinator_log` | MQTT serial log | Per-message |
| `farm_update` | REST farm CRUD | Immediate |
| `alert_created` | Telemetry anomaly detection | Immediate |
| `alert_updated` | REST alert update | Immediate |
| `tower_status` | MQTT tower status | Per-message |
| `connection_status` | MQTT connection event | Per-message |
| `coordinator_registration_request` | MQTT coordinator announce | Immediate |
| `coordinator_registered` | REST registration approve | Immediate |
| `coordinator_rejected` | REST registration reject | Immediate |
| `coordinator_removed` | REST coordinator delete | Immediate |
| `diagnostics_update` | `DiagnosticsPushService` | 2s interval |
| `pairing_status` | MQTT pairing status | Per-message |
| `pairing_request` | MQTT pairing request | Per-message |
| `pairing_approved` | REST pairing approve | Immediate |
| `pairing_rejected` | REST pairing reject | Immediate |
| `pairing_complete` | MQTT pairing complete | Per-message |
| `device_forgotten` | REST pairing forget | Immediate |
| `digital_twin_update` | Twin state change | Per-change |
