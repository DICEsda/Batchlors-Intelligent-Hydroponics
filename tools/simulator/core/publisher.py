"""MQTT publisher and REST API bootstrapper for the simulator."""

import json
import logging
import time
from typing import List, Optional

import paho.mqtt.client as mqtt
import requests

from .models import Farm, Coordinator, Tower, Reservoir

log = logging.getLogger("simulator.publisher")

# ---------------------------------------------------------------------------
# MQTT publisher
# ---------------------------------------------------------------------------


class MqttPublisher:
    """Thin wrapper around paho-mqtt for publishing telemetry."""

    def __init__(
        self,
        host: str = "localhost",
        port: int = 1883,
        username: str = "user1",
        password: str = "user1",
        client_id: str = "hydro-simulator",
    ):
        self._host = host
        self._port = port
        self._client = mqtt.Client(client_id=client_id)
        if username:
            self._client.username_pw_set(username, password)
        self._client.on_connect = self._on_connect
        self._client.on_disconnect = self._on_disconnect
        self._connected = False
        self.messages_published: int = 0
        self.errors: int = 0

    # -- lifecycle -----------------------------------------------------------

    def connect(self) -> None:
        log.info("Connecting to MQTT broker %s:%s ...", self._host, self._port)
        self._client.connect(self._host, self._port, keepalive=60)
        self._client.loop_start()
        # Wait for connection
        deadline = time.time() + 10
        while not self._connected and time.time() < deadline:
            time.sleep(0.1)
        if not self._connected:
            raise ConnectionError(
                f"Could not connect to MQTT broker at {self._host}:{self._port}"
            )
        log.info("MQTT connected.")

    def disconnect(self) -> None:
        self._client.loop_stop()
        self._client.disconnect()
        log.info(
            "MQTT disconnected. Total messages: %d, errors: %d",
            self.messages_published,
            self.errors,
        )

    # -- callbacks -----------------------------------------------------------

    def _on_connect(self, client, userdata, flags, rc):
        if rc == 0:
            self._connected = True
            log.debug("MQTT connected (rc=%d)", rc)
        else:
            log.error("MQTT connection failed (rc=%d)", rc)

    def _on_disconnect(self, client, userdata, rc):
        self._connected = False
        if rc != 0:
            log.warning("MQTT unexpected disconnect (rc=%d)", rc)

    # -- publish helpers -----------------------------------------------------

    def _pub(self, topic: str, payload: dict) -> None:
        """Publish a JSON payload to *topic*."""
        try:
            raw = json.dumps(payload)
            info = self._client.publish(topic, raw, qos=0)
            if info.rc == mqtt.MQTT_ERR_SUCCESS:
                self.messages_published += 1
            else:
                self.errors += 1
                log.warning("Publish failed on %s (rc=%d)", topic, info.rc)
        except Exception as exc:
            self.errors += 1
            log.error("Publish exception on %s: %s", topic, exc)

    # -- coordinator ---------------------------------------------------------

    def announce_coordinator(self, coord: Coordinator) -> None:
        """Publish coordinator announce (triggers pending registration)."""
        topic = f"coordinator/{coord.coord_id}/announce"
        self._pub(
            topic,
            {
                "mac": coord.coord_id,
                "fw_version": coord.fw_version,
                "chip_model": "ESP32-S3",
                "free_heap": coord.free_heap,
                "wifi_rssi": coord.wifi_rssi,
                "ip": coord.ip,
            },
        )

    def publish_connection_status(
        self, coord: Coordinator, event: str = "mqtt_connected"
    ) -> None:
        topic = f"farm/{coord.farm_id}/coord/{coord.coord_id}/status/connection"
        self._pub(
            topic,
            {
                "ts": int(time.time()),
                "coord_id": coord.coord_id,
                "farm_id": coord.farm_id,
                "event": event,
                "wifi_connected": coord.is_online,
                "wifi_rssi": coord.wifi_rssi,
                "mqtt_connected": coord.is_online,
                "uptime_ms": coord.uptime_s * 1000,
                "free_heap": coord.free_heap,
            },
        )

    # -- reservoir -----------------------------------------------------------

    def publish_reservoir_telemetry(self, coord: Coordinator) -> None:
        """Publish reservoir telemetry (updates coordinator twin)."""
        if coord.reservoir is None:
            return
        r: Reservoir = coord.reservoir
        topic = f"farm/{coord.farm_id}/coord/{coord.coord_id}/reservoir/telemetry"
        self._pub(
            topic,
            {
                "fw_version": coord.fw_version,
                "towers_online": len([t for t in coord.towers if t._online]),
                "wifi_rssi": coord.wifi_rssi,
                "status_mode": coord.status_mode,
                "uptime_s": coord.uptime_s,
                "temp_c": round(coord.temp_c, 1),
                "ph": round(r.ph, 2),
                "ec_ms_cm": round(r.ec_ms_cm, 2),
                "tds_ppm": round(r.tds_ppm, 0),
                "water_temp_c": round(r.water_temp_c, 1),
                "water_level_pct": round(r.water_level_pct, 1),
                "water_level_cm": round(r.water_level_cm, 1),
                "low_water_alert": r.low_water_alert,
                "main_pump_on": r.main_pump_on,
                "dosing_pump_ph_on": r.dosing_pump_ph_on,
                "dosing_pump_nutrient_on": r.dosing_pump_nutrient_on,
            },
        )

    # -- tower ---------------------------------------------------------------

    def publish_tower_telemetry(self, tower: Tower) -> None:
        topic = (
            f"farm/{tower.farm_id}/coord/{tower.coord_id}"
            f"/tower/{tower.tower_id}/telemetry"
        )
        self._pub(
            topic,
            {
                "air_temp_c": round(tower.air_temp_c, 1),
                "humidity_pct": round(tower.humidity_pct, 1),
                "light_lux": round(tower.light_lux, 0),
                "pump_on": tower.pump_on,
                "light_on": tower.light_on,
                "light_brightness": tower.light_brightness,
                "status_mode": tower.status_mode,
                "vbat_mv": tower.vbat_mv,
                "fw_version": tower.fw_version,
                "uptime_s": tower.uptime_s,
                "signal_quality": tower.signal_quality,
            },
        )

    # -- pairing (for Scenario 6) -------------------------------------------

    def publish_pairing_request(self, coord: Coordinator, tower: Tower) -> None:
        topic = f"farm/{coord.farm_id}/coord/{coord.coord_id}/pairing/request"
        self._pub(
            topic,
            {
                "tower_id": tower.tower_id,
                "mac_address": tower.tower_id,
                "fw_version": tower.fw_version,
                "capabilities": {
                    "dht_sensor": True,
                    "light_sensor": True,
                    "pump_relay": True,
                    "grow_light": True,
                },
                "rssi": tower.signal_quality,
            },
        )

    def publish_pairing_complete(self, coord: Coordinator, tower: Tower) -> None:
        topic = f"farm/{coord.farm_id}/coord/{coord.coord_id}/pairing/complete"
        self._pub(
            topic,
            {
                "tower_id": tower.tower_id,
                "status": "paired",
                "coord_id": coord.coord_id,
            },
        )


# ---------------------------------------------------------------------------
# REST API bootstrapper -- create farms & approve coordinators
# ---------------------------------------------------------------------------


class RestBootstrapper:
    """Call the backend REST API to register farms and coordinators so that
    MQTT telemetry is accepted."""

    def __init__(self, base_url: str = "http://localhost:8000"):
        self._base = base_url.rstrip("/")
        self._session = requests.Session()
        self._session.headers["Content-Type"] = "application/json"

    def health_check(self) -> bool:
        try:
            r = self._session.get(f"{self._base}/health/live", timeout=5)
            return r.status_code == 200
        except Exception:
            return False

    def wait_for_backend(self, timeout: float = 30.0) -> None:
        """Block until the backend is reachable."""
        log.info("Waiting for backend at %s ...", self._base)
        deadline = time.time() + timeout
        while time.time() < deadline:
            if self.health_check():
                log.info("Backend is healthy.")
                return
            time.sleep(1)
        raise ConnectionError(f"Backend not reachable at {self._base} after {timeout}s")

    def create_farm(self, farm: Farm) -> bool:
        """POST /api/v1/farms -- create farm (ignores 409 Conflict)."""
        url = f"{self._base}/api/v1/farms"
        body = {
            "farm_id": farm.farm_id,
            "name": farm.name,
            "description": f"Simulated farm {farm.farm_id}",
        }
        try:
            r = self._session.post(url, json=body, timeout=5)
            if r.status_code in (200, 201):
                log.info("  Created farm %s", farm.farm_id)
                return True
            if r.status_code == 409:
                log.debug("  Farm %s already exists", farm.farm_id)
                return True
            log.warning("  Farm creation failed (%d): %s", r.status_code, r.text[:200])
            return False
        except Exception as exc:
            log.error("  Farm creation error: %s", exc)
            return False

    def approve_coordinator(self, coord: Coordinator) -> bool:
        """POST /api/coordinators/register/approve."""
        url = f"{self._base}/api/coordinators/register/approve"
        body = {
            "coord_id": coord.coord_id,
            "farm_id": coord.farm_id,
            "name": coord.name,
        }
        try:
            r = self._session.post(url, json=body, timeout=5)
            if r.status_code == 200:
                log.info("    Approved coordinator %s", coord.coord_id)
                return True
            if r.status_code == 400 and "already registered" in r.text.lower():
                log.debug("    Coordinator %s already registered", coord.coord_id)
                return True
            log.warning("    Approval failed (%d): %s", r.status_code, r.text[:200])
            return False
        except Exception as exc:
            log.error("    Approval error: %s", exc)
            return False

    def bootstrap(self, farms: List[Farm], mqtt_pub: MqttPublisher) -> None:
        """Full bootstrap: create farms, announce + approve coordinators."""
        self.wait_for_backend()

        log.info("--- Bootstrap: creating %d farms ---", len(farms))
        for farm in farms:
            self.create_farm(farm)

        log.info("--- Bootstrap: announcing & approving coordinators ---")
        # Announce all coordinators via MQTT first
        for farm in farms:
            for coord in farm.coordinators:
                mqtt_pub.announce_coordinator(coord)
        time.sleep(2)  # Let backend process announces

        # Now approve each via REST
        for farm in farms:
            for coord in farm.coordinators:
                self.approve_coordinator(coord)
                time.sleep(0.05)  # Small delay to avoid overwhelming

        log.info("--- Bootstrap complete ---")
