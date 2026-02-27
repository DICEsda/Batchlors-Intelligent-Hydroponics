"""Generate farm/coordinator/tower topology with realistic IDs."""

import random
from typing import List

from .models import Farm, Coordinator, Tower, Reservoir, CROP_TYPES, CROP_CONFIG


def _mac(prefix: int, farm_idx: int, coord_idx: int, tower_idx: int = 0) -> str:
    """Generate a deterministic MAC-style ID.

    Format: ``PP:PP:FF:CC:TT:01`` where PP=prefix, FF=farm, CC=coord, TT=tower.
    """
    return (
        f"{(prefix >> 8) & 0xFF:02X}:{prefix & 0xFF:02X}"
        f":{farm_idx & 0xFF:02X}:{coord_idx & 0xFF:02X}"
        f":{tower_idx & 0xFF:02X}:01"
    )


def generate_topology(
    n_farms: int = 2,
    n_coords_per_farm: int = 3,
    n_towers_per_coord: int = 5,
    randomize_crops: bool = True,
    seed: int | None = 42,
) -> List[Farm]:
    """Build the full farm hierarchy.

    Default: 2 farms x 3 coordinators x 5 towers = 30 towers.

    Returns a list of :class:`Farm` objects with fully wired coordinators,
    reservoirs, and towers.
    """
    if seed is not None:
        random.seed(seed)

    farms: List[Farm] = []

    for fi in range(n_farms):
        farm_id = f"farm-{fi + 1:03d}"
        farm_name = f"Hydro Farm {fi + 1}"
        farm = Farm(farm_id=farm_id, name=farm_name)

        for ci in range(n_coords_per_farm):
            coord_mac = _mac(0xAABB, fi + 1, ci + 1)
            coord_name = f"Coordinator {fi + 1}-{ci + 1}"
            ip_addr = f"192.168.{fi + 1}.{ci + 10}"

            reservoir = Reservoir(
                coord_id=coord_mac,
                farm_id=farm_id,
                ph=round(random.uniform(5.8, 6.3), 2),
                ec_ms_cm=round(random.uniform(1.2, 2.0), 2),
                water_temp_c=round(random.uniform(19.0, 21.0), 1),
                water_level_pct=round(random.uniform(75.0, 95.0), 1),
            )
            reservoir.tds_ppm = round(reservoir.ec_ms_cm * 500, 0)
            reservoir.water_level_cm = round(reservoir.water_level_pct * 0.4, 1)

            coord = Coordinator(
                coord_id=coord_mac,
                farm_id=farm_id,
                name=coord_name,
                reservoir=reservoir,
                ip=ip_addr,
            )

            for ti in range(n_towers_per_coord):
                tower_mac = _mac(0xCCDD, fi + 1, ci + 1, ti + 1)
                crop = (
                    random.choice(CROP_TYPES)
                    if randomize_crops
                    else CROP_TYPES[ti % len(CROP_TYPES)]
                )
                planting_offset = round(random.uniform(1.0, 20.0), 1)

                tower = Tower(
                    tower_id=tower_mac,
                    coord_id=coord_mac,
                    farm_id=farm_id,
                    crop_type=crop,
                    planting_offset_days=planting_offset,
                    signal_quality=random.randint(-65, -30),
                    vbat_mv=random.randint(3400, 3800),
                )
                # Initialise height from planting offset
                cfg = CROP_CONFIG[crop]
                from .physics import growth_sigmoid

                tower.height_cm = round(
                    growth_sigmoid(
                        planting_offset, cfg["max_height_cm"], cfg["harvest_days"]
                    ),
                    1,
                )

                coord.towers.append(tower)

            farm.coordinators.append(coord)

        farms.append(farm)

    return farms


def topology_stats(farms: List[Farm]) -> dict:
    """Return summary counts."""
    n_coords = sum(len(f.coordinators) for f in farms)
    n_towers = sum(len(c.towers) for f in farms for c in f.coordinators)
    return {
        "farms": len(farms),
        "coordinators": n_coords,
        "reservoirs": n_coords,
        "towers": n_towers,
    }
