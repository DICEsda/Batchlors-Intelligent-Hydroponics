"""Stateless sensor-physics functions for realistic hydroponic simulation.

All functions are pure: they take current state + elapsed time and return
the new value.  Noise is added via ``noise()``; callers decide when to apply it.
"""

import math
import random

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def clamp(value: float, lo: float, hi: float) -> float:
    """Clamp *value* to [lo, hi]."""
    return max(lo, min(hi, value))


def noise(value: float, std: float) -> float:
    """Add Gaussian noise with standard deviation *std*."""
    return value + random.gauss(0, std)


# ---------------------------------------------------------------------------
# Temperature
# ---------------------------------------------------------------------------


def day_night_temp(
    sim_hours: float,
    base: float = 22.0,
    amplitude: float = 4.0,
    peak_hour: float = 14.0,
) -> float:
    """Sinusoidal day/night temperature cycle.

    Returns the ambient air temperature at *sim_hours* (hours since sim start).
    Peak at *peak_hour* (24-h clock), trough 12 h later.
    """
    hour_of_day = sim_hours % 24.0
    phase = 2.0 * math.pi * (hour_of_day - peak_hour) / 24.0
    return base + amplitude * math.cos(phase)


def humidity_from_temp(
    temp: float, base_humidity: float = 70.0, sensitivity: float = 1.5
) -> float:
    """Inverse relationship: hotter -> lower humidity."""
    # Every degree above 22 C drops humidity by *sensitivity* %
    return clamp(base_humidity - sensitivity * (temp - 22.0), 30.0, 95.0)


# ---------------------------------------------------------------------------
# Light
# ---------------------------------------------------------------------------


def grow_light_schedule(
    sim_hours: float,
    on_hour: float = 6.0,
    off_hour: float = 22.0,
    max_lux: float = 30000.0,
    ramp_minutes: float = 30.0,
) -> tuple[bool, float]:
    """Return (light_on, lux) based on a 16/8 grow schedule.

    Light ramps up/down over *ramp_minutes* for realism.
    """
    hour = sim_hours % 24.0
    ramp_h = ramp_minutes / 60.0

    if on_hour < off_hour:
        in_window = on_hour <= hour < off_hour
    else:
        in_window = hour >= on_hour or hour < off_hour

    if not in_window:
        return False, random.uniform(0, 5)  # ambient darkness

    # Distance into the on-window
    if hour >= on_hour:
        elapsed = hour - on_hour
    else:
        elapsed = hour + (24.0 - on_hour)

    window_len = (off_hour - on_hour) % 24.0
    remaining = window_len - elapsed

    # Ramp factor (0..1)
    ramp = min(elapsed / ramp_h, remaining / ramp_h, 1.0) if ramp_h > 0 else 1.0
    return True, max_lux * clamp(ramp, 0.0, 1.0)


# ---------------------------------------------------------------------------
# Water chemistry
# ---------------------------------------------------------------------------


def ph_drift(current: float, dt_hours: float, rate: float = -0.015) -> float:
    """Natural pH drift (acid accumulation from root exudates).

    *rate* is pH units per hour (negative = acidifying).
    """
    return current + rate * dt_hours


def ec_depletion(
    current: float, dt_hours: float, total_consumption_rate: float = 0.03
) -> float:
    """EC drops as plants absorb nutrients.

    *total_consumption_rate* is the aggregate mS/cm per hour for all towers
    sharing the reservoir.
    """
    return max(0.0, current - total_consumption_rate * dt_hours)


def water_level_depletion(
    current: float, dt_hours: float, n_towers: int, rate_per_tower: float = 0.05
) -> float:
    """Water level (%) drops from evapotranspiration + plant uptake."""
    return max(0.0, current - rate_per_tower * n_towers * dt_hours)


def water_temp_track(
    air_temp: float,
    current_water_temp: float,
    dt_hours: float,
    lag_factor: float = 0.15,
) -> float:
    """Water temperature slowly tracks air temperature (thermal mass).

    Exponential approach: each hour it closes *lag_factor* of the gap.
    """
    gap = air_temp - current_water_temp
    return current_water_temp + gap * lag_factor * dt_hours


def tds_from_ec(ec_ms_cm: float, factor: float = 500.0) -> float:
    """Approximate TDS (ppm) from EC (mS/cm)."""
    return ec_ms_cm * factor


# ---------------------------------------------------------------------------
# Growth
# ---------------------------------------------------------------------------


def growth_sigmoid(
    days_since_planting: float, max_height_cm: float, harvest_days: float
) -> float:
    """Sigmoid (logistic) growth curve.

    Height reaches ~95 % of *max_height_cm* at *harvest_days*.
    """
    if days_since_planting <= 0:
        return 0.0
    # Midpoint at 45% of harvest period, steepness scales with harvest length
    midpoint = harvest_days * 0.45
    k = 8.0 / harvest_days  # steepness
    exponent = -k * (days_since_planting - midpoint)
    exponent = clamp(exponent, -20.0, 20.0)  # prevent overflow
    return max_height_cm / (1.0 + math.exp(exponent))
