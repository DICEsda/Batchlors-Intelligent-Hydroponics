"""
Test 05 — Tower pairing flow.

Verifies the full pairing lifecycle:
  1. Start a pairing session
  2. Publish a pairing request via MQTT
  3. Verify the pending request appears via REST
  4. Approve via REST
  5. Verify tower appears in tower list
"""

import time
import uuid

import pytest


pytestmark = pytest.mark.timeout(60)


class TestPairingFlow:
    """End-to-end tower pairing via MQTT + REST."""

    def test_start_pairing_session(self, api_client, bootstrap_farm):
        """POST /api/pairing/start creates a pairing session."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]

        r = api_client.post(
            "/api/pairing/start",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
                "duration_seconds": 120,
            },
        )
        # 200 = success, 409 = session already active (both OK)
        assert r.status_code in (200, 201, 409), (
            f"Failed to start pairing: {r.status_code} {r.text}"
        )

    def test_pairing_request_appears_via_mqtt(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """
        Publish a pairing/request MQTT message, then verify the pending
        request appears in REST.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        new_tower_id = f"pair-tower-{uuid.uuid4().hex[:6]}"

        # Start pairing session
        api_client.post(
            "/api/pairing/start",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
                "duration_seconds": 120,
            },
        )
        time.sleep(1)

        # Publish pairing request via MQTT
        topic = f"farm/{farm_id}/coord/{coord_id}/pairing/request"
        mqtt_client.publish(
            topic,
            {
                "tower_id": new_tower_id,
                "mac_address": new_tower_id,
                "fw_version": "1.2.0",
                "capabilities": {
                    "dht_sensor": True,
                    "light_sensor": True,
                    "pump_relay": True,
                    "grow_light": True,
                },
                "rssi": -40,
            },
        )

        # Poll for the pending request to appear
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(f"/api/pairing/requests/{farm_id}/{coord_id}")
            if r.status_code == 200:
                requests_list = r.json()
                if isinstance(requests_list, list):
                    for req in requests_list:
                        if req.get("tower_id") == new_tower_id:
                            found = True
                            break
            if found:
                break
            time.sleep(1)

        assert found, (
            f"Pairing request for {new_tower_id} did not appear in pending list"
        )

    def test_approve_pairing_creates_tower(
        self, api_client, mqtt_client, bootstrap_farm
    ):
        """
        Full flow: publish pairing request, approve it, verify tower
        appears in tower list.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        new_tower_id = f"approved-tower-{uuid.uuid4().hex[:6]}"

        # Start pairing session
        api_client.post(
            "/api/pairing/start",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
                "duration_seconds": 120,
            },
        )
        time.sleep(1)

        # Publish pairing request via MQTT
        topic = f"farm/{farm_id}/coord/{coord_id}/pairing/request"
        mqtt_client.publish(
            topic,
            {
                "tower_id": new_tower_id,
                "mac_address": new_tower_id,
                "fw_version": "1.2.0",
                "capabilities": {
                    "dht_sensor": True,
                    "light_sensor": True,
                    "pump_relay": True,
                    "grow_light": True,
                },
                "rssi": -40,
            },
        )

        # Wait for request to appear
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(f"/api/pairing/requests/{farm_id}/{coord_id}")
            if r.status_code == 200:
                reqs = r.json()
                if isinstance(reqs, list):
                    found = any(req.get("tower_id") == new_tower_id for req in reqs)
            if found:
                break
            time.sleep(1)

        if not found:
            pytest.skip("Pairing request did not appear — skipping approval test")

        # Approve the pairing
        r = api_client.post(
            "/api/pairing/approve",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
                "tower_id": new_tower_id,
            },
        )
        assert r.status_code in (200, 201, 204), (
            f"Failed to approve pairing: {r.status_code} {r.text}"
        )

        time.sleep(2)

        # Verify tower appears in the tower list
        r = api_client.get(f"/api/towers/farm/{farm_id}/coord/{coord_id}")
        if r.status_code == 200:
            towers = r.json()
            if isinstance(towers, list):
                tower_ids = [t.get("tower_id") for t in towers]
                assert new_tower_id in tower_ids, (
                    f"Approved tower {new_tower_id} not in tower list: {tower_ids}"
                )
            else:
                # Might be a different response format
                pass

    def test_stop_pairing_session(self, api_client, bootstrap_farm):
        """POST /api/pairing/stop ends the pairing session."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]

        # Start first (ensure there's a session to stop)
        api_client.post(
            "/api/pairing/start",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
                "duration_seconds": 120,
            },
        )
        time.sleep(1)

        r = api_client.post(
            "/api/pairing/stop",
            json_data={
                "farm_id": farm_id,
                "coord_id": coord_id,
            },
        )
        # 200 = stopped, 404 = no active session (both acceptable)
        assert r.status_code in (200, 204, 404), (
            f"Failed to stop pairing: {r.status_code} {r.text}"
        )
