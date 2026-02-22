"""S04 -- Heat Stress Event.

Temperature spikes from 24 C to 38 C over 2 hours (HVAC failure),
humidity drops, water temp rises.  Demonstrates anomaly detection
for environmental outliers.
"""

import logging

from core.models import Tower, Reservoir, Coordinator, CROP_CONFIG
from core.physics import (
    clamp,
    noise,
    day_night_temp,
    humidity_from_temp,
    grow_light_schedule,
    growth_sigmoid,
    ph_drift,
    ec_depletion,
    water_level_depletion,
    water_temp_track,
    tds_from_ec,
)
from .base import BaseScenario

log = logging.getLogger("simulator.heat_stress")


class HeatStressScenario(BaseScenario):
    NAME = "heat-stress"
    DESCRIPTION = (
        "Temperature spike from 24 C to 38 C over 2 hours.  "
        "Humidity drops, water temp rises, growth slows."
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._stress_start_h: float = 1.0  # Start stress at sim hour 1
        self._stress_peak_h: float = 3.0  # Peak at sim hour 3
        self._stress_end_h: float = 5.0  # Recovery by sim hour 5
        self._peak_temp: float = 38.0
        self._normal_temp: float = 24.0

    def _current_temp_override(self, sim_time_h: float) -> float | None:
        """Return an overridden ambient temp, or None for normal physics."""
        if sim_time_h < self._stress_start_h:
            return None
        if sim_time_h < self._stress_peak_h:
            # Linear ramp up
            progress = (sim_time_h - self._stress_start_h) / (
                self._stress_peak_h - self._stress_start_h
            )
            return self._normal_temp + progress * (self._peak_temp - self._normal_temp)
        if sim_time_h < self._stress_end_h:
            # Linear ramp down
            progress = (sim_time_h - self._stress_peak_h) / (
                self._stress_end_h - self._stress_peak_h
            )
            return self._peak_temp - progress * (self._peak_temp - self._normal_temp)
        return None  # Back to normal

    def update_tower(
        self, tower: Tower, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        if not tower._online:
            return

        override = self._current_temp_override(sim_time_h)
        if override is not None:
            tower.air_temp_c = noise(override, 0.3)
            # Heat -> low humidity
            tower.humidity_pct = clamp(
                noise(
                    humidity_from_temp(override, base_humidity=70.0, sensitivity=2.5),
                    1.5,
                ),
                20.0,
                99.0,
            )
        else:
            base_temp = day_night_temp(sim_time_h)
            tower.air_temp_c = noise(base_temp, 0.2)
            tower.humidity_pct = clamp(
                noise(humidity_from_temp(tower.air_temp_c), 1.0), 25.0, 99.0
            )

        # Light schedule (unchanged by heat)
        light_on, lux = grow_light_schedule(sim_time_h)
        tower.light_on = light_on
        tower.light_brightness = int(clamp(lux / 120, 0, 255)) if light_on else 0
        tower.light_lux = max(0.0, noise(lux, 50.0) if light_on else noise(3.0, 2.0))

        # Growth slowed during heat stress
        cfg = CROP_CONFIG[tower.crop_type]
        days = tower.planting_offset_days + sim_time_h / 24.0
        base_height = growth_sigmoid(days, cfg["max_height_cm"], cfg["harvest_days"])
        if override is not None and override > 32:
            # Stress penalty: reduce effective growth
            stress_factor = 1.0 - (override - 32) / 20.0
            base_height *= max(0.3, stress_factor)
        tower.height_cm = round(base_height, 1)

        tower.uptime_s = int(sim_time_h * 3600)

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        n_towers = len([t for t in coord.towers if t._online])
        if n_towers == 0:
            return

        override = self._current_temp_override(sim_time_h)
        air_temp = override if override is not None else day_night_temp(sim_time_h)

        # Water temp rises faster during heat stress
        lag = 0.25 if override and override > 30 else 0.15
        reservoir.water_temp_c = round(
            noise(
                water_temp_track(
                    air_temp - 2.0, reservoir.water_temp_c, dt_h, lag_factor=lag
                ),
                0.1,
            ),
            1,
        )

        if reservoir.water_temp_c > 28 and not getattr(self, "_wt_warned", False):
            log.warning(
                "HIGH WATER TEMP: %.1f C on %s at sim=%.1fh",
                reservoir.water_temp_c,
                coord.coord_id,
                sim_time_h,
            )
            self._wt_warned = True

        # EC/pH/water level: standard
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

        reservoir.ph = clamp(noise(ph_drift(reservoir.ph, dt_h), 0.01), 3.0, 9.0)
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

        coord.temp_c = round(noise(air_temp, 0.15), 1)
        coord.uptime_s = int(sim_time_h * 3600)
