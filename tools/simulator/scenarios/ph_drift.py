"""S02 -- pH Drift Crisis.

pH starts normal (6.0) and accelerates downward over time:
  - Hours 0-3: normal drift  (~-0.015/h)  -> 6.0 -> ~5.55
  - Hours 3-5: accelerating  (~-0.08/h)   -> ~5.55 -> ~5.05
  - Hours 5+:  crisis        (~-0.15/h)   -> below 5.0

Demonstrates drift forecasting and anomaly detection.
"""

import logging

from core.models import Reservoir, Coordinator
from core.physics import clamp, noise, ph_drift
from .base import BaseScenario

log = logging.getLogger("simulator.ph_drift")


class PhDriftScenario(BaseScenario):
    NAME = "ph-drift"
    DESCRIPTION = "pH crisis: accelerating acid drift from 6.0 toward 4.5 over 6 hours."

    def configure_topology(self) -> None:
        # Start all reservoirs at a clean pH 6.0
        for farm in self.farms:
            for coord in farm.coordinators:
                coord.reservoir.ph = 6.0

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        # Phase-dependent drift rate
        if sim_time_h < 3.0:
            rate = -0.015  # normal
        elif sim_time_h < 5.0:
            rate = -0.08  # accelerating
        else:
            rate = -0.15  # crisis

        reservoir.ph = clamp(
            noise(ph_drift(reservoir.ph, dt_h, rate=rate), 0.01),
            3.0,
            9.0,
        )

        if reservoir.ph < 5.0 and not hasattr(self, "_crisis_logged"):
            log.warning(
                "CRISIS: pH dropped below 5.0 on %s at sim=%.1fh",
                coord.coord_id,
                sim_time_h,
            )
            self._crisis_logged = True

        # Delegate remaining reservoir physics to the base
        super()._default_reservoir_physics(reservoir, coord, sim_time_h, dt_h)
        # Restore our overridden pH (base would overwrite it)
        # -- not needed: base applies its own ph_drift, so we skip the
        #    super call for pH only.  Re-apply our value:
        # Actually, let's just not call super for reservoir at all.
        # We handle pH ourselves; do the rest inline.

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Full override: custom pH, default everything else."""
        from core.models import CROP_CONFIG
        from core.physics import (
            ec_depletion,
            water_level_depletion,
            water_temp_track,
            tds_from_ec,
            day_night_temp,
        )

        n_towers = len([t for t in coord.towers if t._online])
        if n_towers == 0:
            return

        # --- pH: our custom accelerating drift ---
        if sim_time_h < 3.0:
            rate = -0.015
        elif sim_time_h < 5.0:
            rate = -0.08
        else:
            rate = -0.15

        reservoir.ph = clamp(
            noise(ph_drift(reservoir.ph, dt_h, rate=rate), 0.01), 3.0, 9.0
        )

        if reservoir.ph < 5.0 and not hasattr(self, "_crisis_logged"):
            log.warning(
                "CRISIS: pH below 5.0 on %s at sim=%.1fh", coord.coord_id, sim_time_h
            )
            self._crisis_logged = True

        # --- EC, water, temp: default ---
        total_ec = sum(
            CROP_CONFIG[t.crop_type]["ec_consumption"]
            for t in coord.towers
            if t._online
        )
        total_water = sum(
            CROP_CONFIG[t.crop_type]["water_consumption"]
            for t in coord.towers
            if t._online
        )

        reservoir.ec_ms_cm = clamp(
            noise(ec_depletion(reservoir.ec_ms_cm, dt_h, total_ec), 0.01), 0.0, 10.0
        )
        reservoir.tds_ppm = tds_from_ec(reservoir.ec_ms_cm)

        reservoir.water_level_pct = clamp(
            noise(
                water_level_depletion(
                    reservoir.water_level_pct,
                    dt_h,
                    n_towers,
                    total_water / max(n_towers, 1),
                ),
                0.2,
            ),
            0.0,
            100.0,
        )
        reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)
        reservoir.low_water_alert = reservoir.water_level_pct < 20.0

        air_temp = day_night_temp(sim_time_h)
        reservoir.water_temp_c = round(
            noise(water_temp_track(air_temp - 3.0, reservoir.water_temp_c, dt_h), 0.1),
            1,
        )
        coord.temp_c = round(noise(air_temp, 0.15), 1)
        coord.uptime_s = int(sim_time_h * 3600)
