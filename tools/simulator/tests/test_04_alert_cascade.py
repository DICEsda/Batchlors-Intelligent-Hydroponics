"""
Test 04 — Alert threshold verification.

Verifies the AlertService by publishing telemetry that violates thresholds:
  - pH out of range -> ph_out_of_range alert
  - Temperature high -> temperature_high alert
  - Low water level -> water_level alert
  - Normal values -> alerts auto-resolve
  - Deduplication: same violation does not create duplicate alerts
"""

import time

import pytest


pytestmark = pytest.mark.timeout(90)


# AlertService thresholds (from AlertService.cs)
PH_MIN = 5.5
PH_MAX = 7.5
TEMP_HIGH = 35.0
TEMP_LOW = 15.0
WATER_LOW = 20.0


def _make_reservoir_telemetry(**overrides) -> dict:
    """Build a reservoir telemetry payload with optional overrides."""
    base = {
        "fw_version": "2.1.0",
        "towers_online": 3,
        "wifi_rssi": -50,
        "status_mode": "operational",
        "uptime_s": 3600,
        "temp_c": 22.0,
        "ph": 6.2,
        "ec_ms_cm": 1.5,
        "tds_ppm": 750.0,
        "water_temp_c": 20.0,
        "water_level_pct": 80.0,
        "water_level_cm": 32.0,
        "low_water_alert": False,
        "main_pump_on": True,
        "dosing_pump_ph_on": False,
        "dosing_pump_nutrient_on": False,
    }
    base.update(overrides)
    return base


def _make_tower_telemetry(**overrides) -> dict:
    """Build a tower telemetry payload with optional overrides."""
    base = {
        "air_temp_c": 22.0,
        "humidity_pct": 65.0,
        "light_lux": 12000.0,
        "pump_on": True,
        "light_on": True,
        "light_brightness": 200,
        "status_mode": "operational",
        "vbat_mv": 3700,
        "fw_version": "1.2.0",
        "uptime_s": 3600,
        "signal_quality": -40,
    }
    base.update(overrides)
    return base


def _get_active_alerts(api_client, farm_id: str) -> list:
    """Fetch all active alerts for a farm."""
    r = api_client.get("/api/alerts/active")
    if r.status_code != 200:
        return []
    alerts = r.json()
    if isinstance(alerts, list):
        return [a for a in alerts if a.get("farm_id") == farm_id]
    return []


def _get_alerts_by_category(api_client, farm_id: str, category: str) -> list:
    """Fetch active alerts for a farm filtered by category."""
    alerts = _get_active_alerts(api_client, farm_id)
    return [a for a in alerts if a.get("category") == category]


def _wait_for_alert(
    api_client, farm_id: str, category: str, timeout: float = 20
) -> dict:
    """Poll until an active alert with the given category appears."""
    deadline = time.time() + timeout
    while time.time() < deadline:
        alerts = _get_alerts_by_category(api_client, farm_id, category)
        if alerts:
            return alerts[0]
        time.sleep(1)
    raise TimeoutError(
        f"Alert '{category}' for farm '{farm_id}' did not appear within {timeout}s"
    )


def _wait_for_alert_resolved(
    api_client, farm_id: str, category: str, timeout: float = 20
) -> None:
    """Poll until no active alert with the given category exists."""
    deadline = time.time() + timeout
    while time.time() < deadline:
        alerts = _get_alerts_by_category(api_client, farm_id, category)
        if not alerts:
            return
        time.sleep(1)
    raise TimeoutError(
        f"Alert '{category}' for farm '{farm_id}' was not resolved within {timeout}s"
    )


class TestPhAlert:
    """pH threshold violation triggers ph_out_of_range alert."""

    def test_low_ph_creates_alert(self, api_client, mqtt_client, bootstrap_farm):
        """pH = 4.0 (below 5.5 min) should trigger ph_out_of_range."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        # Publish reservoir telemetry with dangerously low pH
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=4.0))

        alert = _wait_for_alert(api_client, farm_id, "ph_out_of_range")
        assert alert["severity"] in ("warning", "critical")
        assert alert["status"] == "active"

    def test_high_ph_creates_alert(self, api_client, mqtt_client, bootstrap_farm):
        """pH = 8.5 (above 7.5 max) should trigger ph_out_of_range."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=8.5))

        alert = _wait_for_alert(api_client, farm_id, "ph_out_of_range")
        assert alert["severity"] in ("warning", "critical")

    def test_normal_ph_resolves_alert(self, api_client, mqtt_client, bootstrap_farm):
        """Returning pH to normal range should auto-resolve the alert."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        # First trigger the alert
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=4.0))
        _wait_for_alert(api_client, farm_id, "ph_out_of_range")

        # Now send normal pH
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=6.2))

        # Wait for resolution
        _wait_for_alert_resolved(api_client, farm_id, "ph_out_of_range")


class TestTemperatureAlert:
    """Temperature threshold violation triggers temperature_high alert."""

    def test_high_temp_creates_alert(self, api_client, mqtt_client, bootstrap_farm):
        """air_temp_c = 40.0 (above 35.0) should trigger temperature_high."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][1]
        topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"

        mqtt_client.publish(topic, _make_tower_telemetry(air_temp_c=40.0))

        alert = _wait_for_alert(api_client, farm_id, "temperature_high")
        assert alert["severity"] in ("warning", "critical")

    def test_normal_temp_resolves_alert(self, api_client, mqtt_client, bootstrap_farm):
        """Returning temperature to normal should auto-resolve."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = bootstrap_farm["tower_ids"][1]
        topic = f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry"

        # Trigger
        mqtt_client.publish(topic, _make_tower_telemetry(air_temp_c=40.0))
        _wait_for_alert(api_client, farm_id, "temperature_high")

        # Resolve
        mqtt_client.publish(topic, _make_tower_telemetry(air_temp_c=22.0))
        _wait_for_alert_resolved(api_client, farm_id, "temperature_high")


class TestWaterLevelAlert:
    """Low water level triggers water_level alert."""

    def test_low_water_creates_alert(self, api_client, mqtt_client, bootstrap_farm):
        """water_level_pct = 10 (below 20%) should trigger water_level alert."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        mqtt_client.publish(topic, _make_reservoir_telemetry(water_level_pct=10.0))

        alert = _wait_for_alert(api_client, farm_id, "water_level")
        assert alert["severity"] in ("warning", "critical")

    def test_normal_water_resolves_alert(self, api_client, mqtt_client, bootstrap_farm):
        """Restoring water level should auto-resolve."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        # Trigger
        mqtt_client.publish(topic, _make_reservoir_telemetry(water_level_pct=10.0))
        _wait_for_alert(api_client, farm_id, "water_level")

        # Resolve
        mqtt_client.publish(topic, _make_reservoir_telemetry(water_level_pct=80.0))
        _wait_for_alert_resolved(api_client, farm_id, "water_level")


class TestAlertDeduplication:
    """Same threshold violation should not create duplicate alerts."""

    def test_duplicate_ph_violation_no_new_alert(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """Publishing the same bad pH twice should not create two alerts."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        topic = f"farm/{farm_id}/coord/{coord_id}/reservoir/telemetry"

        # First violation
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=4.0))
        _wait_for_alert(api_client, farm_id, "ph_out_of_range")

        # Count current alerts
        alerts_before = _get_alerts_by_category(api_client, farm_id, "ph_out_of_range")
        count_before = len(alerts_before)

        # Second violation (same)
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=3.8))
        time.sleep(5)

        # Count should not have increased
        alerts_after = _get_alerts_by_category(api_client, farm_id, "ph_out_of_range")
        count_after = len(alerts_after)

        assert count_after == count_before, (
            f"Duplicate alert created: {count_before} -> {count_after}"
        )

        # Clean up: resolve the alert
        mqtt_client.publish(topic, _make_reservoir_telemetry(ph=6.2))
        time.sleep(3)
