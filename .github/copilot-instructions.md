# AI Coding Agent Instructions for Intelligent Hydroponics

Purpose: keep the ESP32 firmware, ASP.NET Core backend, ML service, and Angular UI moving fast without breaking the tight real-time control loops. Favor incremental fixes, follow existing patterns, and document any workflow changes.

## Architecture snapshot
- Coordinator (ESP32-S3, PlatformIO/Arduino) orchestrates ESP-NOW tower nodes, MQTT uplink, Wi-Fi setup, and serial diagnostics; entry point lives in `firmware/coordinator/src/core/Coordinator.*`.
- Tower nodes (ESP32-C3/C6) share code via `firmware/shared/` and talk ESP-NOW only; backend (`backend/src/IoT.Backend`) and Angular frontend (`frontend/src/app`) consume/publish MQTT + WebSockets.
- ML service (`ml/src/`) provides anomaly detection, crop clustering, drift forecasting via FastAPI.
- Data path: Tower telemetry -> ESP-NOW -> Coordinator -> MQTT (`farm/{farmId}/coord/{coordId}/...`) -> backend -> MongoDB + WebSocket broadcast -> frontend dashboard.

## Project structure
- `backend/` — ASP.NET Core 8 API (C#), MongoDB, MQTT bridge, Digital Twin service
- `frontend/` — Angular 19 dashboard with Tailwind, ECharts, Three.js, spartan-ng UI
- `firmware/coordinator/` — ESP32-S3 coordinator firmware
- `firmware/node/` — ESP32-C3/C6 tower node firmware
- `firmware/shared/` — Shared ESP-NOW message types and config
- `ml/` — Python FastAPI ML service (scikit-learn models)
- `tools/simulator/` — Python telemetry simulator for testing
- `docs/` — Unified documentation (plans, reports, architecture, logging, pairing)
- `docs/diagrams/` — PlantUML architecture diagrams

## Coordinator subsystems
- Startup (`src/main.cpp`): Serial banner -> `Logger::begin` -> NVS init -> `coordinator.begin()` -> tight loop with `delay(1)`.
- Subsystems follow the `*Manager` convention (`EspNow`, `Mqtt`, `WifiManager`, `TowerRegistry`, `ZoneControl`, `ThermalControl`, `ButtonControl`, `MmWave`, etc.); each allocates in `Coordinator::begin()` and exposes `loop()` ticks.
- `WifiManager` handles stored credentials + interactive serial provisioning, reconnect backoff, and offline mode.
- `Mqtt` depends on `WifiManager`, loads broker/farm IDs from ConfigManager, publishes coordinator/tower telemetry, and listens for `/cmd` payloads.

## Pairing & telemetry conventions
- Coordinator boots in normal mode; pairing opens via touch button short-press or MQTT command.
- ESP-NOW pairing callback stores towers in NVS (`TowerRegistry`), adds peers, and auto-closes the window.
- MQTT topic structure:
  - `farm/{farmId}/coord/{coordId}/telemetry` — coordinator sensors
  - `farm/{farmId}/coord/{coordId}/reservoir/telemetry` — water quality sensors
  - `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry` — tower DHT22/light/actuators
  - `farm/{farmId}/coord/{coordId}/tower/{towerId}/status` — tower online/offline
  - `farm/{farmId}/coord/{coordId}/pairing/*` — pairing workflow
  - `.../cmd` topics deliver downlink commands. Keep schema additive.

## Developer workflows
- Build & flash coordinator: `cd firmware/coordinator && pio run -e esp32-s3-devkitc-1 -t upload -t monitor`
- Build tower nodes: `cd firmware/node && pio run -e esp32-c3-mini-1`
- Backend: `cd backend/src/IoT.Backend && dotnet run`
- Frontend: `cd frontend && npm install && npm start`
- ML service: `cd ml && pip install -r requirements.txt && uvicorn src.api.main:app`
- Simulator: `cd tools/simulator && python run.py --scenario steady-state`
- Docker: `docker compose up -d` (full stack)

## Guardrails & best practices
- Never double-call `Logger::begin`; adjust verbosity via `Logger::setMinLevel` instead.
- When touching messaging schemas or MQTT topics, audit the docs + frontend expectations; prefer new optional fields over breaking changes.
- Keep ESP-NOW channel/power tweaks inside `EspNow`. Don't reconfigure Wi-Fi elsewhere.
- Use `ConfigManager` namespaces (`"wifi"`, `"mqtt"`, etc.) for persistence.
- Backend follows repository pattern with DI. Services are registered in `Program.cs`.
- Frontend uses standalone Angular components with signals and zoneless change detection.
- For new hardware features, follow the manager pattern: allocate in `Coordinator::begin`, gate failures with `Logger::error`, and add a `loop()` pump plus serial/MQTT observability.
