using IoT.Backend.Models.Ml;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API controller that proxies requests to the ML FastAPI service.
/// Provides endpoints for growth prediction, anomaly detection, and optimal growing conditions.
/// </summary>
/// <remarks>
/// This controller acts as a gateway between the Angular frontend and the Python ML service,
/// handling authentication, request validation, and graceful error handling when the ML
/// service is unavailable.
/// </remarks>
[ApiController]
[Route("api/ml")]
public class MlController : ControllerBase
{
    private readonly IMlService _mlService;
    private readonly ILogger<MlController> _logger;

    /// <summary>
    /// Initializes a new instance of the MlController.
    /// </summary>
    /// <param name="mlService">ML service for communicating with the Python ML API.</param>
    /// <param name="logger">Logger instance.</param>
    public MlController(IMlService mlService, ILogger<MlController> logger)
    {
        _mlService = mlService;
        _logger = logger;
    }

    // ============================================================================
    // Growth Prediction Endpoints
    // ============================================================================

    /// <summary>
    /// Predict plant growth metrics for a tower.
    /// </summary>
    /// <remarks>
    /// Forwards the request to the ML service and returns growth predictions including:
    /// - Predicted height in 7 days
    /// - Expected harvest date
    /// - Days to harvest
    /// - Current growth rate
    /// - Health score based on environmental conditions
    /// </remarks>
    /// <param name="request">Growth prediction request containing tower and plant data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Growth prediction response with predicted metrics.</returns>
    /// <response code="200">Returns the growth prediction.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="503">If the ML service is unavailable.</response>
    [HttpPost("predict/growth")]
    [ProducesResponseType(typeof(GrowthPredictionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GrowthPredictionResponse>> PredictGrowth(
        [FromBody] GrowthPredictionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TowerId))
        {
            return BadRequest(new { error = "tower_id is required" });
        }

        if (string.IsNullOrWhiteSpace(request.CropType))
        {
            return BadRequest(new { error = "crop_type is required" });
        }

        if (request.CurrentHeightCm < 0)
        {
            return BadRequest(new { error = "current_height_cm must be non-negative" });
        }

        if (request.DaysSincePlanting < 0)
        {
            return BadRequest(new { error = "days_since_planting must be non-negative" });
        }

        _logger.LogInformation("Growth prediction requested for tower {TowerId}, crop {CropType}", 
            request.TowerId, request.CropType);

        var result = await _mlService.PredictGrowthAsync(request, ct);
        
        if (result == null)
        {
            _logger.LogWarning("ML service unavailable for growth prediction");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "ML service is currently unavailable" });
        }

        return Ok(result);
    }

    // ============================================================================
    // Anomaly Detection Endpoints
    // ============================================================================

    /// <summary>
    /// Detect anomalies in sensor telemetry data.
    /// </summary>
    /// <remarks>
    /// Analyzes environmental readings and identifies values outside optimal ranges.
    /// Returns severity levels: low, medium, high, critical.
    /// </remarks>
    /// <param name="request">Anomaly detection request containing telemetry data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Anomaly detection response with detected anomalies.</returns>
    /// <response code="200">Returns the anomaly detection results.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="503">If the ML service is unavailable.</response>
    [HttpPost("detect/anomaly")]
    [ProducesResponseType(typeof(AnomalyDetectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AnomalyDetectionResponse>> DetectAnomaly(
        [FromBody] AnomalyDetectionRequest request,
        CancellationToken ct)
    {
        if (request.Telemetry == null)
        {
            return BadRequest(new { error = "telemetry data is required" });
        }

        _logger.LogInformation("Anomaly detection requested for tower {TowerId}", request.TowerId ?? "N/A");

        var result = await _mlService.DetectAnomalyAsync(request, ct);
        
        if (result == null)
        {
            _logger.LogWarning("ML service unavailable for anomaly detection");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "ML service is currently unavailable" });
        }

        return Ok(result);
    }

    // ============================================================================
    // Health Check Endpoints
    // ============================================================================

    /// <summary>
    /// Check the health status of the ML service.
    /// </summary>
    /// <remarks>
    /// Returns detailed information about the ML service including:
    /// - Service status
    /// - API version
    /// - Connected services (MongoDB, MQTT)
    /// - Loaded ML models
    /// - Service uptime
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ML service health status.</returns>
    /// <response code="200">Returns the ML service health status.</response>
    /// <response code="503">If the ML service is unavailable.</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(MlHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MlHealthResponse>> GetHealth(CancellationToken ct)
    {
        _logger.LogDebug("ML service health check requested");

        var result = await _mlService.GetHealthAsync(ct);
        
        if (result == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "ML service is currently unavailable", status = "unhealthy" });
        }

        return Ok(result);
    }

    // ============================================================================
    // Crop Information Endpoints
    // ============================================================================

    /// <summary>
    /// Get the list of supported crop types.
    /// </summary>
    /// <remarks>
    /// Returns all crop types supported by the ML service with basic information
    /// including expected days to harvest, expected height, and optimal pH/EC ranges.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of supported crops.</returns>
    /// <response code="200">Returns the list of supported crops.</response>
    /// <response code="503">If the ML service is unavailable.</response>
    [HttpGet("crops")]
    [ProducesResponseType(typeof(CropsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CropsResponse>> GetCrops(CancellationToken ct)
    {
        _logger.LogDebug("Supported crops list requested");

        var result = await _mlService.GetCropsAsync(ct);
        
        if (result == null)
        {
            _logger.LogWarning("ML service unavailable for crops list");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "ML service is currently unavailable" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get optimal growing conditions for a specific crop.
    /// </summary>
    /// <remarks>
    /// Returns recommended environmental parameters for optimal plant growth including:
    /// - Temperature range (min, max, optimal)
    /// - Humidity range
    /// - Light intensity and hours per day
    /// - pH range
    /// - EC (electrical conductivity) range
    /// - Expected growth metrics
    /// </remarks>
    /// <param name="cropType">Type of crop (e.g., "Lettuce", "Basil").</param>
    /// <param name="growthStage">Growth stage: seedling, vegetative, flowering, fruiting. Defaults to "vegetative".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Optimal growing conditions for the specified crop.</returns>
    /// <response code="200">Returns the optimal growing conditions.</response>
    /// <response code="400">If the crop type is invalid.</response>
    /// <response code="503">If the ML service is unavailable.</response>
    [HttpGet("conditions/{cropType}")]
    [ProducesResponseType(typeof(OptimalConditionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OptimalConditionsResponse>> GetOptimalConditions(
        string cropType,
        [FromQuery] string growthStage = "vegetative",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cropType))
        {
            return BadRequest(new { error = "crop_type is required" });
        }

        var validStages = new[] { "seedling", "vegetative", "flowering", "fruiting" };
        if (!validStages.Contains(growthStage.ToLowerInvariant()))
        {
            return BadRequest(new { 
                error = "Invalid growth_stage", 
                valid_values = validStages 
            });
        }

        _logger.LogDebug("Optimal conditions requested for crop {CropType}, stage {GrowthStage}", 
            cropType, growthStage);

        var result = await _mlService.GetOptimalConditionsAsync(cropType, growthStage, ct);
        
        if (result == null)
        {
            _logger.LogWarning("ML service unavailable for optimal conditions");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "ML service is currently unavailable" });
        }

        return Ok(result);
    }
}
