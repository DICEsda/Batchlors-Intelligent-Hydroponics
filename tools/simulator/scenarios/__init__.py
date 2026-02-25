"""Scenario registry -- maps CLI names to scenario classes."""

from .steady_state import SteadyStateScenario
from .ph_drift import PhDriftScenario
from .nutrient_depletion import NutrientDepletionScenario
from .heat_stress import HeatStressScenario
from .water_emergency import WaterEmergencyScenario
from .tower_pairing import TowerPairingScenario
from .crop_conflict import CropConflictScenario
from .growth_cycle import GrowthCycleScenario
from .reconnection import ReconnectionScenario
from .full_demo import FullDemoScenario
from .scale_test import ScaleTestScenario
from .lwt_disconnect import LwtDisconnectScenario
from .alert_cascade import AlertCascadeScenario

SCENARIOS: dict[str, type] = {
    "steady-state": SteadyStateScenario,
    "ph-drift": PhDriftScenario,
    "nutrient-depletion": NutrientDepletionScenario,
    "heat-stress": HeatStressScenario,
    "water-emergency": WaterEmergencyScenario,
    "tower-pairing": TowerPairingScenario,
    "crop-conflict": CropConflictScenario,
    "growth-cycle": GrowthCycleScenario,
    "reconnection": ReconnectionScenario,
    "full-demo": FullDemoScenario,
    "scale-test": ScaleTestScenario,
    "lwt-disconnect": LwtDisconnectScenario,
    "alert-cascade": AlertCascadeScenario,
}

__all__ = ["SCENARIOS"]
