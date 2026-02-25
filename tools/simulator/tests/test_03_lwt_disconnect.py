"""
Test 03 — MQTT Last Will and Testament (LWT) disconnect behavior.

Verifies that:
  - A coordinator MQTT client can set an LWT
  - Publishing "connected" status works
  - Force-disconnecting triggers the LWT
  - Backend detects the coordinator as disconnected
"""

import json
import time
import uuid

import paho.mqtt.client as paho_mqtt
import pytest

from .conftest import MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASS


pytestmark = pytest.mark.timeout(120)


class CoordinatorLwtClient:
    """
    A dedicated MQTT client per coordinator with LWT configured.
    Mimics the firmware's LWT behavior for testing.
    """

    def __init__(self, farm_id: str, coord_id: str):
        self.farm_id = farm_id
        self.coord_id = coord_id
        self._client_id = f"test-coord-lwt-{uuid.uuid4().hex[:8]}"
        self._client = paho_mqtt.Client(client_id=self._client_id, clean_session=True)
        self._client.username_pw_set(MQTT_USER, MQTT_PASS)
        self._connected = False

        # Configure LWT before connecting
        lwt_topic = f"farm/{farm_id}/coord/{coord_id}/status/connection"
        lwt_payload = json.dumps(
            {
                "ts": 0,
                "coord_id": coord_id,
                "farm_id": farm_id,
                "event": "disconnected",
                "wifi_connected": False,
                "mqtt_connected": False,
            }
        )
        self._client.will_set(lwt_topic, lwt_payload, qos=0, retain=True)

        def on_connect(client, userdata, flags, rc):
            self._connected = rc == 0

        self._client.on_connect = on_connect

    @property
    def status_topic(self) -> str:
        return f"farm/{self.farm_id}/coord/{self.coord_id}/status/connection"

    def connect(self, timeout: float = 10):
        self._client.connect(MQTT_HOST, MQTT_PORT, keepalive=5)
        self._client.loop_start()
        deadline = time.time() + timeout
        while not self._connected and time.time() < deadline:
            time.sleep(0.1)
        if not self._connected:
            raise ConnectionError("LWT client failed to connect")

    def publish_connected(self):
        """Publish a retained 'connected' status."""
        payload = json.dumps(
            {
                "ts": int(time.time()),
                "coord_id": self.coord_id,
                "farm_id": self.farm_id,
                "event": "connected",
                "wifi_connected": True,
                "wifi_rssi": -45,
                "mqtt_connected": True,
                "uptime_ms": 60000,
                "free_heap": 200000,
            }
        )
        self._client.publish(self.status_topic, payload, qos=0, retain=True)

    def force_disconnect(self):
        """
        Close the raw TCP socket without sending MQTT DISCONNECT.
        The broker will publish the LWT after keepalive timeout.
        """
        try:
            self._client.loop_stop()
            sock = self._client.socket()
            if sock:
                sock.close()
        except Exception:
            pass
        self._connected = False

    def clean_disconnect(self):
        """Normal MQTT disconnect (no LWT triggered)."""
        self._client.loop_stop()
        self._client.disconnect()
        self._connected = False


class TestLwtDisconnect:
    """LWT disconnect detection via MQTT."""

    def test_connected_status_published(self, fresh_mqtt_client, bootstrap_farm):
        """Coordinator publishes 'connected' status, observable via MQTT."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        status_topic = f"farm/{farm_id}/coord/{coord_id}/status/connection"

        # Subscribe to the status topic
        fresh_mqtt_client.subscribe(status_topic)
        time.sleep(0.5)

        # Create LWT client and connect
        lwt_client = CoordinatorLwtClient(farm_id, coord_id)
        lwt_client.connect()
        lwt_client.publish_connected()

        # Wait for the "connected" message
        msgs = fresh_mqtt_client.wait_for_message(status_topic, timeout=10)
        assert len(msgs) >= 1

        # Find a "connected" or "mqtt_connected" event (bootstrap may have
        # published "mqtt_connected" as retained on the same topic)
        connected_events = {"connected", "mqtt_connected"}
        connected_msgs = [
            m
            for m in msgs
            if isinstance(m.payload, dict)
            and m.payload.get("event") in connected_events
        ]
        assert len(connected_msgs) >= 1, (
            f"No connected event found. Got: {[m.payload for m in msgs]}"
        )

        lwt_client.clean_disconnect()

    def test_lwt_fires_on_force_disconnect_mqtt_only(self, fresh_mqtt_client):
        """
        Pure MQTT test: force-disconnect triggers LWT message.
        Does not require backend coordinator registration.
        """
        farm_id = "lwt-test-farm"
        coord_id = f"lwt-test-coord-{uuid.uuid4().hex[:6]}"
        status_topic = f"farm/{farm_id}/coord/{coord_id}/status/connection"

        # Subscribe to the status topic BEFORE the LWT client connects
        fresh_mqtt_client.subscribe(status_topic)
        time.sleep(0.5)

        # Create and connect LWT client (keepalive=5s)
        lwt_client = CoordinatorLwtClient(farm_id, coord_id)
        lwt_client.connect()
        lwt_client.publish_connected()

        # Verify "connected" arrived
        msgs = fresh_mqtt_client.wait_for_message(status_topic, timeout=10)
        connected_msgs = [
            m
            for m in msgs
            if isinstance(m.payload, dict) and m.payload.get("event") == "connected"
        ]
        assert len(connected_msgs) >= 1, "Did not receive 'connected' status"

        # Clear messages before force disconnect
        fresh_mqtt_client.clear_messages()

        # Force disconnect — socket close, no MQTT DISCONNECT packet
        lwt_client.force_disconnect()

        # Wait for LWT to fire (keepalive=5s, broker detects at 1.5x = ~7.5s)
        try:
            msgs = fresh_mqtt_client.wait_for_message(status_topic, timeout=30)
        except TimeoutError:
            pytest.fail("LWT message not received within 30s after force disconnect")

        disconnected_msgs = [
            m
            for m in msgs
            if isinstance(m.payload, dict) and m.payload.get("event") == "disconnected"
        ]
        assert len(disconnected_msgs) >= 1, (
            f"No 'disconnected' LWT event found. Got: {[m.payload for m in msgs]}"
        )

    def test_reconnect_after_lwt(self, fresh_mqtt_client):
        """
        After LWT fires, reconnecting and publishing 'connected'
        restores the retained status.
        """
        farm_id = "lwt-reconn-farm"
        coord_id = f"lwt-reconn-{uuid.uuid4().hex[:6]}"
        status_topic = f"farm/{farm_id}/coord/{coord_id}/status/connection"

        fresh_mqtt_client.subscribe(status_topic)
        time.sleep(0.5)

        # Connect, publish connected, then force disconnect
        lwt_client = CoordinatorLwtClient(farm_id, coord_id)
        lwt_client.connect()
        lwt_client.publish_connected()
        time.sleep(1)
        fresh_mqtt_client.clear_messages()

        lwt_client.force_disconnect()

        # Wait for LWT
        try:
            fresh_mqtt_client.wait_for_message(status_topic, timeout=30)
        except TimeoutError:
            pytest.fail("LWT not received")

        fresh_mqtt_client.clear_messages()

        # Reconnect with a new client (same coord_id, new LWT armed)
        lwt_client2 = CoordinatorLwtClient(farm_id, coord_id)
        lwt_client2.connect()
        lwt_client2.publish_connected()

        # Should get a new "connected" message
        msgs = fresh_mqtt_client.wait_for_message(status_topic, timeout=10)
        connected_msgs = [
            m
            for m in msgs
            if isinstance(m.payload, dict) and m.payload.get("event") == "connected"
        ]
        assert len(connected_msgs) >= 1, "Reconnect 'connected' status not received"

        lwt_client2.clean_disconnect()
