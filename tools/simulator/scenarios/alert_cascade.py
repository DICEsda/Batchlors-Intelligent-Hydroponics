"""S13 -- Alert Cascade.

Triggers multiple alert types simultaneously across different farms,
then recovers everything.  Tests alert creation, deduplication, and
auto-resolution through the AlertService thresholds.

Timeline (simulation minutes):
  0:00-1:00  Warmup -- normal values, no alerts
  1:00-3:00  pH drops to ~4.5              (farm-001 reservoirs)  -> ph_out_of_range
  2:00-4:00  Temperature spikes to ~38 C   (farm-002 towers)     -> temperature_high
  3:00-5:00  Water level drops to ~10%     (farm-001 reservoirs)  -> water_level
  4:00-6:00  Main pump fails              (farm-003 reservoirs)  -> pump_failure
  6:00-8:00  All values recover gradually  (all farms)           -> alerts auto-resolve
  8:00-10:00 Steady state                  (all farms)           -> no active alerts

Phase times are expressed in hours so the scenario works at any --speed.
"""

import logging
from typing import List

from core.models import Tower, Reservoir, Coordinator, Farm, CROP_CONFIG
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

log = logging.getLogger("simulator.alert_cascade")

# ---------------------------------------------------------------------------
# Phase boundaries (minutes -> hours)
# ---------------------------------------------------------------------------
PH_DRIFT_START = 1.0 / 60.0  # 1 min
TEMP_SPIKE_START = 2.0 / 60.0  # 2 min
WATER_DROP_START = 3.0 / 60.0  # 3 min
PUMP_FAIL_START = 4.0 / 60.0  # 4 min
RECOVERY_START = 6.0 / 60.0  # 6 min
STEADY_STATE_START = 8.0 / 60.0  # 8 min


class AlertCascadeScenario(BaseScenario):
    NAME = "alert-cascade"
    DESCRIPTION = (
        "Triggers multiple alert types simultaneously across farms, "
        "then recovers. Tests alert creation, deduplication, and auto-resolution."
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._target_farms: List[str] = []

        # Phase-transition flags (logged once per transition)
        self._ph_started = False
        self._temp_started = False
        self._water_started = False
        self._pump_started = False
        self._recovery_started = False
        self._steady_started = False

    # -- topology -----------------------------------------------------------

    def configure_topology(self) -> None:
        """Store the first 3 farm IDs.  If fewer than 3 farms exist, reuse
        farm IDs so every alert type still fires."""
        farm_ids = [f.farm_id for f in self.farms]
        if len(farm_ids) >= 3:
            self._target_farms = farm_ids[:3]
        elif len(farm_ids) == 2:
            self._target_farms = [farm_ids[0], farm_ids[1], farm_ids[0]]
        else:
            self._target_farms = [farm_ids[0]] * 3

        log.info(
            "Alert cascade targets: pH/water=%s  temp=%s  pump=%s",
            self._target_farms[0],
            self._target_farms[1],
            self._target_farms[2],
        )

    # -- per-tick event log -------------------------------------------------

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        if sim_time_h >= PH_DRIFT_START and not self._ph_started:
            self._ph_started = True
            log.warning("ALERT CASCADE: pH drift started on %s", self._target_farms[0])

        if sim_time_h >= TEMP_SPIKE_START and not self._temp_started:
            self._temp_started = True
            log.warning(
                "ALERT CASCADE: temperature spike started on %s",
                self._target_farms[1],
            )

        if sim_time_h >= WATER_DROP_START and not self._water_started:
            self._water_started = True
            log.warning(
                "ALERT CASCADE: water level drop started on %s",
                self._target_farms[0],
            )

        if sim_time_h >= PUMP_FAIL_START and not self._pump_started:
            self._pump_started = True
            log.warning(
                "ALERT CASCADE: pump failure started on %s", self._target_farms[2]
            )

        if sim_time_h >= RECOVERY_START and not self._recovery_started:
            self._recovery_started = True
            log.info("ALERT CASCADE: recovery phase -- all values returning to normal")

        if sim_time_h >= STEADY_STATE_START and not self._steady_started:
            self._steady_started = True
            log.info("ALERT CASCADE: steady state -- all alerts should be resolved")

    # -- reservoir overrides ------------------------------------------------

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        # Apply default physics first (pH, EC, water level, temp, etc.)
        self._default_reservoir_physics(reservoir, coord, sim_time_h, dt_h)

        # --- Farm-001: pH crash + water-level emergency ---
        if coord.farm_id == self._target_farms[0]:
            # pH drift down aggressively (target ~4.5)
            if PH_DRIFT_START <= sim_time_h < RECOVERY_START:
                reservoir.ph = max(4.0, reservoir.ph - 0.5 * dt_h * 60)
            # Water level plummets (target ~10%)
            if WATER_DROP_START <= sim_time_h < RECOVERY_START:
                reservoir.water_level_pct = max(
                    5.0, reservoir.water_level_pct - 3.0 * dt_h * 60
                )
                reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)
                reservoir.low_water_alert = True
            # Recovery: gently restore values
            if sim_time_h >= RECOVERY_START:
                reservoir.ph = min(6.2, reservoir.ph + 0.3 * dt_h * 60)
                reservoir.water_level_pct = min(
                    80.0, reservoir.water_level_pct + 2.0 * dt_h * 60
                )
                reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)
                reservoir.low_water_alert = reservoir.water_level_pct < 20.0

        # --- Farm-003: pump failure ---
        if coord.farm_id == self._target_farms[2]:
            if PUMP_FAIL_START <= sim_time_h < RECOVERY_START:
                reservoir.main_pump_on = False
            if sim_time_h >= RECOVERY_START:
                reservoir.main_pump_on = True

    # -- tower overrides ----------------------------------------------------

    def update_tower(
        self, tower: Tower, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        # Apply default physics first
        self._default_tower_physics(tower, coord, sim_time_h, dt_h)

        # --- Farm-002: temperature spike (target ~38 C) ---
        if coord.farm_id == self._target_farms[1]:
            if TEMP_SPIKE_START <= sim_time_h < RECOVERY_START:
                tower.air_temp_c = noise(38.0, 1.0)
                # Humidity drops under extreme heat
                tower.humidity_pct = clamp(
                    noise(
                        humidity_from_temp(38.0, base_humidity=70.0, sensitivity=2.5),
                        1.5,
                    ),
                    20.0,
                    99.0,
                )
            if sim_time_h >= RECOVERY_START:
                tower.air_temp_c = noise(22.0, 0.5)
                tower.humidity_pct = clamp(
                    noise(humidity_from_temp(22.0), 1.0), 25.0, 99.0
                )
