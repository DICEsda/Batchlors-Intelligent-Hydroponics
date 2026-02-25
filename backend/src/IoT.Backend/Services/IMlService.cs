using IoT.Backend.Models.Ml;

namespace IoT.Backend.Services;

/// <summary>
/// Service interface for communicating with the ML FastAPI service.
/// Provides methods for growth prediction, anomaly detection, and health checks.
/// </summary>
public interface IMlService
{
    /// <summary>
    /// Predicts plant growth metrics for a tower.
    /// </summary>
    /// <param name="request">Growth prediction request with tower and plant data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Growth prediction response, or null if the ML service is unavailable.</returns>
    Task<GrowthPredictionResponse?> PredictGrowthAsync(GrowthPredictionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Detects anomalies in sensor telemetry data.
    /// </summary>
    /// <param name="request">Anomaly detection request with telemetry data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Anomaly detection response, or null if the ML service is unavailable.</returns>
    Task<AnomalyDetectionResponse?> DetectAnomalyAsync(AnomalyDetectionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Checks the health status of the ML service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health check response, or null if the ML service is unavailable.</returns>
    Task<MlHealthResponse?> GetHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the list of supported crop types.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Crops response, or null if the ML service is unavailable.</returns>
    Task<CropsResponse?> GetCropsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets optimal growing conditions for a specific crop and growth stage.
    /// </summary>
    /// <param name="cropType">Type of crop (e.g., "Lettuce", "Basil").</param>
    /// <param name="growthStage">Growth stage (seedling, vegetative, flowering, fruiting).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Optimal conditions response, or null if the ML service is unavailable.</returns>
    Task<OptimalConditionsResponse?> GetOptimalConditionsAsync(string cropType, string growthStage = "vegetative", CancellationToken ct = default);
}
