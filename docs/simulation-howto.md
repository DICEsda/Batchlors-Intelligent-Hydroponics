# Simulation Testing How-To

Guide for running the simulation integration test suite against the isolated Docker environment. Covers environment setup, running tests, understanding each test module, interpreting results, and adding new tests.

---

## Prerequisites

- Docker Desktop (with Docker Compose v2)
- No other services need to be running; the simulation environment is fully isolated

## Quick Start

```bash
# 1. Start the isolated simulation environment
docker compose -f docker-compose.simulation.yml up -d --build

# 2. Wait for all services to be healthy (backend takes ~30s)
docker compose -f docker-compose.simulation.yml ps

# 3. Run the full test suite
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/ -v --tb=short --timeout=120

# 4. Tear down when done
docker compose -f docker-compose.simulation.yml down -v
```

## Environment Architecture

The simulation environment runs four containers on an isolated Docker network:

```
sim-network (bridge)
  |
  +-- mongodb-sim     (mongo:7.0)         port 27018 -> 27017
  +-- mosquitto-sim   (eclipse-mosquitto)  port 1884  -> 1883
  +-- backend-sim     (ASP.NET Core 8)     port 8010  -> 8000
  +-- simulator       (Python 3.13)        test runner
```

All inter-container communication uses Docker internal DNS names (`mosquitto-sim`, `backend-sim`, `mongodb-sim`). The external port mappings (27018, 1884, 8010) are for optional local debugging only.

### Environment Variables

Tests read configuration from environment variables with defaults for Docker internal networking:

| Variable | Default | Description |
|----------|---------|-------------|
| `SIM_MQTT_HOST` | `mosquitto-sim` | MQTT broker hostname |
| `SIM_MQTT_PORT` | `1883` | MQTT broker port |
| `SIM_MQTT_USER` | `user1` | MQTT authentication username |
| `SIM_MQTT_PASS` | `user1` | MQTT authentication password |
| `SIM_API_URL` | `http://backend-sim:8000` | Backend REST API base URL |

To run tests against a local (non-Docker) environment, override these:

```bash
SIM_MQTT_HOST=localhost SIM_MQTT_PORT=1884 SIM_API_URL=http://localhost:8010 \
  python -m pytest tests/ -v
```

---

## Test Suite Structure

All tests live in `tools/simulator/tests/`. They execute in filename order (01 through 06), which matters because later tests depend on the `bootstrap_farm` fixture created during the first test that requests it.

```
tools/simulator/tests/
  __init__.py
  conftest.py                  # Shared fixtures and helpers
  test_01_connectivity.py      # Smoke tests
  test_02_telemetry.py         # Data flow verification
  test_03_lwt_disconnect.py    # LWT disconnect behavior
  test_04_alert_cascade.py     # Alert threshold verification
  test_05_pairing.py           # Tower pairing flow
  test_06_scale.py             # Throughput sanity check
```

### Shared Fixtures (`conftest.py`)

The conftest provides four key fixtures:

| Fixture | Scope | Description |
|---------|-------|-------------|
| `api_client` | session | `ApiTestClient` wrapping `requests.Session`. Waits for backend `/health/live` on first use. Provides `get()`, `post()`, `put()`, `delete()` methods. |
| `mqtt_client` | session | `MqttTestClient` wrapping paho-mqtt. Connects once, reused across all tests. Provides `publish()`, `subscribe()`, `wait_for_message()`, `get_messages()`, `clear_messages()`. |
| `bootstrap_farm` | session | Creates one farm (`test-farm-001`), one coordinator (`test-coord-001`), and five towers (`test-tower-001` through `test-tower-005`) via REST + MQTT. Returns a dict with the IDs. |
| `fresh_mqtt_client` | function | A per-test MQTT client with a clean message buffer, for tests that need isolation from prior messages. |

---

## Test Modules

### Module 1: Connectivity (`test_01_connectivity.py`)

Smoke tests verifying all infrastructure services are reachable. These run first and fast, providing early failure signals if the environment is broken.

| Test | What it does | Pass criteria |
|------|-------------|---------------|
| `test_health_live` | `GET /health/live` | HTTP 200 |
| `test_health_ready` | `GET /health/ready` | HTTP 200 (MQTT connected) |
| `test_health_full` | `GET /health` | HTTP 200 with JSON body |
| `test_mqtt_tcp_reachable` | Raw TCP socket connect to broker | Connection succeeds within 5s |
| `test_mqtt_client_connects` | Paho MQTT client connect with auth | `on_connect` callback fires with `rc=0` |
| `test_mqtt_subscribe_and_publish` | Subscribe to topic, publish, receive | Loopback message received within 10s |
| `test_ws_upgrade_accepted` | HTTP upgrade request to `/ws` | Response is 101 or 400 (not 404) |
| `test_ws_broadcast_endpoint_exists` | HTTP upgrade request to `/ws/broadcast` | Response is 101 or 400 (not 404) |

**Timeout:** 30 seconds per test.

**Why 101 or 400 for WebSocket?** ASP.NET Core's WebSocket middleware only responds to proper HTTP upgrade requests. A `400` means the middleware recognized the WebSocket path but the handshake was malformed (which is expected from a `requests.get` call with upgrade headers). A `404` would mean the endpoint doesn't exist at all.

### Module 2: Telemetry (`test_02_telemetry.py`)

End-to-end data flow tests: publish MQTT telemetry, verify it arrives in the REST API.

| Test | What it does | Pass criteria |
|------|-------------|---------------|
| `test_tower_telemetry_appears_in_history` | Publish tower telemetry via MQTT, poll `GET /api/telemetry/tower/{f}/{c}/{t}/latest` | Matching `air_temp_c` value within 15s |
| `test_tower_telemetry_multiple_towers` | Publish for all 5 bootstrap towers | Each tower has a latest telemetry entry |
| `test_reservoir_telemetry_appears_in_api` | Publish reservoir telemetry, poll `/api/telemetry/reservoir/{f}/{c}/latest` | Matching `ph` value within 15s |
| `test_reservoir_history_accumulates` | Publish 3 reservoir readings 1s apart, query history | >= 3 entries in history response |
| `test_tower_twin_updated_after_telemetry` | Publish tower telemetry, poll `GET /api/twins/towers/{t}` | `reported.air_temp_c` matches published value |

**Timeout:** 60 seconds per test.

**Key pattern:** All tests use polling loops with a deadline rather than fixed sleeps. This handles variable backend processing latency gracefully. The MQTT publish is fire-and-forget (QoS 0), so the test polls the REST API until the data appears or the deadline expires.

**MQTT topics used:**
- Tower: `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry`
- Reservoir: `farm/{farmId}/coord/{coordId}/reservoir/telemetry`

### Module 3: LWT Disconnect (`test_03_lwt_disconnect.py`)

Tests the MQTT Last Will and Testament mechanism for coordinator disconnect detection.

| Test | What it does | Pass criteria |
|------|-------------|---------------|
| `test_connected_status_published` | Create LWT-enabled MQTT client, publish "connected" status | Subscriber receives `event: "connected"` or `"mqtt_connected"` |
| `test_lwt_fires_on_force_disconnect_mqtt_only` | Connect with LWT, force-close TCP socket (no MQTT DISCONNECT) | Subscriber receives `event: "disconnected"` within 30s |
| `test_reconnect_after_lwt` | Force-disconnect, wait for LWT, reconnect, publish "connected" | Subscriber receives new `event: "connected"` after LWT |

**Timeout:** 120 seconds per test.

**How LWT works in these tests:**
1. A `CoordinatorLwtClient` is created with `keepalive=5s` and a will message on topic `farm/{farmId}/coord/{coordId}/status/connection` containing `event: "disconnected"`.
2. On connect, it publishes a retained `"connected"` status to the same topic.
3. To trigger the LWT, the test calls `force_disconnect()` which closes the raw TCP socket without sending an MQTT DISCONNECT packet. The broker detects the dead connection after 1.5x the keepalive interval (~7.5s) and publishes the pre-configured LWT message.
4. A separate `fresh_mqtt_client` subscribes to the status topic and waits for the `"disconnected"` event.

**Why keepalive=5s?** Production firmware uses 60s. The tests use 5s to keep LWT detection fast (under 10s). The keepalive value does not affect the test's validity since the LWT mechanism is identical regardless of the interval.

### Module 4: Alert Cascade (`test_04_alert_cascade.py`)

Tests the AlertService threshold logic by publishing telemetry with out-of-range values.

| Test | What it does | Threshold violated | Expected alert |
|------|-------------|-------------------|----------------|
| `test_low_ph_creates_alert` | Reservoir pH = 4.0 | < 5.5 | `ph_out_of_range` |
| `test_high_ph_creates_alert` | Reservoir pH = 8.5 | > 7.5 | `ph_out_of_range` |
| `test_normal_ph_resolves_alert` | pH 4.0 then 6.2 | Resolved | Alert disappears from active list |
| `test_high_temp_creates_alert` | Tower temp = 40.0C | > 35.0 | `temperature_high` |
| `test_normal_temp_resolves_alert` | Temp 40.0 then 22.0 | Resolved | Alert disappears |
| `test_low_water_creates_alert` | Water level = 10% | < 20% | `water_level` |
| `test_normal_water_resolves_alert` | Water 10% then 80% | Resolved | Alert disappears |
| `test_duplicate_ph_violation_no_new_alert` | pH 4.0 twice | Deduplicated | Alert count stays the same |

**Timeout:** 90 seconds per test.

**How alert checking works:**
1. Test publishes abnormal telemetry via MQTT (e.g., reservoir with pH=4.0).
2. Backend's `TelemetryHandler` receives the message, persists to time-series, updates the coordinator/tower entity document with the sensor values, then calls `AlertService.CheckCoordinatorAlertsAsync`.
3. AlertService evaluates thresholds. If violated, it creates an alert document (with deduplication by `alert_key`). If the value is back to normal, it resolves any existing active alert.
4. Test polls `GET /api/alerts/active` filtering by `farm_id` and `category` until the expected alert appears (or disappears for resolution tests).

**Helper functions:** `_wait_for_alert(api_client, farm_id, category, timeout)` and `_wait_for_alert_resolved(...)` encapsulate the polling logic.

### Module 5: Pairing (`test_05_pairing.py`)

Tests the tower pairing lifecycle: session management, MQTT discovery, REST approval.

| Test | What it does | Pass criteria |
|------|-------------|---------------|
| `test_start_pairing_session` | `POST /api/pairing/start` | HTTP 200 or 409 (already active) |
| `test_pairing_request_appears_via_mqtt` | Publish to `pairing/request` topic, poll pending requests | New tower ID in pending list within 15s |
| `test_approve_pairing_creates_tower` | Full flow: request, approve, verify tower list | Tower appears in `GET /api/towers/farm/{f}/coord/{c}` |
| `test_stop_pairing_session` | `POST /api/pairing/stop` | HTTP 200, 204, or 404 |

**Timeout:** 60 seconds per test.

**Pairing MQTT payload format:**
```json
{
  "tower_id": "pair-tower-abc123",
  "mac_address": "pair-tower-abc123",
  "fw_version": "1.2.0",
  "capabilities": {
    "dht_sensor": true,
    "light_sensor": true,
    "pump_relay": true,
    "grow_light": true
  },
  "rssi": -40
}
```

Each test run generates unique tower IDs using `uuid.uuid4()` to avoid collisions across runs.

### Module 6: Scale (`test_06_scale.py`)

Throughput sanity check with a moderate message volume.

| Test | What it does | Pass criteria |
|------|-------------|---------------|
| `test_throughput_sanity` | Bootstrap 50 towers (5 coordinators x 10), publish 55 MQTT messages per tick at 1s intervals for 15s, verify telemetry persisted | >= 5 telemetry entries for a sample tower |
| `test_multiple_coordinators_all_receive` | After the burst, check each coordinator has reservoir telemetry | Reservoir latest endpoint returns data |

**Timeout:** 120 seconds per test.

**Message volume:** Each tick publishes 50 tower telemetry + 5 reservoir telemetry = 55 messages. Over 15 seconds at 1s intervals, this is ~825 total MQTT messages. The test verifies the backend processed them by checking telemetry history and diagnostics.

---

## Running Specific Tests

```bash
# Run a single module
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/test_04_alert_cascade.py -v --timeout=120

# Run a single test
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/test_02_telemetry.py::TestTowerTelemetry::test_tower_telemetry_appears_in_history -v

# Run with print output (debugging)
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/ -v -s --timeout=120

# Generate JSON report
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/ -v --json-report --json-report-file=/app/test-results.json --timeout=120
```

## Viewing Service Logs

```bash
# Backend logs (most useful for debugging test failures)
docker compose -f docker-compose.simulation.yml logs backend-sim --tail=50

# Filter for errors only
docker compose -f docker-compose.simulation.yml logs backend-sim 2>&1 | grep ERR

# Filter for alert activity
docker compose -f docker-compose.simulation.yml logs backend-sim 2>&1 | grep -i alert

# All services
docker compose -f docker-compose.simulation.yml logs --tail=20
```

## Adding New Tests

### Writing a new test module

1. Create `tools/simulator/tests/test_07_yourfeature.py`
2. Import fixtures from conftest: `api_client`, `mqtt_client`, `bootstrap_farm`, `fresh_mqtt_client`
3. Use the `pytestmark = pytest.mark.timeout(60)` pattern for module-level timeout
4. Group related tests in a class with a descriptive docstring
5. Use polling loops with deadlines for any operation that depends on backend processing

### Template

```python
"""
Test 07 - Your feature description.

What this module verifies.
"""

import time
import pytest

pytestmark = pytest.mark.timeout(60)


class TestYourFeature:
    """Description of the test group."""

    def test_basic_operation(self, api_client, mqtt_client, bootstrap_farm):
        """What this specific test verifies."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]

        # Publish MQTT message
        mqtt_client.publish(
            f"farm/{farm_id}/coord/{coord_id}/your/topic",
            {"key": "value"},
        )

        # Poll REST API for expected result
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get("/api/your/endpoint")
            if r.status_code == 200:
                body = r.json()
                if body.get("key") == "value":
                    found = True
                    break
            time.sleep(1)

        assert found, "Expected result did not appear within 15s"
```

### Rebuilding after changes

After modifying test files, rebuild only the simulator:

```bash
docker compose -f docker-compose.simulation.yml build simulator
docker compose -f docker-compose.simulation.yml run --rm \
  --entrypoint python simulator \
  -m pytest tests/ -v --timeout=120
```

After modifying backend code, rebuild the backend and restart:

```bash
docker compose -f docker-compose.simulation.yml down -v
docker compose -f docker-compose.simulation.yml up -d --build
# Then run tests as usual
```

The `down -v` is important to clear MongoDB data between backend changes, ensuring a clean state.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `TimeoutError: Backend not healthy after 60s` | Backend failed to start | Check `docker compose logs backend-sim` for startup errors |
| `ConnectionError: Failed to connect to MQTT` | Mosquitto not running or auth failed | Check `docker compose logs mosquitto-sim` and verify pwfile |
| `AssertionError: ... 500` on tower/coordinator creation | MongoDB duplicate key or missing field | Check `docker compose logs backend-sim` for MongoDB errors |
| Alert tests timeout | AlertService not wired or entity docs not synced | Check backend logs for `Alert check failed` warnings |
| LWT test timeout | Broker keepalive too long | Verify `CoordinatorLwtClient` uses `keepalive=5` |
| Scale test slow | Docker resource limits | Ensure Docker Desktop has >= 4GB RAM allocated |
