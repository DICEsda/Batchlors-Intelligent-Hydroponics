"""
DeviceSimulator — simulates an ESP32 device subscribing to downstream MQTT
command topics. Used by integration tests to verify that REST API calls
result in the correct MQTT commands being published to the device.
"""

import json
import os
import time
import threading
import uuid
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

import paho.mqtt.client as mqtt


MQTT_HOST = os.environ.get("SIM_MQTT_HOST", "mosquitto")
MQTT_PORT = int(os.environ.get("SIM_MQTT_PORT", "1883"))
MQTT_USER = os.environ.get("SIM_MQTT_USER", "user1")
MQTT_PASS = os.environ.get("SIM_MQTT_PASS", "user1")


@dataclass
class ReceivedCommand:
    """A command received by the simulated device."""
    topic: str
    payload: Any
    timestamp: float = field(default_factory=time.time)


class DeviceSimulator:
    """
    Simulates an ESP32 coordinator + tower nodes subscribing to downstream
    MQTT command topics. Records commands for test assertions.
    
    Usage:
        sim = DeviceSimulator(farm_id="farm-1", coord_id="coord-1",
                              tower_ids=["tower-1", "tower-2"])
        sim.start()
        # ... trigger REST API call that sends MQTT command ...
        cmd = sim.wait_for_command("tower/+/cmd", timeout=10)
        assert cmd.payload["cmd"] == "set_light"
        sim.stop()
    """

    def __init__(
        self,
        farm_id: str,
        coord_id: str,
        tower_ids: Optional[List[str]] = None,
        host: str = MQTT_HOST,
        port: int = MQTT_PORT,
        username: str = MQTT_USER,
        password: str = MQTT_PASS,
    ):
        self.farm_id = farm_id
        self.coord_id = coord_id
        self.tower_ids = tower_ids or []
        self._host = host
        self._port = port

        self._client_id = f"device-sim-{uuid.uuid4().hex[:8]}"
        self._client = mqtt.Client(client_id=self._client_id, clean_session=True)
        self._client.username_pw_set(username, password)

        self._connected = threading.Event()
        self._commands: List[ReceivedCommand] = []
        self._waiters: List[tuple] = []  # (topic_filter, event, container)
        self._lock = threading.Lock()

        self._client.on_connect = self._on_connect
        self._client.on_message = self._on_message

    def _on_connect(self, client, userdata, flags, rc):
        if rc == 0:
            self._connected.set()
            # Subscribe to all downstream command topics
            subs = self._get_subscriptions()
            for topic in subs:
                client.subscribe(topic, qos=0)

    def _get_subscriptions(self) -> List[str]:
        """Return all MQTT topics this simulated device should subscribe to."""
        f, c = self.farm_id, self.coord_id
        topics = [
            # Coordinator-level commands
            f"farm/{f}/coord/{c}/cmd",
            f"farm/{f}/coord/{c}/reservoir/cmd",
            f"farm/{f}/coord/{c}/ota/start",
            f"farm/{f}/coord/{c}/ota/cancel",
            f"coordinator/{c}/config",
            f"coordinator/{c}/cmd",
            f"coordinator/{c}/registered",
            # Wildcard for all tower commands under this coordinator
            f"farm/{f}/coord/{c}/tower/+/cmd",
        ]
        return topics

    def _on_message(self, client, userdata, msg):
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            payload = msg.payload

        cmd = ReceivedCommand(
            topic=msg.topic,
            payload=payload,
        )

        with self._lock:
            self._commands.append(cmd)
            # Wake up any matching waiters
            for topic_filter, event, container in self._waiters:
                if mqtt.topic_matches_sub(topic_filter, msg.topic):
                    container.append(cmd)
                    event.set()

    def start(self, timeout: float = 15):
        """Connect and subscribe to all command topics."""
        self._client.connect(self._host, self._port, keepalive=60)
        self._client.loop_start()
        if not self._connected.wait(timeout):
            raise ConnectionError(
                f"DeviceSimulator failed to connect to MQTT at "
                f"{self._host}:{self._port} within {timeout}s"
            )

    def stop(self):
        """Disconnect gracefully."""
        self._client.loop_stop()
        self._client.disconnect()

    def wait_for_command(
        self,
        topic_filter: Optional[str] = None,
        timeout: float = 15,
    ) -> ReceivedCommand:
        """
        Block until a command matching topic_filter arrives.
        If topic_filter is None, matches any command topic.
        Returns the first matching command.
        """
        if topic_filter is None:
            topic_filter = "#"

        # Prepend the farm/coord prefix if the filter is relative
        if not topic_filter.startswith("farm/") and not topic_filter.startswith("coordinator/"):
            topic_filter = f"farm/{self.farm_id}/coord/{self.coord_id}/{topic_filter}"

        event = threading.Event()
        container: List[ReceivedCommand] = []

        with self._lock:
            # Check already-received commands
            for cmd in self._commands:
                if mqtt.topic_matches_sub(topic_filter, cmd.topic):
                    return cmd
            # Register waiter
            waiter = (topic_filter, event, container)
            self._waiters.append(waiter)

        event.wait(timeout=timeout)

        with self._lock:
            if waiter in self._waiters:
                self._waiters.remove(waiter)

        if not container:
            raise TimeoutError(
                f"DeviceSimulator waited {timeout}s for command on "
                f"'{topic_filter}', got nothing. "
                f"Total commands received: {len(self._commands)}"
            )
        return container[0]

    def get_commands(self, topic_filter: Optional[str] = None) -> List[ReceivedCommand]:
        """Return all received commands, optionally filtered by topic pattern."""
        with self._lock:
            if topic_filter is None:
                return list(self._commands)
            return [
                cmd for cmd in self._commands
                if mqtt.topic_matches_sub(topic_filter, cmd.topic)
            ]

    def clear(self):
        """Clear all received commands."""
        with self._lock:
            self._commands.clear()

    def publish_telemetry_ack(
        self,
        tower_id: str,
        overrides: Optional[Dict[str, Any]] = None,
    ):
        """
        Publish tower telemetry that reflects a command being executed.
        This simulates the device responding to a command by updating
        its reported state through telemetry.
        """
        payload = {
            "air_temp_c": 23.5,
            "humidity_pct": 65.0,
            "light_lux": 15000.0,
            "pump_on": False,
            "light_on": False,
            "light_brightness": 0,
            "status_mode": "operational",
            "vbat_mv": 3700,
            "fw_version": "1.2.0",
            "uptime_s": 7200,
            "signal_quality": -38,
        }
        if overrides:
            payload.update(overrides)

        topic = f"farm/{self.farm_id}/coord/{self.coord_id}/tower/{tower_id}/telemetry"
        self._client.publish(topic, json.dumps(payload), qos=0)

    def publish_reservoir_telemetry_ack(
        self,
        overrides: Optional[Dict[str, Any]] = None,
    ):
        """
        Publish reservoir telemetry that reflects a command being executed.
        """
        payload = {
            "fw_version": "2.1.0",
            "towers_online": 5,
            "wifi_rssi": -45,
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
        if overrides:
            payload.update(overrides)

        topic = f"farm/{self.farm_id}/coord/{self.coord_id}/reservoir/telemetry"
        self._client.publish(topic, json.dumps(payload), qos=0)

    def publish_ota_status(
        self,
        status: str = "in_progress",
        progress: int = 0,
        message: str = "",
        error: Optional[str] = None,
    ):
        """Publish OTA progress status as the device would."""
        payload = {
            "status": status,
            "progress": progress,
            "message": message,
            "timestamp": int(time.time()),
        }
        if error:
            payload["error"] = error

        topic = f"farm/{self.farm_id}/coord/{self.coord_id}/ota/status"
        self._client.publish(topic, json.dumps(payload), qos=0)
