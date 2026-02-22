"""S01 -- Steady-State Farm.

Everything runs normally.  5 farms x 5 coordinators x 10 towers.
Temperature follows day/night cycle, pH drifts slowly, EC depletes,
water level drops.  This is the baseline "everything works" scenario.
"""

from .base import BaseScenario


class SteadyStateScenario(BaseScenario):
    NAME = "steady-state"
    DESCRIPTION = (
        "Normal operation: day/night cycle, slow pH drift, "
        "gradual EC depletion, water consumption.  Baseline demo."
    )

    # No overrides needed -- BaseScenario's default physics IS the
    # steady-state scenario.  All sensors behave realistically with
    # small Gaussian noise.
