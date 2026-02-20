"""
ML inference logic for growth prediction and anomaly detection.
Provides model loading, prediction, and result formatting.
"""

import os
import pickle
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional, Any
import numpy as np
from loguru import logger

from ..config import config


# =============================================================================
# Crop Configuration (Optimal conditions by crop type)
# =============================================================================

CROP_CONFIG = {
    "Lettuce": {
        "temp_min_c": 15, "temp_max_c": 21, "temp_optimal_c": 18,
        "humidity_min_pct": 50, "humidity_max_pct": 70, "humidity_optimal_pct": 60,
        "light_min_lux": 10000, "light_max_lux": 15000, "light_hours_per_day": 14,
        "ph_min": 5.5, "ph_max": 6.5, "ph_optimal": 6.0,
        "ec_min_ms_cm": 0.8, "ec_max_ms_cm": 1.2, "ec_optimal_ms_cm": 1.0,
        "expected_days_to_harvest": 45, "expected_height_cm": 25,
        "growth_rate_cm_per_day": 0.6,
    },
    "Basil": {
        "temp_min_c": 18, "temp_max_c": 30, "temp_optimal_c": 24,
        "humidity_min_pct": 40, "humidity_max_pct": 60, "humidity_optimal_pct": 50,
        "light_min_lux": 12000, "light_max_lux": 20000, "light_hours_per_day": 16,
        "ph_min": 5.5, "ph_max": 6.5, "ph_optimal": 6.0,
        "ec_min_ms_cm": 1.0, "ec_max_ms_cm": 1.6, "ec_optimal_ms_cm": 1.3,
        "expected_days_to_harvest": 30, "expected_height_cm": 30,
        "growth_rate_cm_per_day": 1.0,
    },
    "Spinach": {
        "temp_min_c": 10, "temp_max_c": 20, "temp_optimal_c": 15,
        "humidity_min_pct": 45, "humidity_max_pct": 65, "humidity_optimal_pct": 55,
        "light_min_lux": 8000, "light_max_lux": 12000, "light_hours_per_day": 12,
        "ph_min": 6.0, "ph_max": 7.0, "ph_optimal": 6.5,
        "ec_min_ms_cm": 1.8, "ec_max_ms_cm": 2.3, "ec_optimal_ms_cm": 2.0,
        "expected_days_to_harvest": 40, "expected_height_cm": 20,
        "growth_rate_cm_per_day": 0.5,
    },
    "Kale": {
        "temp_min_c": 7, "temp_max_c": 24, "temp_optimal_c": 16,
        "humidity_min_pct": 45, "humidity_max_pct": 65, "humidity_optimal_pct": 55,
        "light_min_lux": 10000, "light_max_lux": 15000, "light_hours_per_day": 14,
        "ph_min": 6.0, "ph_max": 7.5, "ph_optimal": 6.5,
        "ec_min_ms_cm": 1.0, "ec_max_ms_cm": 1.5, "ec_optimal_ms_cm": 1.25,
        "expected_days_to_harvest": 55, "expected_height_cm": 35,
        "growth_rate_cm_per_day": 0.65,
    },
    "Strawberry": {
        "temp_min_c": 15, "temp_max_c": 26, "temp_optimal_c": 20,
        "humidity_min_pct": 60, "humidity_max_pct": 80, "humidity_optimal_pct": 70,
        "light_min_lux": 15000, "light_max_lux": 25000, "light_hours_per_day": 12,
        "ph_min": 5.5, "ph_max": 6.5, "ph_optimal": 6.0,
        "ec_min_ms_cm": 1.0, "ec_max_ms_cm": 1.5, "ec_optimal_ms_cm": 1.2,
        "expected_days_to_harvest": 90, "expected_height_cm": 20,
        "growth_rate_cm_per_day": 0.2,
    },
    "Tomato": {
        "temp_min_c": 18, "temp_max_c": 29, "temp_optimal_c": 24,
        "humidity_min_pct": 50, "humidity_max_pct": 70, "humidity_optimal_pct": 60,
        "light_min_lux": 20000, "light_max_lux": 40000, "light_hours_per_day": 16,
        "ph_min": 5.5, "ph_max": 6.8, "ph_optimal": 6.2,
        "ec_min_ms_cm": 2.0, "ec_max_ms_cm": 5.0, "ec_optimal_ms_cm": 3.5,
        "expected_days_to_harvest": 80, "expected_height_cm": 150,
        "growth_rate_cm_per_day": 1.9,
    },
    "Pepper": {
        "temp_min_c": 18, "temp_max_c": 30, "temp_optimal_c": 25,
        "humidity_min_pct": 50, "humidity_max_pct": 70, "humidity_optimal_pct": 60,
        "light_min_lux": 20000, "light_max_lux": 35000, "light_hours_per_day": 14,
        "ph_min": 6.0, "ph_max": 6.8, "ph_optimal": 6.4,
        "ec_min_ms_cm": 2.0, "ec_max_ms_cm": 3.5, "ec_optimal_ms_cm": 2.5,
        "expected_days_to_harvest": 75, "expected_height_cm": 80,
        "growth_rate_cm_per_day": 1.1,
    },
    "Mint": {
        "temp_min_c": 15, "temp_max_c": 25, "temp_optimal_c": 20,
        "humidity_min_pct": 50, "humidity_max_pct": 70, "humidity_optimal_pct": 60,
        "light_min_lux": 8000, "light_max_lux": 15000, "light_hours_per_day": 14,
        "ph_min": 6.0, "ph_max": 7.0, "ph_optimal": 6.5,
        "ec_min_ms_cm": 1.5, "ec_max_ms_cm": 2.4, "ec_optimal_ms_cm": 2.0,
        "expected_days_to_harvest": 30, "expected_height_cm": 40,
        "growth_rate_cm_per_day": 1.3,
    },
    "Cilantro": {
        "temp_min_c": 10, "temp_max_c": 24, "temp_optimal_c": 18,
        "humidity_min_pct": 40, "humidity_max_pct": 60, "humidity_optimal_pct": 50,
        "light_min_lux": 10000, "light_max_lux": 15000, "light_hours_per_day": 12,
        "ph_min": 6.0, "ph_max": 7.0, "ph_optimal": 6.5,
        "ec_min_ms_cm": 1.2, "ec_max_ms_cm": 1.8, "ec_optimal_ms_cm": 1.5,
        "expected_days_to_harvest": 25, "expected_height_cm": 25,
        "growth_rate_cm_per_day": 1.0,
    },
    "Microgreens": {
        "temp_min_c": 18, "temp_max_c": 24, "temp_optimal_c": 21,
        "humidity_min_pct": 40, "humidity_max_pct": 60, "humidity_optimal_pct": 50,
        "light_min_lux": 5000, "light_max_lux": 10000, "light_hours_per_day": 12,
        "ph_min": 5.5, "ph_max": 6.5, "ph_optimal": 6.0,
        "ec_min_ms_cm": 0.5, "ec_max_ms_cm": 1.0, "ec_optimal_ms_cm": 0.75,
        "expected_days_to_harvest": 14, "expected_height_cm": 8,
        "growth_rate_cm_per_day": 0.6,
    },
}

# Default for unknown crops
DEFAULT_CROP_CONFIG = {
    "temp_min_c": 15, "temp_max_c": 25, "temp_optimal_c": 20,
    "humidity_min_pct": 50, "humidity_max_pct": 70, "humidity_optimal_pct": 60,
    "light_min_lux": 10000, "light_max_lux": 20000, "light_hours_per_day": 14,
    "ph_min": 5.5, "ph_max": 7.0, "ph_optimal": 6.2,
    "ec_min_ms_cm": 1.0, "ec_max_ms_cm": 2.0, "ec_optimal_ms_cm": 1.5,
    "expected_days_to_harvest": 45, "expected_height_cm": 25,
    "growth_rate_cm_per_day": 0.6,
}


# =============================================================================
# Model Manager
# =============================================================================

class ModelManager:
    """
    Manages loading and caching of ML models.
    Falls back to rule-based predictions when models aren't available.
    """
    
    MODEL_VERSION = "1.0.0"
    
    def __init__(self, model_dir: Optional[Path] = None):
        self.model_dir = model_dir or config.ml.model_dir
        self._models: dict[str, Any] = {}
        self._load_models()
    
    def _load_models(self) -> None:
        """Load all available models from disk."""
        model_dir = Path(self.model_dir)
        
        if not model_dir.exists():
            logger.warning(f"Model directory {model_dir} does not exist")
            return
        
        # Try to load growth model
        growth_model_path = model_dir / "growth_predictor.pkl"
        if growth_model_path.exists():
            try:
                with open(growth_model_path, "rb") as f:
                    self._models["growth"] = pickle.load(f)
                logger.success(f"Loaded growth model from {growth_model_path}")
            except Exception as e:
                logger.error(f"Failed to load growth model: {e}")
        
        # Try to load anomaly model
        anomaly_model_path = model_dir / "anomaly_detector.pkl"
        if anomaly_model_path.exists():
            try:
                with open(anomaly_model_path, "rb") as f:
                    self._models["anomaly"] = pickle.load(f)
                logger.success(f"Loaded anomaly model from {anomaly_model_path}")
            except Exception as e:
                logger.error(f"Failed to load anomaly model: {e}")
    
    @property
    def loaded_models(self) -> list[str]:
        """List of loaded model names."""
        return list(self._models.keys())
    
    def has_model(self, name: str) -> bool:
        """Check if a model is loaded."""
        return name in self._models
    
    def get_model(self, name: str) -> Optional[Any]:
        """Get a loaded model by name."""
        return self._models.get(name)


# =============================================================================
# Growth Predictor
# =============================================================================

class GrowthPredictor:
    """
    Predicts plant growth metrics using ML models or rule-based fallback.
    """
    
    def __init__(self, model_manager: ModelManager):
        self.model_manager = model_manager
    
    def predict(
        self,
        tower_id: str,
        crop_type: str,
        current_height_cm: float,
        days_since_planting: int,
        avg_temp_c: Optional[float] = None,
        avg_humidity_pct: Optional[float] = None,
        avg_light_lux: Optional[float] = None,
        avg_ph: Optional[float] = None,
        avg_ec_ms_cm: Optional[float] = None,
    ) -> dict:
        """
        Generate growth predictions for a plant.
        
        Returns dict with:
            - predicted_height_cm
            - predicted_harvest_date
            - days_to_harvest
            - growth_rate_cm_per_day
            - health_score
            - confidence
        """
        # Get crop-specific config
        crop_config = CROP_CONFIG.get(crop_type, DEFAULT_CROP_CONFIG)
        
        # Calculate health score based on environmental conditions
        health_score = self._calculate_health_score(
            crop_config, avg_temp_c, avg_humidity_pct, avg_light_lux, avg_ph, avg_ec_ms_cm
        )
        
        # Try ML model first
        if self.model_manager.has_model("growth"):
            try:
                return self._ml_predict(
                    tower_id, crop_type, current_height_cm, days_since_planting,
                    health_score, crop_config, avg_temp_c, avg_humidity_pct, 
                    avg_light_lux, avg_ph, avg_ec_ms_cm
                )
            except Exception as e:
                logger.warning(f"ML prediction failed, falling back to rules: {e}")
        
        # Rule-based fallback
        return self._rule_based_predict(
            tower_id, crop_type, current_height_cm, days_since_planting,
            health_score, crop_config
        )
    
    def _ml_predict(
        self,
        tower_id: str,
        crop_type: str,
        current_height_cm: float,
        days_since_planting: int,
        health_score: float,
        crop_config: dict,
        avg_temp_c: Optional[float],
        avg_humidity_pct: Optional[float],
        avg_light_lux: Optional[float],
        avg_ph: Optional[float],
        avg_ec_ms_cm: Optional[float],
    ) -> dict:
        """Use ML model for prediction."""
        model = self.model_manager.get_model("growth")
        
        # Build feature vector
        features = np.array([[
            days_since_planting,
            current_height_cm,
            avg_temp_c or crop_config["temp_optimal_c"],
            avg_humidity_pct or crop_config["humidity_optimal_pct"],
            avg_light_lux or crop_config["light_max_lux"],
            avg_ph or crop_config["ph_optimal"],
            avg_ec_ms_cm or crop_config["ec_optimal_ms_cm"],
        ]])
        
        # Predict future height (e.g., 7 days from now)
        predicted_height = model.predict(features)[0]
        
        # Calculate growth rate
        if days_since_planting > 0:
            growth_rate = current_height_cm / days_since_planting
        else:
            growth_rate = crop_config["growth_rate_cm_per_day"]
        
        # Adjust by health score
        effective_growth_rate = growth_rate * health_score
        
        # Estimate days to harvest
        target_height = crop_config["expected_height_cm"]
        remaining_height = max(0, target_height - current_height_cm)
        days_to_harvest = int(remaining_height / effective_growth_rate) if effective_growth_rate > 0 else 30
        
        harvest_date = datetime.utcnow() + timedelta(days=days_to_harvest)
        
        return {
            "tower_id": tower_id,
            "crop_type": crop_type,
            "predicted_height_cm": round(predicted_height, 2),
            "predicted_harvest_date": harvest_date.isoformat(),
            "days_to_harvest": days_to_harvest,
            "growth_rate_cm_per_day": round(effective_growth_rate, 3),
            "health_score": round(health_score, 3),
            "confidence": 0.85,  # ML model confidence
            "model_name": "growth_predictor",
            "model_version": self.model_manager.MODEL_VERSION,
            "generated_at": datetime.utcnow().isoformat(),
        }
    
    def _rule_based_predict(
        self,
        tower_id: str,
        crop_type: str,
        current_height_cm: float,
        days_since_planting: int,
        health_score: float,
        crop_config: dict,
    ) -> dict:
        """Rule-based fallback prediction."""
        base_growth_rate = crop_config["growth_rate_cm_per_day"]
        effective_growth_rate = base_growth_rate * health_score
        
        # Predict height in 7 days
        predicted_height = current_height_cm + (effective_growth_rate * 7)
        
        # Estimate days to harvest
        target_height = crop_config["expected_height_cm"]
        remaining_height = max(0, target_height - current_height_cm)
        days_to_harvest = int(remaining_height / effective_growth_rate) if effective_growth_rate > 0 else 30
        
        # Cap at reasonable bounds
        days_to_harvest = min(days_to_harvest, crop_config["expected_days_to_harvest"] * 2)
        harvest_date = datetime.utcnow() + timedelta(days=days_to_harvest)
        
        return {
            "tower_id": tower_id,
            "crop_type": crop_type,
            "predicted_height_cm": round(predicted_height, 2),
            "predicted_harvest_date": harvest_date.isoformat(),
            "days_to_harvest": days_to_harvest,
            "growth_rate_cm_per_day": round(effective_growth_rate, 3),
            "health_score": round(health_score, 3),
            "confidence": 0.65,  # Lower confidence for rule-based
            "model_name": "rule_based",
            "model_version": self.model_manager.MODEL_VERSION,
            "generated_at": datetime.utcnow().isoformat(),
        }
    
    def _calculate_health_score(
        self,
        crop_config: dict,
        temp: Optional[float],
        humidity: Optional[float],
        light: Optional[float],
        ph: Optional[float],
        ec: Optional[float],
    ) -> float:
        """
        Calculate health score (0-1) based on how close conditions are to optimal.
        """
        scores = []
        
        if temp is not None:
            temp_score = self._range_score(temp, crop_config["temp_min_c"], 
                                           crop_config["temp_max_c"], crop_config["temp_optimal_c"])
            scores.append(temp_score)
        
        if humidity is not None:
            humidity_score = self._range_score(humidity, crop_config["humidity_min_pct"],
                                               crop_config["humidity_max_pct"], crop_config["humidity_optimal_pct"])
            scores.append(humidity_score)
        
        if light is not None:
            light_score = self._range_score(light, crop_config["light_min_lux"],
                                            crop_config["light_max_lux"], 
                                            (crop_config["light_min_lux"] + crop_config["light_max_lux"]) / 2)
            scores.append(light_score)
        
        if ph is not None:
            ph_score = self._range_score(ph, crop_config["ph_min"], 
                                         crop_config["ph_max"], crop_config["ph_optimal"])
            scores.append(ph_score)
        
        if ec is not None:
            ec_score = self._range_score(ec, crop_config["ec_min_ms_cm"],
                                         crop_config["ec_max_ms_cm"], crop_config["ec_optimal_ms_cm"])
            scores.append(ec_score)
        
        # Average all available scores, default to 0.85 if no env data
        return np.mean(scores) if scores else 0.85
    
    def _range_score(self, value: float, min_val: float, max_val: float, optimal: float) -> float:
        """Calculate score based on how close value is to optimal range."""
        if min_val <= value <= max_val:
            # Within acceptable range - score based on distance from optimal
            distance = abs(value - optimal)
            max_distance = max(optimal - min_val, max_val - optimal)
            return 1.0 - (distance / max_distance) * 0.3  # 0.7 to 1.0
        else:
            # Outside range - penalize more heavily
            if value < min_val:
                distance = min_val - value
            else:
                distance = value - max_val
            return max(0.3, 0.7 - (distance / (max_val - min_val)) * 0.4)


# =============================================================================
# Anomaly Detector
# =============================================================================

class AnomalyDetector:
    """
    Detects anomalies in sensor telemetry using ML or rule-based thresholds.
    """
    
    # Absolute safe ranges for sensors
    SAFE_RANGES = {
        "air_temp_c": (5, 40),
        "humidity_pct": (20, 95),
        "light_lux": (0, 100000),
        "ph": (4.0, 9.0),
        "ec_ms_cm": (0, 6.0),
        "tds_ppm": (0, 4000),
        "water_temp_c": (10, 35),
        "water_level_pct": (10, 100),
    }
    
    # Warning ranges (tighter than safe)
    WARNING_RANGES = {
        "air_temp_c": (12, 32),
        "humidity_pct": (35, 85),
        "light_lux": (1000, 50000),
        "ph": (5.0, 7.5),
        "ec_ms_cm": (0.3, 4.0),
        "tds_ppm": (200, 2500),
        "water_temp_c": (15, 28),
        "water_level_pct": (25, 95),
    }
    
    def __init__(self, model_manager: ModelManager):
        self.model_manager = model_manager
    
    def detect(
        self,
        tower_id: Optional[str],
        coord_id: Optional[str],
        telemetry: dict,
    ) -> dict:
        """
        Detect anomalies in telemetry data.
        
        Returns dict with:
            - is_anomalous: bool
            - anomaly_score: float (0-1)
            - anomalies: list of AnomalyResult dicts
        """
        anomalies = []
        
        # Check each sensor value against thresholds
        for feature, (safe_min, safe_max) in self.SAFE_RANGES.items():
            value = telemetry.get(feature)
            if value is None:
                continue
            
            warn_min, warn_max = self.WARNING_RANGES.get(feature, (safe_min, safe_max))
            
            result = self._check_value(feature, value, safe_min, safe_max, warn_min, warn_max)
            if result:
                anomalies.append(result)
        
        # Calculate overall anomaly score
        if anomalies:
            severity_weights = {"low": 0.25, "medium": 0.5, "high": 0.75, "critical": 1.0}
            total_weight = sum(severity_weights.get(a["severity"], 0.5) for a in anomalies)
            anomaly_score = min(1.0, total_weight / 4)  # Normalize to 0-1
        else:
            anomaly_score = 0.0
        
        return {
            "tower_id": tower_id,
            "coord_id": coord_id,
            "is_anomalous": len(anomalies) > 0,
            "anomaly_score": round(anomaly_score, 3),
            "anomalies": anomalies,
            "model_name": "rule_based_anomaly",
            "model_version": ModelManager.MODEL_VERSION,
            "generated_at": datetime.utcnow().isoformat(),
        }
    
    def _check_value(
        self,
        feature: str,
        value: float,
        safe_min: float,
        safe_max: float,
        warn_min: float,
        warn_max: float,
    ) -> Optional[dict]:
        """Check a single value against thresholds."""
        feature_labels = {
            "air_temp_c": "Air Temperature",
            "humidity_pct": "Humidity",
            "light_lux": "Light Intensity",
            "ph": "pH Level",
            "ec_ms_cm": "Electrical Conductivity",
            "tds_ppm": "Total Dissolved Solids",
            "water_temp_c": "Water Temperature",
            "water_level_pct": "Water Level",
        }
        
        label = feature_labels.get(feature, feature)
        
        # Critical: outside safe range
        if value < safe_min:
            severity = "critical"
            message = f"{label} is critically low at {value:.1f} (safe minimum: {safe_min})"
        elif value > safe_max:
            severity = "critical"
            message = f"{label} is critically high at {value:.1f} (safe maximum: {safe_max})"
        # High: just outside warning range
        elif value < warn_min:
            severity = "high" if value < (safe_min + warn_min) / 2 else "medium"
            message = f"{label} is low at {value:.1f} (optimal range: {warn_min}-{warn_max})"
        elif value > warn_max:
            severity = "high" if value > (safe_max + warn_max) / 2 else "medium"
            message = f"{label} is high at {value:.1f} (optimal range: {warn_min}-{warn_max})"
        else:
            return None  # Within acceptable range
        
        return {
            "feature": feature,
            "value": round(value, 2),
            "expected_min": warn_min,
            "expected_max": warn_max,
            "severity": severity,
            "message": message,
        }


# =============================================================================
# Optimal Conditions Provider
# =============================================================================

def get_optimal_conditions(crop_type: str, growth_stage: str = "vegetative") -> dict:
    """
    Get optimal growing conditions for a crop type and growth stage.
    """
    config = CROP_CONFIG.get(crop_type, DEFAULT_CROP_CONFIG)
    
    # Adjust for growth stage
    stage_adjustments = {
        "seedling": {
            "temp_offset": -2,
            "humidity_offset": 10,
            "light_factor": 0.6,
            "ec_factor": 0.5,
        },
        "vegetative": {
            "temp_offset": 0,
            "humidity_offset": 0,
            "light_factor": 1.0,
            "ec_factor": 1.0,
        },
        "flowering": {
            "temp_offset": 1,
            "humidity_offset": -5,
            "light_factor": 1.2,
            "ec_factor": 1.1,
        },
        "fruiting": {
            "temp_offset": 2,
            "humidity_offset": -10,
            "light_factor": 1.3,
            "ec_factor": 1.2,
        },
    }
    
    adj = stage_adjustments.get(growth_stage, stage_adjustments["vegetative"])
    
    return {
        "crop_type": crop_type,
        "growth_stage": growth_stage,
        "temp_min_c": config["temp_min_c"] + adj["temp_offset"],
        "temp_max_c": config["temp_max_c"] + adj["temp_offset"],
        "temp_optimal_c": config["temp_optimal_c"] + adj["temp_offset"],
        "humidity_min_pct": max(30, config["humidity_min_pct"] + adj["humidity_offset"]),
        "humidity_max_pct": min(90, config["humidity_max_pct"] + adj["humidity_offset"]),
        "humidity_optimal_pct": config["humidity_optimal_pct"] + adj["humidity_offset"],
        "light_min_lux": config["light_min_lux"] * adj["light_factor"],
        "light_max_lux": config["light_max_lux"] * adj["light_factor"],
        "light_hours_per_day": config["light_hours_per_day"],
        "ph_min": config["ph_min"],
        "ph_max": config["ph_max"],
        "ph_optimal": config["ph_optimal"],
        "ec_min_ms_cm": config["ec_min_ms_cm"] * adj["ec_factor"],
        "ec_max_ms_cm": config["ec_max_ms_cm"] * adj["ec_factor"],
        "ec_optimal_ms_cm": config["ec_optimal_ms_cm"] * adj["ec_factor"],
        "expected_days_to_harvest": config["expected_days_to_harvest"],
        "expected_height_cm": config["expected_height_cm"],
    }


# =============================================================================
# Singleton instances
# =============================================================================

model_manager = ModelManager()
growth_predictor = GrowthPredictor(model_manager)
anomaly_detector = AnomalyDetector(model_manager)
