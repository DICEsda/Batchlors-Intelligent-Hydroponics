"""S08 -- Growth Cycle (time-lapsed).

Simulates 30 days of growth in 30 minutes (1 day = 1 minute real-time).
All towers start at day 0 (freshly planted).  Height follows sigmoid
growth curves; periodic height "measurements" are published.

Demonstrates growth prediction and harvest-date estimation.
"""

import logging

from core.models import Tower, Coordinator, CROP_CONFIG
from core.physics import growth_sigmoid, noise
from .base import BaseScenario

log = logging.getLogger("simulator.growth_cycle")


class GrowthCycleScenario(BaseScenario):
    NAME = "growth-cycle"
    DESCRIPTION = (
        "30-day growth simulation in 30 minutes.  "
        "Sigmoid growth curves, harvest date prediction."
    )

    def __init__(self, *args, **kwargs):
        # Override speed: 1 sim-day = 1 real-minute -> speed = 1440
        # (1440 sim-seconds per 1 real-second)
        kwargs.setdefault("speed", 1440.0)
        kwargs.setdefault("duration", 1800.0)  # 30 min real
        super().__init__(*args, **kwargs)

    def configure_topology(self) -> None:
        """Start all towers at day 0 (freshly planted)."""
        for farm in self.farms:
            for coord in farm.coordinators:
                for tower in coord.towers:
                    tower.planting_offset_days = 0.0
                    tower.height_cm = 0.0

    def update_tower(
        self, tower: Tower, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Growth is the star here.  Environmental sensors still update."""
        # Call default for temp/humidity/light
        self._default_tower_physics(tower, coord, sim_time_h, dt_h)

        # Override height with clean growth curve (less noise for clarity)
        cfg = CROP_CONFIG[tower.crop_type]
        days = sim_time_h / 24.0  # No planting offset (starts at 0)
        tower.height_cm = round(
            noise(growth_sigmoid(days, cfg["max_height_cm"], cfg["harvest_days"]), 0.1),
            1,
        )

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        """Log growth milestones."""
        days = sim_time_h / 24.0
        # Log every simulated day
        if int(days) > getattr(self, "_last_logged_day", -1):
            self._last_logged_day = int(days)
            # Sample first tower of first coordinator
            sample = self.farms[0].coordinators[0].towers[0] if self.farms else None
            if sample:
                cfg = CROP_CONFIG[sample.crop_type]
                pct = (sample.height_cm / cfg["max_height_cm"]) * 100
                log.info(
                    "Day %d: %s height=%.1f cm (%.0f%% of max %.0f cm)",
                    int(days),
                    sample.crop_type,
                    sample.height_cm,
                    pct,
                    cfg["max_height_cm"],
                )
