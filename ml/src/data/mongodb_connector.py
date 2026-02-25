"""
MongoDB connector for fetching historical telemetry data.
Provides pandas DataFrames for ML training and analysis.
"""

from datetime import datetime, timedelta
from typing import Optional
import pandas as pd
from pymongo import MongoClient
from pymongo.database import Database
from loguru import logger

from ..config import config


class MongoDBConnector:
    """
    Connects to MongoDB and fetches telemetry data as pandas DataFrames.

    Usage:
        connector = MongoDBConnector()
        df = connector.get_tower_telemetry(
            farm_id="farm001",
            hours=168  # Last 7 days
        )
    """

    def __init__(self, connection_string: Optional[str] = None):
        """
        Initialize MongoDB connection.

        Args:
            connection_string: MongoDB URI. Uses config if not provided.
        """
        self.connection_string = connection_string or config.mongodb.connection_string
        self._client: Optional[MongoClient] = None
        self._db: Optional[Database] = None

    def connect(self) -> None:
        """Establish connection to MongoDB."""
        if self._client is None:
            logger.info(
                f"Connecting to MongoDB at {config.mongodb.host}:{config.mongodb.port}"
            )
            self._client = MongoClient(self.connection_string)
            self._db = self._client[config.mongodb.database]
            logger.success("Connected to MongoDB")

    def close(self) -> None:
        """Close MongoDB connection."""
        if self._client:
            self._client.close()
            self._client = None
            self._db = None
            logger.info("MongoDB connection closed")

    @property
    def db(self) -> Database:
        """Get database instance, connecting if necessary."""
        if self._db is None:
            self.connect()
        return self._db

    def __enter__(self):
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()

    # -------------------------------------------------------------------------
    # Tower Telemetry
    # -------------------------------------------------------------------------

    def get_tower_telemetry(
        self,
        farm_id: Optional[str] = None,
        coord_id: Optional[str] = None,
        tower_id: Optional[str] = None,
        hours: int = 24,
        start_time: Optional[datetime] = None,
        end_time: Optional[datetime] = None,
    ) -> pd.DataFrame:
        """
        Fetch tower telemetry data as a DataFrame.

        Args:
            farm_id: Filter by farm ID
            coord_id: Filter by coordinator ID
            tower_id: Filter by specific tower ID
            hours: Hours of data to fetch (default 24)
            start_time: Explicit start time (overrides hours)
            end_time: Explicit end time (default: now)

        Returns:
            DataFrame with columns: timestamp, tower_id, coord_id, farm_id,
            air_temp_c, humidity_pct, light_lux, pump_on, light_on,
            light_brightness, vbat_mv, signal_quality, status_mode
        """
        query = self._build_time_query(hours, start_time, end_time)

        if farm_id:
            query["farm_id"] = farm_id
        if coord_id:
            query["coord_id"] = coord_id
        if tower_id:
            query["tower_id"] = tower_id

        logger.debug(f"Fetching tower telemetry with query: {query}")

        cursor = self.db.tower_telemetry.find(query).sort("timestamp", 1)
        df = pd.DataFrame(list(cursor))

        if df.empty:
            logger.warning("No tower telemetry data found")
            return pd.DataFrame()

        # Clean up MongoDB _id field
        if "_id" in df.columns:
            df = df.drop("_id", axis=1)

        # Ensure timestamp is datetime
        if "timestamp" in df.columns:
            df["timestamp"] = pd.to_datetime(df["timestamp"])

        logger.info(f"Fetched {len(df)} tower telemetry records")
        return df

    # -------------------------------------------------------------------------
    # Reservoir Telemetry
    # -------------------------------------------------------------------------

    def get_reservoir_telemetry(
        self,
        farm_id: Optional[str] = None,
        coord_id: Optional[str] = None,
        hours: int = 24,
        start_time: Optional[datetime] = None,
        end_time: Optional[datetime] = None,
    ) -> pd.DataFrame:
        """
        Fetch reservoir/water quality telemetry as a DataFrame.

        Args:
            farm_id: Filter by farm ID
            coord_id: Filter by coordinator ID
            hours: Hours of data to fetch (default 24)
            start_time: Explicit start time
            end_time: Explicit end time

        Returns:
            DataFrame with columns: timestamp, coord_id, farm_id, ph, ec_ms_cm,
            tds_ppm, water_temp_c, water_level_pct, main_pump_on, etc.
        """
        query = self._build_time_query(hours, start_time, end_time)

        if farm_id:
            query["farm_id"] = farm_id
        if coord_id:
            query["coord_id"] = coord_id

        logger.debug(f"Fetching reservoir telemetry with query: {query}")

        cursor = self.db.reservoir_telemetry.find(query).sort("timestamp", 1)
        df = pd.DataFrame(list(cursor))

        if df.empty:
            logger.warning("No reservoir telemetry data found")
            return pd.DataFrame()

        if "_id" in df.columns:
            df = df.drop("_id", axis=1)

        if "timestamp" in df.columns:
            df["timestamp"] = pd.to_datetime(df["timestamp"])

        logger.info(f"Fetched {len(df)} reservoir telemetry records")
        return df

    # -------------------------------------------------------------------------
    # Height Measurements (Plant Growth)
    # -------------------------------------------------------------------------

    def get_height_measurements(
        self,
        farm_id: Optional[str] = None,
        tower_id: Optional[str] = None,
        crop_type: Optional[str] = None,
        hours: Optional[int] = None,
        start_time: Optional[datetime] = None,
        end_time: Optional[datetime] = None,
    ) -> pd.DataFrame:
        """
        Fetch plant height measurements for growth tracking.

        Args:
            farm_id: Filter by farm ID
            tower_id: Filter by tower ID
            crop_type: Filter by crop type (e.g., "Lettuce", "Basil")
            hours: Hours of data (None = all data)
            start_time: Explicit start time
            end_time: Explicit end time

        Returns:
            DataFrame with columns: timestamp, tower_id, slot_index,
            height_cm, crop_type, method, planted_date, days_since_planting
        """
        query = {}

        if hours or start_time or end_time:
            query = self._build_time_query(
                hours or 8760, start_time, end_time
            )  # Default 1 year

        if farm_id:
            query["farm_id"] = farm_id
        if tower_id:
            query["tower_id"] = tower_id
        if crop_type:
            query["crop_type"] = crop_type

        logger.debug(f"Fetching height measurements with query: {query}")

        cursor = self.db.height_measurements.find(query).sort("timestamp", 1)
        df = pd.DataFrame(list(cursor))

        if df.empty:
            logger.warning("No height measurement data found")
            return pd.DataFrame()

        if "_id" in df.columns:
            df = df.drop("_id", axis=1)

        # Calculate days since planting
        if "timestamp" in df.columns and "planted_date" in df.columns:
            df["timestamp"] = pd.to_datetime(df["timestamp"])
            df["planted_date"] = pd.to_datetime(df["planted_date"])
            df["days_since_planting"] = (df["timestamp"] - df["planted_date"]).dt.days

        logger.info(f"Fetched {len(df)} height measurements")
        return df

    # -------------------------------------------------------------------------
    # Combined Dataset for ML
    # -------------------------------------------------------------------------

    def get_ml_dataset(
        self,
        farm_id: str,
        hours: int = 168,  # 7 days default
        resample_interval: str = "1h",
    ) -> pd.DataFrame:
        """
        Get a combined, resampled dataset ready for ML training.

        Joins tower telemetry with reservoir telemetry and resamples
        to a consistent time interval.

        Args:
            farm_id: Farm ID to fetch data for
            hours: Hours of historical data
            resample_interval: Pandas resample interval (e.g., "1h", "30min")

        Returns:
            DataFrame with combined features, indexed by timestamp
        """
        # Fetch raw data
        tower_df = self.get_tower_telemetry(farm_id=farm_id, hours=hours)
        reservoir_df = self.get_reservoir_telemetry(farm_id=farm_id, hours=hours)

        if tower_df.empty and reservoir_df.empty:
            logger.error("No data available for ML dataset")
            return pd.DataFrame()

        # Process tower telemetry - aggregate per timestamp
        if not tower_df.empty:
            tower_df = tower_df.set_index("timestamp")

            # Aggregate numeric columns by mean, boolean by any
            numeric_cols = tower_df.select_dtypes(include=["float64", "int64"]).columns
            tower_agg = tower_df[numeric_cols].resample(resample_interval).mean()
            tower_agg.columns = [f"tower_{col}" for col in tower_agg.columns]
        else:
            tower_agg = pd.DataFrame()

        # Process reservoir telemetry
        if not reservoir_df.empty:
            reservoir_df = reservoir_df.set_index("timestamp")
            numeric_cols = reservoir_df.select_dtypes(
                include=["float64", "int64"]
            ).columns
            reservoir_agg = (
                reservoir_df[numeric_cols].resample(resample_interval).mean()
            )
            reservoir_agg.columns = [
                f"reservoir_{col}" for col in reservoir_agg.columns
            ]
        else:
            reservoir_agg = pd.DataFrame()

        # Join datasets
        if not tower_agg.empty and not reservoir_agg.empty:
            combined = tower_agg.join(reservoir_agg, how="outer")
        elif not tower_agg.empty:
            combined = tower_agg
        else:
            combined = reservoir_agg

        # Forward fill missing values (within reason)
        combined = combined.ffill(limit=3)

        logger.info(
            f"Created ML dataset with {len(combined)} samples and {len(combined.columns)} features"
        )
        return combined

    # -------------------------------------------------------------------------
    # Helpers
    # -------------------------------------------------------------------------

    def _build_time_query(
        self,
        hours: int,
        start_time: Optional[datetime],
        end_time: Optional[datetime],
    ) -> dict:
        """Build MongoDB time range query."""
        if end_time is None:
            end_time = datetime.utcnow()

        if start_time is None:
            start_time = end_time - timedelta(hours=hours)

        return {
            "timestamp": {
                "$gte": start_time,
                "$lte": end_time,
            }
        }

    # -------------------------------------------------------------------------
    # Tower Twins (crop context for ML)
    # -------------------------------------------------------------------------

    def get_active_crop_load(
        self,
        farm_id: Optional[str] = None,
        coord_id: Optional[str] = None,
    ) -> pd.DataFrame:
        """
        Fetch currently active crops across towers.

        Returns a DataFrame with one row per tower that has a crop planted:
        tower_id, coord_id, crop_type, planting_date, last_height_cm.
        """
        query: dict = {"crop_type": {"$nin": [None, "Unknown"]}}
        if farm_id:
            query["farm_id"] = farm_id
        if coord_id:
            query["coord_id"] = coord_id

        projection = {
            "_id": 0,
            "tower_id": 1,
            "coord_id": 1,
            "farm_id": 1,
            "crop_type": 1,
            "planting_date": 1,
            "last_height_cm": 1,
        }

        cursor = self.db.tower_twins.find(query, projection)
        df = pd.DataFrame(list(cursor))

        if df.empty:
            logger.debug("No active crops found in tower twins")
            return pd.DataFrame()

        if "planting_date" in df.columns:
            df["planting_date"] = pd.to_datetime(df["planting_date"])

        logger.info(f"Fetched {len(df)} active crop records from tower twins")
        return df

    # -------------------------------------------------------------------------
    # Crop Compatibility Results
    # -------------------------------------------------------------------------

    def save_crop_compatibility(self, data: dict) -> None:
        """
        Save (upsert) a crop compatibility clustering result.

        The ``data`` dict should contain at minimum:
        version, trained_at, clusters, compatibility_matrix, crop_profiles.
        """
        self.db.crop_compatibility.replace_one(
            {"version": data.get("version", "latest")},
            data,
            upsert=True,
        )
        logger.info("Saved crop compatibility results to MongoDB")

    def get_crop_compatibility(self, version: str = "latest") -> Optional[dict]:
        """Load the latest crop compatibility clustering result."""
        doc = self.db.crop_compatibility.find_one(
            {"version": version},
            {"_id": 0},
        )
        return doc

    # -------------------------------------------------------------------------
    # Reservoir Drift Forecasts
    # -------------------------------------------------------------------------

    def save_reservoir_predictions(
        self,
        coord_id: str,
        predictions: dict,
    ) -> None:
        """
        Write reservoir ML predictions into the coordinator twin document.
        """
        self.db.coordinator_twins.update_one(
            {"coord_id": coord_id},
            {
                "$set": {
                    "reservoir_ml_predictions": predictions,
                    "reservoir_ml_predictions_updated_at": datetime.utcnow(),
                }
            },
        )
        logger.info(f"Saved reservoir predictions for coord {coord_id}")

    # -------------------------------------------------------------------------
    # Collection Statistics
    # -------------------------------------------------------------------------

    def get_collection_stats(self) -> dict:
        """Get statistics about available data collections."""
        stats = {}

        collections = [
            "tower_telemetry",
            "reservoir_telemetry",
            "height_measurements",
            "crop_compatibility",
        ]
        for collection_name in collections:
            collection = self.db[collection_name]
            count = collection.count_documents({})

            if count > 0:
                oldest = collection.find_one(sort=[("timestamp", 1)])
                newest = collection.find_one(sort=[("timestamp", -1)])
                stats[collection_name] = {
                    "count": count,
                    "oldest": oldest.get("timestamp") if oldest else None,
                    "newest": newest.get("timestamp") if newest else None,
                }
            else:
                stats[collection_name] = {"count": 0}

        return stats

    def get_data_readiness(self) -> dict:
        """
        Assess whether enough data exists to train each model.

        Returns a dict keyed by model name with: ready (bool), reason,
        sample counts.
        """
        stats = self.get_collection_stats()
        tower_count = stats.get("tower_telemetry", {}).get("count", 0)
        reservoir_count = stats.get("reservoir_telemetry", {}).get("count", 0)
        height_count = stats.get("height_measurements", {}).get("count", 0)

        # Count distinct crop types in height measurements
        crop_types = 0
        if height_count > 0:
            crop_types = len(self.db.height_measurements.distinct("crop_type"))

        return {
            "clustering": {
                "ready": height_count >= 50 and crop_types >= 3,
                "reason": (
                    f"Need height data for 3+ crops; have {crop_types} crops, "
                    f"{height_count} measurements"
                ),
                "height_measurements": height_count,
                "distinct_crops": crop_types,
            },
            "drift_forecasting": {
                "ready": reservoir_count >= 2000,
                "reason": (
                    f"Need ~2 weeks of reservoir telemetry; have "
                    f"{reservoir_count} records"
                ),
                "reservoir_telemetry": reservoir_count,
            },
            "growth_prediction": {
                "ready": height_count >= 50 and tower_count >= 100,
                "reason": (
                    f"Need 50+ height measurements + tower telemetry; have "
                    f"{height_count} heights, {tower_count} tower records"
                ),
                "height_measurements": height_count,
                "tower_telemetry": tower_count,
            },
            "consumption_prediction": {
                "ready": reservoir_count >= 500,
                "reason": (
                    f"Need ~1 week of reservoir telemetry; have "
                    f"{reservoir_count} records"
                ),
                "reservoir_telemetry": reservoir_count,
            },
        }
