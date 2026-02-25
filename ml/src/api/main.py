"""
FastAPI ML Service for IoT Hydroponics.

Provides REST endpoints for:
- Growth prediction
- Anomaly detection
- Optimal conditions lookup
- Digital twin sync

Run with: uvicorn src.api.main:app --host 0.0.0.0 --port 8000
"""

import time
from contextlib import asynccontextmanager
from datetime import datetime
from typing import Optional

from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from loguru import logger

from ..config import config
from ..data.mongodb_connector import MongoDBConnector
from .inference import (
    model_manager,
    growth_predictor,
    anomaly_detector,
    drift_forecaster,
    consumption_predictor,
    get_optimal_conditions,
    CROP_CONFIG,
)
from .clustering import crop_compatibility_service
from .schemas import (
    GrowthPredictionRequest,
    GrowthPredictionResponse,
    BatchGrowthPredictionRequest,
    BatchGrowthPredictionResponse,
    AnomalyDetectionRequest,
    AnomalyDetectionResponse,
    TelemetryInput,
    OptimalConditionsRequest,
    OptimalConditionsResponse,
    HealthCheckResponse,
    TwinSyncRequest,
    TwinSyncResponse,
    CompatibilityMatrixResponse,
    ClustersResponse,
    ClusterRecommendationRequest,
    ClusterRecommendationResponse,
    PairwiseScoreResponse,
    DriftPredictionRequest,
    DriftPredictionResponse,
    ConsumptionPredictionRequest,
    ConsumptionPredictionResponse,
    ModelStatusResponse,
    ModelStatusEntry,
)

# =============================================================================
# Application Lifecycle
# =============================================================================

START_TIME = time.time()
mongodb_connector: Optional[MongoDBConnector] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application startup and shutdown."""
    global mongodb_connector

    logger.info("Starting ML API Service...")

    # Initialize MongoDB connection
    try:
        mongodb_connector = MongoDBConnector()
        mongodb_connector.connect()
        logger.success("MongoDB connected")
    except Exception as e:
        logger.warning(f"MongoDB connection failed (will retry on demand): {e}")
        mongodb_connector = None

    logger.info(
        f"Loaded models: {model_manager.loaded_models or ['None (using rule-based)']}"
    )
    logger.success(f"ML API ready on {config.api_host}:{config.api_port}")

    yield  # Application runs

    # Shutdown
    logger.info("Shutting down ML API Service...")
    if mongodb_connector:
        mongodb_connector.close()


# =============================================================================
# FastAPI Application
# =============================================================================

app = FastAPI(
    title="IoT Hydroponics ML API",
    description="Machine Learning service for plant growth prediction and anomaly detection",
    version="1.0.0",
    lifespan=lifespan,
)

# CORS middleware for frontend access
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# =============================================================================
# Health Check Endpoints
# =============================================================================


@app.get("/health", response_model=HealthCheckResponse, tags=["Health"])
async def health_check():
    """
    Check API health status and connected services.
    """
    mongodb_connected = False
    if mongodb_connector:
        try:
            mongodb_connector.db.command("ping")
            mongodb_connected = True
        except Exception:
            pass

    return HealthCheckResponse(
        status="healthy",
        version="1.0.0",
        mongodb_connected=mongodb_connected,
        mqtt_connected=False,  # MQTT is subscription-based, not persistent
        models_loaded=model_manager.loaded_models or ["rule_based"],
        uptime_seconds=round(time.time() - START_TIME, 2),
    )


@app.get("/", tags=["Health"])
async def root():
    """Root endpoint with API info."""
    return {
        "service": "IoT Hydroponics ML API",
        "version": "1.0.0",
        "docs": "/docs",
        "health": "/health",
    }


# =============================================================================
# Growth Prediction Endpoints
# =============================================================================


@app.post(
    "/api/predict/growth", response_model=GrowthPredictionResponse, tags=["Predictions"]
)
async def predict_growth(request: GrowthPredictionRequest):
    """
    Predict plant growth metrics for a single tower.

    Returns:
    - Predicted height in 7 days
    - Expected harvest date
    - Days to harvest
    - Current growth rate
    - Health score based on environmental conditions
    """
    try:
        result = growth_predictor.predict(
            tower_id=request.tower_id,
            crop_type=request.crop_type,
            current_height_cm=request.current_height_cm,
            days_since_planting=request.days_since_planting,
            avg_temp_c=request.avg_temp_c,
            avg_humidity_pct=request.avg_humidity_pct,
            avg_light_lux=request.avg_light_lux,
            avg_ph=request.avg_ph,
            avg_ec_ms_cm=request.avg_ec_ms_cm,
        )
        return GrowthPredictionResponse(**result)
    except Exception as e:
        logger.error(f"Growth prediction failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post(
    "/api/predict/growth/batch",
    response_model=BatchGrowthPredictionResponse,
    tags=["Predictions"],
)
async def predict_growth_batch(request: BatchGrowthPredictionRequest):
    """
    Predict growth for multiple towers in a single request.
    """
    predictions = []
    errors = 0

    for req in request.predictions:
        try:
            result = growth_predictor.predict(
                tower_id=req.tower_id,
                crop_type=req.crop_type,
                current_height_cm=req.current_height_cm,
                days_since_planting=req.days_since_planting,
                avg_temp_c=req.avg_temp_c,
                avg_humidity_pct=req.avg_humidity_pct,
                avg_light_lux=req.avg_light_lux,
                avg_ph=req.avg_ph,
                avg_ec_ms_cm=req.avg_ec_ms_cm,
            )
            predictions.append(GrowthPredictionResponse(**result))
        except Exception as e:
            logger.warning(f"Batch prediction failed for tower {req.tower_id}: {e}")
            errors += 1

    return BatchGrowthPredictionResponse(
        predictions=predictions,
        total_count=len(request.predictions),
        success_count=len(predictions),
        error_count=errors,
    )


@app.get(
    "/api/predict/growth/{tower_id}",
    response_model=GrowthPredictionResponse,
    tags=["Predictions"],
)
async def predict_growth_from_twin(
    tower_id: str,
    crop_type: str = Query(..., description="Crop type (e.g., Lettuce, Basil)"),
):
    """
    Predict growth for a tower using data from MongoDB twin.

    Fetches current tower state from the digital twin and generates predictions.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        # Fetch tower twin data
        twin = mongodb_connector.db.twin_twins.find_one({"tower_id": tower_id})
        if not twin:
            raise HTTPException(status_code=404, detail=f"Tower {tower_id} not found")

        # Extract current state
        reported = twin.get("reported", {})
        current_height = twin.get("last_height_cm", 0)
        planting_date = twin.get("planting_date")

        if planting_date:
            days_since = (datetime.utcnow() - planting_date).days
        else:
            days_since = 0

        result = growth_predictor.predict(
            tower_id=tower_id,
            crop_type=crop_type,
            current_height_cm=current_height,
            days_since_planting=days_since,
            avg_temp_c=reported.get("air_temp_c"),
            avg_humidity_pct=reported.get("humidity_pct"),
            avg_light_lux=reported.get("light_lux"),
        )
        return GrowthPredictionResponse(**result)

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to predict from twin: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Anomaly Detection Endpoints
# =============================================================================


@app.post(
    "/api/detect/anomaly",
    response_model=AnomalyDetectionResponse,
    tags=["Anomaly Detection"],
)
async def detect_anomaly(request: AnomalyDetectionRequest):
    """
    Detect anomalies in sensor telemetry data.

    Analyzes environmental readings and identifies values outside optimal ranges.
    Returns severity levels: low, medium, high, critical.
    """
    try:
        telemetry_dict = request.telemetry.model_dump(exclude_none=True)

        result = anomaly_detector.detect(
            tower_id=request.tower_id,
            coord_id=request.coord_id,
            telemetry=telemetry_dict,
        )
        return AnomalyDetectionResponse(**result)
    except Exception as e:
        logger.error(f"Anomaly detection failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get(
    "/api/detect/anomaly/{tower_id}",
    response_model=AnomalyDetectionResponse,
    tags=["Anomaly Detection"],
)
async def detect_anomaly_from_twin(tower_id: str):
    """
    Detect anomalies using current tower telemetry from MongoDB.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        # Fetch latest telemetry
        telemetry = mongodb_connector.db.tower_telemetry.find_one(
            {"tower_id": tower_id}, sort=[("timestamp", -1)]
        )

        if not telemetry:
            raise HTTPException(
                status_code=404, detail=f"No telemetry found for tower {tower_id}"
            )

        result = anomaly_detector.detect(
            tower_id=tower_id,
            coord_id=telemetry.get("coord_id"),
            telemetry=telemetry,
        )
        return AnomalyDetectionResponse(**result)

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to detect anomaly from twin: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Optimal Conditions Endpoints
# =============================================================================


@app.get(
    "/api/conditions/optimal",
    response_model=OptimalConditionsResponse,
    tags=["Conditions"],
)
async def get_optimal_growing_conditions(
    crop_type: str = Query(..., description="Crop type (e.g., Lettuce, Basil)"),
    growth_stage: str = Query(
        "vegetative",
        description="Growth stage: seedling, vegetative, flowering, fruiting",
    ),
):
    """
    Get optimal growing conditions for a specific crop and growth stage.
    """
    conditions = get_optimal_conditions(crop_type, growth_stage)
    return OptimalConditionsResponse(**conditions)


@app.get("/api/conditions/crops", tags=["Conditions"])
async def list_supported_crops():
    """
    List all supported crop types with their basic info.
    """
    crops = []
    for name, cfg in CROP_CONFIG.items():
        crops.append(
            {
                "name": name,
                "days_to_harvest": cfg["expected_days_to_harvest"],
                "expected_height_cm": cfg["expected_height_cm"],
                "ph_range": f"{cfg['ph_min']}-{cfg['ph_max']}",
                "ec_range_ms_cm": f"{cfg['ec_min_ms_cm']}-{cfg['ec_max_ms_cm']}",
            }
        )
    return {"crops": crops, "count": len(crops)}


# =============================================================================
# Data Endpoints
# =============================================================================


@app.get("/api/data/telemetry/tower/{tower_id}", tags=["Data"])
async def get_tower_telemetry(
    tower_id: str,
    hours: int = Query(24, ge=1, le=720, description="Hours of data to fetch"),
):
    """
    Fetch historical tower telemetry data.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        df = mongodb_connector.get_tower_telemetry(tower_id=tower_id, hours=hours)

        if df.empty:
            return {"data": [], "count": 0}

        # Convert to JSON-serializable format
        records = df.to_dict(orient="records")
        for r in records:
            if "timestamp" in r and hasattr(r["timestamp"], "isoformat"):
                r["timestamp"] = r["timestamp"].isoformat()

        return {"data": records, "count": len(records)}
    except Exception as e:
        logger.error(f"Failed to fetch telemetry: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/api/data/stats", tags=["Data"])
async def get_data_stats():
    """
    Get statistics about available data in MongoDB.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        stats = mongodb_connector.get_collection_stats()
        return stats
    except Exception as e:
        logger.error(f"Failed to get stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Twin Sync Endpoints
# =============================================================================


@app.post("/api/twins/sync", response_model=TwinSyncResponse, tags=["Digital Twins"])
async def sync_predictions_to_twin(request: TwinSyncRequest):
    """
    Sync ML predictions to a digital twin (MongoDB or Azure DT).

    This endpoint is called by scheduled jobs to push predictions back to twins.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        # Update MongoDB twin with predictions
        if request.twin_type == "tower":
            collection = mongodb_connector.db.twin_twins
            filter_query = {"tower_id": request.twin_id}
        elif request.twin_type == "coordinator":
            collection = mongodb_connector.db.twin_twins
            filter_query = {"coord_id": request.twin_id, "tower_id": {"$exists": False}}
        else:
            raise HTTPException(
                status_code=400, detail=f"Unknown twin type: {request.twin_type}"
            )

        result = collection.update_one(
            filter_query,
            {
                "$set": {
                    "ml_predictions": request.predictions,
                    "ml_predictions_updated_at": datetime.utcnow(),
                }
            },
        )

        if result.matched_count == 0:
            return TwinSyncResponse(
                success=False,
                twin_id=request.twin_id,
                message=f"Twin {request.twin_id} not found",
                synced_at=datetime.utcnow().isoformat(),
            )

        return TwinSyncResponse(
            success=True,
            twin_id=request.twin_id,
            message="Predictions synced to MongoDB twin",
            synced_at=datetime.utcnow().isoformat(),
        )

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to sync twin: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Crop Compatibility Clustering Endpoints
# =============================================================================


@app.get(
    "/api/clustering/compatibility",
    response_model=CompatibilityMatrixResponse,
    tags=["Clustering"],
)
async def get_compatibility_matrix():
    """
    Get the pairwise crop compatibility matrix.

    Each cell (i, j) is a score from 0 to 1 indicating how well crop i
    and crop j can share a reservoir (1 = identical optimal conditions).
    """
    result = crop_compatibility_service.get_compatibility_matrix()
    if "error" in result:
        raise HTTPException(
            status_code=404, detail=result.get("message", "No clustering model")
        )
    return CompatibilityMatrixResponse(**result)


@app.get(
    "/api/clustering/clusters", response_model=ClustersResponse, tags=["Clustering"]
)
async def get_clusters():
    """
    Get all cluster assignments and recommended reservoir setpoints per group.
    """
    result = crop_compatibility_service.get_clusters()
    if "error" in result:
        raise HTTPException(status_code=404, detail="No clustering model trained yet")
    return ClustersResponse(**result)


@app.post(
    "/api/clustering/recommend",
    response_model=ClusterRecommendationResponse,
    tags=["Clustering"],
)
async def recommend_reservoir_grouping(request: ClusterRecommendationRequest):
    """
    Given a list of crops, recommend how to split them across reservoirs
    based on compatibility clustering.
    """
    result = crop_compatibility_service.recommend_grouping(request.crops)
    if "error" in result:
        raise HTTPException(
            status_code=404, detail=result.get("message", "No clustering model")
        )
    return ClusterRecommendationResponse(**result)


@app.get(
    "/api/clustering/score", response_model=PairwiseScoreResponse, tags=["Clustering"]
)
async def get_pairwise_compatibility(
    crop_a: str = Query(..., description="First crop name"),
    crop_b: str = Query(..., description="Second crop name"),
):
    """
    Get the compatibility score between two specific crops.
    """
    result = crop_compatibility_service.get_pairwise_score(crop_a, crop_b)
    if "error" in result:
        if result["error"] == "unknown_crop":
            raise HTTPException(
                status_code=404, detail=f"Unknown crop(s): {result.get('missing')}"
            )
        raise HTTPException(status_code=404, detail="No clustering model")
    return PairwiseScoreResponse(**result)


# =============================================================================
# Reservoir Drift Forecasting Endpoints
# =============================================================================


@app.post(
    "/api/predict/drift", response_model=DriftPredictionResponse, tags=["Predictions"]
)
async def predict_drift(request: DriftPredictionRequest):
    """
    Predict future reservoir pH, EC, and water level at t+1h, t+6h, t+24h.

    Uses the trained drift forecasting model if available, otherwise returns
    a low-confidence rule-based response.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        # Fetch latest reservoir values
        latest = mongodb_connector.db.reservoir_telemetry.find_one(
            {"coord_id": request.coord_id},
            sort=[("timestamp", -1)],
        )
        current_values = {}
        if latest:
            for m in ["ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]:
                if m in latest and latest[m] is not None:
                    current_values[m] = float(latest[m])

        result = drift_forecaster.predict(
            coord_id=request.coord_id,
            features={},  # Empty features for rule-based fallback
            current_values=current_values,
        )
        return DriftPredictionResponse(**result)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Drift prediction failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get(
    "/api/predict/drift/{coord_id}",
    response_model=DriftPredictionResponse,
    tags=["Predictions"],
)
async def predict_drift_from_db(
    coord_id: str,
    hours: int = Query(168, ge=24, le=720, description="Hours of history for features"),
):
    """
    Predict reservoir drift for a coordinator using data from MongoDB.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        latest = mongodb_connector.db.reservoir_telemetry.find_one(
            {"coord_id": coord_id},
            sort=[("timestamp", -1)],
        )
        if not latest:
            raise HTTPException(
                status_code=404, detail=f"No telemetry for coord {coord_id}"
            )

        current_values = {}
        for m in ["ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]:
            if m in latest and latest[m] is not None:
                current_values[m] = float(latest[m])

        result = drift_forecaster.predict(
            coord_id=coord_id,
            features={},
            current_values=current_values,
        )
        return DriftPredictionResponse(**result)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Drift prediction from DB failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Nutrient Consumption Prediction Endpoints
# =============================================================================


@app.post(
    "/api/predict/consumption",
    response_model=ConsumptionPredictionResponse,
    tags=["Predictions"],
)
async def predict_consumption(request: ConsumptionPredictionRequest):
    """
    Predict nutrient and water depletion rates for a reservoir.

    Returns hourly depletion rates for pH, EC, and water level,
    plus derived recommendations (water change timing, nutrient top-up).
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        latest = mongodb_connector.db.reservoir_telemetry.find_one(
            {"coord_id": request.coord_id},
            sort=[("timestamp", -1)],
        )
        current_values = {}
        if latest:
            for m in ["ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]:
                if m in latest and latest[m] is not None:
                    current_values[m] = float(latest[m])

        result = consumption_predictor.predict(
            coord_id=request.coord_id,
            features={},
            current_values=current_values,
        )
        return ConsumptionPredictionResponse(**result)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Consumption prediction failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get(
    "/api/predict/consumption/{coord_id}",
    response_model=ConsumptionPredictionResponse,
    tags=["Predictions"],
)
async def predict_consumption_from_db(coord_id: str):
    """
    Predict consumption rates for a coordinator using data from MongoDB.
    """
    if not mongodb_connector:
        raise HTTPException(status_code=503, detail="MongoDB not available")

    try:
        latest = mongodb_connector.db.reservoir_telemetry.find_one(
            {"coord_id": coord_id},
            sort=[("timestamp", -1)],
        )
        if not latest:
            raise HTTPException(
                status_code=404, detail=f"No telemetry for coord {coord_id}"
            )

        current_values = {}
        for m in ["ph", "ec_ms_cm", "water_temp_c", "water_level_pct"]:
            if m in latest and latest[m] is not None:
                current_values[m] = float(latest[m])

        result = consumption_predictor.predict(
            coord_id=coord_id,
            features={},
            current_values=current_values,
        )
        return ConsumptionPredictionResponse(**result)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Consumption prediction from DB failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# =============================================================================
# Model Status Endpoints
# =============================================================================


@app.get("/api/models/status", response_model=ModelStatusResponse, tags=["Models"])
async def get_model_status():
    """
    List all ML models with their training status and data readiness.
    """
    from ..training.model_manager import model_manager as training_mgr

    # Check data readiness
    readiness = {}
    if mongodb_connector:
        try:
            readiness = mongodb_connector.get_data_readiness()
        except Exception as e:
            logger.warning(f"Data readiness check failed: {e}")

    model_defs = [
        ("growth_predictor", "growth_prediction"),
        ("anomaly_detector", "growth_prediction"),  # anomaly has no separate readiness
        ("crop_compatibility", "clustering"),
        ("drift_forecaster", "drift_forecasting"),
        ("consumption_predictor", "consumption_prediction"),
    ]

    entries = []
    for model_name, readiness_key in model_defs:
        trained = training_mgr.model_exists(model_name)
        meta = training_mgr.get_metadata(model_name)
        dr = readiness.get(readiness_key, {})

        entries.append(
            ModelStatusEntry(
                name=model_name,
                trained=trained,
                data_ready=dr.get("ready", False),
                data_reason=dr.get("reason", "Unable to check"),
                version=meta.version if meta else None,
                last_trained=meta.trained_at.isoformat() if meta else None,
                metrics=meta.metrics if meta else None,
            )
        )

    return ModelStatusResponse(models=entries)


# =============================================================================
# Run Server (for development)
# =============================================================================

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "src.api.main:app",
        host=config.api_host,
        port=config.api_port,
        reload=True,
    )
