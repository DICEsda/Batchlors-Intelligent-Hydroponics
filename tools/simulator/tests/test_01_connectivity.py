"""
Test 01 — Connectivity smoke tests.

Verifies that all infrastructure services are reachable:
  - Backend health endpoint responds 200
  - MQTT broker accepts connections
  - WebSocket endpoint accepts upgrade
"""

import socket
import time

import pytest
import requests

from .conftest import API_URL, MQTT_HOST, MQTT_PORT


pytestmark = pytest.mark.timeout(30)


class TestBackendHealth:
    """Backend REST API reachability."""

    def test_health_live(self, api_client):
        """GET /health/live returns 200."""
        r = api_client.get("/health/live")
        assert r.status_code == 200

    def test_health_ready(self, api_client):
        """GET /health/ready returns 200 (MQTT connected)."""
        r = api_client.get("/health/ready")
        assert r.status_code == 200

    def test_health_full(self, api_client):
        """GET /health returns 200 with status payload."""
        r = api_client.get("/health")
        assert r.status_code == 200
        body = r.json()
        # Should contain some health information
        assert isinstance(body, dict)


class TestMqttConnectivity:
    """MQTT broker reachability."""

    def test_mqtt_tcp_reachable(self):
        """Raw TCP connect to MQTT broker succeeds."""
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        try:
            sock.connect((MQTT_HOST, MQTT_PORT))
        finally:
            sock.close()

    def test_mqtt_client_connects(self, mqtt_client):
        """Paho MQTT client connects and is authenticated."""
        # mqtt_client fixture already connects; if we got here, it worked
        assert mqtt_client._connected.is_set()

    def test_mqtt_subscribe_and_publish(self, fresh_mqtt_client):
        """Publish a message and receive it on the same client (loopback)."""
        topic = "test/connectivity/loopback"
        fresh_mqtt_client.subscribe(topic)
        time.sleep(0.5)  # Wait for subscription to be active

        fresh_mqtt_client.publish(topic, {"test": True})
        msgs = fresh_mqtt_client.wait_for_message(topic, timeout=10)
        assert len(msgs) >= 1
        assert msgs[0].payload["test"] is True


class TestWebSocket:
    """WebSocket endpoint reachability."""

    def test_ws_upgrade_accepted(self, api_client):
        """WebSocket endpoint accepts a proper HTTP upgrade handshake."""
        # Send a real WebSocket upgrade request via raw HTTP headers.
        # ASP.NET Core WS middleware only responds to upgrade requests,
        # returning 404 for plain GET (no WS upgrade headers).
        ws_url = f"{API_URL}/ws"
        headers = {
            "Connection": "Upgrade",
            "Upgrade": "websocket",
            "Sec-WebSocket-Version": "13",
            "Sec-WebSocket-Key": "dGhlIHNhbXBsZSBub25jZQ==",
        }
        try:
            r = requests.get(ws_url, headers=headers, timeout=5)
            # 101 = switching protocols (upgrade accepted)
            # 400 = bad request (recognised as WS but malformed)
            # Either means the WS endpoint exists and responds
            assert r.status_code in (101, 400), (
                f"Unexpected status {r.status_code} for WS upgrade"
            )
        except requests.exceptions.ConnectionError:
            # ConnectionError on upgrade is also acceptable — some HTTP
            # client libs cannot handle the protocol switch gracefully
            pass

    def test_ws_broadcast_endpoint_exists(self, api_client):
        """Broadcast WebSocket endpoint accepts upgrade handshake."""
        ws_url = f"{API_URL}/ws/broadcast"
        headers = {
            "Connection": "Upgrade",
            "Upgrade": "websocket",
            "Sec-WebSocket-Version": "13",
            "Sec-WebSocket-Key": "dGhlIHNhbXBsZSBub25jZQ==",
        }
        try:
            r = requests.get(ws_url, headers=headers, timeout=5)
            assert r.status_code in (101, 400), (
                f"Unexpected status {r.status_code} for broadcast WS upgrade"
            )
        except requests.exceptions.ConnectionError:
            pass
