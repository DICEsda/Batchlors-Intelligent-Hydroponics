"""S06 -- Tower Pairing Flow.

Starts with only coordinators (no towers).  Every 30 simulated seconds,
a new tower "discovers" a coordinator and goes through the pairing
handshake: pairing_request -> pairing_complete -> telemetry starts.

Demonstrates dynamic device onboarding and frontend reactivity.
"""

import logging
import random

from core.models import Tower, Coordinator, CROP_TYPES, CROP_CONFIG
from core.physics import growth_sigmoid
from core.publisher import MqttPublisher
from .base import BaseScenario

log = logging.getLogger("simulator.tower_pairing")


class TowerPairingScenario(BaseScenario):
    NAME = "tower-pairing"
    DESCRIPTION = (
        "Towers pair dynamically: one new tower every 30 sim-seconds.  "
        "Full pairing handshake -> telemetry begins."
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._pending_towers: list[tuple[Coordinator, Tower]] = []
        self._pair_interval_h = 30.0 / 3600.0  # 30 sim-seconds in hours
        self._next_pair_h = 0.0
        self._pair_step: dict[str, int] = {}  # tower_id -> step (0=request, 1=complete)

    def configure_topology(self) -> None:
        """Detach all towers from coordinators; they'll be added during run."""
        for farm in self.farms:
            for coord in farm.coordinators:
                # Move towers to pending list
                for tower in coord.towers:
                    tower._online = False
                    self._pending_towers.append((coord, tower))
                coord.towers = []

        random.shuffle(self._pending_towers)
        log.info(
            "Tower pairing scenario: %d towers queued for pairing",
            len(self._pending_towers),
        )

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        # Check if it's time to pair another tower
        if sim_time_h < self._next_pair_h:
            return
        if not self._pending_towers:
            return

        coord, tower = self._pending_towers.pop(0)
        self._next_pair_h = sim_time_h + self._pair_interval_h

        # Step 1: Send pairing request
        log.info(
            "PAIRING: tower %s -> coord %s (crop=%s)",
            tower.tower_id,
            coord.coord_id,
            tower.crop_type,
        )
        self.mqtt.publish_pairing_request(coord, tower)
        self._pair_step[tower.tower_id] = 0

        # Step 2: After a short delay (next tick), send pairing complete
        # and add tower to coordinator
        tower._online = True
        coord.towers.append(tower)
        self.mqtt.publish_pairing_complete(coord, tower)
        self._pair_step[tower.tower_id] = 1

        paired = sum(len(c.towers) for f in self.farms for c in f.coordinators)
        remaining = len(self._pending_towers)
        log.info("  Paired: %d total, %d remaining", paired, remaining)
