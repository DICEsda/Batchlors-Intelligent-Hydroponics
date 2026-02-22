"""S03 -- Nutrient Depletion.

EC drops from ~1.5 to below 0.7 over 12 hours as heavy-feeding crops
(tomato, pepper) drain the reservoir.  Demonstrates consumption-rate
prediction and ``nutrient_top_up_recommended`` alerting.
"""

import logging

from core.models import Reservoir, Coordinator, CROP_CONFIG
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

log = logging.getLogger("simulator.nutrient_depletion")


class NutrientDepletionScenario(BaseScenario):
    NAME = "nutrient-depletion"
    DESCRIPTION = (
        "Fast nutrient drain: EC drops from 1.5 to <0.7 in 12 hours.  "
        "Heavy-feeding crops (tomato, pepper) dominate."
    )

    def configure_topology(self) -> None:
        """Force 60% of towers to high-consumption crops."""
        heavy_crops = ["tomato", "pepper", "kale"]
        light_crops = ["lettuce", "mint", "cilantro"]
        import random

        for farm in self.farms:
            for coord in farm.coordinators:
                coord.reservoir.ec_ms_cm = 1.5
                coord.reservoir.ph = 6.0
                for i, tower in enumerate(coord.towers):
                    if i < len(coord.towers) * 0.6:
                        tower.crop_type = random.choice(heavy_crops)
                    else:
                        tower.crop_type = random.choice(light_crops)

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        n_towers = len([t for t in coord.towers if t._online])
        if n_towers == 0:
            return

        # Boost consumption rates by 2x to accelerate the demo
        total_ec = sum(
            CROP_CONFIG[t.crop_type]["ec_consumption"] * 2.0
            for t in coord.towers
            if t._online
        )
        total_water = sum(
            CROP_CONFIG[t.crop_type]["water_consumption"] * 1.5
            for t in coord.towers
            if t._online
        )

        # pH: normal drift
        reservoir.ph = clamp(noise(ph_drift(reservoir.ph, dt_h), 0.01), 3.0, 9.0)

        # EC: accelerated depletion
        reservoir.ec_ms_cm = clamp(
            noise(ec_depletion(reservoir.ec_ms_cm, dt_h, total_ec), 0.01), 0.0, 10.0
        )
        reservoir.tds_ppm = tds_from_ec(reservoir.ec_ms_cm)

        if reservoir.ec_ms_cm < 0.7 and not getattr(self, "_ec_warned", False):
            log.warning(
                "LOW EC: %.2f mS/cm on %s at sim=%.1fh -- nutrient top-up needed",
                reservoir.ec_ms_cm,
                coord.coord_id,
                sim_time_h,
            )
            self._ec_warned = True

        # Water level
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

        # Water temp
        air_temp = day_night_temp(sim_time_h)
        reservoir.water_temp_c = round(
            noise(water_temp_track(air_temp - 3.0, reservoir.water_temp_c, dt_h), 0.1),
            1,
        )
        coord.temp_c = round(noise(air_temp, 0.15), 1)
        coord.uptime_s = int(sim_time_h * 3600)
