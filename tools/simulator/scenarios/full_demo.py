"""S10 -- Full Demo Sequence.

A scripted 15-minute demo that cycles through multiple scenarios:
  0:00 - 1:30  Steady state (warmup, data accumulates)
  1:30 - 4:00  pH drift begins on farm-001 coordinators
  4:00 - 6:00  Heat stress event on farm-002 coordinators
  6:00 - 8:00  Water emergency on farm-003 coordinators
  8:00 - 10:00 Recovery (all normal)
  10:00-12:00  Nutrient depletion on farm-004 coordinators
  12:00-14:00  Coordinator reconnection on farm-005
  14:00-15:00  Final steady state

Each phase only affects its target farm; others stay normal.
"""

import logging

from core.models import Coordinator, Reservoir, CROP_CONFIG
from core.physics import (
    clamp,
    noise,
    ph_drift,
    ec_depletion,
    water_level_depletion,
    water_temp_track,
    tds_from_ec,
    day_night_temp,
)
from .base import BaseScenario

log = logging.getLogger("simulator.full_demo")


class FullDemoScenario(BaseScenario):
    NAME = "full-demo"
    DESCRIPTION = (
        "15-minute scripted demo cycling through pH drift, heat stress, "
        "water emergency, nutrient depletion, and reconnection."
    )

    def __init__(self, *args, **kwargs):
        kwargs.setdefault("duration", 900.0)  # 15 min real
        super().__init__(*args, **kwargs)
        self._phase = "warmup"
        self._phase_logged: set[str] = set()

    def _log_phase(self, name: str) -> None:
        if name not in self._phase_logged:
            self._phase_logged.add(name)
            log.info("=== DEMO PHASE: %s ===", name)
            self._phase = name

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        sim_min = sim_time_h * 60.0

        if sim_min < 1.5:
            self._log_phase("warmup")
        elif sim_min < 4.0:
            self._log_phase("ph-drift (farm-001)")
        elif sim_min < 6.0:
            self._log_phase("heat-stress (farm-002)")
        elif sim_min < 8.0:
            self._log_phase("water-emergency (farm-003)")
        elif sim_min < 10.0:
            self._log_phase("recovery")
            # Reconnect any offline coordinators
            for f in self.farms:
                for c in f.coordinators:
                    c.is_online = True
                    for t in c.towers:
                        t._online = True
        elif sim_min < 12.0:
            self._log_phase("nutrient-depletion (farm-004)")
        elif sim_min < 14.0:
            self._log_phase("reconnection (farm-005)")
            # Disconnect farm-005 coordinators
            if len(self.farms) >= 5:
                for c in self.farms[4].coordinators:
                    c.is_online = False
                    for t in c.towers:
                        t._online = False
        else:
            self._log_phase("final steady-state")
            # Reconnect everything
            for f in self.farms:
                for c in f.coordinators:
                    c.is_online = True
                    for t in c.towers:
                        t._online = True

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        sim_min = sim_time_h * 60.0
        farm_id = coord.farm_id

        # Decide which modifier to apply based on current phase + farm
        if farm_id == "farm-001" and 1.5 <= sim_min < 4.0:
            self._ph_drift_reservoir(reservoir, coord, sim_time_h, dt_h)
        elif farm_id == "farm-003" and 6.0 <= sim_min < 8.0:
            self._water_emergency_reservoir(reservoir, coord, sim_time_h, dt_h)
        elif farm_id == "farm-004" and 10.0 <= sim_min < 12.0:
            self._nutrient_depletion_reservoir(reservoir, coord, sim_time_h, dt_h)
        else:
            super()._default_reservoir_physics(reservoir, coord, sim_time_h, dt_h)

    def _ph_drift_reservoir(
        self, r: Reservoir, c: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Accelerated pH drift for the demo."""
        r.ph = clamp(noise(ph_drift(r.ph, dt_h, rate=-0.12), 0.01), 3.0, 9.0)
        # Keep rest normal
        self._standard_rest(r, c, sim_time_h, dt_h)

    def _water_emergency_reservoir(
        self, r: Reservoir, c: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Accelerated water loss."""
        n = len([t for t in c.towers if t._online])
        r.water_level_pct = clamp(r.water_level_pct - 0.8 * n * dt_h, 0.0, 100.0)
        r.water_level_cm = round(r.water_level_pct * 0.4, 1)
        r.low_water_alert = r.water_level_pct < 20.0
        if r.water_level_pct < 20.0:
            r.main_pump_on = False
        self._standard_rest(r, c, sim_time_h, dt_h)

    def _nutrient_depletion_reservoir(
        self, r: Reservoir, c: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Accelerated EC drop."""
        total_ec = sum(
            CROP_CONFIG[t.crop_type]["ec_consumption"] * 3.0
            for t in c.towers
            if t._online
        )
        r.ec_ms_cm = clamp(
            noise(ec_depletion(r.ec_ms_cm, dt_h, total_ec), 0.01), 0.0, 10.0
        )
        r.tds_ppm = tds_from_ec(r.ec_ms_cm)
        self._standard_rest(r, c, sim_time_h, dt_h)

    def _standard_rest(
        self, r: Reservoir, c: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Apply default physics for fields not overridden."""
        n = len([t for t in c.towers if t._online])
        if n == 0:
            return
        air_temp = day_night_temp(sim_time_h)
        r.water_temp_c = round(
            noise(water_temp_track(air_temp - 3.0, r.water_temp_c, dt_h), 0.1), 1
        )
        c.temp_c = round(noise(air_temp, 0.15), 1)
        c.uptime_s = int(sim_time_h * 3600)
