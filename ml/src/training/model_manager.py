"""
Model Manager for loading, saving, and versioning ML models.
Provides a central registry for all trained models.
"""

import json
import pickle
from datetime import datetime
from pathlib import Path
from typing import Any, Optional
from loguru import logger

from ..config import config


class ModelMetadata:
    """Metadata about a trained model."""
    
    def __init__(
        self,
        name: str,
        version: str,
        model_type: str,
        trained_at: datetime,
        metrics: dict,
        features: list[str],
        target: str,
        training_samples: int,
        hyperparameters: dict,
    ):
        self.name = name
        self.version = version
        self.model_type = model_type
        self.trained_at = trained_at
        self.metrics = metrics
        self.features = features
        self.target = target
        self.training_samples = training_samples
        self.hyperparameters = hyperparameters
    
    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "version": self.version,
            "model_type": self.model_type,
            "trained_at": self.trained_at.isoformat(),
            "metrics": self.metrics,
            "features": self.features,
            "target": self.target,
            "training_samples": self.training_samples,
            "hyperparameters": self.hyperparameters,
        }
    
    @classmethod
    def from_dict(cls, data: dict) -> "ModelMetadata":
        return cls(
            name=data["name"],
            version=data["version"],
            model_type=data["model_type"],
            trained_at=datetime.fromisoformat(data["trained_at"]),
            metrics=data["metrics"],
            features=data["features"],
            target=data["target"],
            training_samples=data["training_samples"],
            hyperparameters=data["hyperparameters"],
        )


class ModelManager:
    """
    Manages trained ML models including saving, loading, and versioning.
    
    Models are saved with metadata for reproducibility and tracking.
    
    Usage:
        manager = ModelManager()
        
        # Save a model
        manager.save_model(
            model=trained_model,
            name="growth_predictor",
            version="1.0.0",
            model_type="RandomForestRegressor",
            metrics={"rmse": 0.5, "r2": 0.85},
            features=["days_since_planting", "temp", "humidity"],
            target="height_cm",
            training_samples=1000,
            hyperparameters={"n_estimators": 100},
        )
        
        # Load a model
        model, metadata = manager.load_model("growth_predictor")
    """
    
    def __init__(self, model_dir: Optional[Path] = None):
        self.model_dir = Path(model_dir or config.ml.model_dir)
        self.model_dir.mkdir(parents=True, exist_ok=True)
        self._registry: dict[str, ModelMetadata] = {}
        self._load_registry()
    
    def _registry_path(self) -> Path:
        return self.model_dir / "model_registry.json"
    
    def _load_registry(self) -> None:
        """Load model registry from disk."""
        registry_path = self._registry_path()
        if registry_path.exists():
            try:
                with open(registry_path, "r") as f:
                    data = json.load(f)
                self._registry = {
                    name: ModelMetadata.from_dict(meta)
                    for name, meta in data.items()
                }
                logger.info(f"Loaded model registry with {len(self._registry)} models")
            except Exception as e:
                logger.warning(f"Failed to load model registry: {e}")
                self._registry = {}
        else:
            self._registry = {}
    
    def _save_registry(self) -> None:
        """Save model registry to disk."""
        registry_path = self._registry_path()
        try:
            with open(registry_path, "w") as f:
                json.dump(
                    {name: meta.to_dict() for name, meta in self._registry.items()},
                    f,
                    indent=2,
                )
        except Exception as e:
            logger.error(f"Failed to save model registry: {e}")
    
    def save_model(
        self,
        model: Any,
        name: str,
        version: str,
        model_type: str,
        metrics: dict,
        features: list[str],
        target: str,
        training_samples: int,
        hyperparameters: dict,
    ) -> Path:
        """
        Save a trained model to disk with metadata.
        
        Args:
            model: The trained model object
            name: Model name (e.g., "growth_predictor")
            version: Version string (e.g., "1.0.0")
            model_type: Type of model (e.g., "RandomForestRegressor")
            metrics: Training metrics (e.g., {"rmse": 0.5, "r2": 0.85})
            features: List of feature names used
            target: Target variable name
            training_samples: Number of samples used for training
            hyperparameters: Model hyperparameters
        
        Returns:
            Path to the saved model file
        """
        # Create metadata
        metadata = ModelMetadata(
            name=name,
            version=version,
            model_type=model_type,
            trained_at=datetime.utcnow(),
            metrics=metrics,
            features=features,
            target=target,
            training_samples=training_samples,
            hyperparameters=hyperparameters,
        )
        
        # Save model
        model_path = self.model_dir / f"{name}.pkl"
        with open(model_path, "wb") as f:
            pickle.dump(model, f)
        
        # Save metadata separately
        metadata_path = self.model_dir / f"{name}_metadata.json"
        with open(metadata_path, "w") as f:
            json.dump(metadata.to_dict(), f, indent=2)
        
        # Update registry
        self._registry[name] = metadata
        self._save_registry()
        
        logger.success(f"Saved model '{name}' v{version} to {model_path}")
        return model_path
    
    def load_model(self, name: str) -> tuple[Any, ModelMetadata]:
        """
        Load a trained model and its metadata.
        
        Args:
            name: Model name to load
        
        Returns:
            Tuple of (model, metadata)
        
        Raises:
            FileNotFoundError: If model doesn't exist
        """
        model_path = self.model_dir / f"{name}.pkl"
        metadata_path = self.model_dir / f"{name}_metadata.json"
        
        if not model_path.exists():
            raise FileNotFoundError(f"Model '{name}' not found at {model_path}")
        
        # Load model
        with open(model_path, "rb") as f:
            model = pickle.load(f)
        
        # Load metadata
        if metadata_path.exists():
            with open(metadata_path, "r") as f:
                metadata = ModelMetadata.from_dict(json.load(f))
        else:
            metadata = self._registry.get(name)
            if metadata is None:
                raise FileNotFoundError(f"Metadata for model '{name}' not found")
        
        logger.info(f"Loaded model '{name}' v{metadata.version}")
        return model, metadata
    
    def list_models(self) -> list[dict]:
        """List all registered models with their metadata."""
        return [
            {
                "name": name,
                "version": meta.version,
                "model_type": meta.model_type,
                "trained_at": meta.trained_at.isoformat(),
                "metrics": meta.metrics,
            }
            for name, meta in self._registry.items()
        ]
    
    def get_metadata(self, name: str) -> Optional[ModelMetadata]:
        """Get metadata for a specific model."""
        return self._registry.get(name)
    
    def model_exists(self, name: str) -> bool:
        """Check if a model exists."""
        return (self.model_dir / f"{name}.pkl").exists()
    
    def delete_model(self, name: str) -> bool:
        """Delete a model and its metadata."""
        model_path = self.model_dir / f"{name}.pkl"
        metadata_path = self.model_dir / f"{name}_metadata.json"
        
        deleted = False
        
        if model_path.exists():
            model_path.unlink()
            deleted = True
        
        if metadata_path.exists():
            metadata_path.unlink()
        
        if name in self._registry:
            del self._registry[name]
            self._save_registry()
        
        if deleted:
            logger.info(f"Deleted model '{name}'")
        
        return deleted


# Singleton instance
model_manager = ModelManager()
