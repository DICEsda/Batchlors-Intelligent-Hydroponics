"""
Nutrient consumption prediction model training script.

Trains a regression model to predict the natural depletion rates of
EC (mS/cm per hour), pH drift (units per hour), and water consumption
(% per hour) based on:
- Current reservoir state (pH, EC, water level, water temp)
- Environmental context (air temp, humidity)
- Crop load (number of active towers)
- Time since last dosing event

The training data filters out windows where dosing pumps were active so
the model only learns organic consumption patterns.

CLI:
    python -m src.training.train_consumption --hours 168 --coord-id coord01

Imported:
    from src.training.train_consumption import train_consumption_model
    model, metrics = train_consumption_model(hours=168)
"""

import argparse
from datetime import datetime
from typing import Optional

import numpy as np
import pandas as pd
from loguru import logger
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.metrics import mean_squared_error, mean_absolute_error, r2_score
from sklearn.multioutput import MultiOutputRegressor

from ..config import config
from ..data.mongodb_connector import MongoDBConnector
from .model_manager import model_manager
from .feature_engineering import FeatureEngineer, RESERVOIR_METRICS


# Targets: delta (depletion) per hour for each reservoir metric
TARGET_COLS = [f"{m}_delta" for m in RESERVOIR_METRICS]


def _get_regressor(model_type: str, **kwargs):
    """Instantiate the chosen regressor."""
    if model_type == "xgboost":
        from xgboost import XGBRegressor

        return XGBRegressor(
            n_estimators=kwargs.get("n_estimators", 150),
            max_depth=kwargs.get("max_depth", 5),
            learning_rate=kwargs.get("learning_rate", 0.1),
            random_state=config.ml.random_seed,
            n_jobs=-1,
        )
    elif model_type == "lightgbm":
        from lightgbm import LGBMRegressor

        return LGBMRegressor(
            n_estimators=kwargs.get("n_estimators", 150),
            max_depth=kwargs.get("max_depth", 5),
            learning_rate=kwargs.get("learning_rate", 0.1),
            random_state=config.ml.random_seed,
            n_jobs=-1,
            verbose=-1,
        )
    else:
        from sklearn.ensemble import GradientBoostingRegressor

        return GradientBoostingRegressor(
            n_estimators=kwargs.get("n_estimators", 150),
            max_depth=kwargs.get("max_depth", 5),
            learning_rate=kwargs.get("learning_rate", 0.1),
            random_state=config.ml.random_seed,
        )


def train_consumption_model(
    hours: int = 168,
    coord_id: Optional[str] = None,
    farm_id: Optional[str] = None,
    model_type: str = "xgboost",
    resample_interval: str = "1h",
    **model_kwargs,
) -> tuple:
    """
    Train the nutrient consumption prediction model.

    Parameters
    ----------
    hours : int
        Hours of historical data.
    coord_id, farm_id : str, optional
        Filter scope.
    model_type : str
        ``"xgboost"`` | ``"lightgbm"`` | ``"gradient_boosting"``.
    resample_interval : str
        Pandas freq for resampling.

    Returns
    -------
    (model, metrics_dict)
    """
    logger.info(
        f"Starting consumption model training (type={model_type}, hours={hours})"
    )

    # ---- Build features ----
    with MongoDBConnector() as mongodb:
        fe = FeatureEngineer(mongodb)
        df = fe.build_consumption_features(
            coord_id=coord_id,
            farm_id=farm_id,
            hours=hours,
            resample_interval=resample_interval,
        )

    if df.empty:
        logger.error("No data returned from consumption feature builder")
        return None, {"error": "no_data"}

    # ---- Separate features / targets ----
    available_targets = [c for c in TARGET_COLS if c in df.columns]
    if not available_targets:
        logger.error("No target columns found in consumption features")
        return None, {"error": "no_targets"}

    feature_cols = [c for c in df.columns if c not in TARGET_COLS]
    X = df[feature_cols].values
    Y = df[available_targets].values

    logger.info(
        f"Consumption dataset: {X.shape[0]} samples, {X.shape[1]} features, "
        f"{Y.shape[1]} targets"
    )

    if X.shape[0] < 30:
        logger.error(f"Only {X.shape[0]} samples; need at least 30")
        return None, {"error": "insufficient_data", "samples": X.shape[0]}

    # ---- Split (temporal) ----
    split_idx = int(len(X) * (1 - config.ml.test_split_ratio))
    X_train, X_test = X[:split_idx], X[split_idx:]
    Y_train, Y_test = Y[:split_idx], Y[split_idx:]

    logger.info(f"Train: {len(X_train)}, Test: {len(X_test)}")

    # ---- Train ----
    base_model = _get_regressor(model_type, **model_kwargs)
    model = MultiOutputRegressor(base_model)

    logger.info("Training consumption model...")
    model.fit(X_train, Y_train)

    # ---- Evaluate ----
    Y_pred = model.predict(X_test)
    per_target_metrics: dict[str, dict] = {}
    for i, tgt in enumerate(available_targets):
        rmse = float(np.sqrt(mean_squared_error(Y_test[:, i], Y_pred[:, i])))
        mae = float(mean_absolute_error(Y_test[:, i], Y_pred[:, i]))
        r2_val = (
            float(r2_score(Y_test[:, i], Y_pred[:, i])) if Y_test.shape[0] > 1 else 0.0
        )
        per_target_metrics[tgt] = {"rmse": rmse, "mae": mae, "r2": r2_val}

    overall_rmse = float(np.sqrt(mean_squared_error(Y_test, Y_pred)))
    overall_r2 = float(np.mean([m["r2"] for m in per_target_metrics.values()]))

    metrics = {
        "overall_rmse": overall_rmse,
        "overall_r2": overall_r2,
        "per_target": per_target_metrics,
        "n_train": len(X_train),
        "n_test": len(X_test),
    }

    logger.success(
        f"Consumption model trained - RMSE: {overall_rmse:.6f}, R2: {overall_r2:.3f}"
    )

    # ---- Feature importance (per sub-estimator) ----
    try:
        importances = np.mean(
            [est.feature_importances_ for est in model.estimators_], axis=0
        )
        metrics["feature_importance"] = dict(zip(feature_cols, importances.tolist()))
    except Exception:
        pass

    # ---- Save ----
    version = datetime.utcnow().strftime("%Y%m%d.%H%M")
    hyperparams = {
        "model_type": model_type,
        "resample_interval": resample_interval,
        "n_estimators": model_kwargs.get("n_estimators", 150),
        "max_depth": model_kwargs.get("max_depth", 5),
        "learning_rate": model_kwargs.get("learning_rate", 0.1),
    }

    model_manager.save_model(
        model=model,
        name="consumption_predictor",
        version=version,
        model_type=f"MultiOutput({model_type})",
        metrics=metrics,
        features=feature_cols,
        target=",".join(available_targets),
        training_samples=len(X_train),
        hyperparameters=hyperparams,
    )

    return model, metrics


def main():
    """CLI entry point."""
    parser = argparse.ArgumentParser(description="Train nutrient consumption model")
    parser.add_argument(
        "--hours", type=int, default=168, help="Hours of data (default 7 days)"
    )
    parser.add_argument("--coord-id", type=str, help="Filter by coordinator")
    parser.add_argument("--farm-id", type=str, help="Filter by farm")
    parser.add_argument(
        "--model-type",
        type=str,
        default="xgboost",
        choices=["xgboost", "lightgbm", "gradient_boosting"],
    )
    parser.add_argument(
        "--resample", type=str, default="1h", help="Resample interval (default 1h)"
    )
    parser.add_argument("--n-estimators", type=int, default=150)
    parser.add_argument("--max-depth", type=int, default=5)

    args = parser.parse_args()

    model, metrics = train_consumption_model(
        hours=args.hours,
        coord_id=args.coord_id,
        farm_id=args.farm_id,
        model_type=args.model_type,
        resample_interval=args.resample,
        n_estimators=args.n_estimators,
        max_depth=args.max_depth,
    )

    if model is None:
        print(f"\nTraining failed: {metrics.get('error', 'unknown')}")
        return

    print("\n=== Consumption Training Complete ===")
    print(f"Overall RMSE: {metrics['overall_rmse']:.6f}")
    print(f"Overall R2:   {metrics['overall_r2']:.4f}")
    print("\nPer-target:")
    for tgt, m in metrics["per_target"].items():
        print(f"  {tgt:30s}  RMSE={m['rmse']:.6f}  R2={m['r2']:.3f}")


if __name__ == "__main__":
    main()
