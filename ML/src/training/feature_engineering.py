"""
Shared feature engineering pipeline for all ML models.

Provides functions for:
- Joining height measurements with telemetry data
- Building crop performance profiles (for clustering)
- Time-series windowing with lag features (for drift forecasting)
- Computing depletion rates (for consumption prediction)
- Growth-stage inference from days since planting

All models share this module so feature definitions stay consistent.

Usage:
    from src.training.feature_engineering import FeatureEngineer

    fe = FeatureEngineer(mongodb_connector)
    profiles = fe.build_crop_profiles(farm_id="farm001", hours=720)
    drift_df = fe.build_drift_features(coord_id="coord01", hours=168)
"""

from datetime import datetime, timedelta
from typing import Optional

import numpy as np
import pandas as pd
from loguru import logger

from ..data.mongodb_connector import MongoDBConnector


# =============================================================================
# Constants
# =============================================================================

# Growth stages inferred from days since planting (proportion of harvest cycle)
GROWTH_STAGE_THRESHOLDS = {
    "seedling": 0.15,  # First 15% of growth cycle
    "vegetative": 0.55,  # 15-55%
    "flowering": 0.80,  # 55-80%
    "fruiting": 1.00,  # 80-100%+
}

# Expected days to harvest per crop (used for stage inference)
CROP_HARVEST_DAYS = {
    "Lettuce": 45,
    "Basil": 30,
    "Spinach": 40,
    "Kale": 55,
    "Strawberry": 90,
    "Tomato": 80,
    "Pepper": 75,
    "Mint": 30,
    "Cilantro": 25,
    "Microgreens": 14,
    "Arugula": 35,
    "SwissChard": 50,
    "BokChoy": 45,
    "Parsley": 40,
    "Dill": 35,
    "Chives": 60,
    "Oregano": 45,
    "Thyme": 50,
    "Rosemary": 90,
    "Cucumber": 60,
    "Sunflower": 12,
    "Pea": 10,
    "Radish": 8,
}

# Default lag horizons for drift forecasting (in hours)
DRIFT_LAG_HOURS = [1, 2, 3, 6, 12, 24]

# Reservoir metrics used across models
RESERVOIR_METRICS = ["ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]

# Tower environment metrics used across models
TOWER_ENV_METRICS = ["air_temp_c", "humidity_pct", "light_lux"]


# =============================================================================
# Growth Stage Inference
# =============================================================================


def infer_growth_stage(days_since_planting: int, crop_type: str = "Unknown") -> str:
    """
    Infer the growth stage from days since planting relative to expected
    harvest cycle length for the crop.

    Returns one of: "seedling", "vegetative", "flowering", "fruiting".
    """
    harvest_days = CROP_HARVEST_DAYS.get(crop_type, 45)
    progress = days_since_planting / harvest_days if harvest_days > 0 else 0.0

    for stage, threshold in GROWTH_STAGE_THRESHOLDS.items():
        if progress <= threshold:
            return stage
    return "fruiting"


def add_growth_stage_column(df: pd.DataFrame) -> pd.DataFrame:
    """Add a ``growth_stage`` column derived from ``days_since_planting`` and ``crop_type``."""
    if "days_since_planting" not in df.columns:
        return df
    crop_col = "crop_type" if "crop_type" in df.columns else None
    df["growth_stage"] = df.apply(
        lambda row: infer_growth_stage(
            int(row["days_since_planting"]),
            row[crop_col] if crop_col else "Unknown",
        ),
        axis=1,
    )
    return df


# =============================================================================
# Feature Engineer
# =============================================================================


class FeatureEngineer:
    """
    Central feature engineering pipeline.

    All model-specific training scripts call into this class so that join
    logic, resampling, and feature naming are defined once.
    """

    def __init__(self, mongodb: MongoDBConnector):
        self.mongodb = mongodb

    # -----------------------------------------------------------------
    # 1. Crop Performance Profiles  (Clustering)
    # -----------------------------------------------------------------

    def build_crop_profiles(
        self,
        farm_id: Optional[str] = None,
        hours: int = 4320,  # 180 days default
    ) -> pd.DataFrame:
        """
        Build an empirical performance profile for each crop type.

        For every crop that has height measurements we:
        1. Compute growth rate (delta height / delta time) per measurement pair.
        2. Join with the tower + reservoir telemetry that was active during
           those measurement windows.
        3. Keep only "good growth" windows (growth rate above the per-crop
           median) so the profile represents conditions where the crop thrived.
        4. Aggregate per crop: mean and std of each reservoir & env metric.

        Returns a DataFrame indexed by ``crop_type`` with columns like
        ``mean_ph``, ``std_ph``, ``mean_ec``, ``std_ec``, etc.
        Returns empty DataFrame if insufficient data.
        """
        logger.info("Building crop performance profiles for clustering")

        # --- Fetch raw data ---
        heights_df = self.mongodb.get_height_measurements(
            farm_id=farm_id,
            hours=hours,
        )
        if heights_df.empty or len(heights_df) < 2:
            logger.warning("Not enough height measurements for crop profiles")
            return pd.DataFrame()

        tower_df = self.mongodb.get_tower_telemetry(
            farm_id=farm_id,
            hours=hours,
        )
        reservoir_df = self.mongodb.get_reservoir_telemetry(
            farm_id=farm_id,
            hours=hours,
        )

        # --- Compute per-measurement growth rates ---
        heights_df = heights_df.sort_values(["tower_id", "timestamp"])
        heights_df["prev_height"] = heights_df.groupby("tower_id")["height_cm"].shift(1)
        heights_df["prev_ts"] = heights_df.groupby("tower_id")["timestamp"].shift(1)
        heights_df = heights_df.dropna(subset=["prev_height", "prev_ts"])

        delta_days = (
            heights_df["timestamp"] - heights_df["prev_ts"]
        ).dt.total_seconds() / 86400
        delta_days = delta_days.replace(0, np.nan)
        heights_df["growth_rate_cm_day"] = (
            heights_df["height_cm"] - heights_df["prev_height"]
        ) / delta_days
        heights_df = heights_df.dropna(subset=["growth_rate_cm_day"])
        # Filter out negative growth (measurement noise)
        heights_df = heights_df[heights_df["growth_rate_cm_day"] >= 0]

        if heights_df.empty:
            logger.warning("No valid growth rate windows computed")
            return pd.DataFrame()

        # --- Join telemetry to each measurement window ---
        records: list[dict] = []
        for _, row in heights_df.iterrows():
            window_start = row["prev_ts"]
            window_end = row["timestamp"]
            tower_id = row["tower_id"]
            crop = row.get("crop_type", "Unknown")

            env = self._window_avg(
                tower_df,
                window_start,
                window_end,
                filter_col="tower_id",
                filter_val=tower_id,
                metrics=TOWER_ENV_METRICS,
            )
            res = self._window_avg(
                reservoir_df,
                window_start,
                window_end,
                metrics=RESERVOIR_METRICS,
            )
            records.append(
                {
                    "crop_type": crop,
                    "growth_rate_cm_day": row["growth_rate_cm_day"],
                    **env,
                    **res,
                }
            )

        profile_df = pd.DataFrame(records)

        # --- Keep only "good growth" windows per crop ---
        median_rates = profile_df.groupby("crop_type")["growth_rate_cm_day"].transform(
            "median"
        )
        good = profile_df[profile_df["growth_rate_cm_day"] >= median_rates]

        if good.empty:
            good = profile_df  # fallback: use all data

        # --- Aggregate per crop ---
        agg_metrics = TOWER_ENV_METRICS + RESERVOIR_METRICS
        agg_funcs = {}
        for m in agg_metrics:
            if m in good.columns:
                agg_funcs[m] = ["mean", "std"]
        agg_funcs["growth_rate_cm_day"] = ["mean", "count"]

        result = good.groupby("crop_type").agg(agg_funcs)
        # Flatten multi-level columns
        result.columns = ["_".join(col).strip("_") for col in result.columns]
        result = result.rename(columns={"growth_rate_cm_day_count": "n_samples"})

        logger.info(
            f"Built profiles for {len(result)} crops "
            f"({int(result['n_samples'].sum())} total good-growth windows)"
        )
        return result

    # -----------------------------------------------------------------
    # 2. Drift Forecasting Features  (Time-series regression)
    # -----------------------------------------------------------------

    def build_drift_features(
        self,
        coord_id: Optional[str] = None,
        farm_id: Optional[str] = None,
        hours: int = 168,  # 7 days
        resample_interval: str = "30min",
        lag_hours: Optional[list[int]] = None,
    ) -> pd.DataFrame:
        """
        Build a feature matrix for reservoir drift forecasting.

        Each row is a resampled reservoir reading augmented with:
        - Lag features for ph, ec, water_level at multiple horizons
        - Rate-of-change features (first difference)
        - Rolling statistics (6h std, 12h linear trend slope)
        - Contextual features: hour of day, active tower count
        - Dosing pump duty cycle (fraction of recent window where pump was on)

        Also computes forward-looking targets (ph/ec/water_level at t+1h,
        t+6h, t+24h) so the same DataFrame can be used for training.

        Returns empty DataFrame if insufficient data.
        """
        if lag_hours is None:
            lag_hours = DRIFT_LAG_HOURS

        logger.info(f"Building drift features (coord={coord_id}, hours={hours})")

        res_df = self.mongodb.get_reservoir_telemetry(
            coord_id=coord_id,
            farm_id=farm_id,
            hours=hours,
        )
        if res_df.empty:
            logger.warning("No reservoir telemetry for drift features")
            return pd.DataFrame()

        # --- Resample to regular intervals ---
        res_df["timestamp"] = pd.to_datetime(res_df["timestamp"])
        res_df = res_df.set_index("timestamp").sort_index()

        numeric_cols = [c for c in RESERVOIR_METRICS if c in res_df.columns]
        pump_cols = [
            c
            for c in ["main_pump_on", "dosing_pump_ph_on", "dosing_pump_nutrient_on"]
            if c in res_df.columns
        ]

        # Mean for numeric, max (any-True) for booleans
        agg_dict = {c: "mean" for c in numeric_cols}
        for pc in pump_cols:
            agg_dict[pc] = "max"

        df = res_df[numeric_cols + pump_cols].resample(resample_interval).agg(agg_dict)
        df = df.ffill(limit=6).dropna()

        if len(df) < 24:
            logger.warning(f"Only {len(df)} resampled rows - need at least 24")
            return pd.DataFrame()

        # --- Lag features ---
        resample_minutes = (
            pd.tseries.frequencies.to_offset(resample_interval).nanos / 60e9
        )
        for metric in numeric_cols:
            for h in lag_hours:
                periods = max(1, int(h * 60 / resample_minutes))
                df[f"{metric}_lag_{h}h"] = df[metric].shift(periods)

        # --- Rate of change (per-step first difference) ---
        for metric in numeric_cols:
            df[f"{metric}_roc"] = df[metric].diff()

        # --- Rolling statistics ---
        window_6h = max(1, int(6 * 60 / resample_minutes))
        window_12h = max(1, int(12 * 60 / resample_minutes))
        for metric in numeric_cols:
            df[f"{metric}_std_6h"] = df[metric].rolling(window_6h, min_periods=1).std()
            # 12h linear trend (slope via simple polyfit proxy)
            df[f"{metric}_trend_12h"] = (
                df[metric]
                .rolling(window_12h, min_periods=2)
                .apply(self._linear_slope, raw=True)
            )

        # --- Pump duty cycles (fraction of last 6h where pump was on) ---
        for pc in pump_cols:
            df[f"{pc}_duty_6h"] = (
                df[pc].astype(float).rolling(window_6h, min_periods=1).mean()
            )

        # --- Contextual: hour of day ---
        df["hour_of_day"] = df.index.hour + df.index.minute / 60.0

        # --- Forward targets (for supervised training) ---
        for metric in numeric_cols:
            for h in [1, 6, 24]:
                periods = max(1, int(h * 60 / resample_minutes))
                df[f"{metric}_target_{h}h"] = df[metric].shift(-periods)

        # Drop rows missing lag or target columns
        df = df.dropna()

        logger.info(
            f"Built drift feature matrix: {df.shape[0]} rows x {df.shape[1]} cols"
        )
        return df

    # -----------------------------------------------------------------
    # 3. Nutrient Consumption Features
    # -----------------------------------------------------------------

    def build_consumption_features(
        self,
        coord_id: Optional[str] = None,
        farm_id: Optional[str] = None,
        hours: int = 168,
        resample_interval: str = "1h",
    ) -> pd.DataFrame:
        """
        Build features for nutrient / water consumption rate prediction.

        The target variables are *natural depletion rates* of EC, pH, and
        water level - so we filter out time windows where dosing pumps were
        active (we only want organic consumption, not dosing artifacts).

        Each row represents a 1-hour window with:
        - Starting reservoir state (pH, EC, water_level, water_temp)
        - Environmental context (avg tower temp, humidity)
        - Crop load context (active tower count from telemetry count proxy)
        - Targets: Δph/h, Δec/h, Δwater_level/h

        Returns empty DataFrame if insufficient data.
        """
        logger.info(f"Building consumption features (coord={coord_id})")

        res_df = self.mongodb.get_reservoir_telemetry(
            coord_id=coord_id,
            farm_id=farm_id,
            hours=hours,
        )
        tower_df = self.mongodb.get_tower_telemetry(
            farm_id=farm_id,
            hours=hours,
        )

        if res_df.empty:
            logger.warning("No reservoir telemetry for consumption features")
            return pd.DataFrame()

        # --- Resample reservoir ---
        res_df["timestamp"] = pd.to_datetime(res_df["timestamp"])
        res_df = res_df.set_index("timestamp").sort_index()

        metric_cols = [c for c in RESERVOIR_METRICS if c in res_df.columns]
        pump_cols = [
            c
            for c in ["dosing_pump_ph_on", "dosing_pump_nutrient_on"]
            if c in res_df.columns
        ]

        agg_dict = {c: "mean" for c in metric_cols}
        for pc in pump_cols:
            agg_dict[pc] = "max"  # True if pump ran at all during window

        df = res_df[metric_cols + pump_cols].resample(resample_interval).agg(agg_dict)
        df = df.ffill(limit=3)

        # --- Filter out windows where dosing was active ---
        dosing_mask = pd.Series(False, index=df.index)
        for pc in pump_cols:
            if pc in df.columns:
                dosing_mask = dosing_mask | (df[pc].fillna(0).astype(bool))
        df["dosing_active"] = dosing_mask
        # Keep only non-dosing windows for natural depletion
        df_clean = df[~df["dosing_active"]].copy()

        if len(df_clean) < 10:
            logger.warning(
                "Not enough non-dosing windows; using all data with dosing flag"
            )
            df_clean = df.copy()

        # --- Compute depletion rates (targets) ---
        for metric in metric_cols:
            df_clean[f"{metric}_delta"] = df_clean[metric].diff()

        # --- Add tower env context ---
        if not tower_df.empty:
            tower_df["timestamp"] = pd.to_datetime(tower_df["timestamp"])
            tower_df = tower_df.set_index("timestamp").sort_index()
            env_agg = (
                tower_df[[c for c in TOWER_ENV_METRICS if c in tower_df.columns]]
                .resample(resample_interval)
                .mean()
            )
            # Count unique towers as proxy for crop load
            if "tower_id" in tower_df.columns:
                tower_count = tower_df["tower_id"].resample(resample_interval).nunique()
                env_agg["active_tower_count"] = tower_count
            df_clean = df_clean.join(env_agg, how="left")

        # --- Hours since last dosing event ---
        if not dosing_mask.empty:
            last_dosing = dosing_mask.where(dosing_mask).ffill()
            time_since_dosing = (
                df_clean.index.to_series()
                - last_dosing.reindex(df_clean.index).index.to_series()
            )
            # Simpler approach: cumulative count of non-dosing rows
            non_dose_group = (~df["dosing_active"]).reindex(df_clean.index).fillna(True)
            df_clean["hours_since_dosing"] = non_dose_group.cumsum()

        df_clean = df_clean.drop(columns=pump_cols + ["dosing_active"], errors="ignore")
        df_clean = df_clean.dropna()

        logger.info(
            f"Built consumption feature matrix: {df_clean.shape[0]} rows x {df_clean.shape[1]} cols"
        )
        return df_clean

    # -----------------------------------------------------------------
    # 4. Enhanced Growth Features (for improved growth model)
    # -----------------------------------------------------------------

    def build_growth_features(
        self,
        farm_id: Optional[str] = None,
        hours: int = 4320,
        rolling_window: str = "24h",
    ) -> pd.DataFrame:
        """
        Build an enhanced feature set for growth prediction.

        Improvements over the existing train_growth.py:
        - One-hot encoded crop_type
        - Growth stage derived from days_since_planting
        - Rolling 24h averages instead of single-day snapshots
        - Interaction features (temp x humidity)
        - pH / EC deviation from crop optimal
        - Cumulative light hours

        Returns a DataFrame with features + ``height_cm`` target.
        """
        logger.info("Building enhanced growth features")

        heights_df = self.mongodb.get_height_measurements(farm_id=farm_id, hours=hours)
        if heights_df.empty:
            logger.warning("No height measurements for growth features")
            return pd.DataFrame()

        tower_df = self.mongodb.get_tower_telemetry(farm_id=farm_id, hours=hours)
        reservoir_df = self.mongodb.get_reservoir_telemetry(
            farm_id=farm_id, hours=hours
        )

        # --- Build rolling env averages ---
        tower_rolling = pd.DataFrame()
        if not tower_df.empty:
            tower_df["timestamp"] = pd.to_datetime(tower_df["timestamp"])
            for col in TOWER_ENV_METRICS:
                if col in tower_df.columns:
                    ts = tower_df.set_index("timestamp")[col].sort_index()
                    ts_resampled = ts.resample("1h").mean().ffill()
                    tower_rolling[f"avg_{col}_24h"] = ts_resampled.rolling(
                        rolling_window, min_periods=1
                    ).mean()

        reservoir_rolling = pd.DataFrame()
        if not reservoir_df.empty:
            reservoir_df["timestamp"] = pd.to_datetime(reservoir_df["timestamp"])
            for col in RESERVOIR_METRICS:
                if col in reservoir_df.columns:
                    ts = reservoir_df.set_index("timestamp")[col].sort_index()
                    ts_resampled = ts.resample("1h").mean().ffill()
                    reservoir_rolling[f"avg_{col}_24h"] = ts_resampled.rolling(
                        rolling_window, min_periods=1
                    ).mean()

        # --- Attach rolling avgs to each height measurement ---
        rows: list[dict] = []
        for _, h in heights_df.iterrows():
            ts = pd.to_datetime(h["timestamp"])
            crop = h.get("crop_type", "Unknown")
            days = h.get("days_since_planting", 0) or 0
            stage = infer_growth_stage(int(days), crop)

            row: dict = {
                "height_cm": h.get("height_cm", h.get("heightCm", 0)),
                "days_since_planting": days,
                "crop_type": crop,
                "growth_stage": stage,
            }

            # Nearest rolling env values
            for rolling_df in [tower_rolling, reservoir_rolling]:
                if not rolling_df.empty:
                    idx = rolling_df.index.get_indexer([ts], method="nearest")[0]
                    if 0 <= idx < len(rolling_df):
                        for col in rolling_df.columns:
                            row[col] = rolling_df.iloc[idx][col]

            rows.append(row)

        df = pd.DataFrame(rows)
        if df.empty:
            return df

        # --- Interaction features ---
        if "avg_air_temp_c_24h" in df.columns and "avg_humidity_pct_24h" in df.columns:
            df["temp_x_humidity"] = (
                df["avg_air_temp_c_24h"] * df["avg_humidity_pct_24h"]
            )

        # --- pH / EC deviation from crop-type optimal ---
        from ..api.inference import CROP_CONFIG, DEFAULT_CROP_CONFIG

        if "avg_ph_24h" in df.columns:
            df["ph_deviation"] = df.apply(
                lambda r: r.get("avg_ph_24h", 0)
                - CROP_CONFIG.get(r.get("crop_type", ""), DEFAULT_CROP_CONFIG).get(
                    "ph_optimal", 6.2
                ),
                axis=1,
            )
        if "avg_ec_ms_cm_24h" in df.columns:
            df["ec_deviation"] = df.apply(
                lambda r: r.get("avg_ec_ms_cm_24h", 0)
                - CROP_CONFIG.get(r.get("crop_type", ""), DEFAULT_CROP_CONFIG).get(
                    "ec_optimal_ms_cm", 1.5
                ),
                axis=1,
            )

        logger.info(
            f"Built growth feature matrix: {df.shape[0]} rows x {df.shape[1]} cols"
        )
        return df

    # -----------------------------------------------------------------
    # Helpers
    # -----------------------------------------------------------------

    @staticmethod
    def _window_avg(
        df: pd.DataFrame,
        start: datetime,
        end: datetime,
        metrics: list[str],
        filter_col: Optional[str] = None,
        filter_val: Optional[str] = None,
    ) -> dict[str, float]:
        """Return mean of *metrics* in *df* between *start* and *end*."""
        if df.empty:
            return {}
        mask = (df["timestamp"] >= start) & (df["timestamp"] <= end)
        if filter_col and filter_val:
            mask = mask & (df[filter_col] == filter_val)
        subset = df.loc[mask]
        result = {}
        for m in metrics:
            if m in subset.columns and not subset[m].isna().all():
                result[m] = float(subset[m].mean())
        return result

    @staticmethod
    def _linear_slope(values: np.ndarray) -> float:
        """Compute slope of a simple linear fit over *values*."""
        n = len(values)
        if n < 2:
            return 0.0
        x = np.arange(n)
        valid = ~np.isnan(values)
        if valid.sum() < 2:
            return 0.0
        coeffs = np.polyfit(x[valid], values[valid], 1)
        return float(coeffs[0])
