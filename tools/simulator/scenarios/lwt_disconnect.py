"""S12 -- LWT Disconnect Detection.

Tests the MQTT broker's Last Will and Testament mechanism:
  - 0-2 min:  Normal operation, all coordinators online
  - 2 min:    50% of coordinators forcefully disconnect (socket close, no DISCONNECT packet)
  - 2-7 min:  Broker publishes LWT "disconnected" messages automatically
              Remaining coordinators continue publishing normally
  - 7 min:    Disconnected coordinators reconnect with fresh LWT
  - 7+ min:   All online, publishing "connected" status

Unlike the 'reconnection' scenario which manually publishes disconnect events,
this scenario relies on the broker's LWT mechanism for disconnect detection.
"""

import json
import logging
import random
import time

import paho.mqtt.client as paho_mqtt

from core.models import Coordinator
from .base import BaseScenario

log = logging.getLogger("simulator.lwt_disconnect")


# ---------------------------------------------------------------------------
# Per-coordinator MQTT client with LWT configured
# ---------------------------------------------------------------------------


class CoordinatorMqttClient:
    """Dedicated MQTT client for a single coordinator, with LWT set so the
    broker publishes a retained ``disconnected`` status when the socket is
    closed without a clean DISCONNECT packet."""

    def __init__(
        self,
        coord: Coordinator,
        host: str,
        port: int,
        user: str,
        password: str,
    ):
        self.coord = coord
        self._host = host
        self._port = port
        self._connected = False

        client_id = f"sim-coord-{coord.coord_id}"
        self._client = paho_mqtt.Client(client_id=client_id)
        if user:
            self._client.username_pw_set(user, password)

        self._client.on_connect = self._on_connect
        self._client.on_disconnect = self._on_disconnect

        # -- Configure LWT ------------------------------------------------
        lwt_topic = f"farm/{coord.farm_id}/coord/{coord.coord_id}/status/connection"
        lwt_payload = json.dumps(
            {
                "ts": 0,
                "coord_id": coord.coord_id,
                "farm_id": coord.farm_id,
                "event": "disconnected",
                "wifi_connected": False,
                "mqtt_connected": False,
            }
        )
        self._client.will_set(lwt_topic, lwt_payload, qos=0, retain=True)

    # -- lifecycle --------------------------------------------------------

    def connect(self) -> None:
        """Connect to the broker and publish a retained 'connected' status."""
        log.debug("Connecting coordinator client %s ...", self.coord.coord_id)
        self._client.connect(self._host, self._port, keepalive=60)
        self._client.loop_start()

        # Wait for the connection callback
        deadline = time.time() + 10
        while not self._connected and time.time() < deadline:
            time.sleep(0.1)
        if not self._connected:
            log.error("Coordinator client %s failed to connect", self.coord.coord_id)
            return

        self._publish_status("connected")
        log.info("Coordinator client %s connected (LWT armed)", self.coord.coord_id)

    def force_disconnect(self) -> None:
        """Forcefully close the socket *without* sending a DISCONNECT packet.

        This is the key mechanism: the broker detects the unclean disconnect
        via keep-alive timeout and publishes the LWT message on behalf of
        the now-dead client.
        """
        try:
            sock = self._client._sock
            if sock is not None:
                sock.close()
                self._client._sock = None
        except Exception as exc:
            log.debug(
                "Socket close exception for %s (expected): %s",
                self.coord.coord_id,
                exc,
            )
        self._client.loop_stop()
        self._connected = False
        log.warning(
            "FORCE DISCONNECT: %s (LWT will fire after keep-alive expiry)",
            self.coord.coord_id,
        )

    def reconnect(self) -> None:
        """Reconnect (re-arms LWT) and publish a retained 'connected' status."""
        log.debug("Reconnecting coordinator client %s ...", self.coord.coord_id)
        self._client.reconnect()
        self._client.loop_start()

        deadline = time.time() + 10
        while not self._connected and time.time() < deadline:
            time.sleep(0.1)
        if not self._connected:
            log.error(
                "Coordinator client %s failed to reconnect",
                self.coord.coord_id,
            )
            return

        self._publish_status("connected")
        log.info("Coordinator client %s reconnected", self.coord.coord_id)

    def cleanup(self) -> None:
        """Cleanly shut down the paho loop (best-effort)."""
        try:
            self._client.loop_stop()
            self._client.disconnect()
        except Exception:
            pass

    # -- internal ---------------------------------------------------------

    def _publish_status(self, event: str) -> None:
        topic = (
            f"farm/{self.coord.farm_id}/coord/{self.coord.coord_id}/status/connection"
        )
        payload = json.dumps(
            {
                "ts": int(time.time()),
                "coord_id": self.coord.coord_id,
                "farm_id": self.coord.farm_id,
                "event": event,
                "wifi_connected": event == "connected",
                "mqtt_connected": event == "connected",
                "uptime_ms": self.coord.uptime_s * 1000,
                "free_heap": self.coord.free_heap,
            }
        )
        self._client.publish(topic, payload, qos=0, retain=True)

    def _on_connect(self, client, userdata, flags, rc):
        if rc == 0:
            self._connected = True
            log.debug("Coordinator client %s connected (rc=0)", self.coord.coord_id)
        else:
            log.error(
                "Coordinator client %s connect failed (rc=%d)",
                self.coord.coord_id,
                rc,
            )

    def _on_disconnect(self, client, userdata, rc):
        self._connected = False
        if rc != 0:
            log.debug(
                "Coordinator client %s unexpected disconnect (rc=%d)",
                self.coord.coord_id,
                rc,
            )


# ---------------------------------------------------------------------------
# Scenario
# ---------------------------------------------------------------------------


class LwtDisconnectScenario(BaseScenario):
    NAME = "lwt-disconnect"
    DESCRIPTION = (
        "Tests broker LWT mechanism: coordinators forcefully disconnect, "
        "broker publishes retained 'disconnected' status automatically."
    )

    def __init__(
        self, *args, mqtt_user: str = "user1", mqtt_pass: str = "user1", **kwargs
    ):
        super().__init__(*args, **kwargs)
        self._mqtt_user = mqtt_user
        self._mqtt_pass = mqtt_pass

        self._coord_clients: list[CoordinatorMqttClient] = []
        self._offline_clients: list[CoordinatorMqttClient] = []

        # Phase thresholds (sim-hours, same convention as reconnection.py)
        self._disconnect_h = 2.0 / 60.0  # 2 minutes
        self._reconnect_h = 7.0 / 60.0  # 7 minutes
        self._disconnected = False
        self._reconnected = False

    # -- hooks ------------------------------------------------------------

    def configure_topology(self) -> None:
        """Pre-select which coordinators will be forcefully disconnected."""
        all_coords = [c for f in self.farms for c in f.coordinators]
        random.seed(42)
        n_offline = max(1, len(all_coords) // 2)
        self._target_coords = random.sample(all_coords, n_offline)
        log.info(
            "LWT disconnect scenario: %d of %d coordinators will force-disconnect",
            n_offline,
            len(all_coords),
        )

    def on_start(self) -> None:
        """Create and connect a dedicated MQTT client per coordinator."""
        host = self.mqtt._host
        port = self.mqtt._port

        all_coords = [c for f in self.farms for c in f.coordinators]
        log.info("Creating %d per-coordinator MQTT clients ...", len(all_coords))
        for coord in all_coords:
            client = CoordinatorMqttClient(
                coord=coord,
                host=host,
                port=port,
                user=self._mqtt_user,
                password=self._mqtt_pass,
            )
            client.connect()
            self._coord_clients.append(client)

        log.info(
            "All %d coordinator clients connected with LWT armed",
            len(self._coord_clients),
        )

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        # Phase 2: Force-disconnect 50 % of coordinators
        if sim_time_h >= self._disconnect_h and not self._disconnected:
            self._disconnected = True
            target_ids = {c.coord_id for c in self._target_coords}
            for cc in self._coord_clients:
                if cc.coord.coord_id in target_ids:
                    cc.force_disconnect()
                    cc.coord.is_online = False
                    for tower in cc.coord.towers:
                        tower._online = False
                    self._offline_clients.append(cc)
            log.warning(
                "Phase 2: %d coordinators force-disconnected (LWT pending)",
                len(self._offline_clients),
            )

        # Phase 3: Reconnect
        if (
            sim_time_h >= self._reconnect_h
            and self._disconnected
            and not self._reconnected
        ):
            self._reconnected = True
            for cc in self._offline_clients:
                cc.reconnect()
                cc.coord.is_online = True
                for tower in cc.coord.towers:
                    tower._online = True
            log.info(
                "Phase 3: %d coordinators reconnected (LWT re-armed)",
                len(self._offline_clients),
            )

    def run(self) -> None:
        """Run the base simulation loop, then clean up per-coordinator clients."""
        try:
            super().run()
        finally:
            log.info("Cleaning up %d coordinator clients ...", len(self._coord_clients))
            for cc in self._coord_clients:
                cc.cleanup()
