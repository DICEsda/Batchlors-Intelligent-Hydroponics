"""
Pydantic schemas for ML API request/response models.
"""

from datetime import datetime
from typing import Optional
from pydantic import BaseModel, Field


# =============================================================================
# Growth Prediction Schemas
# =============================================================================


class GrowthPredictionRequest(BaseModel):
    """Request for plant growth prediction."""

    tower_id: str = Field(..., description="Tower identifier")
    crop_type: str = Field(..., description="Type of crop (e.g., 'Lettuce', 'Basil')")
    current_height_cm: float = Field(
        ..., ge=0, description="Current plant height in cm"
    )
    days_since_planting: int = Field(..., ge=0, description="Days since planting")

    # Optional environmental context
    avg_temp_c: Optional[float] = Field(
        None, description="Average temperature (Celsius)"
    )
    avg_humidity_pct: Optional[float] = Field(
        None, ge=0, le=100, description="Average humidity (%)"
    )
    avg_light_lux: Optional[float] = Field(
        None, ge=0, description="Average light intensity (lux)"
    )
    avg_ph: Optional[float] = Field(None, ge=0, le=14, description="Average pH level")
    avg_ec_ms_cm: Optional[float] = Field(None, ge=0, description="Average EC (mS/cm)")


class GrowthPredictionResponse(BaseModel):
    """Response with growth predictions."""

    tower_id: str
    crop_type: str

    # Predictions
    predicted_height_cm: float = Field(..., description="Predicted height in cm")
    predicted_harvest_date: str = Field(
        ..., description="Expected harvest date (ISO format)"
    )
    days_to_harvest: int = Field(..., description="Days until harvest")
    growth_rate_cm_per_day: float = Field(..., description="Estimated growth rate")
    health_score: float = Field(..., ge=0, le=1, description="Plant health score (0-1)")

    # Confidence
    confidence: float = Field(..., ge=0, le=1, description="Prediction confidence")

    # Metadata
    model_name: str
    model_version: str
    generated_at: str


class BatchGrowthPredictionRequest(BaseModel):
    """Batch request for multiple towers."""

    predictions: list[GrowthPredictionRequest]


class BatchGrowthPredictionResponse(BaseModel):
    """Batch response with multiple predictions."""

    predictions: list[GrowthPredictionResponse]
    total_count: int
    success_count: int
    error_count: int


# =============================================================================
# Anomaly Detection Schemas
# =============================================================================


class TelemetryInput(BaseModel):
    """Single telemetry reading for anomaly detection."""

    timestamp: Optional[str] = None

    # Tower sensors
    air_temp_c: Optional[float] = None
    humidity_pct: Optional[float] = None
    light_lux: Optional[float] = None

    # Reservoir sensors
    ph: Optional[float] = None
    ec_ms_cm: Optional[float] = None
    tds_ppm: Optional[float] = None
    water_temp_c: Optional[float] = None
    water_level_pct: Optional[float] = None


class AnomalyDetectionRequest(BaseModel):
    """Request for anomaly detection."""

    tower_id: Optional[str] = None
    coord_id: Optional[str] = None
    telemetry: TelemetryInput


class AnomalyResult(BaseModel):
    """Single anomaly detection result."""

    feature: str = Field(..., description="Feature name that may be anomalous")
    value: float = Field(..., description="Current value")
    expected_min: float = Field(..., description="Expected minimum")
    expected_max: float = Field(..., description="Expected maximum")
    severity: str = Field(
        ..., description="Severity level: low, medium, high, critical"
    )
    message: str = Field(..., description="Human-readable description")


class AnomalyDetectionResponse(BaseModel):
    """Response with anomaly detection results."""

    tower_id: Optional[str]
    coord_id: Optional[str]

    is_anomalous: bool = Field(..., description="Whether any anomaly was detected")
    anomaly_score: float = Field(
        ..., ge=0, le=1, description="Overall anomaly score (0-1)"
    )
    anomalies: list[AnomalyResult] = Field(default_factory=list)

    # Metadata
    model_name: str
    model_version: str
    generated_at: str


# =============================================================================
# Optimal Conditions Schemas
# =============================================================================


class OptimalConditionsRequest(BaseModel):
    """Request for optimal growing conditions."""

    crop_type: str = Field(..., description="Type of crop")
    growth_stage: str = Field(
        "vegetative",
        description="Growth stage: seedling, vegetative, flowering, fruiting",
    )


class OptimalConditionsResponse(BaseModel):
    """Response with recommended growing conditions."""

    crop_type: str
    growth_stage: str

    # Optimal ranges
    temp_min_c: float
    temp_max_c: float
    temp_optimal_c: float

    humidity_min_pct: float
    humidity_max_pct: float
    humidity_optimal_pct: float

    light_min_lux: float
    light_max_lux: float
    light_hours_per_day: int

    ph_min: float
    ph_max: float
    ph_optimal: float

    ec_min_ms_cm: float
    ec_max_ms_cm: float
    ec_optimal_ms_cm: float

    # Growth expectations
    expected_days_to_harvest: int
    expected_height_cm: float


# =============================================================================
# Health Check Schemas
# =============================================================================


class HealthCheckResponse(BaseModel):
    """API health check response."""

    status: str
    version: str
    mongodb_connected: bool
    mqtt_connected: bool
    models_loaded: list[str]
    uptime_seconds: float


# =============================================================================
# Twin Sync Schemas
# =============================================================================


class TwinSyncRequest(BaseModel):
    """Request to sync ML predictions to a digital twin."""

    twin_id: str = Field(..., description="Digital twin identifier")
    twin_type: str = Field(..., description="Twin type: tower, coordinator, reservoir")
    predictions: dict = Field(..., description="Prediction data to sync")


class TwinSyncResponse(BaseModel):
    """Response from twin sync operation."""

    success: bool
    twin_id: str
    message: str
    synced_at: str


# =============================================================================
# Crop Compatibility Clustering Schemas
# =============================================================================


class CompatibilityMatrixResponse(BaseModel):
    """Pairwise crop compatibility matrix."""

    crops: list[str] = Field(..., description="Ordered list of crop names")
    matrix: list[list[float]] = Field(
        ..., description="NxN compatibility scores (0-1, 1 = identical profile)"
    )


class ClusterGroup(BaseModel):
    """A single reservoir-sharing cluster."""

    reservoir_group: int = Field(..., description="Cluster label")
    crops: list[str] = Field(..., description="Crops in this group")
    average_compatibility: float = Field(
        ..., ge=0, le=1, description="Avg pairwise compatibility within group"
    )
    recommended_setpoints: dict = Field(
        default_factory=dict,
        description="Suggested reservoir targets (ph_target, ec_ms_cm_target, ...)",
    )
    note: Optional[str] = Field(None, description="Extra info (e.g. unknown crops)")


class ClusterRecommendationRequest(BaseModel):
    """Request: recommend reservoir groupings for these crops."""

    crops: list[str] = Field(
        ...,
        min_length=1,
        description="List of crop names to group (e.g. ['Lettuce', 'Basil', 'Tomato'])",
    )


class ClusterRecommendationResponse(BaseModel):
    """Recommended reservoir groupings."""

    input_crops: list[str]
    n_reservoirs_needed: int
    recommendations: list[ClusterGroup]


class ClustersResponse(BaseModel):
    """All cluster assignments and setpoints."""

    clusters: dict[str, list[str]] = Field(
        ..., description="Cluster label -> list of crop names"
    )
    setpoints: dict[str, dict] = Field(
        ..., description="Cluster label -> recommended reservoir setpoints"
    )


class PairwiseScoreResponse(BaseModel):
    """Compatibility score between two specific crops."""

    crop_a: str
    crop_b: str
    compatibility_score: float = Field(..., ge=0, le=1)
    same_cluster: bool


# =============================================================================
# Reservoir Drift Forecasting Schemas
# =============================================================================


class DriftPredictionRequest(BaseModel):
    """Request to predict reservoir metric drift."""

    coord_id: str = Field(..., description="Coordinator / reservoir ID")
    farm_id: Optional[str] = Field(None, description="Farm ID (optional scope)")
    hours_history: int = Field(
        168,
        ge=24,
        le=720,
        description="Hours of historical data to use for features",
    )


class MetricForecast(BaseModel):
    """Forecast for a single reservoir metric at multiple horizons."""

    metric: str = Field(..., description="Metric name (e.g. ph, ec_ms_cm)")
    current_value: Optional[float] = Field(None, description="Latest observed value")
    predicted_1h: Optional[float] = Field(None, description="Predicted value in 1 hour")
    predicted_6h: Optional[float] = Field(
        None, description="Predicted value in 6 hours"
    )
    predicted_24h: Optional[float] = Field(
        None, description="Predicted value in 24 hours"
    )
    time_to_threshold_hours: Optional[float] = Field(
        None, description="Hours until metric leaves acceptable range"
    )


class DriftPredictionResponse(BaseModel):
    """Reservoir drift forecast results."""

    coord_id: str
    forecasts: list[MetricForecast]
    model_name: str
    model_version: str
    confidence: float = Field(..., ge=0, le=1)
    generated_at: str


# =============================================================================
# Nutrient Consumption Prediction Schemas
# =============================================================================


class ConsumptionPredictionRequest(BaseModel):
    """Request to predict nutrient consumption rates."""

    coord_id: str = Field(..., description="Coordinator / reservoir ID")
    farm_id: Optional[str] = None
    hours_history: int = Field(
        168,
        ge=24,
        le=720,
        description="Hours of historical data for features",
    )


class ConsumptionRate(BaseModel):
    """Predicted depletion rate for a single metric."""

    metric: str = Field(..., description="Metric name")
    rate_per_hour: float = Field(
        ..., description="Predicted change per hour (negative = depletion)"
    )
    hours_until_critical: Optional[float] = Field(
        None, description="Estimated hours until metric reaches critical threshold"
    )


class ConsumptionPredictionResponse(BaseModel):
    """Nutrient consumption prediction results."""

    coord_id: str
    rates: list[ConsumptionRate]
    water_change_recommended_in_days: Optional[float] = Field(
        None, description="Days until a full water change is recommended"
    )
    nutrient_top_up_recommended: bool = Field(
        False, description="Whether nutrient top-up is needed soon"
    )
    model_name: str
    model_version: str
    confidence: float = Field(..., ge=0, le=1)
    generated_at: str


# =============================================================================
# Model Status Schema
# =============================================================================


class ModelStatusEntry(BaseModel):
    """Status of a single ML model."""

    name: str
    trained: bool = Field(..., description="Whether a trained model exists on disk")
    data_ready: bool = Field(
        ..., description="Whether enough data exists to train / retrain"
    )
    data_reason: str = Field(
        ..., description="Human-readable explanation of data readiness"
    )
    version: Optional[str] = None
    last_trained: Optional[str] = None
    metrics: Optional[dict] = None


class ModelStatusResponse(BaseModel):
    """Status of all ML models."""

    models: list[ModelStatusEntry]
