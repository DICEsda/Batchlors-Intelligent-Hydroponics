"""
Crop compatibility clustering training script.

Groups crops that thrive under similar reservoir conditions so they can
share a nutrient solution (same reservoir).

Algorithm
---------
1. Build per-crop empirical condition profiles from real sensor data
   (mean/std of pH, EC, temp, humidity during good-growth periods).
2. Normalize the profile vectors with StandardScaler.
3. Run Agglomerative (hierarchical) clustering to get a dendrogram.
4. Cut the dendrogram at a threshold that maximises the silhouette score,
   or fall back to a user-specified ``n_clusters``.
5. Compute a pairwise compatibility matrix (1 = identical profile,
   0 = completely incompatible) based on Euclidean distance.
6. Persist: cluster labels, compatibility matrix, dendrogram linkage,
   and crop profiles to disk + MongoDB.

CLI:
    python -m src.training.train_clustering --hours 4320 --farm-id farm001

Imported:
    from src.training.train_clustering import train_clustering_model
    result = train_clustering_model(hours=4320)
"""

import argparse
from datetime import datetime
from typing import Optional

import numpy as np
import pandas as pd
from loguru import logger
from scipy.cluster.hierarchy import linkage, fcluster
from scipy.spatial.distance import pdist, squareform
from sklearn.cluster import AgglomerativeClustering
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import silhouette_score

from ..config import config
from ..data.mongodb_connector import MongoDBConnector
from .model_manager import model_manager
from .feature_engineering import FeatureEngineer


# Profile features used for clustering (must match FeatureEngineer output)
PROFILE_MEAN_COLS = [
    "ph_mean",
    "ec_ms_cm_mean",
    "water_temp_c_mean",
    "air_temp_c_mean",
    "humidity_pct_mean",
    "light_lux_mean",
]
PROFILE_STD_COLS = [
    "ph_std",
    "ec_ms_cm_std",
    "water_temp_c_std",
    "air_temp_c_std",
    "humidity_pct_std",
    "light_lux_std",
]


def train_clustering_model(
    hours: int = 4320,
    farm_id: Optional[str] = None,
    n_clusters: Optional[int] = None,
    linkage_method: str = "ward",
) -> dict:
    """
    Train the crop compatibility clustering model.

    Parameters
    ----------
    hours : int
        Hours of historical data to consider.
    farm_id : str, optional
        Restrict to a single farm.
    n_clusters : int, optional
        Force this many clusters. If None the optimal k is chosen via
        silhouette score (2..n_crops-1).
    linkage_method : str
        Linkage criterion for hierarchical clustering.

    Returns
    -------
    dict with keys: clusters, compatibility_matrix, crop_profiles,
         dendrogram_linkage, metrics, model_metadata.
    """
    logger.info(f"Starting crop compatibility clustering (hours={hours})")

    # ---- 1. Build crop profiles ----
    with MongoDBConnector() as mongodb:
        fe = FeatureEngineer(mongodb)
        profiles = fe.build_crop_profiles(farm_id=farm_id, hours=hours)

        if profiles.empty or len(profiles) < 3:
            logger.error(
                f"Need profiles for at least 3 crops, got {len(profiles)}. "
                "Cannot train clustering model."
            )
            return {"error": "insufficient_data", "crops_found": len(profiles)}

        # ---- 2. Prepare feature matrix ----
        available = [
            c for c in PROFILE_MEAN_COLS + PROFILE_STD_COLS if c in profiles.columns
        ]
        if len(available) < 2:
            logger.error(f"Only {len(available)} features available; need >= 2")
            return {"error": "insufficient_features", "available": available}

        X_raw = profiles[available].fillna(0).values
        crop_names = profiles.index.tolist()
        n_crops = len(crop_names)

        scaler = StandardScaler()
        X = scaler.fit_transform(X_raw)

        # ---- 3. Hierarchical clustering + dendrogram ----
        Z = linkage(X, method=linkage_method)

        # ---- 4. Choose optimal k ----
        if n_clusters is None:
            best_k, best_score = 2, -1.0
            for k in range(2, min(n_crops, 8)):
                labels = fcluster(Z, t=k, criterion="maxclust")
                if len(set(labels)) < 2:
                    continue
                score = silhouette_score(X, labels)
                if score > best_score:
                    best_k, best_score = k, score
            n_clusters = best_k
            logger.info(f"Optimal k={n_clusters} (silhouette={best_score:.3f})")
        else:
            best_score = -1.0

        labels = fcluster(Z, t=n_clusters, criterion="maxclust")

        if len(set(labels)) >= 2:
            sil = silhouette_score(X, labels)
        else:
            sil = 0.0

        # ---- 5. Pairwise compatibility matrix ----
        dists = squareform(pdist(X, metric="euclidean"))
        max_dist = dists.max() if dists.max() > 0 else 1.0
        compatibility = 1.0 - (dists / max_dist)
        np.fill_diagonal(compatibility, 1.0)

        # ---- 6. Build result structures ----
        cluster_groups: dict[int, list[str]] = {}
        for crop, label in zip(crop_names, labels):
            cluster_groups.setdefault(int(label), []).append(crop)

        # Per-cluster recommended setpoints (mean of member profiles)
        cluster_setpoints: dict[int, dict] = {}
        for cid, members in cluster_groups.items():
            member_profiles = profiles.loc[members]
            setpoint: dict = {}
            for col in PROFILE_MEAN_COLS:
                if col in member_profiles.columns:
                    metric_name = col.replace("_mean", "")
                    setpoint[f"{metric_name}_target"] = round(
                        float(member_profiles[col].mean()), 2
                    )
                    # Tolerance = max spread across members
                    if f"{metric_name}_std" in member_profiles.columns:
                        setpoint[f"{metric_name}_tolerance"] = round(
                            float(member_profiles[f"{metric_name}_std"].max()), 2
                        )
            cluster_setpoints[cid] = setpoint

        result = {
            "version": datetime.utcnow().strftime("%Y%m%d.%H%M"),
            "trained_at": datetime.utcnow().isoformat(),
            "n_crops": n_crops,
            "n_clusters": n_clusters,
            "crops": crop_names,
            "labels": [int(l) for l in labels],
            "clusters": {str(k): v for k, v in cluster_groups.items()},
            "cluster_setpoints": {str(k): v for k, v in cluster_setpoints.items()},
            "compatibility_matrix": compatibility.tolist(),
            "dendrogram_linkage": Z.tolist(),
            "crop_profiles": {
                crop: {
                    col: round(float(profiles.loc[crop, col]), 4)
                    for col in available
                    if crop in profiles.index and col in profiles.columns
                }
                for crop in crop_names
            },
            "features_used": available,
            "scaler_mean": scaler.mean_.tolist(),
            "scaler_scale": scaler.scale_.tolist(),
        }

        metrics = {
            "silhouette_score": round(sil, 4),
            "n_crops": n_crops,
            "n_clusters": n_clusters,
            "linkage_method": linkage_method,
        }
        result["metrics"] = metrics

        # ---- 7. Persist ----
        # Save as a pickle model (scaler + labels + matrix)
        model_artifact = {
            "scaler": scaler,
            "linkage": Z,
            "labels": labels,
            "crop_names": crop_names,
            "compatibility": compatibility,
            "features": available,
            "cluster_setpoints": cluster_setpoints,
        }

        version = result["version"]
        model_manager.save_model(
            model=model_artifact,
            name="crop_compatibility",
            version=version,
            model_type="AgglomerativeClustering",
            metrics=metrics,
            features=available,
            target="cluster_label",
            training_samples=n_crops,
            hyperparameters={
                "n_clusters": n_clusters,
                "linkage_method": linkage_method,
            },
        )

        # Also save to MongoDB for the API to read directly
        mongodb.save_crop_compatibility(result)

    logger.success(
        f"Clustering complete: {n_clusters} groups from {n_crops} crops "
        f"(silhouette={sil:.3f})"
    )
    return result


def main():
    """CLI entry point."""
    parser = argparse.ArgumentParser(description="Train crop compatibility clustering")
    parser.add_argument(
        "--hours",
        type=int,
        default=4320,
        help="Hours of historical data (default 180 days)",
    )
    parser.add_argument("--farm-id", type=str, help="Filter by farm ID")
    parser.add_argument(
        "--n-clusters",
        type=int,
        default=None,
        help="Force number of clusters (auto if omitted)",
    )
    parser.add_argument(
        "--linkage",
        type=str,
        default="ward",
        choices=["ward", "complete", "average", "single"],
    )

    args = parser.parse_args()

    result = train_clustering_model(
        hours=args.hours,
        farm_id=args.farm_id,
        n_clusters=args.n_clusters,
        linkage_method=args.linkage,
    )

    if "error" in result:
        print(f"\nTraining failed: {result['error']}")
        return

    print("\n=== Clustering Complete ===")
    print(f"Crops:    {result['n_crops']}")
    print(f"Clusters: {result['n_clusters']}")
    print(f"Silhouette: {result['metrics']['silhouette_score']:.4f}")
    print("\nGroups:")
    for cid, members in result["clusters"].items():
        setpoints = result["cluster_setpoints"].get(cid, {})
        ph = setpoints.get("ph_target", "?")
        ec = setpoints.get("ec_ms_cm_target", "?")
        print(f"  Cluster {cid}: {', '.join(members)}  (pH~{ph}, EC~{ec})")


if __name__ == "__main__":
    main()
