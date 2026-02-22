"""S07 -- Multi-Crop Reservoir Conflict.

Forces incompatible crops onto shared reservoirs:
  - Towers 1-3:  Lettuce (pH 5.5-6.5, EC 0.8-1.2)
  - Towers 4-6:  Tomato  (pH 5.5-6.8, EC 2.0-5.0)  <- high EC
  - Towers 7-8:  Mint    (pH 5.5-6.5, EC 1.6-2.4)
  - Towers 9-10: Cilantro (pH 6.0-7.0)               <- wants higher pH

Reservoir pH/EC settle at a compromise that's suboptimal for everyone.
Demonstrates ML clustering and compatibility matrix.
"""

import logging

from core.models import Coordinator, CROP_CONFIG
from .base import BaseScenario

log = logging.getLogger("simulator.crop_conflict")

# Explicit crop assignment per tower slot
CONFLICT_CROPS = [
    "lettuce",
    "lettuce",
    "lettuce",
    "tomato",
    "tomato",
    "tomato",
    "mint",
    "mint",
    "cilantro",
    "cilantro",
]


class CropConflictScenario(BaseScenario):
    NAME = "crop-conflict"
    DESCRIPTION = (
        "Incompatible crops (lettuce vs tomato vs cilantro) share "
        "reservoirs.  Demonstrates clustering / compatibility analysis."
    )

    def configure_topology(self) -> None:
        for farm in self.farms:
            for coord in farm.coordinators:
                # Assign the conflict crop pattern
                for i, tower in enumerate(coord.towers):
                    crop = CONFLICT_CROPS[i % len(CONFLICT_CROPS)]
                    tower.crop_type = crop

                # Set reservoir to the "compromise" value between
                # lettuce (EC ~1.0) and tomato (EC ~3.0)
                coord.reservoir.ec_ms_cm = 1.8
                coord.reservoir.ph = 6.2

        # Log the conflict
        log.info("Crop conflict scenario: forced incompatible crops.")
        for crop in ["lettuce", "tomato", "mint", "cilantro"]:
            cfg = CROP_CONFIG[crop]
            log.info(
                "  %s: pH %.1f-%.1f, EC %.1f-%.1f",
                crop,
                cfg["ph_range"][0],
                cfg["ph_range"][1],
                cfg["ec_range"][0],
                cfg["ec_range"][1],
            )

    # Physics are default -- the interesting output is the DATA
    # that the clustering algorithm will process.  The conflict
    # manifests in the fact that:
    # - Tomato towers are EC-starved (reservoir EC << optimal 3.0)
    # - Lettuce towers are EC-overloaded (reservoir EC >> optimal 1.0)
    # - Cilantro wants higher pH than lettuce/tomato
