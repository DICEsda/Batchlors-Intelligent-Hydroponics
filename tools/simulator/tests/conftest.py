"""
Shared fixtures for simulation integration tests.

Environment variables (defaults assume Docker internal networking):
    SIM_MQTT_HOST   - MQTT broker host       (default: mosquitto)
    SIM_MQTT_PORT   - MQTT broker port       (default: 1883)
    SIM_MQTT_USER   - MQTT username           (default: user1)
    SIM_MQTT_PASS   - MQTT password           (default: user1)
    SIM_API_URL     - Backend REST API URL    (default: http://backend:8000)
    API_KEY         - Backend API key         (default: hydro-thesis-2026)
"""

import json
import os
import socket
import time
import threading
import uuid
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

import paho.mqtt.client as mqtt
import pytest
import requests

# ---------------------------------------------------------------------------
# Environment configuration
# ---------------------------------------------------------------------------

MQTT_HOST = os.environ.get("SIM_MQTT_HOST", "mosquitto")
MQTT_PORT = int(os.environ.get("SIM_MQTT_PORT", "1883"))
MQTT_USER = os.environ.get("SIM_MQTT_USER", "user1")
MQTT_PASS = os.environ.get("SIM_MQTT_PASS", "user1")
API_URL = os.environ.get("SIM_API_URL", "http://backend:8000").rstrip("/")
API_KEY = os.environ.get("API_KEY", "hydro-thesis-2026")

# Timeouts
HEALTH_WAIT_TIMEOUT = 60  # seconds to wait for backend health
MQTT_CONNECT_TIMEOUT = 15  # seconds to wait for MQTT connect
MSG_WAIT_TIMEOUT = 30  # seconds to wait for an MQTT message


# ---------------------------------------------------------------------------
# MQTT helper
# ---------------------------------------------------------------------------


@dataclass
class ReceivedMessage:
    topic: str
    payload: Any
    qos: int
    retain: bool
    timestamp: float = field(default_factory=time.time)


class MqttTestClient:
    """Thin wrapper around paho-mqtt for test convenience."""

    def __init__(self, host: str, port: int, username: str, password: str):
        self._host = host
        self._port = port
        self._client_id = f"pytest-{uuid.uuid4().hex[:8]}"
        self._client = mqtt.Client(client_id=self._client_id, clean_session=True)
        self._client.username_pw_set(username, password)

        self._connected = threading.Event()
        self._messages: Dict[str, List[ReceivedMessage]] = {}
        self._waiters: List[tuple] = []  # (topic_filter, event, container)
        self._lock = threading.Lock()

        self._client.on_connect = self._on_connect
        self._client.on_message = self._on_message

    def _on_connect(self, client, userdata, flags, rc):
        if rc == 0:
            self._connected.set()

    def _on_message(self, client, userdata, msg):
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            payload = msg.payload

        received = ReceivedMessage(
            topic=msg.topic,
            payload=payload,
            qos=msg.qos,
            retain=msg.retain,
        )

        with self._lock:
            self._messages.setdefault(msg.topic, []).append(received)
            # Wake up any waiters whose topic filter matches
            for topic_filter, event, container in self._waiters:
                if mqtt.topic_matches_sub(topic_filter, msg.topic):
                    container.append(received)
                    event.set()

    def connect(self, timeout: float = MQTT_CONNECT_TIMEOUT):
        self._client.connect(self._host, self._port, keepalive=60)
        self._client.loop_start()
        if not self._connected.wait(timeout):
            raise ConnectionError(
                f"Failed to connect to MQTT broker at {self._host}:{self._port} "
                f"within {timeout}s"
            )

    def disconnect(self):
        self._client.loop_stop()
        self._client.disconnect()

    def subscribe(self, topic: str, qos: int = 0):
        self._client.subscribe(topic, qos)

    def publish(self, topic: str, payload: Any, qos: int = 0, retain: bool = False):
        if isinstance(payload, (dict, list)):
            payload = json.dumps(payload)
        self._client.publish(topic, payload, qos=qos, retain=retain)

    def get_messages(self, topic: str) -> List[ReceivedMessage]:
        with self._lock:
            return list(self._messages.get(topic, []))

    def get_all_messages(self) -> Dict[str, List[ReceivedMessage]]:
        with self._lock:
            return {k: list(v) for k, v in self._messages.items()}

    def clear_messages(self):
        with self._lock:
            self._messages.clear()

    def wait_for_message(
        self, topic_filter: str, timeout: float = MSG_WAIT_TIMEOUT, count: int = 1
    ) -> List[ReceivedMessage]:
        """Block until at least `count` messages matching `topic_filter` arrive."""
        event = threading.Event()
        container: List[ReceivedMessage] = []

        with self._lock:
            # Check already-received messages
            for t, msgs in self._messages.items():
                if mqtt.topic_matches_sub(topic_filter, t):
                    container.extend(msgs)
            if len(container) >= count:
                return container[:count]
            # Register waiter
            waiter = (topic_filter, event, container)
            self._waiters.append(waiter)

        deadline = time.time() + timeout
        while len(container) < count:
            remaining = deadline - time.time()
            if remaining <= 0:
                break
            event.wait(timeout=remaining)
            event.clear()

        with self._lock:
            if waiter in self._waiters:
                self._waiters.remove(waiter)

        if len(container) < count:
            raise TimeoutError(
                f"Waited {timeout}s for {count} message(s) on '{topic_filter}', "
                f"got {len(container)}"
            )
        return container[:count]

    @property
    def raw_client(self) -> mqtt.Client:
        """Access underlying paho client for advanced operations."""
        return self._client


# ---------------------------------------------------------------------------
# API helper
# ---------------------------------------------------------------------------


class ApiTestClient:
    """Thin wrapper around requests.Session for REST API tests."""

    def __init__(self, base_url: str, api_key: str = ""):
        self.base_url = base_url
        self.session = requests.Session()
        self.session.headers.update({"Content-Type": "application/json"})
        if api_key:
            self.session.headers.update({"X-API-Key": api_key})

    def url(self, path: str) -> str:
        return f"{self.base_url}/{path.lstrip('/')}"

    def get(self, path: str, **kwargs) -> requests.Response:
        return self.session.get(self.url(path), **kwargs)

    def post(self, path: str, json_data: Any = None, **kwargs) -> requests.Response:
        return self.session.post(self.url(path), json=json_data, **kwargs)

    def put(self, path: str, json_data: Any = None, **kwargs) -> requests.Response:
        return self.session.put(self.url(path), json=json_data, **kwargs)

    def delete(self, path: str, **kwargs) -> requests.Response:
        return self.session.delete(self.url(path), **kwargs)

    def wait_for_health(self, timeout: float = HEALTH_WAIT_TIMEOUT):
        """Poll /health/live until it returns 200."""
        deadline = time.time() + timeout
        last_err = None
        while time.time() < deadline:
            try:
                r = self.get("/health/live", timeout=5)
                if r.status_code == 200:
                    return
            except requests.exceptions.ConnectionError as e:
                last_err = e
            time.sleep(1)
        raise TimeoutError(
            f"Backend at {self.base_url} not healthy after {timeout}s. "
            f"Last error: {last_err}"
        )


# ---------------------------------------------------------------------------
# Bootstrap helpers
# ---------------------------------------------------------------------------


def _create_farm(api: ApiTestClient, farm_id: str, name: str) -> None:
    """Create a farm via REST API (idempotent)."""
    r = api.post(
        "/api/farms",
        json_data={
            "farm_id": farm_id,
            "name": name,
            "description": f"Test farm {farm_id}",
        },
    )
    assert r.status_code in (200, 201, 409), (
        f"Failed to create farm {farm_id}: {r.status_code} {r.text}"
    )


def _register_coordinator(
    api: ApiTestClient,
    mqtt_client: MqttTestClient,
    farm_id: str,
    coord_id: str,
    name: str,
) -> None:
    """Announce coordinator via MQTT, then approve via REST."""
    # Announce
    mqtt_client.publish(
        f"coordinator/{coord_id}/announce",
        {
            "mac": coord_id,
            "fw_version": "2.1.0",
            "chip_model": "ESP32-S3",
            "free_heap": 200000,
            "wifi_rssi": -45,
            "ip": "192.168.1.100",
        },
    )
    time.sleep(1)  # Let backend process the announce

    # Approve
    r = api.post(
        "/api/coordinators/register/approve",
        json_data={
            "coord_id": coord_id,
            "farm_id": farm_id,
            "name": name,
        },
    )
    # 200 = approved, 400 with "already registered" = idempotent
    assert r.status_code in (200, 400), (
        f"Failed to approve coordinator {coord_id}: {r.status_code} {r.text}"
    )


def _register_tower(
    api: ApiTestClient,
    farm_id: str,
    coord_id: str,
    tower_id: str,
    name: str,
    crop_type: int = 0,
) -> None:
    """Create/register a tower via REST PUT."""
    r = api.put(
        f"/api/towers/{farm_id}/{coord_id}/{tower_id}",
        json_data={
            "name": name,
            "crop_type": crop_type,
        },
    )
    assert r.status_code in (200, 201, 204), (
        f"Failed to create tower {tower_id}: {r.status_code} {r.text}"
    )


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------


@pytest.fixture(scope="session")
def api_client() -> ApiTestClient:
    """Session-scoped API client. Waits for backend health on first use."""
    client = ApiTestClient(API_URL, api_key=API_KEY)
    client.wait_for_health()
    return client


@pytest.fixture(scope="session")
def mqtt_client() -> MqttTestClient:
    """Session-scoped MQTT client."""
    client = MqttTestClient(MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASS)
    client.connect()
    yield client
    client.disconnect()


@pytest.fixture(scope="session")
def bootstrap_farm(
    api_client: ApiTestClient, mqtt_client: MqttTestClient
) -> Dict[str, Any]:
    """
    Create a single test farm with 1 coordinator and 5 towers.
    Returns dict with IDs for use in tests.
    """
    farm_id = "test-farm-001"
    coord_id = "test-coord-001"
    tower_ids = [f"test-tower-{i:03d}" for i in range(1, 6)]

    _create_farm(api_client, farm_id, "Integration Test Farm")
    _register_coordinator(
        api_client, mqtt_client, farm_id, coord_id, "Test Coordinator 1"
    )

    for i, tid in enumerate(tower_ids):
        _register_tower(api_client, farm_id, coord_id, tid, f"Tower {i + 1}")

    # Publish initial connection status so coordinator appears online
    mqtt_client.publish(
        f"farm/{farm_id}/coord/{coord_id}/status/connection",
        {
            "ts": int(time.time()),
            "coord_id": coord_id,
            "farm_id": farm_id,
            "event": "mqtt_connected",
            "wifi_connected": True,
            "wifi_rssi": -45,
            "mqtt_connected": True,
            "uptime_ms": 60000,
            "free_heap": 200000,
        },
        retain=True,
    )

    # Give backend a moment to process all registrations
    time.sleep(2)

    return {
        "farm_id": farm_id,
        "coord_id": coord_id,
        "tower_ids": tower_ids,
    }


@pytest.fixture(scope="function")
def fresh_mqtt_client() -> MqttTestClient:
    """Function-scoped MQTT client for tests that need a clean message buffer."""
    client = MqttTestClient(MQTT_HOST, MQTT_PORT, MQTT_USER, MQTT_PASS)
    client.connect()
    yield client
    client.disconnect()
