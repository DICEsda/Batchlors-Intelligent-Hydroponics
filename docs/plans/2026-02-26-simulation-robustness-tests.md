# Simulation Robustness Test Suite — Implementation Plan

**Goal:** Prove bidirectional data flow (upstream + downstream) end-to-end through comprehensive integration tests, fix 2 blocking backend bugs, add DeviceSimulator and WebSocket test infrastructure, and deliver 42 scenario-driven tests across 6 new modules.

**Architecture:** Fix TwinService MQTT topic mismatch and AlertService pump false-positive first. Add DeviceSimulator (subscribes to downstream MQTT commands) and WebSocketTestClient (connects to /ws/broadcast). Then 6 test modules: downlink roundtrip, alert full lifecycle, WebSocket broadcast, CRUD+validation, OTA flow, and stress resilience. Each test exercises multiple subsystems simultaneously.

**Tech Stack:** Python 3.13 (pytest, paho-mqtt, websockets, requests), ASP.NET Core 8 (C#), Docker Compose simulation environment.

**Addresses GitHub Issues:** #68, #103, #104, #105, #106, #107, #109, #110, #111, #114, #115

---

## Parallelism Map

```
Phase 1 — Bug Fixes (parallel):
  Task 1: Fix TwinService MQTT topics (#106)
  Task 2: Fix AlertService pump false-positive (#68)

Phase 2 — Test Infrastructure (parallel):
  Task 3: DeviceSimulator class (#103)
  Task 4: WebSocketTestClient class (#104)
  Task 5: Add websockets dependency

Phase 3 — Test Modules (sequential, each builds on prior):
  Task 6: test_07_downlink_roundtrip.py (#105, #106)
  Task 7: test_08_alert_full_lifecycle.py (#107, #68)
  Task 8: test_09_websocket_broadcast.py (#109, #104)
  Task 9: test_10_crud_and_validation.py (#110)
  Task 10: test_11_ota_flow.py (#111)
  Task 11: test_12_stress_resilience.py

Phase 4 — CI:
  Task 12: Update simulation CI workflow (#114)
```

Tasks 1-2 are independent. Tasks 3-5 are independent. Tasks 6-11 depend on 1-5.

---

## Phase 1 — Bug Fixes

### Task 1: TwinService MQTT Topic Mismatch (#106)
**File:** `Backend/src/IoT.Backend/Services/TwinService.cs`
- Line 416-417: Changed hardcoded `$"hydro/{twin.FarmId}/{twin.CoordId}/tower/{twin.TowerId}/cmd"` to `MqttTopics.TowerCmd(twin.FarmId, twin.CoordId, twin.TowerId)`
- Line 435-436: Changed hardcoded `$"hydro/{twin.FarmId}/{twin.CoordId}/cmd"` to `MqttTopics.CoordinatorCmd(twin.FarmId, twin.CoordId)`
- **Root cause:** The twin sync code used a `hydro/` prefix instead of `farm/` and was missing the `coord/` path segment, so all downstream twin commands were silently lost.

### Task 2: AlertService Pump False-Positive (#68)
**File:** `Backend/src/IoT.Backend/Services/AlertService.cs`
- Lines 154-166: Removed the `if (coordinator.MainPumpOn == false)` block that falsely triggered `pump_failure` critical alerts when the pump was simply OFF during normal scheduled downtime.
- Added unconditional `AutoResolveAlertAsync` to clean up any pre-existing false-positive alerts.

---

## Phase 2 — Test Infrastructure

### Task 3: DeviceSimulator (#103)
**File:** `tools/simulator/tests/device_simulator.py` (271 lines)
- Simulates an ESP32 device subscribing to all 8 downstream MQTT command topics
- Topics: `tower/+/cmd`, `reservoir/cmd`, `coordinator/cmd`, `coordinator/config`, `ota/start`, `ota/cancel`, `coordinator/registered`, `farm/.../cmd`
- API: `start()`, `stop()`, `wait_for_command(topic_filter, timeout)`, `get_commands()`, `clear()`, `publish_telemetry_ack()`, `publish_reservoir_telemetry_ack()`, `publish_ota_status()`

### Task 4: WebSocketTestClient (#104)
**File:** `tools/simulator/tests/ws_client.py` (208 lines)
- Real WebSocket client connecting to `/ws/broadcast` using `websockets` library
- Runs async receive loop in a background daemon thread
- Synchronous API: `wait_for_message(type_filter, timeout)`, `get_messages()`, `clear()`, `send_subscribe()`, `disconnect()`

### Task 5: Dependency Update
**File:** `tools/simulator/requirements.txt`
- Added `websockets>=12.0,<14.0`

---

## Phase 3 — Test Modules (42 tests total)

### Module 7: Downlink Roundtrip (test_07) — 9 tests
**File:** `tools/simulator/tests/test_07_downlink_roundtrip.py` (347 lines)
**Issues:** #105, #106

| Class | Test | What It Proves |
|-------|------|----------------|
| TestTowerCommands | test_light_command_roundtrip | REST → MQTT → DeviceSimulator receives light cmd |
| TestTowerCommands | test_pump_command_roundtrip | REST → MQTT → DeviceSimulator receives pump cmd |
| TestReservoirCommands | test_reservoir_pump_command | REST → MQTT → reservoir pump delivered |
| TestReservoirCommands | test_reservoir_dosing_command | REST → MQTT → dosing delivered |
| TestCoordinatorCommands | test_coordinator_restart | REST → MQTT → coordinator restart delivered |
| TestTwinDesiredReportedSync | test_desired_state_persisted | Twin desired state written to DB via REST |
| TestTwinDesiredReportedSync | test_delta_when_desired_diverges | Twin delta computed when desired != reported |
| TestTwinDesiredReportedSync | test_sequential_commands_ordered | Multiple commands arrive in order |
| TestTwinDesiredReportedSync | test_command_acknowledged | Command + ack cycle |

### Module 8: Alert Full Lifecycle (test_08) — 8 tests
**File:** `tools/simulator/tests/test_08_alert_full_lifecycle.py` (421 lines)
**Issues:** #107, #68

| Class | Test | What It Proves |
|-------|------|----------------|
| TestMultiAlertCascade | test_reservoir_multi_alert_cascade | Single bad reservoir msg → pH + water alerts |
| TestMultiAlertCascade | test_tower_multi_alert_cascade | Single bad tower msg → temp + battery alerts |
| TestNewAlertTypes | test_temperature_low_alert | Low temp triggers alert (previously untested) |
| TestNewAlertTypes | test_battery_low_alert | Low battery triggers alert (previously untested) |
| TestPumpFalsePositiveRegression | test_pump_off_no_false_alert | Pump OFF → NO alert (regression for #68) |
| TestAlertAcknowledgeResolveLifecycle | test_full_alert_state_machine | create → acknowledge → resolve |
| TestAlertDeduplicationExtended | test_five_violations_one_alert | 5 consecutive violations = 1 alert |
| TestAlertDeduplicationExtended | test_rapid_trigger_resolve_retrigger | trigger → resolve → retrigger cycle |

### Module 9: WebSocket Broadcast (test_09) — 6 tests
**File:** `tools/simulator/tests/test_09_websocket_broadcast.py` (338 lines)
**Issues:** #109, #104

| Class | Test | What It Proves |
|-------|------|----------------|
| TestTelemetryBroadcast | test_tower_telemetry_broadcast | MQTT telemetry → WS broadcast |
| TestTelemetryBroadcast | test_reservoir_telemetry_broadcast | Reservoir telemetry → WS broadcast |
| TestConnectionStatusBroadcast | test_connection_status | Coordinator connect/disconnect → WS |
| TestRegistrationBroadcast | test_registration_flow | Announce → WS request → approve → WS registered |
| TestDiagnosticsStream | test_diagnostics_update | Periodic diagnostics arrive at WS |
| TestMultiClientBroadcast | test_multi_client_fanout | 3 WS clients all receive same messages |

### Module 10: CRUD & Validation (test_10) — 9 tests
**File:** `tools/simulator/tests/test_10_crud_and_validation.py` (273 lines)
**Issue:** #110

| Class | Test | What It Proves |
|-------|------|----------------|
| TestFarmLifecycle | test_create_read_update_delete | Full CRUD cycle for farms |
| TestCoordinatorMetadata | test_read_coordinator | GET coordinator by ID |
| TestCoordinatorMetadata | test_update_coordinator | PATCH coordinator metadata |
| TestCoordinatorMetadata | test_list_coordinators | GET all coordinators |
| TestTowerCrudWithTelemetry | test_create_tower_then_telemetry | Create tower → MQTT data → REST verify + twin |
| TestAuthenticationAndValidation | test_missing_api_key_401 | No API key → 401 |
| TestAuthenticationAndValidation | test_invalid_input_400 | Bad input → 400 |
| TestAuthenticationAndValidation | test_nonexistent_resource_404 | Missing resource → 404 |
| TestAuthenticationAndValidation | test_malformed_json_400 | Garbage JSON → 400 |

### Module 11: OTA Flow (test_11) — 5 tests
**File:** `tools/simulator/tests/test_11_ota_flow.py` (281 lines)
**Issue:** #111

| Class | Test | What It Proves |
|-------|------|----------------|
| TestOtaJobLifecycle | test_ota_start_delivered | REST start → MQTT delivery |
| TestOtaJobLifecycle | test_ota_progress_tracking | Progress updates tracked via REST |
| TestOtaJobLifecycle | test_ota_completion | Full OTA success cycle |
| TestOtaJobLifecycle | test_ota_cancellation | Cancel mid-flight |
| TestOtaJobLifecycle | test_ota_failure_handling | Firmware reports failure → REST reflects |

### Module 12: Stress & Resilience (test_12) — 5 tests
**File:** `tools/simulator/tests/test_12_stress_resilience.py` (454 lines)

| Class | Test | What It Proves |
|-------|------|----------------|
| TestSustainedLoad | test_100_towers_60s | 100 towers × 60s (~6,600 msgs) no drops |
| TestMalformedPayloads | test_malformed_then_valid | 10 bad msgs + valid → backend survives |
| TestRapidConnectDisconnect | test_client_churn | 5×5 rapid MQTT connect/disconnect |
| TestConcurrentMixedOperations | test_mixed_concurrent | 3 threads: telemetry + REST + pairing |
| TestBurstRecovery | test_500_burst_recovery | 500 msgs in <2s → drain → verify |

---

## Phase 4 — CI Workflow Update (#114)

**File:** `.github/workflows/simulation-tests.yml`
- Job timeout: 30 → 45 minutes
- Test step timeout: 15 → 25 minutes
- Per-test timeout: 120s → 300s (stress tests need headroom)

---

## Test Count Summary

| Module | Tests | Coverage |
|--------|-------|----------|
| test_01 through test_06 (existing) | 30 | Upstream telemetry, registration, alerts, twin |
| test_07_downlink_roundtrip | 9 | Downstream commands, twin sync |
| test_08_alert_full_lifecycle | 8 | Multi-alert, dedup, state machine, regression |
| test_09_websocket_broadcast | 6 | Real-time WS fan-out, all event types |
| test_10_crud_and_validation | 9 | REST CRUD, auth, error codes |
| test_11_ota_flow | 5 | OTA lifecycle end-to-end |
| test_12_stress_resilience | 5 | Load, malformed data, churn, burst |
| **TOTAL** | **72** | **Full bidirectional coverage** |
