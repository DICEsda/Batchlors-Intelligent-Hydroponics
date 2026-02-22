"""Data models for the hydroponics telemetry simulator."""

from dataclasses import dataclass, field
from typing import List, Optional

# ---------------------------------------------------------------------------
# Crop catalog -- optimal ranges & growth parameters
# ---------------------------------------------------------------------------

CROP_TYPES: list[str] = [
    "lettuce",
    "basil",
    "spinach",
    "kale",
    "tomato",
    "pepper",
    "strawberry",
    "mint",
    "cilantro",
]

CROP_CONFIG: dict[str, dict] = {
    "lettuce": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.5),
        "ec_opt": 1.0,
        "ec_range": (0.8, 1.2),
        "temp_opt": 20.0,
        "temp_range": (15.0, 24.0),
        "humidity_opt": 70.0,
        "max_height_cm": 25.0,
        "harvest_days": 45,
        "ec_consumption": 0.020,  # mS/cm lost per hour (whole reservoir)
        "water_consumption": 0.04,  # % level lost per hour per tower
    },
    "basil": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.5),
        "ec_opt": 1.3,
        "ec_range": (1.0, 1.6),
        "temp_opt": 22.0,
        "temp_range": (18.0, 28.0),
        "humidity_opt": 65.0,
        "max_height_cm": 30.0,
        "harvest_days": 35,
        "ec_consumption": 0.025,
        "water_consumption": 0.05,
    },
    "spinach": {
        "ph_opt": 6.5,
        "ph_range": (6.0, 7.0),
        "ec_opt": 2.0,
        "ec_range": (1.8, 2.3),
        "temp_opt": 18.0,
        "temp_range": (10.0, 24.0),
        "humidity_opt": 65.0,
        "max_height_cm": 20.0,
        "harvest_days": 40,
        "ec_consumption": 0.030,
        "water_consumption": 0.04,
    },
    "kale": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.5),
        "ec_opt": 2.0,
        "ec_range": (1.5, 2.5),
        "temp_opt": 18.0,
        "temp_range": (7.0, 24.0),
        "humidity_opt": 65.0,
        "max_height_cm": 35.0,
        "harvest_days": 55,
        "ec_consumption": 0.030,
        "water_consumption": 0.05,
    },
    "tomato": {
        "ph_opt": 6.2,
        "ph_range": (5.5, 6.8),
        "ec_opt": 3.0,
        "ec_range": (2.0, 5.0),
        "temp_opt": 24.0,
        "temp_range": (18.0, 32.0),
        "humidity_opt": 60.0,
        "max_height_cm": 100.0,
        "harvest_days": 75,
        "ec_consumption": 0.050,
        "water_consumption": 0.08,
    },
    "pepper": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.8),
        "ec_opt": 2.5,
        "ec_range": (2.0, 3.5),
        "temp_opt": 24.0,
        "temp_range": (18.0, 32.0),
        "humidity_opt": 60.0,
        "max_height_cm": 60.0,
        "harvest_days": 70,
        "ec_consumption": 0.040,
        "water_consumption": 0.07,
    },
    "strawberry": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.5),
        "ec_opt": 1.2,
        "ec_range": (1.0, 1.5),
        "temp_opt": 20.0,
        "temp_range": (15.0, 26.0),
        "humidity_opt": 70.0,
        "max_height_cm": 20.0,
        "harvest_days": 60,
        "ec_consumption": 0.020,
        "water_consumption": 0.05,
    },
    "mint": {
        "ph_opt": 6.0,
        "ph_range": (5.5, 6.5),
        "ec_opt": 2.0,
        "ec_range": (1.6, 2.4),
        "temp_opt": 21.0,
        "temp_range": (15.0, 25.0),
        "humidity_opt": 70.0,
        "max_height_cm": 30.0,
        "harvest_days": 30,
        "ec_consumption": 0.025,
        "water_consumption": 0.05,
    },
    "cilantro": {
        "ph_opt": 6.5,
        "ph_range": (6.0, 7.0),
        "ec_opt": 1.5,
        "ec_range": (1.2, 1.8),
        "temp_opt": 20.0,
        "temp_range": (10.0, 24.0),
        "humidity_opt": 65.0,
        "max_height_cm": 25.0,
        "harvest_days": 40,
        "ec_consumption": 0.020,
        "water_consumption": 0.04,
    },
}


# ---------------------------------------------------------------------------
# Simulation data models
# ---------------------------------------------------------------------------


@dataclass
class Tower:
    tower_id: str
    coord_id: str
    farm_id: str
    crop_type: str
    planting_offset_days: float = 5.0  # Days already planted at t=0

    # Sensor state (mutated by physics each tick)
    air_temp_c: float = 22.0
    humidity_pct: float = 65.0
    light_lux: float = 0.0
    pump_on: bool = True
    light_on: bool = False
    light_brightness: int = 200
    height_cm: float = 0.0

    # System
    vbat_mv: int = 3700
    fw_version: str = "1.2.0"
    uptime_s: int = 0
    signal_quality: int = -40
    status_mode: str = "operational"

    # Internal bookkeeping (not published)
    _online: bool = True


@dataclass
class Reservoir:
    coord_id: str
    farm_id: str

    # Water quality
    ph: float = 6.0
    ec_ms_cm: float = 1.5
    tds_ppm: float = 750.0
    water_temp_c: float = 20.0
    water_level_pct: float = 85.0
    water_level_cm: float = 34.0

    # Actuators
    main_pump_on: bool = True
    dosing_pump_ph_on: bool = False
    dosing_pump_nutrient_on: bool = False
    low_water_alert: bool = False


@dataclass
class Coordinator:
    coord_id: str
    farm_id: str
    name: str
    towers: List[Tower] = field(default_factory=list)
    reservoir: Optional[Reservoir] = None

    # System
    fw_version: str = "2.1.0"
    wifi_rssi: int = -45
    temp_c: float = 22.0
    uptime_s: int = 0
    status_mode: str = "operational"
    ip: str = "192.168.1.100"
    free_heap: int = 200000

    # Internal
    is_online: bool = True


@dataclass
class Farm:
    farm_id: str
    name: str
    coordinators: List[Coordinator] = field(default_factory=list)
