"""S01 -- Steady-State Farm.

Everything runs normally.  2 farms x 3 coordinators x 5 towers.
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
