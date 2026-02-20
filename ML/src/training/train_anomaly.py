"""
Anomaly detection model training script.

Trains an Isolation Forest model to detect anomalies in sensor telemetry.

Can be run as a CLI:
    python -m src.training.train_anomaly --hours 720 --contamination 0.05

Or imported:
    from src.training.train_anomaly import train_anomaly_model
    model, metrics = train_anomaly_model(hours=720)
"""

import argparse
from datetime import datetime
from typing import Optional

import numpy as np
import pandas as pd
from loguru import logger
from sklearn.ensemble import IsolationForest
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import silhouette_score

from ..config import config
from ..data.mongodb_connector import MongoDBConnector
from .model_manager import model_manager


# Feature columns for anomaly detection
FEATURES = [
    "air_temp_c",
    "humidity_pct",
    "light_lux",
    "ph",
    "ec_ms_cm",
    "water_temp_c",
    "water_level_pct",
]


def prepare_training_data(
    mongodb: MongoDBConnector,
    farm_id: Optional[str] = None,
    hours: int = 720,
) -> pd.DataFrame:
    """
    Prepare training dataset by combining tower and reservoir telemetry.
    
    Args:
        mongodb: MongoDB connector
        farm_id: Optional farm ID filter
        hours: Hours of data to fetch
    
    Returns:
        DataFrame with telemetry features
    """
    logger.info(f"Fetching anomaly training data (last {hours} hours)")
    
    # Get tower telemetry
    tower_df = mongodb.get_tower_telemetry(farm_id=farm_id, hours=hours)
    
    # Get reservoir telemetry
    reservoir_df = mongodb.get_reservoir_telemetry(farm_id=farm_id, hours=hours)
    
    if tower_df.empty and reservoir_df.empty:
        logger.warning("No telemetry found - generating synthetic data")
        return generate_synthetic_data(1000)
    
    # Combine datasets
    dfs = []
    
    if not tower_df.empty:
        tower_df = tower_df[["timestamp", "air_temp_c", "humidity_pct", "light_lux"]].dropna()
        tower_df["timestamp"] = pd.to_datetime(tower_df["timestamp"])
        tower_df = tower_df.set_index("timestamp").resample("5min").mean().reset_index()
        dfs.append(tower_df)
    
    if not reservoir_df.empty:
        reservoir_df = reservoir_df[["timestamp", "ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]].dropna()
        reservoir_df["timestamp"] = pd.to_datetime(reservoir_df["timestamp"])
        reservoir_df = reservoir_df.set_index("timestamp").resample("5min").mean().reset_index()
        dfs.append(reservoir_df)
    
    if len(dfs) == 2:
        df = dfs[0].merge(dfs[1], on="timestamp", how="outer")
    elif len(dfs) == 1:
        df = dfs[0]
    else:
        return pd.DataFrame()
    
    # Fill missing values with forward fill then backward fill
    df = df.sort_values("timestamp").ffill().bfill()
    
    logger.info(f"Prepared dataset with {len(df)} samples")
    return df


def generate_synthetic_data(n_samples: int = 1000) -> pd.DataFrame:
    """
    Generate synthetic telemetry data including some anomalies.
    
    Uses realistic sensor distributions with ~5% intentional anomalies.
    """
    logger.info(f"Generating {n_samples} synthetic samples with anomalies")
    
    np.random.seed(config.ml.random_seed)
    
    # Normal data (~95%)
    n_normal = int(n_samples * 0.95)
    
    normal_data = {
        "air_temp_c": np.random.normal(22, 2, n_normal),
        "humidity_pct": np.random.normal(60, 8, n_normal),
        "light_lux": np.random.normal(15000, 2000, n_normal),
        "ph": np.random.normal(6.2, 0.3, n_normal),
        "ec_ms_cm": np.random.normal(1.5, 0.2, n_normal),
        "water_temp_c": np.random.normal(20, 1.5, n_normal),
        "water_level_pct": np.random.normal(70, 10, n_normal),
    }
    
    # Anomalous data (~5%)
    n_anomaly = n_samples - n_normal
    
    anomaly_data = {
        "air_temp_c": np.random.choice([
            np.random.normal(10, 2, n_anomaly // 2),  # Too cold
            np.random.normal(35, 2, n_anomaly - n_anomaly // 2),  # Too hot
        ]),
        "humidity_pct": np.random.choice([
            np.random.normal(20, 5, n_anomaly // 2),  # Too dry
            np.random.normal(95, 3, n_anomaly - n_anomaly // 2),  # Too humid
        ]),
        "light_lux": np.random.uniform(0, 3000, n_anomaly),  # Light failure
        "ph": np.random.choice([
            np.random.normal(4.5, 0.3, n_anomaly // 2),  # Too acidic
            np.random.normal(8.0, 0.3, n_anomaly - n_anomaly // 2),  # Too alkaline
        ]),
        "ec_ms_cm": np.random.choice([
            np.random.normal(0.2, 0.1, n_anomaly // 2),  # Too low
            np.random.normal(4.0, 0.5, n_anomaly - n_anomaly // 2),  # Too high
        ]),
        "water_temp_c": np.random.choice([
            np.random.normal(10, 2, n_anomaly // 2),  # Too cold
            np.random.normal(32, 2, n_anomaly - n_anomaly // 2),  # Too warm
        ]),
        "water_level_pct": np.random.uniform(5, 20, n_anomaly),  # Low water
    }
    
    # Combine
    df = pd.DataFrame({
        feature: np.concatenate([normal_data[feature], anomaly_data[feature].flatten()[:n_anomaly]])
        for feature in FEATURES if feature in normal_data
    })
    
    # Add timestamp
    df["timestamp"] = pd.date_range(end=datetime.utcnow(), periods=len(df), freq="5min")
    
    # Shuffle
    df = df.sample(frac=1, random_state=config.ml.random_seed).reset_index(drop=True)
    
    return df


class AnomalyModel:
    """
    Wrapper for Isolation Forest with StandardScaler.
    
    Stores both the scaler and model for proper inference.
    """
    
    def __init__(self, contamination: float = 0.05, n_estimators: int = 100):
        self.scaler = StandardScaler()
        self.model = IsolationForest(
            contamination=contamination,
            n_estimators=n_estimators,
            random_state=config.ml.random_seed,
            n_jobs=-1,
        )
        self.feature_names: list[str] = []
    
    def fit(self, X: np.ndarray, feature_names: list[str]) -> "AnomalyModel":
        """Fit scaler and model."""
        self.feature_names = feature_names
        X_scaled = self.scaler.fit_transform(X)
        self.model.fit(X_scaled)
        return self
    
    def predict(self, X: np.ndarray) -> np.ndarray:
        """Predict anomaly labels (-1 for anomaly, 1 for normal)."""
        X_scaled = self.scaler.transform(X)
        return self.model.predict(X_scaled)
    
    def decision_function(self, X: np.ndarray) -> np.ndarray:
        """Get anomaly scores (lower = more anomalous)."""
        X_scaled = self.scaler.transform(X)
        return self.model.decision_function(X_scaled)
    
    def score_samples(self, X: np.ndarray) -> np.ndarray:
        """Get normalized anomaly scores (0-1, higher = more anomalous)."""
        scores = self.decision_function(X)
        # Convert to 0-1 range (more anomalous = higher score)
        min_score, max_score = scores.min(), scores.max()
        if max_score > min_score:
            return 1 - (scores - min_score) / (max_score - min_score)
        return np.zeros_like(scores)


def train_anomaly_model(
    hours: int = 720,
    farm_id: Optional[str] = None,
    contamination: float = 0.05,
    n_estimators: int = 100,
) -> tuple:
    """
    Train an anomaly detection model.
    
    Args:
        hours: Hours of historical data to use
        farm_id: Optional farm ID filter
        contamination: Expected proportion of outliers (0-0.5)
        n_estimators: Number of trees in the forest
    
    Returns:
        Tuple of (model, metrics_dict)
    """
    logger.info(f"Starting anomaly model training (contamination={contamination})")
    
    # Get data
    with MongoDBConnector() as mongodb:
        df = prepare_training_data(mongodb, farm_id=farm_id, hours=hours)
    
    if df.empty or len(df) < config.ml.min_samples_for_training:
        logger.warning("Insufficient data, generating synthetic samples")
        df = generate_synthetic_data(1000)
    
    # Prepare features
    available_features = [f for f in FEATURES if f in df.columns]
    X = df[available_features].dropna().values
    
    logger.info(f"Training on {len(X)} samples with {len(available_features)} features")
    
    # Create and train model
    model = AnomalyModel(contamination=contamination, n_estimators=n_estimators)
    model.fit(X, available_features)
    
    # Evaluate
    predictions = model.predict(X)
    scores = model.decision_function(X)
    
    n_anomalies = (predictions == -1).sum()
    anomaly_rate = n_anomalies / len(predictions)
    
    metrics = {
        "n_samples": len(X),
        "n_anomalies": int(n_anomalies),
        "anomaly_rate": float(anomaly_rate),
        "mean_score": float(scores.mean()),
        "std_score": float(scores.std()),
        "min_score": float(scores.min()),
        "max_score": float(scores.max()),
    }
    
    # Try to compute silhouette score (only if we have both classes)
    if 0 < n_anomalies < len(X):
        try:
            metrics["silhouette_score"] = float(silhouette_score(X, predictions))
        except Exception:
            pass
    
    logger.success(f"Model trained - Detected {n_anomalies} anomalies ({anomaly_rate:.1%})")
    
    # Save model
    version = datetime.utcnow().strftime("%Y%m%d.%H%M")
    hyperparameters = {
        "contamination": contamination,
        "n_estimators": n_estimators,
    }
    
    model_manager.save_model(
        model=model,
        name="anomaly_detector",
        version=version,
        model_type="IsolationForest",
        metrics=metrics,
        features=available_features,
        target="anomaly_label",
        training_samples=len(X),
        hyperparameters=hyperparameters,
    )
    
    return model, metrics


def main():
    """CLI entry point."""
    parser = argparse.ArgumentParser(description="Train anomaly detection model")
    parser.add_argument("--hours", type=int, default=720, help="Hours of data to use")
    parser.add_argument("--farm-id", type=str, help="Filter by farm ID")
    parser.add_argument("--contamination", type=float, default=0.05,
                        help="Expected anomaly proportion (0-0.5)")
    parser.add_argument("--n-estimators", type=int, default=100)
    
    args = parser.parse_args()
    
    model, metrics = train_anomaly_model(
        hours=args.hours,
        farm_id=args.farm_id,
        contamination=args.contamination,
        n_estimators=args.n_estimators,
    )
    
    print("\n=== Training Complete ===")
    print(f"Samples: {metrics['n_samples']}")
    print(f"Anomalies: {metrics['n_anomalies']} ({metrics['anomaly_rate']:.1%})")
    print(f"Score range: [{metrics['min_score']:.4f}, {metrics['max_score']:.4f}]")
    if "silhouette_score" in metrics:
        print(f"Silhouette: {metrics['silhouette_score']:.4f}")


if __name__ == "__main__":
    main()
