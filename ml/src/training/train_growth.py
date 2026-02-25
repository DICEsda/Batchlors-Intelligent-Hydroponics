"""
Growth prediction model training script.

Trains a RandomForest model to predict plant height based on:
- Days since planting
- Environmental conditions (temperature, humidity, light)
- Water quality (pH, EC)

Can be run as a CLI:
    python -m src.training.train_growth --hours 720 --farm-id farm001

Or imported:
    from src.training.train_growth import train_growth_model
    model, metrics = train_growth_model(hours=720)
"""

import argparse
from datetime import datetime
from typing import Optional

import numpy as np
import pandas as pd
from loguru import logger
from sklearn.ensemble import RandomForestRegressor, GradientBoostingRegressor
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.metrics import mean_squared_error, mean_absolute_error, r2_score

from ..config import config
from ..data.mongodb_connector import MongoDBConnector
from .model_manager import model_manager


# Legacy feature columns (kept for backwards compatibility)
FEATURES = [
    "days_since_planting",
    "air_temp_c",
    "humidity_pct",
    "light_lux",
    "ph",
    "ec_ms_cm",
]

# Enhanced feature columns (used when FeatureEngineer data is available)
ENHANCED_FEATURES = [
    "days_since_planting",
    "avg_air_temp_c_24h",
    "avg_humidity_pct_24h",
    "avg_light_lux_24h",
    "avg_ph_24h",
    "avg_ec_ms_cm_24h",
    "avg_water_temp_c_24h",
    "temp_x_humidity",
    "ph_deviation",
    "ec_deviation",
]

# Categorical columns that get one-hot encoded
CATEGORICAL_FEATURES = ["crop_type", "growth_stage"]

TARGET = "height_cm"


def prepare_training_data(
    mongodb: MongoDBConnector,
    farm_id: Optional[str] = None,
    hours: int = 720,  # 30 days default
    use_enhanced: bool = True,
) -> pd.DataFrame:
    """
    Prepare training dataset by joining height measurements with telemetry.

    When ``use_enhanced=True`` (default), uses FeatureEngineer for rolling
    24h averages, growth stage, interaction features, and crop-specific
    pH/EC deviations.  Falls back to the legacy daily-join approach if
    the enhanced pipeline fails or returns no data.

    Args:
        mongodb: MongoDB connector
        farm_id: Optional farm ID filter
        hours: Hours of data to fetch
        use_enhanced: Use the enhanced feature engineering pipeline

    Returns:
        DataFrame with features and target
    """
    logger.info(f"Fetching training data (last {hours} hours, enhanced={use_enhanced})")

    # --- Try enhanced pipeline first ---
    if use_enhanced:
        try:
            from .feature_engineering import FeatureEngineer

            fe = FeatureEngineer(mongodb)
            df = fe.build_growth_features(farm_id=farm_id, hours=hours)
            if not df.empty and TARGET in df.columns and len(df) >= 10:
                logger.info(f"Enhanced pipeline returned {len(df)} samples")
                return df
            logger.info("Enhanced pipeline returned insufficient data, trying legacy")
        except Exception as e:
            logger.warning(f"Enhanced feature pipeline failed: {e}")

    # --- Legacy pipeline ---
    heights_df = mongodb.get_height_measurements(farm_id=farm_id, hours=hours)

    if heights_df.empty:
        logger.warning("No height measurements found - generating synthetic data")
        return generate_synthetic_data(500)

    # Get tower telemetry
    tower_df = mongodb.get_tower_telemetry(farm_id=farm_id, hours=hours)

    # Get reservoir telemetry for pH/EC
    reservoir_df = mongodb.get_reservoir_telemetry(farm_id=farm_id, hours=hours)

    # Join datasets
    if not tower_df.empty:
        tower_df["date"] = pd.to_datetime(tower_df["timestamp"]).dt.date
        tower_daily = (
            tower_df.groupby(["tower_id", "date"])
            .agg(
                {
                    "air_temp_c": "mean",
                    "humidity_pct": "mean",
                    "light_lux": "mean",
                }
            )
            .reset_index()
        )

        heights_df["date"] = pd.to_datetime(heights_df["timestamp"]).dt.date
        df = heights_df.merge(tower_daily, on=["tower_id", "date"], how="left")
    else:
        df = heights_df

    if not reservoir_df.empty:
        reservoir_df["date"] = pd.to_datetime(reservoir_df["timestamp"]).dt.date
        reservoir_daily = (
            reservoir_df.groupby("date")
            .agg(
                {
                    "ph": "mean",
                    "ec_ms_cm": "mean",
                }
            )
            .reset_index()
        )
        df = df.merge(reservoir_daily, on="date", how="left")

    # Fill missing values with reasonable defaults
    df["air_temp_c"] = df.get("air_temp_c", pd.Series()).fillna(22)
    df["humidity_pct"] = df.get("humidity_pct", pd.Series()).fillna(60)
    df["light_lux"] = df.get("light_lux", pd.Series()).fillna(15000)
    df["ph"] = df.get("ph", pd.Series()).fillna(6.0)
    df["ec_ms_cm"] = df.get("ec_ms_cm", pd.Series()).fillna(1.2)

    # Ensure target column exists
    if TARGET not in df.columns:
        logger.warning(f"Target column '{TARGET}' not found")
        return pd.DataFrame()

    logger.info(f"Prepared dataset with {len(df)} samples")
    return df


def generate_synthetic_data(n_samples: int = 500) -> pd.DataFrame:
    """
    Generate synthetic training data when real data is not available.

    Uses realistic growth curves and environmental relationships.
    """
    logger.info(f"Generating {n_samples} synthetic samples")

    np.random.seed(config.ml.random_seed)

    # Generate realistic distributions
    days = np.random.randint(1, 60, n_samples)
    temp = np.random.normal(22, 3, n_samples).clip(15, 32)
    humidity = np.random.normal(60, 10, n_samples).clip(30, 90)
    light = np.random.normal(15000, 3000, n_samples).clip(5000, 30000)
    ph = np.random.normal(6.2, 0.4, n_samples).clip(5.0, 7.5)
    ec = np.random.normal(1.5, 0.3, n_samples).clip(0.5, 3.0)

    # Generate height based on days and conditions
    # Base growth rate: ~0.6 cm/day for lettuce
    base_rate = 0.6

    # Environmental modifiers
    temp_factor = 1 - 0.03 * np.abs(temp - 20)  # Optimal around 20C
    humidity_factor = 1 - 0.01 * np.abs(humidity - 60)  # Optimal around 60%
    light_factor = 0.8 + 0.2 * (light - 5000) / 25000  # More light = better
    ph_factor = 1 - 0.1 * np.abs(ph - 6.2)  # Optimal pH 6.2
    ec_factor = 1 - 0.1 * np.abs(ec - 1.5)  # Optimal EC 1.5

    # Combined growth factor
    growth_factor = temp_factor * humidity_factor * light_factor * ph_factor * ec_factor
    growth_factor = np.clip(growth_factor, 0.3, 1.2)

    # Height = days * rate * growth_factor + noise
    height = days * base_rate * growth_factor + np.random.normal(0, 1, n_samples)
    height = np.clip(height, 0.5, 40)  # Realistic bounds

    return pd.DataFrame(
        {
            "days_since_planting": days,
            "air_temp_c": temp,
            "humidity_pct": humidity,
            "light_lux": light,
            "ph": ph,
            "ec_ms_cm": ec,
            "height_cm": height,
        }
    )


def train_growth_model(
    hours: int = 720,
    farm_id: Optional[str] = None,
    model_type: str = "random_forest",
    n_estimators: int = 100,
    max_depth: Optional[int] = 10,
) -> tuple:
    """
    Train a growth prediction model.

    Args:
        hours: Hours of historical data to use
        farm_id: Optional farm ID filter
        model_type: "random_forest" or "gradient_boosting"
        n_estimators: Number of trees
        max_depth: Maximum tree depth

    Returns:
        Tuple of (model, metrics_dict)
    """
    logger.info(f"Starting growth model training (type={model_type})")

    # Get data
    with MongoDBConnector() as mongodb:
        df = prepare_training_data(mongodb, farm_id=farm_id, hours=hours)

    if df.empty or len(df) < config.ml.min_samples_for_training:
        logger.warning("Insufficient data, generating synthetic samples")
        df = generate_synthetic_data(500)

    # Prepare features and target - prefer enhanced features, fall back to legacy
    enhanced_available = [f for f in ENHANCED_FEATURES if f in df.columns]
    legacy_available = [f for f in FEATURES if f in df.columns]
    available_features = (
        enhanced_available if len(enhanced_available) >= 4 else legacy_available
    )

    # One-hot encode categorical columns if present
    for cat_col in CATEGORICAL_FEATURES:
        if cat_col in df.columns:
            dummies = pd.get_dummies(df[cat_col], prefix=cat_col, drop_first=True)
            df = pd.concat([df, dummies], axis=1)
            available_features += list(dummies.columns)

    X = df[available_features].fillna(0).values
    y = df[TARGET].values

    # Split data
    X_train, X_test, y_train, y_test = train_test_split(
        X,
        y,
        test_size=config.ml.test_split_ratio,
        random_state=config.ml.random_seed,
    )

    logger.info(
        f"Training set: {len(X_train)} samples, Test set: {len(X_test)} samples"
    )

    # Create model
    hyperparameters = {
        "n_estimators": n_estimators,
        "max_depth": max_depth,
        "random_state": config.ml.random_seed,
    }

    if model_type == "gradient_boosting":
        model = GradientBoostingRegressor(**hyperparameters)
    else:
        hyperparameters["n_jobs"] = -1
        model = RandomForestRegressor(**hyperparameters)

    # Train
    logger.info("Training model...")
    model.fit(X_train, y_train)

    # Evaluate
    y_pred = model.predict(X_test)

    metrics = {
        "rmse": float(np.sqrt(mean_squared_error(y_test, y_pred))),
        "mae": float(mean_absolute_error(y_test, y_pred)),
        "r2": float(r2_score(y_test, y_pred)),
    }

    # Cross-validation
    cv_scores = cross_val_score(model, X, y, cv=5, scoring="neg_mean_squared_error")
    metrics["cv_rmse_mean"] = float(np.sqrt(-cv_scores.mean()))
    metrics["cv_rmse_std"] = float(np.sqrt(-cv_scores).std())

    # Feature importance
    importances = dict(zip(available_features, model.feature_importances_.tolist()))

    logger.success(
        f"Model trained - RMSE: {metrics['rmse']:.3f}, R²: {metrics['r2']:.3f}"
    )
    logger.info(f"Feature importances: {importances}")

    # Save model
    version = datetime.utcnow().strftime("%Y%m%d.%H%M")
    model_manager.save_model(
        model=model,
        name="growth_predictor",
        version=version,
        model_type=type(model).__name__,
        metrics=metrics,
        features=available_features,
        target=TARGET,
        training_samples=len(df),
        hyperparameters=hyperparameters,
    )

    return model, metrics


def main():
    """CLI entry point."""
    parser = argparse.ArgumentParser(description="Train growth prediction model")
    parser.add_argument("--hours", type=int, default=720, help="Hours of data to use")
    parser.add_argument("--farm-id", type=str, help="Filter by farm ID")
    parser.add_argument(
        "--model-type",
        choices=["random_forest", "gradient_boosting"],
        default="random_forest",
    )
    parser.add_argument("--n-estimators", type=int, default=100)
    parser.add_argument("--max-depth", type=int, default=10)

    args = parser.parse_args()

    model, metrics = train_growth_model(
        hours=args.hours,
        farm_id=args.farm_id,
        model_type=args.model_type,
        n_estimators=args.n_estimators,
        max_depth=args.max_depth,
    )

    print("\n=== Training Complete ===")
    print(f"RMSE: {metrics['rmse']:.4f}")
    print(f"MAE: {metrics['mae']:.4f}")
    print(f"R²: {metrics['r2']:.4f}")
    print(f"CV RMSE: {metrics['cv_rmse_mean']:.4f} ± {metrics['cv_rmse_std']:.4f}")


if __name__ == "__main__":
    main()
