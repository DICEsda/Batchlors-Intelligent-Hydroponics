"""S05 -- Water Level Emergency.

Water level drops from 80% to 15% over 8 hours.
  - Hours 0-4: normal consumption (80% -> ~55%)
  - Hours 4-7: accelerated (pump cavitation / leak) -> ~20%
  - Hour 7+:   low-water alert fires, pump stops, pH/EC go erratic.
"""

import logging
import random

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

log = logging.getLogger("simulator.water_emergency")


class WaterEmergencyScenario(BaseScenario):
    NAME = "water-emergency"
    DESCRIPTION = (
        "Water level drops from 80% to <15% in 8 hours.  "
        "Pump cavitation, low-water alert, erratic pH/EC readings."
    )

    def configure_topology(self) -> None:
        for farm in self.farms:
            for coord in farm.coordinators:
                coord.reservoir.water_level_pct = 80.0
                coord.reservoir.water_level_cm = 32.0
                coord.reservoir.ph = 6.0

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        n_towers = len([t for t in coord.towers if t._online])
        if n_towers == 0:
            return

        # Phase-dependent water depletion rate
        base_water = sum(
            CROP_CONFIG[t.crop_type]["water_consumption"]
            for t in coord.towers
            if t._online
        )

        if sim_time_h < 4.0:
            water_rate = base_water / max(n_towers, 1)  # Normal
        elif sim_time_h < 7.0:
            water_rate = (base_water / max(n_towers, 1)) * 3.0  # Leak / cavitation
        else:
            water_rate = (base_water / max(n_towers, 1)) * 0.2  # Pump stopped

        reservoir.water_level_pct = clamp(
            noise(
                water_level_depletion(
                    reservoir.water_level_pct, dt_h, n_towers, water_rate
                ),
                0.3,
            ),
            0.0,
            100.0,
        )
        reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)

        # Low-water alert
        if reservoir.water_level_pct < 20.0:
            reservoir.low_water_alert = True
            reservoir.main_pump_on = False  # Auto-stop to prevent dry-run
            if not getattr(self, "_low_warned", False):
                log.warning(
                    "LOW WATER ALERT on %s at sim=%.1fh (%.1f%%)",
                    coord.coord_id,
                    sim_time_h,
                    reservoir.water_level_pct,
                )
                self._low_warned = True

        # pH/EC become erratic at low volume (concentrated + unstable)
        if reservoir.water_level_pct < 25.0:
            ec_noise = 0.15  # Much more noise
            ph_noise = 0.08
            # EC spikes as water concentrates
            reservoir.ec_ms_cm = clamp(
                reservoir.ec_ms_cm + random.gauss(0.02, ec_noise) * dt_h, 0.0, 10.0
            )
            reservoir.ph = clamp(
                reservoir.ph + random.gauss(-0.02, ph_noise) * dt_h, 3.0, 9.0
            )
        else:
            # Normal EC/pH
            total_ec = sum(
                CROP_CONFIG[t.crop_type]["ec_consumption"]
                for t in coord.towers
                if t._online
            )
            reservoir.ph = clamp(noise(ph_drift(reservoir.ph, dt_h), 0.01), 3.0, 9.0)
            reservoir.ec_ms_cm = clamp(
                noise(ec_depletion(reservoir.ec_ms_cm, dt_h, total_ec), 0.01), 0.0, 10.0
            )

        reservoir.tds_ppm = tds_from_ec(reservoir.ec_ms_cm)

        # Water temp
        air_temp = day_night_temp(sim_time_h)
        reservoir.water_temp_c = round(
            noise(water_temp_track(air_temp - 3.0, reservoir.water_temp_c, dt_h), 0.1),
            1,
        )
        coord.temp_c = round(noise(air_temp, 0.15), 1)
        coord.uptime_s = int(sim_time_h * 3600)
