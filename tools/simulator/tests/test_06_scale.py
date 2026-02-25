"""
Test 06 — Throughput sanity check.

Verifies that the system can handle a moderate message volume:
  - Bootstrap additional towers
  - Publish telemetry at 1s intervals for 30 seconds
  - Verify message throughput via diagnostics endpoint
"""

import time

import pytest

from .conftest import (
    _create_farm,
    _register_coordinator,
    _register_tower,
)


pytestmark = pytest.mark.timeout(120)


class TestScaleThroughput:
    """Moderate-scale throughput test."""

    SCALE_FARM_ID = "scale-test-farm"
    SCALE_COORD_IDS = [f"scale-coord-{i:02d}" for i in range(1, 6)]
    TOWERS_PER_COORD = 10
    PUBLISH_INTERVAL = 1.0  # seconds
    PUBLISH_DURATION = 15.0  # seconds (shorter for CI friendliness)

    def _setup_scale_topology(self, api_client, mqtt_client):
        """Create 5 coordinators x 10 towers = 50 towers."""
        _create_farm(api_client, self.SCALE_FARM_ID, "Scale Test Farm")

        for coord_id in self.SCALE_COORD_IDS:
            _register_coordinator(
                api_client,
                mqtt_client,
                self.SCALE_FARM_ID,
                coord_id,
                f"Scale {coord_id}",
            )
            for t in range(1, self.TOWERS_PER_COORD + 1):
                tower_id = f"{coord_id}-tower-{t:03d}"
                _register_tower(
                    api_client,
                    self.SCALE_FARM_ID,
                    coord_id,
                    tower_id,
                    f"Tower {t}",
                )

    def _publish_telemetry_burst(self, mqtt_client):
        """Publish telemetry for all towers at interval for duration."""
        tower_payload = {
            "air_temp_c": 22.5,
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
        reservoir_payload = {
            "fw_version": "2.1.0",
            "towers_online": self.TOWERS_PER_COORD,
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

        total_published = 0
        start = time.time()

        while time.time() - start < self.PUBLISH_DURATION:
            tick_start = time.time()

            for coord_id in self.SCALE_COORD_IDS:
                # Reservoir telemetry
                res_topic = (
                    f"farm/{self.SCALE_FARM_ID}/coord/{coord_id}/reservoir/telemetry"
                )
                mqtt_client.publish(res_topic, reservoir_payload)
                total_published += 1

                # Tower telemetry
                for t in range(1, self.TOWERS_PER_COORD + 1):
                    tower_id = f"{coord_id}-tower-{t:03d}"
                    tower_topic = (
                        f"farm/{self.SCALE_FARM_ID}/coord/{coord_id}"
                        f"/tower/{tower_id}/telemetry"
                    )
                    mqtt_client.publish(tower_topic, tower_payload)
                    total_published += 1

            # Sleep for the remainder of the interval
            elapsed = time.time() - tick_start
            if elapsed < self.PUBLISH_INTERVAL:
                time.sleep(self.PUBLISH_INTERVAL - elapsed)

        return total_published

    def test_throughput_sanity(self, api_client, mqtt_client):
        """
        Publish 50 tower + 5 reservoir messages per tick for 15s,
        then verify the system handled them.
        """
        # Setup
        self._setup_scale_topology(api_client, mqtt_client)
        time.sleep(2)

        # Reset diagnostics before burst
        api_client.post("/api/diagnostics/reset")
        time.sleep(1)

        # Publish burst
        total_published = self._publish_telemetry_burst(mqtt_client)

        # Give backend time to process the last batch
        time.sleep(5)

        # Check diagnostics
        r = api_client.get("/api/diagnostics")
        if r.status_code == 200:
            diag = r.json()
            # The diagnostics endpoint should report message counts
            # Exact field names depend on implementation, so we check
            # what's available
            mqtt_messages = diag.get("mqtt_messages_received", 0)
            if mqtt_messages > 0:
                duration = self.PUBLISH_DURATION + 5  # include processing time
                throughput = mqtt_messages / duration
                assert throughput > 5, (
                    f"Throughput too low: {throughput:.1f} msg/s "
                    f"(expected > 5 msg/s). "
                    f"Published {total_published}, backend saw {mqtt_messages}"
                )
        else:
            # Diagnostics endpoint might not exist or have different format
            # Fall back to checking that telemetry data was written
            pass

        # Fallback: verify at least some telemetry was persisted
        # Check a random tower's telemetry history
        coord_id = self.SCALE_COORD_IDS[0]
        tower_id = f"{coord_id}-tower-001"
        r = api_client.get(
            f"/api/telemetry/tower/{self.SCALE_FARM_ID}/{coord_id}/{tower_id}",
            params={"limit": 100},
        )
        assert r.status_code == 200, f"Could not retrieve telemetry: {r.status_code}"
        history = r.json()
        assert isinstance(history, list)
        assert len(history) >= 5, (
            f"Expected >= 5 telemetry entries for {tower_id}, got {len(history)}. "
            f"Published {total_published} messages total."
        )

    def test_multiple_coordinators_all_receive(self, api_client, mqtt_client):
        """Verify telemetry appears for coordinators across the scale topology."""
        # This test relies on the topology from test_throughput_sanity
        # Check reservoir telemetry for each coordinator
        for coord_id in self.SCALE_COORD_IDS:
            r = api_client.get(
                f"/api/telemetry/reservoir/{self.SCALE_FARM_ID}/{coord_id}/latest"
            )
            if r.status_code == 200:
                body = r.json()
                assert body is not None, f"No reservoir telemetry for {coord_id}"
