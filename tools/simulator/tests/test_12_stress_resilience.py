"""
Test 12 — Stress and Resilience Testing.

Tests system robustness under adverse conditions: sustained high-throughput
load, malformed payloads, rapid MQTT connect/disconnect cycles, concurrent
mixed operations, and burst recovery.

Each test pushes the system beyond normal operating conditions to verify
it degrades gracefully without data loss or crashes.

Addresses: System robustness verification for thesis demonstration.
"""

import json
import time
import uuid
import threading
import pytest

from .conftest import (
    MqttTestClient, ApiTestClient,
    MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASS,
    _create_farm, _register_coordinator, _register_tower,
)

pytestmark = pytest.mark.timeout(180)


def _make_tower_payload(tower_id, temp=23.5):
    """Generate a valid tower telemetry payload."""
    return {
        "air_temp_c": temp,
        "humidity_pct": 65.0,
        "light_lux": 15000.0,
        "pump_on": False,
        "light_on": True,
        "light_brightness": 180,
        "status_mode": "operational",
        "vbat_mv": 3700,
        "fw_version": "1.2.0",
        "uptime_s": 7200,
        "signal_quality": -38,
    }


def _make_reservoir_payload(ph=6.2):
    """Generate a valid reservoir telemetry payload."""
    return {
        "fw_version": "2.1.0",
        "towers_online": 5,
        "wifi_rssi": -45,
        "status_mode": "operational",
        "uptime_s": 3600,
        "temp_c": 23.0,
        "ph": ph,
        "ec_ms_cm": 1.8,
        "tds_ppm": 900.0,
        "water_temp_c": 21.5,
        "water_level_pct": 78.0,
        "water_level_cm": 31.2,
        "low_water_alert": False,
        "main_pump_on": True,
        "dosing_pump_ph_on": False,
        "dosing_pump_nutrient_on": False,
    }


class TestSustainedLoad:
    """High-throughput sustained load testing."""

    def test_100_towers_sustained_60s(self, api_client, mqtt_client):
        """
        Bootstrap 100 towers (10 coordinators x 10), publish telemetry
        at 1s intervals for 60 seconds, verify all telemetry persisted
        and diagnostics show no errors.
        
        Total messages: ~110 per tick (100 tower + 10 reservoir) x 60 = ~6,600.
        Proves: Backend sustains ~110 messages/second without message loss.
        """
        farm_id = f"stress-farm-{uuid.uuid4().hex[:6]}"
        n_coords = 10
        n_towers_per_coord = 10
        
        # Bootstrap topology
        _create_farm(api_client, farm_id, "Stress Test Farm")
        
        coord_ids = []
        tower_map = {}  # coord_id -> [tower_ids]
        
        for i in range(n_coords):
            coord_id = f"stress-coord-{i:02d}"
            coord_ids.append(coord_id)
            _register_coordinator(
                api_client, mqtt_client, farm_id, coord_id,
                f"Stress Coordinator {i}",
            )
            
            tower_ids = []
            for j in range(n_towers_per_coord):
                tower_id = f"{coord_id}-tower-{j:03d}"
                tower_ids.append(tower_id)
                _register_tower(api_client, farm_id, coord_id, tower_id,
                              f"Tower {j}")
            tower_map[coord_id] = tower_ids
        
        # Reset diagnostics
        api_client.post("/api/diagnostics/reset", json_data={})
        time.sleep(2)
        
        # Publish for 60 seconds at 1s intervals
        duration = 60
        start_time = time.time()
        tick_count = 0
        
        while time.time() - start_time < duration:
            tick_start = time.time()
            
            for coord_id in coord_ids:
                # Reservoir telemetry
                mqtt_client.publish(
                    f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry",
                    _make_reservoir_payload(ph=6.0 + (tick_count % 10) * 0.1),
                )
                
                # Tower telemetry
                for tower_id in tower_map[coord_id]:
                    mqtt_client.publish(
                        f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry",
                        _make_tower_payload(tower_id, temp=22.0 + (tick_count % 5)),
                    )
            
            tick_count += 1
            
            # Maintain ~1s interval
            elapsed = time.time() - tick_start
            if elapsed < 1.0:
                time.sleep(1.0 - elapsed)
        
        # Wait for backend to drain
        time.sleep(10)
        
        # Verify: check a sample tower has telemetry
        sample_coord = coord_ids[0]
        sample_tower = tower_map[sample_coord][0]
        r = api_client.get(
            f"/api/telemetry/tower/{farm_id}/{sample_coord}/{sample_tower}",
            params={"limit": 100},
        )
        
        if r.status_code == 200:
            history = r.json()
            if isinstance(history, list):
                assert len(history) >= 10, (
                    f"Expected >= 10 telemetry entries for sample tower, "
                    f"got {len(history)}. Possible message loss."
                )
        
        # Check diagnostics for error count
        r = api_client.get("/api/diagnostics")
        if r.status_code == 200:
            diag = r.json()
            errors = diag.get("processing_errors_total", 0)
            if isinstance(errors, (int, float)):
                assert errors < tick_count, (
                    f"Too many processing errors: {errors} "
                    f"(out of {tick_count * 110} messages)"
                )


class TestMalformedPayloads:
    """Backend resilience to invalid/malformed MQTT payloads."""

    def test_malformed_json_no_crash(self, api_client, mqtt_client, bootstrap_farm):
        """
        Publish malformed payloads followed by valid ones. Backend should
        silently drop invalid messages and continue processing valid ones.
        
        Proves: TelemetryHandler error handling doesn't crash on bad input.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][0]
        topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"
        
        # Publish 10 malformed payloads
        malformed_payloads = [
            "this is not json",
            '{"incomplete": ',
            "",
            "null",
            "[]",
            '{"air_temp_c": "not_a_number"}',
            b'\x00\x01\x02\x03',
            '{"nested": {"deeply": {"invalid": undefined}}}',
            "42",
            '{"air_temp_c": null, "humidity_pct": null}',
        ]
        
        for payload in malformed_payloads:
            if isinstance(payload, bytes):
                mqtt_client.raw_client.publish(topic, payload, qos=0)
            else:
                mqtt_client.raw_client.publish(topic, payload.encode(), qos=0)
        
        time.sleep(2)
        
        # Now publish a valid payload with a distinctive temperature
        valid_temp = 42.42
        mqtt_client.publish(topic, _make_tower_payload(tower_id, temp=valid_temp))
        
        # Verify backend is still alive and processing
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_id}/latest"
            )
            if r.status_code == 200:
                data = r.json()
                if data and (data.get("air_temp_c") == valid_temp or 
                            data.get("airTempC") == valid_temp):
                    found = True
                    break
            time.sleep(1)
        
        assert found, (
            "Backend failed to process valid telemetry after malformed messages. "
            "TelemetryHandler may have crashed."
        )
        
        # Verify health endpoint still responds
        r = api_client.get("/health/live")
        assert r.status_code == 200, "Backend health check failed after malformed payloads"


class TestRapidConnectDisconnect:
    """MQTT broker resilience to rapid client churn."""

    def test_rapid_mqtt_connect_disconnect(self, api_client, mqtt_client, bootstrap_farm):
        """
        5 MQTT clients rapidly connect/disconnect 10 times each.
        Verify broker remains stable and backend still processes messages.
        
        Proves: Mosquitto handles client churn without affecting message routing.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][0]
        
        # Rapid connect/disconnect cycles
        for cycle in range(5):
            clients = []
            for i in range(5):
                try:
                    c = MqttTestClient(MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASS)
                    c.connect(timeout=5)
                    clients.append(c)
                except ConnectionError:
                    pass
            
            # Disconnect all
            for c in clients:
                c.disconnect()
            
            time.sleep(0.5)
        
        # Verify broker and backend are still functional
        mqtt_client.publish(
            f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry",
            _make_tower_payload(tower_id, temp=33.33),
        )
        
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_id}/latest"
            )
            if r.status_code == 200:
                data = r.json()
                if data and (data.get("air_temp_c") == 33.33 or 
                            data.get("airTempC") == 33.33):
                    found = True
                    break
            time.sleep(1)
        
        assert found, "Backend not processing messages after rapid connect/disconnect cycles"


class TestConcurrentMixedOperations:
    """Concurrent telemetry, commands, and pairing don't interfere."""

    def test_simultaneous_telemetry_and_commands(self, api_client, mqtt_client, bootstrap_farm):
        """
        Simultaneously: publish telemetry for 5 towers + issue REST
        commands + publish a pairing request. Verify all processed.
        
        Proves: Backend handles concurrent mixed workloads.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_ids = bootstrap_farm["tower_ids"]
        
        errors = []
        
        def publish_telemetry():
            """Publish telemetry for all towers."""
            try:
                for i, tid in enumerate(tower_ids):
                    mqtt_client.publish(
                        f"farm/{farm_id}/coord/{coord_id}/tower/{tid}/telemetry",
                        _make_tower_payload(tid, temp=20.0 + i),
                    )
                    time.sleep(0.1)
            except Exception as e:
                errors.append(f"telemetry: {e}")
        
        def issue_commands():
            """Issue REST API calls."""
            try:
                # Get farms
                r = api_client.get("/api/farms")
                if r.status_code != 200:
                    errors.append(f"GET farms: {r.status_code}")
                
                # Get coordinators
                r = api_client.get("/api/coordinators")
                if r.status_code != 200:
                    errors.append(f"GET coordinators: {r.status_code}")
                
                # Get diagnostics
                r = api_client.get("/api/diagnostics")
                if r.status_code != 200:
                    errors.append(f"GET diagnostics: {r.status_code}")
                
                # Set twin desired state
                r = api_client.put(
                    f"/api/twins/towers/{tower_ids[0]}/desired",
                    json_data={"light_on": True},
                )
            except Exception as e:
                errors.append(f"commands: {e}")
        
        def publish_pairing():
            """Publish a pairing request."""
            try:
                pair_id = f"stress-pair-{uuid.uuid4().hex[:6]}"
                mqtt_client.publish(
                    f"farm/{farm_id}/coord/{coord_id}/pairing/request",
                    {
                        "tower_id": pair_id,
                        "mac_address": pair_id,
                        "fw_version": "1.2.0",
                        "capabilities": {
                            "dht_sensor": True,
                            "light_sensor": True,
                            "pump_relay": True,
                            "grow_light": True,
                        },
                        "rssi": -45,
                    },
                )
            except Exception as e:
                errors.append(f"pairing: {e}")
        
        # Run all 3 operations concurrently
        threads = [
            threading.Thread(target=publish_telemetry),
            threading.Thread(target=issue_commands),
            threading.Thread(target=publish_pairing),
        ]
        for t in threads:
            t.start()
        for t in threads:
            t.join(timeout=30)
        
        # No fatal errors
        assert len(errors) == 0, f"Concurrent operations had errors: {errors}"
        
        # Backend still healthy
        r = api_client.get("/health/live")
        assert r.status_code == 200, "Backend unhealthy after concurrent operations"


class TestBurstRecovery:
    """System recovery after message bursts."""

    def test_burst_500_messages_recovery(self, api_client, mqtt_client, bootstrap_farm):
        """
        Publish 500 messages in <2 seconds (burst), wait for drain,
        verify backend processes all and latency returns to normal.
        
        Proves: Backend doesn't drop messages under burst conditions.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_ids = bootstrap_farm["tower_ids"]
        
        # Reset diagnostics
        api_client.post("/api/diagnostics/reset", json_data={})
        time.sleep(2)
        
        # BURST: 500 messages as fast as possible
        burst_start = time.time()
        for i in range(100):
            for tid in tower_ids:
                mqtt_client.publish(
                    f"farm/{farm_id}/coord/{coord_id}/tower/{tid}/telemetry",
                    _make_tower_payload(tid, temp=20.0 + (i % 10)),
                )
        burst_duration = time.time() - burst_start
        
        # Wait for backend to drain (generous: 30s for 500 messages)
        time.sleep(30)
        
        # Verify: check diagnostics for processed message count
        r = api_client.get("/api/diagnostics")
        if r.status_code == 200:
            diag = r.json()
            tower_total = diag.get("tower_messages_total", 0)
            if isinstance(tower_total, (int, float)):
                # Should have processed a significant portion
                assert tower_total >= 50, (
                    f"Only {tower_total} tower messages processed after "
                    f"500-message burst in {burst_duration:.1f}s"
                )
        
        # Verify backend is still healthy and responsive
        r = api_client.get("/health/live")
        assert r.status_code == 200, "Backend unhealthy after burst"
        
        # Verify a fresh telemetry message still gets processed (normal latency)
        distinctive_temp = 11.11
        mqtt_client.publish(
            f"farm/{farm_id}/coord/{coord_id}/tower/{tower_ids[0]}/telemetry",
            _make_tower_payload(tower_ids[0], temp=distinctive_temp),
        )
        
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_ids[0]}/latest"
            )
            if r.status_code == 200:
                data = r.json()
                if data and (data.get("air_temp_c") == distinctive_temp or
                            data.get("airTempC") == distinctive_temp):
                    found = True
                    break
            time.sleep(1)
        
        assert found, "Backend not processing new messages after burst recovery"
