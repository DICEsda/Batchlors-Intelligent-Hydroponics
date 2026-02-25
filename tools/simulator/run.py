#!/usr/bin/env python3
"""Hydroponics Telemetry Simulator -- CLI entry point.

Usage examples:
    # Steady-state with 250 towers (default topology)
    python run.py --scenario steady-state

    # pH drift crisis, 10x time compression
    python run.py --scenario ph-drift --speed 10

    # 1000-tower stress test, 1-second publish interval
    python run.py --scenario scale-test --towers 1000 --interval 1

    # Full 15-min demo sequence
    python run.py --scenario full-demo

    # List all scenarios
    python run.py --list

    # Skip REST bootstrap (assumes farms/coordinators already exist)
    python run.py --scenario steady-state --no-bootstrap
"""

import argparse
import logging
import os
import sys

# Ensure the simulator package is importable when run directly
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from core.publisher import MqttPublisher, RestBootstrapper
from core.topology import generate_topology, topology_stats
from scenarios import SCENARIOS
from scenarios.scale_test import ScaleTestScenario
from scenarios.lwt_disconnect import LwtDisconnectScenario


def setup_logging(verbose: bool = False) -> None:
    level = logging.DEBUG if verbose else logging.INFO
    fmt = "%(asctime)s [%(levelname)-5s] %(name)s: %(message)s"
    logging.basicConfig(level=level, format=fmt, datefmt="%H:%M:%S")
    # Quiet down paho-mqtt
    logging.getLogger("paho").setLevel(logging.WARNING)


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="Hydroponics Telemetry Simulator",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    p.add_argument(
        "--scenario",
        "-s",
        choices=list(SCENARIOS.keys()),
        default="steady-state",
        help="Scenario to run (default: steady-state)",
    )
    p.add_argument(
        "--list", action="store_true", help="List all available scenarios and exit"
    )

    # Topology
    p.add_argument("--farms", type=int, default=5, help="Number of farms (default: 5)")
    p.add_argument(
        "--coordinators", type=int, default=5, help="Coordinators per farm (default: 5)"
    )
    p.add_argument(
        "--towers-per-coord",
        type=int,
        default=10,
        help="Towers per coordinator (default: 10)",
    )
    p.add_argument(
        "--towers",
        type=int,
        default=None,
        help="Total tower count override (for scale-test)",
    )

    # Simulation
    p.add_argument(
        "--speed",
        type=float,
        default=1.0,
        help="Time multiplier: sim-seconds per real-second (default: 1)",
    )
    p.add_argument(
        "--interval",
        type=float,
        default=5.0,
        help="Telemetry publish interval in real seconds (default: 5)",
    )
    p.add_argument(
        "--duration",
        type=float,
        default=3600.0,
        help="Max run time in real seconds (default: 3600)",
    )

    # Infrastructure
    p.add_argument(
        "--mqtt-host", default="localhost", help="MQTT broker host (default: localhost)"
    )
    p.add_argument(
        "--mqtt-port", type=int, default=1883, help="MQTT broker port (default: 1883)"
    )
    p.add_argument(
        "--mqtt-user", default="user1", help="MQTT username (default: user1)"
    )
    p.add_argument(
        "--mqtt-pass", default="user1", help="MQTT password (default: user1)"
    )
    p.add_argument(
        "--api-url",
        default="http://localhost:8000",
        help="Backend REST API URL (default: http://localhost:8000)",
    )
    p.add_argument(
        "--no-bootstrap",
        action="store_true",
        help="Skip farm/coordinator creation via REST API",
    )

    # Other
    p.add_argument(
        "--seed",
        type=int,
        default=42,
        help="Random seed for reproducible topology (default: 42)",
    )
    p.add_argument("--verbose", "-v", action="store_true", help="Enable debug logging")

    return p


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.list:
        print("\nAvailable scenarios:\n")
        for name, cls in SCENARIOS.items():
            print(f"  {name:25s}  {cls.DESCRIPTION}")
        print()
        return

    setup_logging(args.verbose)
    log = logging.getLogger("simulator")

    # ---- Build topology ------------------------------------------------
    if args.scenario == "scale-test" and args.towers:
        # Auto-compute topology params from total tower count
        tpc = args.towers_per_coord  # towers per coordinator
        n_coords = max(1, args.towers // tpc)
        n_farms = min(10, n_coords)
        cpf = max(1, n_coords // n_farms)  # coordinators per farm
        log.info(
            "Scale test: %d towers -> %d farms x %d coords x %d towers",
            args.towers,
            n_farms,
            cpf,
            tpc,
        )
        farms = generate_topology(
            n_farms=n_farms,
            n_coords_per_farm=cpf,
            n_towers_per_coord=tpc,
            seed=args.seed,
        )
    else:
        log.info(
            "Generating topology: %d farms x %d coords x %d towers = %d towers",
            args.farms,
            args.coordinators,
            args.towers_per_coord,
            args.farms * args.coordinators * args.towers_per_coord,
        )
        farms = generate_topology(
            n_farms=args.farms,
            n_coords_per_farm=args.coordinators,
            n_towers_per_coord=args.towers_per_coord,
            seed=args.seed,
        )
    stats = topology_stats(farms)
    log.info(
        "Topology: %(farms)d farms, %(coordinators)d coordinators, %(towers)d towers",
        stats,
    )

    # ---- Connect MQTT --------------------------------------------------
    mqtt = MqttPublisher(
        host=args.mqtt_host,
        port=args.mqtt_port,
        username=args.mqtt_user,
        password=args.mqtt_pass,
    )
    mqtt.connect()

    try:
        # ---- Bootstrap (create farms + approve coordinators) -----------
        if not args.no_bootstrap:
            bootstrap = RestBootstrapper(base_url=args.api_url)
            bootstrap.bootstrap(farms, mqtt)
        else:
            log.info("Skipping REST bootstrap (--no-bootstrap)")

        # ---- Instantiate and run scenario ------------------------------
        scenario_cls = SCENARIOS[args.scenario]

        if args.scenario == "scale-test":
            scenario = ScaleTestScenario(
                farms=farms,
                mqtt=mqtt,
                speed=args.speed,
                interval=args.interval,
                duration=args.duration,
                total_towers=args.towers or stats["towers"],
            )
        elif args.scenario == "lwt-disconnect":
            scenario = LwtDisconnectScenario(
                farms=farms,
                mqtt=mqtt,
                speed=args.speed,
                interval=args.interval,
                duration=args.duration,
                mqtt_user=args.mqtt_user,
                mqtt_pass=args.mqtt_pass,
            )
        else:
            scenario = scenario_cls(
                farms=farms,
                mqtt=mqtt,
                speed=args.speed,
                interval=args.interval,
                duration=args.duration,
            )

        scenario.run()

    except KeyboardInterrupt:
        log.info("Interrupted by user.")
    except Exception:
        log.exception("Simulator crashed")
        sys.exit(1)
    finally:
        mqtt.disconnect()

    log.info("Simulator finished.")


if __name__ == "__main__":
    main()
