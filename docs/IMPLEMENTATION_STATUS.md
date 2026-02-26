# Implementation Status Report — Intelligent Hydroponics System

**Bachelorprojekt: Intelligent Hydroponics**
**Dato:** 26. februar 2026
**Status:** Aktiv udvikling

---

## Indholdsfortegnelse

1. [Systemoversigt](#1-systemoversigt)
2. [Arkitektur](#2-arkitektur)
3. [Firmware (ESP32)](#3-firmware-esp32)
4. [Backend (ASP.NET Core)](#4-backend-aspnet-core)
5. [Frontend (Angular)](#5-frontend-angular)
6. [Machine Learning (Python/FastAPI)](#6-machine-learning-pythonfastapi)
7. [Infrastruktur og DevOps](#7-infrastruktur-og-devops)
8. [Simulator](#8-simulator)
9. [Test og Kvalitetssikring](#9-test-og-kvalitetssikring)
10. [Dokumentation](#10-dokumentation)
11. [Opsummering](#11-opsummering)

---

## 1. Systemoversigt

Systemet er en distribueret IoT-platform til overvagning og styring af hydroponiske dyrkningssystemer. Platformen bestar af fire hovedlag:

| Lag | Teknologi | Formaal |
|-----|-----------|---------|
| **Firmware** | ESP32-S3 / ESP32-C3, C++, PlatformIO | Sensor-aflasning, LED-styring, ESP-NOW mesh-kommunikation |
| **Backend** | ASP.NET Core 8, C# 12, MongoDB 7.0 | REST API, MQTT-bridge, WebSocket, Digital Twin, Alert-system |
| **Frontend** | Angular 19, TailwindCSS, Three.js, ECharts | Dashboard, realtidsvisualisering, 3D-tower-visualisering |
| **ML** | Python 3.11, FastAPI, scikit-learn, XGBoost | Vaekstprediktion, anomalidetektion, reservoir-drift, crop-clustering |

### Dataflow

```
ESP32-C3 Tower Nodes
    | (ESP-NOW, 2s interval)
    v
ESP32-S3 Coordinator (Reservoir)
    | (MQTT over WiFi)
    v
Eclipse Mosquitto Broker (port 1883/9001)
    | (MQTT subscriptions)
    v
ASP.NET Core Backend (port 8000)
    |-- MongoDB (persistering, TTL-indeksering)
    |-- WebSocket /ws/broadcast (throttled 500ms)
    |-- REST API (50+ endpoints)
    |-- ML Service (HTTP proxy)
    v
Angular Frontend (port 4200)
    |-- ECharts (realtidsgrafer)
    |-- Three.js (3D tower rack)
    |-- Spartan UI (komponentbibliotek)
```

---

## 2. Arkitektur

### 2.1 Domainehierarki

```
Farm
  |-- Coordinator (ESP32-S3, reservoir-controller)
  |     |-- Reservoir Sensors (pH, EC, TDS, vandtemperatur, vandniveau)
  |     |-- mmWave Radar (LD2450, tilstedevaerelsedetektering)
  |     |-- Ambient Light Sensor (TSL2561)
  |     |-- SK6812B LED-strip (16 pixels, statusindikation)
  |     |-- Pumpe-styring (hoved, pH-dosering, naeringsstof-dosering)
  |     |
  |     |-- Tower Node 1 (ESP32-C3)
  |     |     |-- TMP117 temperatursensor (I2C)
  |     |     |-- SK6812B RGBW LEDs (4 pixels)
  |     |     |-- Knap-input (kort/langt tryk)
  |     |     |-- Pumpe-relay
  |     |     |-- PWM Grow Light
  |     |
  |     |-- Tower Node 2 (ESP32-C3)
  |     |-- Tower Node N ...
  |
  |-- Coordinator 2 ...
```

### 2.2 Kommunikationsprotokoller

| Protokol | Lag | Formaal |
|----------|-----|---------|
| **ESP-NOW** | Tower <-> Coordinator | Lav-latens, peer-to-peer, kanal-hopping under pairing |
| **MQTT** | Coordinator <-> Backend | Telemetri-publicering, kommando-subscription, LWT |
| **WebSocket** | Backend <-> Frontend | Realtids-broadcast af telemetri og events |
| **REST/HTTP** | Frontend <-> Backend, Backend <-> ML | CRUD-operationer, ML-inference |

### 2.3 Digital Twin Pattern

Systemet implementerer et fuldstaendigt Digital Twin-pattern med:

- **Desired State**: Frontend saetter oensket tilstand (LED-farve, pumpe on/off, grow light brightness)
- **Reported State**: Firmware rapporterer aktuel tilstand via telemetri
- **Delta Computation**: Backend beregner forskellen mellem desired og reported
- **Sync Status**: Tracking af hvornaar synkronisering lykkedes/fejlede
- **ML Predictions**: ML-forudsigelser embeddes direkte i twin-dokumenter

---

## 3. Firmware (ESP32)

### 3.1 Coordinator Firmware (ESP32-S3)

**Placering:** `Firmware/coordinator/src/`
**Chip:** ESP32-S3-DevKitC-1
**Biblioteker:** ArduinoJson, PubSubClient, AsyncMqttClient, Adafruit NeoPixel, TSL2561, LD2450

#### Filstruktur (37 source-filer)

```
coordinator/src/
|-- main.cpp                        # Boot-sekvens: NVS -> ConfigStore -> Coordinator.begin()
|-- Models.h                        # TowerInfo, MmWaveEvent, ReservoirSensorSnapshot
|-- Logger.h                        # Serial + MQTT log-system
|
|-- core/
|   |-- Coordinator.h/.cpp          # Slim entry-point (WiFi + MQTT + ESP-NOW + NodeRegistry)
|   |-- Reservoir.h/.cpp            # Full reservoir-controller med alle subsystemer
|   |-- LedController.h/.cpp        # SK6812B RGBW strip-styring (16 pixels, 4 per tower)
|   |-- BootManager.h/.cpp          # Boot-status tracking og boot-summary
|   |-- SerialConsole.h/.cpp        # Serial-kommando interface
|
|-- comm/
|   |-- WifiManager.h/.cpp          # WiFi-forbindelse med auto-reconnect
|   |-- Mqtt.h/.cpp                 # PubSubClient MQTT (synkron)
|   |-- AsyncMqtt.h/.cpp            # AsyncMqttClient (asynkron alternativ)
|   |-- EspNow.h/.cpp               # ESP-NOW v2.0 manager med peer-haandtering
|   |-- MqttLogger.h                # Log-streaming til MQTT
|
|-- sensors/
|   |-- MmWave.h/.cpp               # HLK-LD2450 mmWave radar (UART, 256000 baud)
|   |-- AmbientLightSensor.h/.cpp   # TSL2561 lux-sensor (I2C)
|   |-- ThermalControl.h/.cpp       # Temperatur-overvagning og derating
|
|-- actuators/
|   |-- ReservoirPumpController.h/.cpp  # Hoved-pumpe + pH/naering dosering
|   |-- IReservoirPumpController.h      # Interface for pump-controller
|
|-- towers/
|   |-- TowerRegistry.h/.cpp        # Registry af tilknyttede tower nodes
|
|-- nodes/
|   |-- NodeRegistry.h/.cpp         # Legacy node registry (backward compatibility)
|
|-- pairing/
|   |-- PairingStateMachine.h/.cpp  # Zigbee-inspireret permit-join pairing FSM
|   |-- DiscoveryManager.h/.cpp     # Node-discovery med RSSI og timeout
|
|-- zones/
|   |-- ZoneControl.h/.cpp          # Zone-baseret LED/pumpe-styring
|
|-- managers/
|   |-- ThermalManager.h/.cpp       # Termisk derating-manager
|
|-- input/
|   |-- ButtonControl.h/.cpp        # Fysisk knap-haandtering (kort/langt tryk)
|
|-- config/
|   |-- PinConfig.h                 # GPIO-pin definitions (S3)
|
|-- utils/
|   |-- LogStreamer.h/.cpp          # Serial-log streaming til MQTT
|   |-- OtaUpdater.h/.cpp          # HTTP-baseret OTA firmware update
|   |-- PowerManager.h/.cpp        # Power management
|   |-- SystemWatchdog.h/.cpp      # Hardware watchdog
|   |-- StatusLed.h                # Onboard status LED helper
|   |-- Logger.h                   # Logging utility
```

#### Pin-konfiguration (ESP32-S3)

| Pin | Funktion |
|-----|----------|
| GPIO44/43 | UART1 RX/TX til LD2450 mmWave radar (256000 baud) |
| GPIO15 | SK6812B LED data (16 pixels) |
| GPIO0 | Pairing-knap (BOOT button med pull-up) |
| GPIO16/17 | I2C SDA/SCL til TSL2561 ambient light sensor |
| GPIO12/13/11/10 | SPI bus (udvidelsesmulighed) |

#### Coordinator Funktionalitet

1. **Boot-sekvens**: NVS init -> ConfigStore -> WiFi -> MQTT -> ESP-NOW -> Sensorer -> LED -> Ready
2. **WiFi Management**: Auto-reconnect, kanal-detection for ESP-NOW synkronisering
3. **MQTT Publishing**: Reservoir-telemetri, tower-telemetri (proxy), status, pairing-events
4. **MQTT Subscription**: Kommandoer fra backend (pumpe, dosering, LED, pairing, restart, OTA)
5. **ESP-NOW Hub**: Modtager telemetri fra towers, videresender kommandoer, pairing-handshake
6. **mmWave Radar**: Realtids tilstedevaerelses-detektion med 3 samtidige targets, zone-filtrering
7. **LED-mapping**: 4 pixels per tower group, automatisk mapping ved tower-tilknytning
8. **Termisk styring**: Temperatur-overvagning med automatisk derating ved overophedning
9. **Serial Console**: Debug-kommandoer via USB-CDC
10. **Health Pings**: Periodisk ESP-NOW ping til tilknyttede towers

#### mmWave Radar (LD2450) Implementation

- UART-streaming ved 256000 baud
- Op til 3 samtidige targets med position (X, Y), afstand, hastighed
- Zone-filtrering: Er target inden for defineret zone?
- Confidence-beregning baseret pa andel af gyldige targets
- Auto-restart ved stream-stall (max 4 restart-forsoeg foer offline-markering)
- Debounce (150ms) og publiceringsrate-limiter (120ms minimum interval)

---

### 3.2 Node Firmware (ESP32-C3)

**Placering:** `Firmware/node/src/`
**Chip:** ESP32-C3-MINI-1 (ogsa ESP32-C6 support)
**Biblioteker:** ArduinoJson, Adafruit NeoPixel, ESP32Time, Adafruit TMP117

#### Filstruktur

```
node/src/
|-- main.cpp                    # SmartTileNode klasse (1177 linjer)
|                               # To modes: STANDALONE_TEST og normal operation
|-- TowerCommandHandler.h       # Kommando-haandtering fra coordinator
|-- TowerTelemetrySender.h      # Telemetri-afsendelse
|
|-- config/
|   |-- PinConfig.h             # GPIO-pin definitions (C3)
|   |-- PinConfig_C6.h          # GPIO-pin definitions (C6)
|   |-- TowerConfig.h           # Tower-specifik konfiguration
|
|-- led/
|   |-- LedController.h/.cpp    # SK6812B RGBW med fade, status-animationer
|
|-- sensor/
|   |-- TMP177Sensor.h          # TMP117 I2C temperatursensor
|
|-- actuators/
|   |-- IPumpController.h       # Pumpe-interface
|   |-- RelayPumpController.h   # Relay-baseret pumpe
|   |-- IGrowLightController.h  # Grow light interface
|   |-- PWMGrowLightController.h # PWM grow light styring
|
|-- pairing/
|   |-- NodePairingFSM.h/.cpp   # Pairing state machine (node-side)
|
|-- input/
|   |-- ButtonInput.h/.cpp      # Knap med short/long press callbacks
|
|-- power/
|   |-- PowerManager.h/.cpp     # Light sleep power management
|
|-- utils/
|   |-- OtaUpdater.h/.cpp       # WiFi-baseret OTA update
|   |-- Logger.h/.cpp           # Serial logging
|   |-- EspNowLogger.h          # ESP-NOW message logging
```

#### Pin-konfiguration (ESP32-C3)

| Pin | Funktion |
|-----|----------|
| GPIO4 | SK6812B LED data (4 RGBW pixels) |
| GPIO5/6 | LED data strip 2/3 (valgfrit) |
| GPIO8 | Built-in RGB status LED |
| GPIO3 | Knap-input |
| GPIO1/2 | I2C SDA/SCL til TMP117 |
| GPIO21/20 | UART0 TX/RX (debug) |

#### Node State Machine

```
PAIRING ----[JOIN_ACCEPT]----> OPERATIONAL
    ^                              |
    |                              | [5 min timeout + 50 telemetri uden svar]
    +-------[re-pair]--------------+
    
OPERATIONAL --[OTA cmd]--> UPDATE ---> REBOOT
```

#### Node Funktionalitet

1. **ESP-NOW v2.0**: Kanal-scanning (13 kanaler), broadcast JOIN_REQUEST, kanal-locking ved coordinator-svar
2. **Pairing**: Auto-start ved boot, 120s vindue, 600ms join-request interval, kanal-hopping indtil locked
3. **RGBW LED**: 4 SK6812B pixels med fade-transition, status-animationer (pairing=blaa puls, fejl=roed flash)
4. **TMP117 Sensor**: I2C temperatursensor med auto-detection og I2C bus-scanning ved boot
5. **Telemetri**: Status (RGBW-vaerdier, temperatur, batterispanding, firmware-version, mode) sendt hvert 1s
6. **Kommandoer**: SET_LIGHT (RGBW + per-pixel + fade), ACK, reboot, OTA
7. **Reconnection**: Automatisk re-pairing efter 5 min uden coordinator-svar og 50 ubesvarede telemetri
8. **Standalone Mode**: Kompilerings-flag `STANDALONE_TEST` for LED-test uden coordinator

---

### 3.3 Shared Firmware Library

**Placering:** `Firmware/shared/src/`

| Fil | Linjer | Formaal |
|-----|--------|---------|
| `EspNowMessage.h` | 565 | Alle ESP-NOW beskedtyper (20+ message types) |
| `EspNowMessage.cpp` | ~800 | JSON/binary serialisering for alle beskedtyper |
| `ConfigManager.h/.cpp` | ~300 | NVS-baseret key-value konfiguration |
| `ConfigStore.h/.cpp` | ~200 | Unified configuration store |
| `Config.h` | ~100 | Konfigurationsnoegler og standardvaerdier |

#### ESP-NOW Beskedtyper (20 typer)

| Kategori | Beskedtype | Retning | Format |
|----------|-----------|---------|--------|
| **Legacy Pairing** | `JOIN_REQUEST` | Node -> Broadcast | JSON |
| | `JOIN_ACCEPT` | Coordinator -> Node | JSON |
| **Styring** | `SET_LIGHT` | Coordinator -> Node | JSON |
| | `NODE_STATUS` | Node -> Coordinator | JSON |
| | `ACK` | Begge retninger | JSON |
| | `ERROR` | Begge retninger | JSON |
| **Hydroponics** | `TOWER_JOIN_REQUEST` | Tower -> Coordinator | JSON |
| | `TOWER_JOIN_ACCEPT` | Coordinator -> Tower | JSON |
| | `TOWER_TELEMETRY` | Tower -> Coordinator | JSON |
| | `TOWER_COMMAND` | Coordinator -> Tower | JSON |
| | `RESERVOIR_TELEMETRY` | Coordinator intern | JSON |
| **V2 Pairing** | `PAIRING_ADVERTISEMENT` | Node -> Broadcast | Binary (22B) |
| | `PAIRING_OFFER` | Coordinator -> Node | Binary (23B) |
| | `PAIRING_ACCEPT` | Node -> Coordinator | Binary (13B) |
| | `PAIRING_CONFIRM` | Coordinator -> Node | Binary (26B) |
| | `PAIRING_REJECT` | Coordinator -> Node | Binary (12B) |
| | `PAIRING_ABORT` | Node -> Coordinator | Binary (12B) |
| **ESP-NOW OTA** | `OTA_BEGIN` | Coordinator -> Tower | Binary (45B) |
| | `OTA_CHUNK` | Coordinator -> Tower | Binary (max 205B) |
| | `OTA_CHUNK_ACK` | Tower -> Coordinator | Binary (5B) |
| | `OTA_ABORT` | Begge retninger | Binary (4B) |
| | `OTA_COMPLETE` | Tower -> Coordinator | Binary (3B) |

#### V2 Pairing Protocol (Zigbee-inspireret)

```
Node                          Coordinator                    Backend/Frontend
  |                               |                               |
  |--PAIRING_ADVERTISEMENT------->|  (broadcast hvert 100ms)      |
  |  (binary, 22 bytes)           |                               |
  |                               |--MQTT pairing/request-------->|
  |                               |                               |
  |                               |<--MQTT approve/reject---------|
  |                               |                               |
  |<--PAIRING_OFFER---------------|  (unicast, 23 bytes)          |
  |  (coord_mac, tower_id, token) |                               |
  |                               |                               |
  |--PAIRING_ACCEPT-------------->|  (13 bytes)                   |
  |  (echoed token)               |                               |
  |                               |                               |
  |<--PAIRING_CONFIRM-------------|  (26 bytes, encryption key)   |
  |                               |--MQTT pairing/complete------->|
  |                               |                               |
  [OPERATIONAL]                   [Tower Registered]              [UI Updated]
```

**Timing-konstanter:**
- Advertisement interval: 100ms (+ 20ms jitter)
- Advertisement timeout: 5 min
- Nonce rotation: 30s
- Discovery TTL: 30s
- Binding timeout: 10s
- Max discovered nodes: 32

---

## 4. Backend (ASP.NET Core)

**Placering:** `Backend/src/IoT.Backend/`
**Framework:** .NET 8.0, ASP.NET Core Web API
**Database:** MongoDB 7.0 via MongoDB.Driver
**MQTT:** MQTTnet 4.3.7
**Validering:** FluentValidation
**Logging:** Serilog (structured logging)

### 4.1 Arkitektur-patterns

- **Repository Pattern**: Enkelt `MongoRepository` implementerer 7 interfaces
- **Digital Twin Pattern**: Desired/reported state med delta-beregning
- **Background Services**: 6 hosted services for asynkrone opgaver
- **Middleware Pipeline**: API Key auth + global error handling
- **WebSocket Broadcast**: Throttled 500ms batching med per-device deduplicering

### 4.2 MongoDB Collections (12+)

| Collection | TTL | Formaal |
|------------|-----|---------|
| `coordinators` | - | ESP32-S3 coordinator-enheder med reservoir-sensorer |
| `towers` | - | ESP32-C3 tower nodes med miljo-sensorer og crop tracking |
| `farms` | - | Gaard/lokation med geo-koordinater |
| `zones` | - | Zoner inden for en farm |
| `settings` | - | Site-indstillinger (auto mode, WiFi/MQTT config) |
| `ota_jobs` | - | OTA update-jobs med device-level tracking |
| `firmware_versions` | - | Firmware version registry |
| `reservoir_telemetry` | **7 dage** | Tidserie: pH, EC, TDS, vandtemp, vandniveau, pumper |
| `tower_telemetry` | **7 dage** | Tidserie: lufttemp, fugtighed, lys, pumpe, grow light |
| `height_measurements` | - | Plante-hojdemaalinger per slot |
| `coordinator_twins` | - | Digital Twin for coordinators |
| `tower_twins` | - | Digital Twin for towers |
| `alerts` | - | Alarmer med lifecycle (active -> acknowledged -> resolved) |
| `crop_compatibility` | - | ML-genereret crop compatibility matrix |

### 4.3 API Endpoints (50+)

#### Coordinators (`/api/coordinators`)

| Method | Endpoint | Formaal |
|--------|----------|---------|
| GET | `/api/coordinators` | List alle coordinators |
| GET | `/api/coordinators/{coordId}` | Hent specifik coordinator |
| PATCH | `/api/coordinators/{coordId}` | Opdater metadata |
| GET | `/api/coordinators/farm/{farmId}` | Coordinators efter farm |
| GET | `/api/coordinators/{farmId}/{coordId}/reservoir` | Reservoir-tilstand |
| POST | `/api/coordinators/{farmId}/{coordId}/reservoir/pump` | Pumpe-styring |
| POST | `/api/coordinators/{farmId}/{coordId}/reservoir/dosing` | Naeringsstof-dosering |
| PUT | `/api/coordinators/{farmId}/{coordId}/reservoir/targets` | Opdater setpoints |
| GET | `/api/coordinators/pending` | Ventende registreringer |
| POST | `/api/coordinators/register/approve` | Godkend registrering |
| POST | `/api/coordinators/register/reject` | Afvis registrering |
| PUT | `/api/coordinators/{coordId}/config` | Opdater config (DB + MQTT) |
| POST | `/api/coordinators/{coordId}/restart` | Genstart via MQTT |
| DELETE | `/api/coordinators/{coordId}` | Fjern coordinator |

#### Towers (`/api/towers`)

| Method | Endpoint | Formaal |
|--------|----------|---------|
| GET | `/api/towers/farm/{farmId}` | Towers efter farm |
| GET | `/api/towers/farm/{farmId}/coord/{coordId}` | Towers efter coordinator |
| GET | `/api/towers/{farmId}/{coordId}/{towerId}` | Specifik tower |
| PUT | `/api/towers/{farmId}/{coordId}/{towerId}` | Upsert tower |
| PATCH | `/api/towers/{farmId}/{coordId}/{towerId}/name` | Opdater navn |
| DELETE | `/api/towers/{farmId}/{coordId}/{towerId}` | Slet tower |
| POST | `/api/towers/{farmId}/{coordId}/{towerId}/command` | Send MQTT-kommando |
| POST | `/api/towers/{farmId}/{coordId}/{towerId}/light` | Grow light styring |
| POST | `/api/towers/{farmId}/{coordId}/{towerId}/pump` | Pumpe-styring |
| GET | `/api/towers/{farmId}/{coordId}/{towerId}/telemetry` | Telemetri-historik |
| GET | `/api/towers/{farmId}/{coordId}/{towerId}/telemetry/latest` | Seneste telemetri |
| GET | `/api/towers/{farmId}/{coordId}/{towerId}/height` | Hojdemaalinger |
| POST | `/api/towers/{farmId}/{coordId}/{towerId}/height` | Registrer hojde |
| POST | `/api/towers/{farmId}/{coordId}/{towerId}/crop` | Saet afgrode-info |

#### Telemetri (`/api/telemetry`)

| Method | Endpoint | Formaal |
|--------|----------|---------|
| GET | `/api/telemetry/reservoir/{farmId}/{coordId}` | Reservoir-historik |
| GET | `/api/telemetry/reservoir/{farmId}/{coordId}/latest` | Seneste reservoir |
| GET | `/api/telemetry/reservoir/{farmId}/{coordId}/daily` | Daglige gennemsnit |
| GET | `/api/telemetry/tower/{farmId}/{coordId}/{towerId}` | Tower-historik |
| GET | `/api/telemetry/tower/{farmId}/{coordId}/{towerId}/latest` | Seneste tower |
| GET | `/api/telemetry/tower/{farmId}/{coordId}/{towerId}/daily` | Daglige gennemsnit |
| GET | `/api/telemetry/reservoir/history?minutes=` | Historik pr. minutter |
| GET | `/api/telemetry/tower/history?minutes=` | Historik pr. minutter |
| GET | `/api/telemetry/height/{farmId}/{towerId}` | Plante-hojder |

#### Farms, Alerts, Twins, ML, Diagnostics

| Controller | Endpoints | Formaal |
|------------|-----------|---------|
| `FarmsController` | CRUD + relationer | Farm-haandtering med cascade-tjek |
| `AlertsController` | List, filter, acknowledge, resolve | Alert lifecycle management |
| `TwinsController` | GET/PUT desired, delta, sync | Digital Twin CRUD og synkronisering |
| `MlController` | Growth predict, anomaly detect, health | ML-service proxy |
| `DiagnosticsController` | Metrics snapshot, history, reset | System performance metrics |
| `HealthController` | `/health`, `/health/ready`, `/health/live` | Kubernetes-kompatible probes |
| `SettingsController` | GET/PUT/PATCH | Site-indstillinger |
| `ZonesController` | Full CRUD | Zone-haandtering |
| `CustomizeController` | Device config, LED, light | Enhedstilpasning via MQTT |

### 4.4 MQTT Integration

#### Subscriptions (11 topic-patterns)

| Topic Pattern | Formaal |
|---------------|---------|
| `farm/+/coord/+/telemetry` | Coordinator ambient sensorer, mmWave, WiFi |
| `farm/+/coord/+/reservoir/telemetry` | Reservoir vandkvalitet (pH, EC, vandniveau) |
| `farm/+/coord/+/tower/+/telemetry` | Tower miljo (DHT22, lys, aktuatorer) |
| `farm/+/coord/+/tower/+/status` | Tower online/offline, mode-aendringer |
| `farm/+/coord/+/ota/status` | OTA fremskridt |
| `farm/+/coord/+/pairing/request` | Tower pairing-anmodninger |
| `farm/+/coord/+/pairing/status` | Pairing mode status |
| `farm/+/coord/+/pairing/complete` | Pairing faerdiggorelse |
| `farm/+/coord/+/serial` | Serial log streaming |
| `farm/+/coord/+/status/connection` | WiFi/MQTT lifecycle |
| `coordinator/+/announce` | Coordinator discovery/registrering |

#### Publications (10 topic-builders)

| Topic Pattern | Formaal |
|---------------|---------|
| `farm/{farmId}/coord/{coordId}/cmd` | Kommandoer til coordinator |
| `farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd` | Kommandoer til tower via coordinator |
| `farm/{farmId}/coord/{coordId}/reservoir/cmd` | Reservoir pumpe/doserings-kommandoer |
| `farm/{farmId}/coord/{coordId}/ota/start` | Trigger OTA |
| `farm/{farmId}/coord/{coordId}/ota/cancel` | Annuller OTA |
| `coordinator/{coordId}/config` | Push config til coordinator |
| `coordinator/{coordId}/cmd` | Direkte kommandoer (restart) |
| `coordinator/{coordId}/registered` | Registrerings-bekraeftelse |

### 4.5 WebSocket Endpoints

#### `/ws/broadcast` — Throttled Broadcast

Alle klienter modtager automatisk alle events. Throttled med 500ms batching og per-device deduplicering.

**22 broadcast message types:**

| Type | Formaal |
|------|---------|
| `tower_telemetry` | Tower sensor-data |
| `reservoir_telemetry` | Reservoir sensor-data |
| `telemetry_batch` | Samlet batch af telemetri |
| `tower_status` | Tower online/offline |
| `connection_status` | WiFi/MQTT forbindelsesstatus |
| `ota_status` | OTA-fremskridt |
| `alert_created` / `alert_updated` | Alert lifecycle |
| `pairing_status` / `pairing_request` / `pairing_complete` | Pairing-events |
| `coordinator_registration_request` | Ny coordinator opdaget |
| `coordinator_registered` / `coordinator_rejected` / `coordinator_removed` | Registreringsflow |
| `farm_update` | Farm-aendringer |
| `diagnostics_update` | System performance |
| `device_forgotten` | Enhed fjernet |
| `coordinator_log` | Serial log data |
| `zone_change` | Zone-aendringer |

#### `/ws` — Subscription-Based MQTT Bridge

Klienter sender JSON-beskeder (`subscribe`, `unsubscribe`, `publish`, `ping`) og modtager kun matchende MQTT-beskeder.

### 4.6 Background Services (6)

| Service | Formaal |
|---------|---------|
| `TelemetryHandler` | Central MQTT-processor: persist, twin-update, alert-check, broadcast |
| `PairingBackgroundService` | Pairing session timeout og cleanup |
| `TwinSyncBackgroundService` | Periodisk synk af desired/reported state |
| `DiagnosticsPushService` | Push system metrics til WebSocket |
| `AdtSyncService` | Valgfri Azure Digital Twins synkronisering |
| `MlSchedulerBackgroundService` | Periodisk ML-inference (60 min interval, konfigurerbart) |

### 4.7 Alert System

**10+ alert-typer med automatisk detektion og auto-resolution:**

| Alert Type | Trigger | Severity |
|------------|---------|----------|
| Battery Low | vbat_mv < threshold | Warning |
| Temperature High | air_temp > 35C | Critical |
| Temperature Low | air_temp < 10C | Warning |
| Connectivity Lost | Ingen telemetri i X min | Critical |
| Water Level Low | water_level < 20% | Critical |
| pH Out of Range | pH < 5.0 eller > 7.5 | Warning/Critical |
| EC Out of Range | EC < 0.3 eller > 4.0 | Warning |
| Pump Failure | Pumpe-kommando uden respons | Critical |
| Tower Offline | Tower ikke set i X min | Warning |

Alerts deduplikeres via `alert_key` og auto-resolves naar vaerdier vender til normale.

---

## 5. Frontend (Angular)

**Placering:** `Frontend/src/app/`
**Framework:** Angular 19.2 med **standalone components** og **experimental zoneless change detection**
**UI Library:** Spartan UI (@spartan-ng/brain) — headless komponenter med TailwindCSS
**Styling:** TailwindCSS 3.4 med OKLCH farve-system, WCAG AA-kompatibelt tema (lys/moerk/system + 8 accent-farver)
**3D:** Three.js 0.180 (tower rack visualisering) + BabylonJS 8.38
**Charts:** ECharts 6 via ngx-echarts (primaer), D3.js 7 og Chart.js 4.5 (sekundaer)

### 5.1 State Management

Signal-baseret state management gennemgaende (ingen NgRx/NGXS):

- Alle services bruger `signal()` + `computed()`
- `IoTDataService` er den primaere centraliserede state (coordinators, nodes, alerts, sites, zones, health)
- `TwinService` haandterer digital twin state med realtids-WebSocket updates og optimistiske updates
- `AlertService` fuldt alert lifecycle med filtrering, statistik, acknowledge/resolve/dismiss

### 5.2 Services (17)

| Service | Linjer | Formaal |
|---------|--------|---------|
| `ApiService` | 696 | 50+ REST endpoint-wrappers |
| `WebSocketService` | 829 | WS-klient med auto-reconnect, heartbeat, 19 stream subjects, 34 message types |
| `IoTDataService` | ~500 | Centraliseret state med 30s polling auto-refresh |
| `TwinService` | ~400 | Digital twin CRUD med optimistiske updates |
| `AlertService` | ~350 | Alert lifecycle, filtrering, statistik |
| `OtaService` | ~300 | Firmware update management med campaign-support |
| `DiagnosticsService` | ~250 | Backend performance monitoring |
| `TelemetryHistoryService` | ~200 | Historisk telemetri-data |
| `HydroponicDataService` | ~200 | Domaine-specifik data-adapter |
| `ThemeService` | ~150 | Lys/moerk/system tema + accent-farve |
| `ToastService` | ~100 | Notifikationer |
| `NotificationListenerService` | ~200 | WebSocket -> toast routing |
| `SidebarService` | ~50 | Sidebar state |
| `ConfirmDialogService` | ~80 | Bekraeftelses-dialoger |
| `LogStorageService` | ~100 | Frontend log-persistering |

### 5.3 Interceptors

| Interceptor | Formaal |
|-------------|---------|
| `SnakeCaseInterceptor` | Bidirektionel camelCase <-> snake_case transformation for REST API (bevarer `_id`) |
| `ApiKeyInterceptor` | Tilfojer `X-API-Key` header til backend-requests |

### 5.4 Pages (18+)

| Side | Formaal |
|------|---------|
| `farm-overview` | Farm overblik med alle coordinators og towers |
| `farms-list` | Liste over alle farms |
| `farm-detail` | Detaljeret farm-visning |
| `coordinator-detail` | Reservoir detaljer med sensorer og tilknyttede towers |
| `reservoirs-list` | Liste over reservoirs |
| `towers-list` | Liste over towers |
| `node-detail` | Tower node detaljer |
| `digital-twin` | Digital Twin visualisering med desired/reported state |
| `ota-dashboard` | OTA firmware update management |
| `alerts` | Alert liste |
| `alerts-dashboard` | Alert oversigt med statistik |
| `radar-view` | mmWave radar visualisering |
| `predictions` | ML-forudsigelser visning |
| `machine-learning` | ML-model status og management |
| `settings` | System-indstillinger |
| `diagnostics-system` | System diagnostik |
| `diagnostics-sensors` | Sensor diagnostik |
| `diagnostics-scale-test` | Skaleringstest |

### 5.5 Data Visualisering

#### ECharts Telemetry Charts (`TelemetryChartComponent`)
- Realtids-opdatering via WebSocket
- Historisk data med konfigurerbar tidsvindue
- Supporterer alle sensor-typer (pH, EC, temperatur, fugtighed, lys, vandniveau)

#### Three.js 3D Tower Rack (`TowerRack3dComponent`)
- 3D-visualisering af tower rack med individuelle tower-modeller
- Raycasting for interaktion (hover/klik pa towers)
- CSS2D labels for tower-navne og status
- Orbit controls for kamera-navigation
- Farve-kodning baseret pa sensor-vaerdier

### 5.6 UI Komponentbibliotek (20+ Spartan UI komponenter)

Headless UI-komponenter med TailwindCSS Helm-direktiver:
Alert, Aspect Ratio, Avatar, Badge, Button, Card, Collapsible, Command, Dialog, Dropdown Menu, Form Field, Icon, Input, Label, Popover, Select, Separator, Sheet, Skeleton, Sonner (Toast), Spinner, Switch, Table, Tabs, Toggle, Tooltip

### 5.7 Real-time Mekanismer

1. **WebSocket Broadcast**: `ws://localhost:8000/ws/broadcast` med auto-reconnect (max 10 forsoeg, eksponentiel backoff)
2. **Heartbeat**: 30-sekunders ping/pong
3. **Polling Fallback**: 30-sekunders auto-refresh af coordinators/nodes/alerts
4. **Telemetri-batching**: Throttled updates for performance

### 5.8 Docker Deployment

- Multi-stage build: Node 20 (build) -> nginx Alpine (serve)
- Nginx reverse proxy: `/api/` -> backend:8000, `/ws` med WebSocket upgrade
- Static asset caching: 1 aar, immutable
- Gzip-komprimering pa text/css/json/js
- Sikkerhedsheaders: X-Frame-Options, X-Content-Type-Options, X-XSS-Protection
- Health endpoint: `/health`

---

## 6. Machine Learning (Python/FastAPI)

**Placering:** `ML/src/`
**Framework:** FastAPI med uvicorn
**ML Libraries:** scikit-learn, XGBoost, LightGBM
**Data:** MongoDB (primaer), MQTT (realtid), Azure Digital Twins (valgfri)
**Total kodelinjer:** ~5,846 linjer Python pa tvaers af 17 source-filer

### 6.1 ML-modeller (5)

#### Model 1: Growth Predictor

| Aspekt | Detaljer |
|--------|----------|
| **Opgave** | Regression: forudsig plantehojde (cm) |
| **Algoritme** | RandomForestRegressor / GradientBoostingRegressor |
| **Features** | days_since_planting, air_temp_c, humidity_pct, light_lux, ph, ec_ms_cm, rolling 24h averages, temp x humidity interaction, pH/EC deviation, crop_type (one-hot), growth_stage |
| **Target** | height_cm |
| **Evaluering** | RMSE, MAE, R2, 5-fold krydsvalidering |
| **Fallback** | Regelbaseret: afgrode-specifik vaekstrate x sundhedsscore |
| **Confidence** | 0.85 (ML) / 0.65 (regelbaseret) |
| **Output** | predicted_height_cm, predicted_harvest_date, days_to_harvest, growth_rate_cm_per_day, health_score |
| **Afgroder** | 10 (lettuce, basil, spinach, kale, strawberry, tomato, pepper, mint, cilantro, microgreens) |

**Health Score**: Sammensat score (0.0-1.0) baseret pa individuelle range-scores for temperatur, fugtighed, lys, pH og EC - hver scored 0.3-1.0 baseret pa afstand fra afgrodens optimale vaerdi.

**Growth Stage Inference**: Proportion af forventet host-cyklus:
- Seedling: 0-15%
- Vegetative: 15-55%
- Flowering: 55-80%
- Fruiting: 80-100%+

#### Model 2: Anomaly Detector

| Aspekt | Detaljer |
|--------|----------|
| **Opgave** | Unsupervised anomalidetektion |
| **Algoritme** | IsolationForest med StandardScaler |
| **Features** | air_temp_c, humidity_pct, light_lux, ph, ec_ms_cm, water_temp_c, water_level_pct |
| **Contamination** | 5% forventet outlier-ratio |
| **Fallback** | To-lags regelbaseret threshold-system |
| **Severity** | low, medium, high, critical |
| **Output** | is_anomalous, anomaly_score (0-1), per-feature anomaly detaljer med laesbare beskeder |

**Sensor Ranges:**

| Sensor | Safe Range | Warning Range |
|--------|-----------|---------------|
| air_temp_c | 5-40 | 12-32 |
| humidity_pct | 20-95 | 35-85 |
| light_lux | 0-100k | 1k-50k |
| pH | 4.0-9.0 | 5.0-7.5 |
| ec_ms_cm | 0-6.0 | 0.3-4.0 |
| water_temp_c | 10-35 | 15-28 |
| water_level_pct | 10-100 | 25-95 |

#### Model 3: Crop Compatibility Clustering

| Aspekt | Detaljer |
|--------|----------|
| **Opgave** | Unsupervised clustering: grupper afgroder der kan dele reservoir |
| **Algoritme** | AgglomerativeClustering (Ward linkage) |
| **Features** | Per-afgrode empiriske profiler: mean/std af pH, EC, vandtemp, lufttemp, fugtighed, lys |
| **Profil-konstruktion** | Kun "god vækst"-vinduer (over-median vaekstrate) |
| **Optimal k** | Auto-valgt via silhouette score (k=2..8) |
| **Output** | Cluster labels, NxN kompatibilitetsmatrix, per-cluster anbefalede setpoints |

#### Model 4: Reservoir Drift Forecaster

| Aspekt | Detaljer |
|--------|----------|
| **Opgave** | Multi-output regression: forudsig reservoir-vaerdier ved t+1h, t+6h, t+24h |
| **Algoritme** | MultiOutputRegressor med XGBRegressor/LGBMRegressor |
| **Targets** | pH, EC, vandtemp, vandniveau x 3 horisonter = **12 target-kolonner** |
| **Features** | Lag-features (1h-24h), rate-of-change, rolling 6h std, 12h lineaer trend, pumpe duty cycles, time-of-day |
| **Output** | Per-metric forecasts + time_to_threshold_hours |

#### Model 5: Nutrient Consumption Predictor

| Aspekt | Detaljer |
|--------|----------|
| **Opgave** | Multi-output regression: forudsig depletionsrater (delta/time) |
| **Algoritme** | MultiOutputRegressor med XGBoost/LightGBM |
| **Targets** | pH_delta, ec_delta, vandtemp_delta, vandniveau_delta per time |
| **Noegletilgang** | **Filtrerer doserings-pumpe-vinduer ud** — laerer kun naturlige organiske forbrugsmonstre |
| **Output** | rate_per_hour, hours_until_critical, water_change_recommended_in_days |

### 6.2 ML API Endpoints (20+)

| Endpoint | Method | Formaal |
|----------|--------|---------|
| `/api/predict/growth` | POST | Enkelttaarns-vaekstprediktion |
| `/api/predict/growth/batch` | POST | Batch-prediktioner |
| `/api/predict/growth/{tower_id}` | GET | Prediktion fra MongoDB twin state |
| `/api/detect/anomaly` | POST | Anomalidetektion pa raa telemetri |
| `/api/detect/anomaly/{tower_id}` | GET | Detektion fra seneste MongoDB data |
| `/api/predict/drift` | POST | Reservoir metric drift |
| `/api/predict/drift/{coord_id}` | GET | Drift fra MongoDB data |
| `/api/predict/consumption` | POST | Depletionsrater |
| `/api/predict/consumption/{coord_id}` | GET | Forbrug fra MongoDB data |
| `/api/clustering/compatibility` | GET | NxN kompatibilitetsmatrix |
| `/api/clustering/clusters` | GET | Cluster-tildelinger + setpoints |
| `/api/clustering/recommend` | POST | Anbefal reservoir-gruppering |
| `/api/clustering/score` | GET | Parvise kompatibilitetsscores |
| `/api/conditions/optimal` | GET | Optimale betingelser pr. afgrode |
| `/api/conditions/crops` | GET | Liste over supporterede afgroder |
| `/api/twins/sync` | POST | Synk ML-prediktioner til twin |
| `/api/models/status` | GET | Model traening-status + data readiness |
| `/health` | GET | Health check med service-status |

### 6.3 Feature Engineering Pipeline

Central `FeatureEngineer`-klasse med fire specialiserede builders:

1. **`build_crop_profiles()`**: Per-afgrode profiler fra hojdemaalinger + telemetri
2. **`build_drift_features()`**: Lag-features, rate-of-change, rolling stats, trend slopes
3. **`build_consumption_features()`**: Delta-beregning med doserings-vindue filtrering
4. **`build_growth_features()`**: 24h rolling averages, vaekststadie-inference, interaktioner

### 6.4 MLOps Infrastruktur

| Komponent | Formaal |
|-----------|---------|
| `ModelManager` | Pickle-baseret model serialisering med JSON metadata og versionering |
| `FeatureStore` | Parquet-baseret disk-cache med SHA-256 nogler og TTL (6 timer default) |
| `MongoDBConnector` | Data-adgang til telemetri, hojdemaalinger og twin-dokumenter |
| `MQTTSubscriber` | Realtids MQTT streaming med buffered batch-retrieval |
| `ADTConnector` | Azure Digital Twins CRUD og batch-update af ML-prediktioner |

### 6.5 Data Readiness Assessment

Automatisk vurdering af hvornaar der er nok data til at traene modeller:

| Model | Minimum Krav |
|-------|-------------|
| Clustering | >= 50 hojdemaalinger + >= 3 afgrodetyper |
| Drift Forecasting | >= 2000 reservoir-telemetri records (~2 uger) |
| Growth Prediction | >= 50 hojdemaalinger + >= 100 tower-telemetri records |
| Consumption | >= 500 reservoir-telemetri records (~1 uge) |

### 6.6 Inference Strategi

Alle prediktorer folger **ML-first med regelbaseret fallback**:

1. Tjek om traenet model (.pkl) eksisterer pa disk
2. Hvis ja -> ML-inference (hojere confidence: 0.75-0.85)
3. Hvis nej eller fejl -> regelbaseret logik (lavere confidence: 0.20-0.65)

Dette sikrer at systemet **altid er funktionelt** selv foer nogen model er traenet.

### 6.7 DTDL Models (Azure Digital Twins)

Fire DTDL v2-interfaces definerer twin-grafen:

| Interface | Properties |
|-----------|-----------|
| **Farm** | name, location (lat/lon/address), timezone, `hasCoordinator` relation |
| **Coordinator** | mac_address, firmware, status, wifi_rssi, reported_state, `ml_predictions`, `hasTower`+`hasReservoir` relationer |
| **Tower** | crop_type (10 enum), reported_state, desired_state, growth_tracking, `ml_predictions` (predicted_height, harvest_date, health_score, anomaly_score, recommendations) |
| **Reservoir** | capacity, reported_state (pH, EC, TDS, vandtemp, pumper), setpoints, alerts, `ml_predictions` (drift, water change, nutrient top-up) |

---

## 7. Infrastruktur og DevOps

### 7.1 Docker Compose — Production Stack

**6 services** pa en bridge network (`iot-network`) med 5 named volumes:

| Service | Image | Port | Health Check |
|---------|-------|------|-------------|
| `mongodb` | mongo:7.0 | 27017 | mongosh ping |
| `mosquitto` | eclipse-mosquitto:2.0 | 1883, 9001 | mosquitto_sub med auth |
| `backend` | Custom (.NET 8) | 8000 | curl /health/live |
| `frontend` | Custom (nginx) | 4200->80 | wget --spider |
| `ml-api` | Custom (Python 3.11) | 8001->8000 | curl /health |
| `ml-jupyter` | Custom (JupyterLab) | 8888 | Kun med `--profile dev` |

### 7.2 Docker Compose — Simulation Stack

**Fuldstaendig isoleret** fra dev-stacken med separate containere og offset porte:

| Service | Port | Formaal |
|---------|------|---------|
| mongodb-sim | 27018 | Isoleret database |
| mosquitto-sim | 1884 | Isoleret broker |
| backend-sim | 8010 | Isoleret API |
| simulator | - | Python test runner |

### 7.3 Dockerfiles (5)

| Dockerfile | Multi-stage | Sikkerhed | Formaal |
|------------|-------------|-----------|---------|
| Backend | SDK 8.0 -> ASP.NET 8.0 | Non-root `appuser` | API server |
| Backend Tests | SDK 8.0 | Testcontainers support | Integration tests |
| Frontend | Node 20 -> nginx Alpine | - | SPA med reverse proxy |
| ML | Python 3.11-slim | - | FastAPI + Jupyter |
| Simulator | Python 3.13-slim | - | Test-koersel |

### 7.4 CI/CD Workflows (GitHub Actions)

#### `integration-tests.yml`

**Trigger:** Push/PR til `main`/`develop`

| Job | Timeout | Formaal |
|-----|---------|---------|
| `backend-tests` | 30 min | .NET integration tests med Testcontainers, code coverage (Cobertura) |
| `frontend-e2e-tests` | 30 min | Playwright E2E tests med MongoDB + Mosquitto services |
| `full-stack-test` | 20 min | Docker Compose smoke tests |
| `test-summary` | - | Aggregeret statusrapport |

**Artefakter:** backend-test-results (TRX), backend-coverage (Cobertura), playwright-report

#### `simulation-tests.yml`

**Trigger:** Push/PR til `main`/`develop` ved aendringer i `tools/simulator/**`, `Backend/src/**`, eller `docker-compose.simulation.yml`

- Docker Buildx med layer-caching
- Service health verifikation
- pytest med JSON-rapportering og timeout
- Log-capture ved fejl

### 7.5 Scripts (10)

| Script | Formaal |
|--------|---------|
| `build.sh` / `build.bat` | PlatformIO firmware build (coordinator + node), valgfri `--package` |
| `upload.sh` / `upload.bat` | Firmware upload + serial monitor med auto port-detektion |
| `check-health.ps1` | PowerShell polling: retry GET pa /health op til 20 gange |
| `seed-data.js` | MongoDB seed: 1 farm, 2 coordinator twins, 4 tower twins med fuld digital twin |
| `insert-coordinator.js` | Quick single coordinator insert |
| `setup-adt.ps1` | Azure Digital Twins provisionering (resource group, ADT instance, DTDL upload) |
| `render-diagrams.ps1` | PlantUML -> PNG rendering via Docker |
| `dotnet-install.ps1` | .NET SDK installation (CI-brug) |

### 7.6 MQTT Broker Konfiguration

```
listener 1883           # Standard MQTT
listener 9001           # WebSocket transport
allow_anonymous false   # Autentifikation pakraevet
password_file /mosquitto/config/pwfile
persistence true
max_connections -1      # Ubegrаenset
max_inflight_messages 20
max_queued_messages 1000
max_keepalive 60
```

---

## 8. Simulator

**Placering:** `tools/simulator/`
**Sprog:** Python 3.13

### 8.1 Arkitektur

```
tools/simulator/
|-- run.py                     # CLI entry point (argparse)
|-- core/
|   |-- models.py              # Farm, Coordinator, Tower, Reservoir dataklasser + 9 crop configs
|   |-- topology.py            # Deterministisk farm-topologi (default: 5x5x10 = 250 towers)
|   |-- physics.py             # Stateless sensor-fysik
|   |-- publisher.py           # MQTT publisher + REST bootstrapper
|-- scenarios/                 # 13 scenarier
|-- tests/                     # 6 testmoduler (30 tests)
```

### 8.2 Fysikmotor

| Sensor | Model |
|--------|-------|
| Temperatur | Sinusoidal dag/nat-cyklus med crop-specifik variance |
| Fugtighed | Invers temperatur-fugtigheds-forhold |
| Lys | 16/8 grow light schedule med rampe |
| pH | Drift fra rod-exudater + random walk |
| EC | Depletion pr. crop-type |
| Vandniveau | Forbrug pr. tower |
| Plantevakst | Sigmoid vaekstkurver med crop-specifikke parametre |

### 8.3 Scenarier (13)

| Scenarie | Formaal |
|----------|---------|
| `steady_state` | Normal drift |
| `ph_drift` | pH-forsuring krise |
| `nutrient_depletion` | Naeringsstof-udtoemning |
| `heat_stress` | Varmestress |
| `water_emergency` | Vandmangel |
| `tower_pairing` | Pairing workflow |
| `crop_conflict` | Afgrode-kompatibilitetsproblemer |
| `growth_cycle` | Fuld vaekstcyklus |
| `reconnection` | Forbindelsesproblemer |
| `full_demo` | 15-min demo-sekvens |
| `scale_test` | 1000+ tower stress test |
| `lwt_disconnect` | LWT mekanisme-test |
| `alert_cascade` | Threshold-overskridelse kaskade |

### 8.4 Crop Database (9 afgroder med optimale ranges)

Lettuce, Basil, Spinach, Kale, Tomato, Pepper, Strawberry, Mint, Cilantro

---

## 9. Test og Kvalitetssikring

### 9.1 Backend Unit Tests (7 filer)

| Testfil | Daekker |
|---------|---------|
| `MqttBridgeHandlerTests` | WebSocket-to-MQTT bridge |
| `CoordinatorRegistrationServiceTests` | Registrerings-workflow |
| `TwinServiceTests` | Digital Twin desired/reported/delta |
| `AdtTwinMapperTests` | Azure DT mapping |
| `MlServiceTests` | ML service proxy |
| `PairingServiceTests` | Pairing session management |
| `AlertServiceTests` | Alert creation, dedup, auto-resolve |

### 9.2 Backend Integration Tests (9 filer)

| Testfil | Daekker |
|---------|---------|
| `TwinIntegrationTests` | Twin CRUD via API |
| `TowerIntegrationTests` | Tower CRUD + kommandoer |
| `TelemetryIntegrationTests` | Telemetri persistering og queries |
| `SettingsIntegrationTests` | Settings CRUD |
| `PairingIntegrationTests` | End-to-end pairing flow |
| `OtaIntegrationTests` | OTA job creation og status |
| `HealthIntegrationTests` | Health endpoints |
| `CoordinatorIntegrationTests` | Coordinator CRUD + registrering |
| `ApiCrudTests` | Generelle CRUD operationer |

**Test Infrastruktur:**
- **Testcontainers** (MongoDB) for realistisk database-testing
- **WebApplicationFactory** for in-process API testing
- **MQTTnet** test-klient for MQTT integration
- **FluentAssertions** for laesbare assertions
- **Coverlet** for code coverage

### 9.3 Frontend Unit Tests (8 spec-filer)

| Testfil | Daekker |
|---------|---------|
| `AlertService.spec` | 25+ tests: lifecycle, filtrering, statistik |
| `SnakeCaseInterceptor.spec` | 30 tests: camelCase <-> snake_case transformation |
| `ModelUtils.spec` | Model utility functions |
| `ThemeService.spec` | Tema-skift |
| `SidebarService.spec` | Sidebar state |
| `ConfirmDialogService.spec` | Dialog lifecycle |
| `NotificationService.spec` | Notifikationer |
| `LogStorageService.spec` | Log persistering |

### 9.4 Frontend E2E Tests (Playwright)

- Dashboard navigation
- Pairing workflow
- Visual regression
- Real coordinator pairing (integrations-test)

### 9.5 Simulator Tests (30 tests, 6 moduler)

| Modul | Tests | Daekker |
|-------|-------|---------|
| `test_01_connectivity` | 8 | MQTT/REST smoke tests |
| `test_02_telemetry` | 5 | Data flow validering |
| `test_03_lwt_disconnect` | 3 | Last Will and Testament |
| `test_04_alert_cascade` | 8 | Alert threshold tests |
| `test_05_pairing` | 4 | Pairing workflow |
| `test_06_scale` | 2 | Throughput tests |

### 9.6 Firmware Tests

| Testfil | Daekker |
|---------|---------|
| `test_coordinator.cpp` | Coordinator grundlaeggende funktionalitet |
| `test_espnow_message.cpp` | ESP-NOW besked serialisering/deserialisering |

### 9.7 ML Tests

**Status:** Ingen testfiler eksisterer endnu. pytest er i requirements men ingen tests er skrevet.

---

## 10. Dokumentation

| Dokument | Formaal |
|----------|---------|
| `README.md` | Projekt-oversigt med arkitektur, API-docs, quick start |
| `docs/PROPOSAL.md` | Projektforslag |
| `docs/simulation-howto.md` | 353-linjers guide til simulator |
| `docs/simulation-test-report.md` | Detaljeret 30-test rapport |
| `docs/simulation-bottleneck-analysis.md` | Performance-analyse |
| `docs/layer-integration-rapport.md` | Integrationslag-dokumentation |
| `docs/api-collection.json` | Postman/Insomnia API collection |
| `docs/plans/` | Implementeringsplaner (phase-a, frontend-polish, remove-mock-data) |
| `docs/report/` | Akademisk rapport (Architecture Analysis, Design & Implementation, Discussions & Conclusion, Backend Migration) |
| `docs/firmware/` | Build rapport, konfigurationsrefactoring, forbedringer |
| `docs/pairing/` | Pairing arkitektur v2 + PlantUML sekvensdiagram |
| `docs/logging/` | Logging strategi + quick reference |
| `.github/copilot-instructions.md` | AI-agent retningslinjer for kodebasen |

---

## 11. Opsummering

### Implementeringsstatus

| Lag | Status | Hovedfunktioner |
|-----|--------|----------------|
| **Firmware (Coordinator)** | Fuldt implementeret | WiFi, MQTT, ESP-NOW hub, mmWave radar, ambient light, LED-styring, pairing, OTA, serial console, thermal control |
| **Firmware (Node)** | Fuldt implementeret | ESP-NOW med kanal-hopping, TMP117 sensor, RGBW LED (4px), knap, pairing FSM, auto-reconnect, standalone mode |
| **Firmware (Shared)** | Fuldt implementeret | 20+ beskedtyper (JSON + binary), ConfigManager, ConfigStore |
| **Backend API** | Fuldt implementeret | 50+ REST endpoints, 12+ MongoDB collections, 11 MQTT subscriptions, WebSocket broadcast, Digital Twin, Alert system, ML proxy, Diagnostics |
| **Backend Tests** | Omfattende | 7 unit test filer, 9 integration test filer med Testcontainers |
| **Frontend** | Fuldt implementeret | 18+ sider, 17 services, 20+ UI-komponenter, 3D visualisering, realtids-charts, Digital Twin UI, Alert dashboard, OTA management |
| **Frontend Tests** | Delvist | 8 unit spec filer, Playwright E2E |
| **ML Service** | Fuldt implementeret | 5 ML-modeller, 20+ API endpoints, feature engineering, MLOps infra, regelbaseret fallback |
| **ML Tests** | Ikke implementeret | Ingen testfiler |
| **Simulator** | Fuldt implementeret | 13 scenarier, fysikmotor, 9 crop-typer, REST bootstrapping, 30 tests |
| **DevOps** | Fuldt implementeret | 5 Dockerfiles, 2 Docker Compose configs, 2 CI/CD workflows, 10 scripts |
| **Dokumentation** | Omfattende | Akademisk rapport, API collection, simulator guide, arkitekturdokumentation |

### Teknologistakken

| Kategori | Teknologier |
|----------|------------|
| **Firmware** | ESP32-S3, ESP32-C3, C++17, PlatformIO, Arduino ESP32 3.0.0, ArduinoJson, ESP-NOW v2, MQTT, NeoPixel, TSL2561, LD2450, TMP117 |
| **Backend** | .NET 8, ASP.NET Core, C# 12, MongoDB.Driver, MQTTnet 4.3.7, FluentValidation, Serilog |
| **Frontend** | Angular 19.2, TypeScript, TailwindCSS 3.4, Spartan UI, Three.js 0.180, ECharts 6, BabylonJS 8.38 |
| **ML** | Python 3.11, FastAPI, scikit-learn, XGBoost, LightGBM, pandas, numpy, scipy |
| **Database** | MongoDB 7.0 med TTL-indekser og compound-indekser |
| **Messaging** | Eclipse Mosquitto 2.0 (MQTT + WebSocket transport) |
| **Containerization** | Docker, Docker Compose, multi-stage builds |
| **CI/CD** | GitHub Actions, Testcontainers, Playwright, pytest |
| **Cloud (valgfri)** | Azure Digital Twins med DTDL v2 modeller |

### Kodebase-statistik (estimeret)

| Lag | Filer | Estimeret LOC |
|-----|-------|---------------|
| Firmware (coordinator) | ~37 | ~8,000 |
| Firmware (node) | ~20 | ~3,000 |
| Firmware (shared) | 7 | ~1,700 |
| Backend (source) | ~70 | ~12,000 |
| Backend (tests) | ~20 | ~3,000 |
| Frontend (source) | ~100 | ~15,000 |
| Frontend (tests) | ~12 | ~1,500 |
| ML (source) | 17 | ~5,800 |
| Simulator | ~20 | ~3,000 |
| DevOps/Config | ~15 | ~1,500 |
| **Total** | **~318** | **~54,500** |
