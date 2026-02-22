"""S09 -- Coordinator Reconnection.

Coordinators go offline for a period, then reconnect:
  - 0-2 min:  All online (normal)
  - 2-7 min:  50% of coordinators go offline (telemetry stops)
  - 7+ min:   All reconnect (telemetry resumes)

Backend marks offline twins as Stale (120s) then Offline.
Frontend shows disconnected state, then recovery.
"""

import logging
import random

from core.models import Coordinator
from core.publisher import MqttPublisher
from .base import BaseScenario

log = logging.getLogger("simulator.reconnection")


class ReconnectionScenario(BaseScenario):
    NAME = "reconnection"
    DESCRIPTION = (
        "50% of coordinators go offline for 5 minutes, then reconnect.  "
        "Tests stale detection, offline handling, recovery."
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._offline_coords: list[Coordinator] = []
        self._disconnect_h = 2.0 / 60.0  # 2 minutes in hours
        self._reconnect_h = 7.0 / 60.0  # 7 minutes in hours
        self._disconnected = False
        self._reconnected = False

    def configure_topology(self) -> None:
        """Pre-select which coordinators will go offline."""
        all_coords = [c for f in self.farms for c in f.coordinators]
        # Randomly pick 50%
        random.seed(42)
        n_offline = max(1, len(all_coords) // 2)
        self._offline_coords = random.sample(all_coords, n_offline)
        log.info(
            "Reconnection scenario: %d of %d coordinators will disconnect",
            n_offline,
            len(all_coords),
        )

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        # Phase 2: Disconnect
        if sim_time_h >= self._disconnect_h and not self._disconnected:
            self._disconnected = True
            for coord in self._offline_coords:
                coord.is_online = False
                for tower in coord.towers:
                    tower._online = False
                self.mqtt.publish_connection_status(coord, event="mqtt_disconnected")
                log.warning(
                    "DISCONNECT: %s (%s) went offline", coord.name, coord.coord_id
                )

        # Phase 3: Reconnect
        if (
            sim_time_h >= self._reconnect_h
            and self._disconnected
            and not self._reconnected
        ):
            self._reconnected = True
            for coord in self._offline_coords:
                coord.is_online = True
                for tower in coord.towers:
                    tower._online = True
                self.mqtt.publish_connection_status(coord, event="mqtt_connected")
                log.info("RECONNECT: %s (%s) back online", coord.name, coord.coord_id)
