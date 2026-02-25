"""
Crop compatibility clustering inference.

Loads the trained clustering model and provides:
- Cluster assignments for each crop
- Pairwise compatibility scores
- Reservoir sharing recommendations for a given set of crops
- Recommended setpoints per cluster group
"""

import pickle
from datetime import datetime
from pathlib import Path
from typing import Optional

import numpy as np
from loguru import logger

from ..config import config
from ..data.mongodb_connector import MongoDBConnector


class CropCompatibilityService:
    """
    Serves crop compatibility clustering results.

    Tries to load from MongoDB first (``crop_compatibility`` collection),
    then falls back to the pickled model artifact on disk.
    """

    def __init__(self, model_dir: Optional[Path] = None):
        self.model_dir = model_dir or config.ml.model_dir
        self._data: Optional[dict] = None
        self._load()

    def _load(self) -> None:
        """Load clustering results from MongoDB or disk."""
        # Try MongoDB first
        try:
            connector = MongoDBConnector()
            connector.connect()
            doc = connector.get_crop_compatibility(version="latest")
            connector.close()
            if doc:
                self._data = doc
                logger.info("Loaded crop compatibility from MongoDB")
                return
        except Exception as e:
            logger.debug(f"MongoDB load failed (will try disk): {e}")

        # Try pickle on disk
        pkl_path = Path(self.model_dir) / "crop_compatibility.pkl"
        if pkl_path.exists():
            try:
                with open(pkl_path, "rb") as f:
                    artifact = pickle.load(f)
                # Convert artifact to a serveable dict
                self._data = {
                    "crops": artifact.get("crop_names", []),
                    "labels": [int(l) for l in artifact.get("labels", [])],
                    "compatibility_matrix": artifact.get(
                        "compatibility", np.array([])
                    ).tolist()
                    if isinstance(artifact.get("compatibility"), np.ndarray)
                    else artifact.get("compatibility", []),
                    "cluster_setpoints": {
                        str(k): v
                        for k, v in artifact.get("cluster_setpoints", {}).items()
                    },
                }
                logger.info("Loaded crop compatibility from disk")
                return
            except Exception as e:
                logger.warning(f"Failed to load pickle: {e}")

        logger.warning("No crop compatibility model available")

    def reload(self) -> None:
        """Force reload from data sources."""
        self._data = None
        self._load()

    @property
    def is_available(self) -> bool:
        return self._data is not None

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def get_full_results(self) -> dict:
        """Return the complete clustering result (clusters, matrix, profiles)."""
        if not self.is_available:
            return {"error": "no_model", "message": "Clustering model not trained yet"}
        return self._data

    def get_compatibility_matrix(self) -> dict:
        """
        Return the pairwise compatibility matrix.

        Response shape:
        {
            "crops": ["Lettuce", "Basil", ...],
            "matrix": [[1.0, 0.85, ...], ...]
        }
        """
        if not self.is_available:
            return {"error": "no_model"}
        return {
            "crops": self._data.get("crops", []),
            "matrix": self._data.get("compatibility_matrix", []),
        }

    def get_clusters(self) -> dict:
        """
        Return cluster assignments.

        Response shape:
        {
            "clusters": {"1": ["Lettuce", "Basil"], "2": ["Tomato", "Pepper"]},
            "setpoints": {"1": {"ph_target": 6.0, ...}, ...}
        }
        """
        if not self.is_available:
            return {"error": "no_model"}

        crops = self._data.get("crops", [])
        labels = self._data.get("labels", [])
        groups: dict[str, list[str]] = {}
        for crop, label in zip(crops, labels):
            groups.setdefault(str(label), []).append(crop)

        return {
            "clusters": self._data.get("clusters", groups),
            "setpoints": self._data.get("cluster_setpoints", {}),
        }

    def recommend_grouping(self, crop_list: list[str]) -> dict:
        """
        Given a list of crop names the user wants to grow, recommend
        how to split them across reservoirs.

        Strategy:
        - If a trained model exists, use cluster labels to group.
        - Within each cluster, verify pairwise compatibility > threshold.
        - Return groups with recommended setpoints.
        """
        if not self.is_available:
            return {
                "error": "no_model",
                "message": "Train the clustering model first with real sensor data.",
            }

        crops = self._data.get("crops", [])
        labels = self._data.get("labels", [])
        matrix = self._data.get("compatibility_matrix", [])
        setpoints = self._data.get("cluster_setpoints", {})

        crop_to_label = dict(zip(crops, labels))
        crop_to_idx = {c: i for i, c in enumerate(crops)}

        # Separate known vs unknown crops
        known = [c for c in crop_list if c in crop_to_label]
        unknown = [c for c in crop_list if c not in crop_to_label]

        # Group known crops by their cluster label
        groups: dict[int, list[str]] = {}
        for crop in known:
            label = crop_to_label[crop]
            groups.setdefault(label, []).append(crop)

        recommendations: list[dict] = []
        for label, members in groups.items():
            # Compute average pairwise compatibility within group
            if len(members) > 1 and matrix:
                scores = []
                for i, a in enumerate(members):
                    for b in members[i + 1 :]:
                        ai, bi = crop_to_idx.get(a), crop_to_idx.get(b)
                        if ai is not None and bi is not None:
                            scores.append(matrix[ai][bi])
                avg_compat = float(np.mean(scores)) if scores else 1.0
            else:
                avg_compat = 1.0

            recommendations.append(
                {
                    "reservoir_group": int(label),
                    "crops": members,
                    "average_compatibility": round(avg_compat, 3),
                    "recommended_setpoints": setpoints.get(str(label), {}),
                }
            )

        if unknown:
            recommendations.append(
                {
                    "reservoir_group": -1,
                    "crops": unknown,
                    "average_compatibility": 0.0,
                    "recommended_setpoints": {},
                    "note": "These crops were not in the training data; no recommendation available.",
                }
            )

        return {
            "input_crops": crop_list,
            "n_reservoirs_needed": len(recommendations),
            "recommendations": recommendations,
        }

    def get_pairwise_score(self, crop_a: str, crop_b: str) -> dict:
        """Return the compatibility score between two specific crops."""
        if not self.is_available:
            return {"error": "no_model"}

        crops = self._data.get("crops", [])
        matrix = self._data.get("compatibility_matrix", [])
        crop_to_idx = {c: i for i, c in enumerate(crops)}

        ai = crop_to_idx.get(crop_a)
        bi = crop_to_idx.get(crop_b)

        if ai is None or bi is None:
            missing = [c for c in [crop_a, crop_b] if c not in crop_to_idx]
            return {"error": "unknown_crop", "missing": missing}

        return {
            "crop_a": crop_a,
            "crop_b": crop_b,
            "compatibility_score": round(float(matrix[ai][bi]), 4),
            "same_cluster": bool(
                self._data.get("labels", [])[ai] == self._data.get("labels", [])[bi]
            ),
        }


# Singleton
crop_compatibility_service = CropCompatibilityService()
