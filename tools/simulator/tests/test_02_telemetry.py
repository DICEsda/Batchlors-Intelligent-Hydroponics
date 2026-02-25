"""
Test 02 — Telemetry data flow verification.

Verifies the full MQTT -> Backend -> REST pipeline:
  - Publish tower telemetry via MQTT, verify it appears in REST API
  - Publish reservoir telemetry via MQTT, verify MongoDB write via REST
  - Verify digital twin update after telemetry
"""

import time

import pytest

from .conftest import MSG_WAIT_TIMEOUT


pytestmark = pytest.mark.timeout(60)


# ---------------------------------------------------------------------------
# Tower telemetry
# ---------------------------------------------------------------------------


class TestTowerTelemetry:
    """Tower telemetry: MQTT publish -> REST retrieval."""

    TOWER_TELEMETRY = {
        "air_temp_c": 23.5,
        "humidity_pct": 68.0,
        "light_lux": 15000.0,
        "pump_on": True,
        "light_on": True,
        "light_brightness": 180,
        "status_mode": "operational",
        "vbat_mv": 3650,
        "fw_version": "1.2.0",
        "uptime_s": 7200,
        "signal_quality": -38,
    }

    def test_tower_telemetry_appears_in_history(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """Publish tower telemetry via MQTT, then GET it from REST."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][0]

        topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"
        mqtt_client.publish(topic, self.TOWER_TELEMETRY)

        # Poll REST until the telemetry appears (backend needs time to process)
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_id}/latest"
            )
            if r.status_code == 200:
                body = r.json()
                if body and isinstance(body, dict):
                    # Check a distinctive field
                    if body.get("air_temp_c") == self.TOWER_TELEMETRY["air_temp_c"]:
                        found = True
                        break
            time.sleep(1)

        assert found, "Tower telemetry did not appear in REST API within 15s"

    def test_tower_telemetry_multiple_towers(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """Publish telemetry for all 5 towers, verify each appears."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]

        for tower_id in bootstrap_farm["tower_ids"]:
            payload = {**self.TOWER_TELEMETRY, "air_temp_c": 22.0 + hash(tower_id) % 10}
            topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"
            mqtt_client.publish(topic, payload)

        time.sleep(5)  # Let backend process all messages

        for tower_id in bootstrap_farm["tower_ids"]:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_id}/latest"
            )
            assert r.status_code == 200, (
                f"No telemetry for tower {tower_id}: {r.status_code}"
            )


# ---------------------------------------------------------------------------
# Reservoir telemetry
# ---------------------------------------------------------------------------


class TestReservoirTelemetry:
    """Reservoir telemetry: MQTT publish -> REST retrieval."""

    RESERVOIR_TELEMETRY = {
        "fw_version": "2.1.0",
        "towers_online": 5,
        "wifi_rssi": -50,
        "status_mode": "operational",
        "uptime_s": 3600,
        "temp_c": 23.0,
        "ph": 6.2,
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

    def test_reservoir_telemetry_appears_in_api(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """Publish reservoir telemetry via MQTT, then GET it from REST."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]

        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"
        mqtt_client.publish(topic, self.RESERVOIR_TELEMETRY)

        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(f"/api/telemetry/reservoir/{farm_id}/{coord_id}/latest")
            if r.status_code == 200:
                body = r.json()
                if body and isinstance(body, dict):
                    if body.get("ph") == self.RESERVOIR_TELEMETRY["ph"]:
                        found = True
                        break
            time.sleep(1)

        assert found, "Reservoir telemetry did not appear in REST API within 15s"

    def test_reservoir_history_accumulates(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """Publish multiple reservoir readings, verify history grows."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        for i in range(3):
            payload = {**self.RESERVOIR_TELEMETRY, "ph": 6.0 + i * 0.1}
            mqtt_client.publish(topic, payload)
            time.sleep(1)

        time.sleep(3)  # Let backend process

        r = api_client.get(
            f"/api/telemetry/reservoir/{farm_id}/{coord_id}",
            params={"limit": 100},
        )
        assert r.status_code == 200
        history = r.json()
        assert isinstance(history, list)
        assert len(history) >= 3, f"Expected >= 3 history entries, got {len(history)}"


# ---------------------------------------------------------------------------
# Digital twin
# ---------------------------------------------------------------------------


class TestDigitalTwin:
    """Verify that telemetry updates the digital twin's reported state."""

    def test_tower_twin_updated_after_telemetry(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """After publishing telemetry, the tower twin should reflect reported state."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][0]

        # Publish distinctive telemetry
        telemetry = {
            "air_temp_c": 31.7,
            "humidity_pct": 55.0,
            "light_lux": 8000.0,
            "pump_on": False,
            "light_on": True,
            "light_brightness": 255,
            "status_mode": "operational",
            "vbat_mv": 3500,
            "fw_version": "1.2.0",
            "uptime_s": 9999,
            "signal_quality": -50,
        }
        topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"
        mqtt_client.publish(topic, telemetry)

        deadline = time.time() + 15
        twin_matches = False
        while time.time() < deadline:
            r = api_client.get(f"/api/twins/towers/{tower_id}")
            if r.status_code == 200:
                twin = r.json()
                reported = twin.get("reported", twin)
                # Check if some reported fields match what we published
                if reported.get("air_temp_c") == 31.7:
                    twin_matches = True
                    break
            time.sleep(1)

        assert twin_matches, (
            "Tower twin reported state did not update after telemetry within 15s"
        )
