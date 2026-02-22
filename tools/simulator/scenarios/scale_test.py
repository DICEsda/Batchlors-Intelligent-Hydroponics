"""S11 -- Scale Test (1000 towers).

Stress-tests the MQTT broker, backend, and MongoDB with high message
throughput.  Topology: 10 farms x 10 coordinators x 10 towers = 1000 towers.

Tracks:
  - Messages published per second
  - Publish errors
  - Tick duration (overhead)
  - Cumulative message count

Use ``--towers 1000`` (or any number) to adjust scale.
The ``--interval 1`` flag is recommended for maximum throughput.
"""

import logging
import time

from core.models import Farm, Coordinator, Tower
from core.topology import generate_topology, topology_stats
from .base import BaseScenario

log = logging.getLogger("simulator.scale_test")


class ScaleTestScenario(BaseScenario):
    NAME = "scale-test"
    DESCRIPTION = (
        "1000-tower stress test.  Measures MQTT throughput, "
        "backend processing, and MongoDB write performance."
    )

    def __init__(self, *args, total_towers: int = 1000, **kwargs):
        # Topology is built by run.py and passed via `farms` kwarg.
        # Default to 1-second interval for max throughput.
        kwargs.setdefault("interval", 1.0)
        kwargs.setdefault("duration", 300.0)  # 5 min
        super().__init__(*args, **kwargs)
        self._total_towers = total_towers

        # Metrics
        self._tick_times: list[float] = []
        self._last_msgs: int = 0
        self._rates: list[float] = []

    def on_start(self) -> None:
        stats = topology_stats(self.farms)
        log.info(
            "SCALE TEST: %d farms, %d coordinators, %d towers | interval=%.1fs",
            stats["farms"],
            stats["coordinators"],
            stats["towers"],
            self.interval,
        )

    def on_tick(self, sim_time_h: float, dt_h: float) -> None:
        """Track per-tick metrics."""
        pass  # Metrics are tracked in the overridden run()

    def run(self) -> None:
        """Override run() to add per-tick latency tracking."""
        self.configure_topology()

        stats = topology_stats(self.farms)
        total_towers = stats["towers"]
        total_coords = stats["coordinators"]
        log.info(
            "=== SCALE TEST | %d farms, %d coordinators, %d towers | "
            "speed=%.1fx interval=%.1fs duration=%.0fs ===",
            stats["farms"],
            total_coords,
            total_towers,
            self.speed,
            self.interval,
            self.duration,
        )

        self.on_start()

        real_start = time.time()
        tick_count = 0

        while self._running:
            tick_start = time.time()
            real_elapsed = tick_start - real_start
            if real_elapsed >= self.duration:
                break

            # Advance sim clock
            dt_sim = self.interval * self.speed
            self.sim_time += dt_sim
            sim_time_h = self.sim_time / 3600.0
            dt_h = dt_sim / 3600.0

            msgs_before = self.mqtt.messages_published

            # Physics + publish
            for farm in self.farms:
                for coord in farm.coordinators:
                    if not coord.is_online:
                        continue
                    reservoir = coord.reservoir
                    if reservoir is None:
                        continue
                    self.update_reservoir(reservoir, coord, sim_time_h, dt_h)
                    for tower in coord.towers:
                        self.update_tower(tower, coord, sim_time_h, dt_h)
                    self.mqtt.publish_reservoir_telemetry(coord)
                    for tower in coord.towers:
                        if tower._online:
                            self.mqtt.publish_tower_telemetry(tower)

            tick_end = time.time()
            tick_duration = tick_end - tick_start
            self._tick_times.append(tick_duration)

            msgs_this_tick = self.mqtt.messages_published - msgs_before
            rate = msgs_this_tick / max(tick_duration, 0.001)
            self._rates.append(rate)

            tick_count += 1

            # Log every 10 ticks
            if tick_count % 10 == 0:
                avg_tick = sum(self._tick_times[-10:]) / min(10, len(self._tick_times))
                avg_rate = sum(self._rates[-10:]) / min(10, len(self._rates))
                log.info(
                    "[SCALE] tick=%d | %.0fs elapsed | %d msgs total | "
                    "%.0f msg/s avg | tick_time=%.3fs | errors=%d",
                    tick_count,
                    real_elapsed,
                    self.mqtt.messages_published,
                    avg_rate,
                    avg_tick,
                    self.mqtt.errors,
                )

            # Sleep remainder
            sleep_time = max(0, self.interval - tick_duration)
            if sleep_time > 0:
                time.sleep(sleep_time)

        # Final report
        total_time = time.time() - real_start
        total_msgs = self.mqtt.messages_published
        avg_rate = total_msgs / max(total_time, 0.1)
        avg_tick = (
            sum(self._tick_times) / len(self._tick_times) if self._tick_times else 0
        )
        max_tick = max(self._tick_times) if self._tick_times else 0
        min_tick = min(self._tick_times) if self._tick_times else 0

        log.info("=" * 60)
        log.info("SCALE TEST RESULTS")
        log.info("=" * 60)
        log.info("  Towers:          %d", total_towers)
        log.info("  Duration:        %.1f s", total_time)
        log.info("  Total messages:  %d", total_msgs)
        log.info("  Avg msg/s:       %.1f", avg_rate)
        log.info("  Ticks:           %d", tick_count)
        log.info("  Avg tick time:   %.4f s", avg_tick)
        log.info("  Min tick time:   %.4f s", min_tick)
        log.info("  Max tick time:   %.4f s", max_tick)
        log.info("  Errors:          %d", self.mqtt.errors)
        log.info(
            "  Msgs per tick:   %d (towers+reservoirs)", total_towers + total_coords
        )
        log.info("=" * 60)
