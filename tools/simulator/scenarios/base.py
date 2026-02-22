"""Base scenario -- default physics loop shared by all scenarios."""

import logging
import signal
import time
from typing import List

from core.models import (
    Farm,
    Coordinator,
    Tower,
    Reservoir,
    CROP_CONFIG,
)
from core.physics import (
    clamp,
    noise,
    day_night_temp,
    humidity_from_temp,
    grow_light_schedule,
    ph_drift,
    ec_depletion,
    water_level_depletion,
    water_temp_track,
    tds_from_ec,
    growth_sigmoid,
)
from core.publisher import MqttPublisher

log = logging.getLogger("simulator.scenario")


class BaseScenario:
    """Default "steady-state" simulation loop.

    Subclasses override :meth:`on_tick`, :meth:`update_tower`,
    :meth:`update_reservoir`, or :meth:`configure_topology` to inject
    scenario-specific behaviour.
    """

    NAME = "base"
    DESCRIPTION = "Base scenario (override in subclass)"

    def __init__(
        self,
        farms: List[Farm],
        mqtt: MqttPublisher,
        speed: float = 1.0,
        interval: float = 5.0,
        duration: float = 3600.0,
    ):
        self.farms = farms
        self.mqtt = mqtt
        self.speed = speed  # sim-seconds per real-second
        self.interval = interval  # real seconds between telemetry publishes
        self.duration = duration  # real seconds to run
        self.sim_time: float = 0.0  # accumulated simulated seconds
        self._running = True

        # Wire up Ctrl-C
        signal.signal(signal.SIGINT, self._handle_sigint)

    # -- hooks for subclasses ------------------------------------------------

    def configure_topology(self) -> None:
        """Called once before the main loop.  Override to tweak crops, counts,
        initial sensor values, etc."""

    def on_start(self) -> None:
        """Called once after bootstrap, before first tick."""

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        """Called every tick.  Override to inject timed events."""

    def update_tower(
        self, tower: Tower, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Update one tower's sensors.  Override for abnormal behaviour."""
        self._default_tower_physics(tower, coord, sim_time_h, dt_h)

    def update_reservoir(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        """Update one reservoir's sensors.  Override for abnormal behaviour."""
        self._default_reservoir_physics(reservoir, coord, sim_time_h, dt_h)

    # -- default physics implementations -------------------------------------

    def _default_tower_physics(
        self, tower: Tower, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        if not tower._online:
            return

        # Temperature: day/night sine + per-tower noise
        base_temp = day_night_temp(sim_time_h)
        tower.air_temp_c = noise(base_temp, 0.2)

        # Humidity
        tower.humidity_pct = clamp(
            noise(humidity_from_temp(tower.air_temp_c), 1.0), 25.0, 99.0
        )

        # Light schedule
        light_on, lux = grow_light_schedule(sim_time_h)
        tower.light_on = light_on
        tower.light_brightness = int(clamp(lux / 120, 0, 255)) if light_on else 0
        tower.light_lux = noise(lux, 50.0) if light_on else noise(3.0, 2.0)
        tower.light_lux = max(0.0, tower.light_lux)

        # Growth
        cfg = CROP_CONFIG[tower.crop_type]
        days = tower.planting_offset_days + sim_time_h / 24.0
        tower.height_cm = round(
            growth_sigmoid(days, cfg["max_height_cm"], cfg["harvest_days"]), 1
        )

        # Uptime
        tower.uptime_s = int(sim_time_h * 3600)

    def _default_reservoir_physics(
        self, reservoir: Reservoir, coord: Coordinator, sim_time_h: float, dt_h: float
    ) -> None:
        n_towers = len([t for t in coord.towers if t._online])
        if n_towers == 0:
            return

        # Aggregate EC consumption from all crops
        total_ec_rate = 0.0
        total_water_rate = 0.0
        for t in coord.towers:
            if t._online:
                cfg = CROP_CONFIG[t.crop_type]
                total_ec_rate += cfg["ec_consumption"]
                total_water_rate += cfg["water_consumption"]

        # pH drift
        reservoir.ph = clamp(noise(ph_drift(reservoir.ph, dt_h), 0.01), 3.0, 9.0)

        # EC depletion
        reservoir.ec_ms_cm = clamp(
            noise(ec_depletion(reservoir.ec_ms_cm, dt_h, total_ec_rate), 0.01),
            0.0,
            10.0,
        )

        # TDS
        reservoir.tds_ppm = tds_from_ec(reservoir.ec_ms_cm)

        # Water level
        reservoir.water_level_pct = clamp(
            noise(
                water_level_depletion(
                    reservoir.water_level_pct,
                    dt_h,
                    n_towers,
                    total_water_rate / max(n_towers, 1),
                ),
                0.2,
            ),
            0.0,
            100.0,
        )
        reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)

        # Low-water alert
        reservoir.low_water_alert = reservoir.water_level_pct < 20.0

        # Water temperature
        air_temp = day_night_temp(sim_time_h)
        reservoir.water_temp_c = round(
            noise(water_temp_track(air_temp - 3.0, reservoir.water_temp_c, dt_h), 0.1),
            1,
        )

        # Coordinator ambient temp (near reservoir)
        coord.temp_c = round(noise(air_temp, 0.15), 1)
        coord.uptime_s = int(sim_time_h * 3600)

    # -- main loop -----------------------------------------------------------

    def run(self) -> None:
        """Execute the simulation."""
        self.configure_topology()

        total_towers = sum(len(c.towers) for f in self.farms for c in f.coordinators)
        total_coords = sum(len(f.coordinators) for f in self.farms)
        log.info(
            "=== %s | %d farms, %d coordinators, %d towers | "
            "speed=%.1fx interval=%.1fs duration=%.0fs ===",
            self.NAME,
            len(self.farms),
            total_coords,
            total_towers,
            self.speed,
            self.interval,
            self.duration,
        )

        self.on_start()

        real_start = time.time()
        tick_count = 0
        last_status = real_start

        while self._running:
            tick_start = time.time()
            real_elapsed = tick_start - real_start
            if real_elapsed >= self.duration:
                log.info("Duration reached (%.0fs). Stopping.", real_elapsed)
                break

            # Advance simulation clock
            dt_real = self.interval
            dt_sim = dt_real * self.speed
            self.sim_time += dt_sim
            sim_time_h = self.sim_time / 3600.0
            dt_h = dt_sim / 3600.0

            # Physics update + publish
            self.on_tick(sim_time_h, dt_h)

            for farm in self.farms:
                for coord in farm.coordinators:
                    if not coord.is_online:
                        continue

                    # Update physics
                    self.update_reservoir(coord.reservoir, coord, sim_time_h, dt_h)
                    for tower in coord.towers:
                        self.update_tower(tower, coord, sim_time_h, dt_h)

                    # Publish
                    self.mqtt.publish_reservoir_telemetry(coord)
                    for tower in coord.towers:
                        if tower._online:
                            self.mqtt.publish_tower_telemetry(tower)

            tick_count += 1

            # Periodic status line (every 10 s real-time)
            if tick_start - last_status >= 10.0:
                self._print_status(real_elapsed, tick_count)
                last_status = tick_start

            # Sleep for remainder of interval
            elapsed = time.time() - tick_start
            sleep_time = max(0, self.interval - elapsed)
            if sleep_time > 0:
                time.sleep(sleep_time)

        self._print_status(time.time() - real_start, tick_count, final=True)

    # -- helpers -------------------------------------------------------------

    def _print_status(
        self, real_elapsed: float, ticks: int, final: bool = False
    ) -> None:
        sim_h = self.sim_time / 3600.0
        msgs = self.mqtt.messages_published
        rate = msgs / max(real_elapsed, 0.1)
        tag = "FINAL" if final else "STATUS"
        # Pick a representative reservoir for display
        sample_r = self.farms[0].coordinators[0].reservoir if self.farms else None
        ph_str = f"pH={sample_r.ph:.2f}" if sample_r else ""
        ec_str = f"EC={sample_r.ec_ms_cm:.2f}" if sample_r else ""
        wl_str = f"WL={sample_r.water_level_pct:.1f}%" if sample_r else ""
        log.info(
            "[%s] %.0fs real | sim=%.1fh | %d msgs (%.0f/s) | %d errors | %s %s %s",
            tag,
            real_elapsed,
            sim_h,
            msgs,
            rate,
            self.mqtt.errors,
            ph_str,
            ec_str,
            wl_str,
        )

    def _handle_sigint(self, sig, frame):
        log.info("SIGINT received, stopping...")
        self._running = False
