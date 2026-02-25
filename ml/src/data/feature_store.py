"""
Feature store for caching precomputed feature matrices.

Avoids re-running expensive MongoDB aggregations and joins on every
training run or inference call.  Features are cached as Parquet files
on disk with a TTL-based invalidation strategy.

Usage:
    from src.data.feature_store import feature_store

    # Check if cached features are still fresh
    df = feature_store.get("drift_features", coord_id="coord01", max_age_hours=6)
    if df is None:
        df = build_drift_features(...)   # expensive
        feature_store.put("drift_features", df, coord_id="coord01")
"""

import hashlib
import json
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional

import pandas as pd
from loguru import logger

from ..config import config


class FeatureStore:
    """
    Disk-backed feature cache using Parquet files.

    Each cached dataset is keyed by ``(name, **scope_kwargs)`` which is
    hashed into a filename.  A sidecar ``.meta.json`` file stores the
    creation timestamp so callers can decide whether the cache is stale.
    """

    def __init__(self, cache_dir: Optional[Path] = None):
        self.cache_dir = Path(cache_dir or config.ml.data_dir) / "feature_cache"
        self.cache_dir.mkdir(parents=True, exist_ok=True)

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def get(
        self,
        name: str,
        max_age_hours: float = 6.0,
        **scope_kwargs,
    ) -> Optional[pd.DataFrame]:
        """
        Retrieve a cached feature DataFrame if it exists and is fresh.

        Parameters
        ----------
        name : str
            Logical name (e.g. "drift_features").
        max_age_hours : float
            Maximum age in hours before the cache is considered stale.
        **scope_kwargs
            Scope parameters (e.g. coord_id, farm_id) that were used
            when the features were built.  Must match exactly.

        Returns
        -------
        DataFrame or None if cache miss / stale.
        """
        key = self._cache_key(name, scope_kwargs)
        parquet_path = self.cache_dir / f"{key}.parquet"
        meta_path = self.cache_dir / f"{key}.meta.json"

        if not parquet_path.exists() or not meta_path.exists():
            return None

        # Check freshness
        try:
            meta = json.loads(meta_path.read_text())
            created = datetime.fromisoformat(meta["created_at"])
            if datetime.utcnow() - created > timedelta(hours=max_age_hours):
                logger.debug(f"Feature cache '{name}' expired (age > {max_age_hours}h)")
                return None
        except Exception:
            return None

        try:
            df = pd.read_parquet(parquet_path)
            logger.debug(f"Feature cache hit: '{name}' ({len(df)} rows)")
            return df
        except Exception as e:
            logger.warning(f"Failed to read cached features: {e}")
            return None

    def put(
        self,
        name: str,
        df: pd.DataFrame,
        **scope_kwargs,
    ) -> Path:
        """
        Cache a feature DataFrame to disk.

        Returns the path to the Parquet file.
        """
        key = self._cache_key(name, scope_kwargs)
        parquet_path = self.cache_dir / f"{key}.parquet"
        meta_path = self.cache_dir / f"{key}.meta.json"

        df.to_parquet(parquet_path, index=True)
        meta = {
            "name": name,
            "scope": scope_kwargs,
            "created_at": datetime.utcnow().isoformat(),
            "rows": len(df),
            "columns": list(df.columns),
        }
        meta_path.write_text(json.dumps(meta, indent=2))

        logger.debug(f"Cached features '{name}': {len(df)} rows -> {parquet_path}")
        return parquet_path

    def invalidate(self, name: str, **scope_kwargs) -> bool:
        """Remove a cached feature set."""
        key = self._cache_key(name, scope_kwargs)
        parquet_path = self.cache_dir / f"{key}.parquet"
        meta_path = self.cache_dir / f"{key}.meta.json"

        removed = False
        for p in [parquet_path, meta_path]:
            if p.exists():
                p.unlink()
                removed = True
        return removed

    def clear_all(self) -> int:
        """Remove all cached features. Returns number of files deleted."""
        count = 0
        for p in self.cache_dir.iterdir():
            if p.suffix in (".parquet", ".json"):
                p.unlink()
                count += 1
        logger.info(f"Cleared {count} cached feature files")
        return count

    def list_cached(self) -> list[dict]:
        """List all cached feature sets with metadata."""
        entries = []
        for meta_path in self.cache_dir.glob("*.meta.json"):
            try:
                meta = json.loads(meta_path.read_text())
                entries.append(meta)
            except Exception:
                continue
        return entries

    # ------------------------------------------------------------------
    # Internal
    # ------------------------------------------------------------------

    @staticmethod
    def _cache_key(name: str, scope: dict) -> str:
        """Deterministic cache key from name + scope parameters."""
        scope_str = json.dumps(scope, sort_keys=True, default=str)
        raw = f"{name}:{scope_str}"
        return hashlib.sha256(raw.encode()).hexdigest()[:16]


# Singleton
feature_store = FeatureStore()
