using System.Net.Http.Json;
using System.Text.Json;
using IoT.Backend.Models;
using IoT.Backend.Models.Ml;
using Microsoft.Extensions.Options;

namespace IoT.Backend.Services;

/// <summary>
/// Implementation of the ML service client that proxies requests to the ML FastAPI service.
/// Handles HTTP communication, serialization, and graceful failure handling.
/// </summary>
public class MlService : IMlService
{
    private readonly HttpClient _httpClient;
    private readonly MlServiceConfig _config;
    private readonly ILogger<MlService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the MlService.
    /// </summary>
    /// <param name="httpClient">HTTP client configured for the ML service.</param>
    /// <param name="options">ML service configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public MlService(
        HttpClient httpClient,
        IOptions<MlServiceConfig> options,
        ILogger<MlService> logger)
    {
        _httpClient = httpClient;
        _config = options.Value;
        _logger = logger;

        // Configure JSON options to match Python snake_case convention
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<GrowthPredictionResponse?> PredictGrowthAsync(GrowthPredictionRequest request, CancellationToken ct = default)
    {
        const string endpoint = "/api/predict/growth";
        _logger.LogDebug("Calling ML service: POST {Endpoint} for tower {TowerId}", endpoint, request.TowerId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML service returned {StatusCode} for growth prediction", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GrowthPredictionResponse>(_jsonOptions, ct);
            _logger.LogDebug("Growth prediction completed for tower {TowerId}: predicted height {Height}cm", 
                request.TowerId, result?.PredictedHeightCm);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ML service unavailable for growth prediction");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("ML service request timed out for growth prediction");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ML service response for growth prediction");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AnomalyDetectionResponse?> DetectAnomalyAsync(AnomalyDetectionRequest request, CancellationToken ct = default)
    {
        const string endpoint = "/api/detect/anomaly";
        _logger.LogDebug("Calling ML service: POST {Endpoint} for tower {TowerId}", endpoint, request.TowerId ?? "N/A");

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML service returned {StatusCode} for anomaly detection", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AnomalyDetectionResponse>(_jsonOptions, ct);
            _logger.LogDebug("Anomaly detection completed: is_anomalous={IsAnomalous}, score={Score}", 
                result?.IsAnomalous, result?.AnomalyScore);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ML service unavailable for anomaly detection");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("ML service request timed out for anomaly detection");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ML service response for anomaly detection");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<MlHealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        const string endpoint = "/health";
        _logger.LogDebug("Calling ML service: GET {Endpoint}", endpoint);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML service health check returned {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MlHealthResponse>(_jsonOptions, ct);
            _logger.LogDebug("ML service health: status={Status}, uptime={Uptime}s", 
                result?.Status, result?.UptimeSeconds);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ML service unavailable for health check");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("ML service health check timed out");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ML service health response");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<CropsResponse?> GetCropsAsync(CancellationToken ct = default)
    {
        const string endpoint = "/api/conditions/crops";
        _logger.LogDebug("Calling ML service: GET {Endpoint}", endpoint);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML service returned {StatusCode} for crops list", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<CropsResponse>(_jsonOptions, ct);
            _logger.LogDebug("Retrieved {Count} supported crops from ML service", result?.Count);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ML service unavailable for crops list");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("ML service request timed out for crops list");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ML service crops response");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<OptimalConditionsResponse?> GetOptimalConditionsAsync(string cropType, string growthStage = "vegetative", CancellationToken ct = default)
    {
        var endpoint = $"/api/conditions/optimal?crop_type={Uri.EscapeDataString(cropType)}&growth_stage={Uri.EscapeDataString(growthStage)}";
        _logger.LogDebug("Calling ML service: GET {Endpoint}", endpoint);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML service returned {StatusCode} for optimal conditions (crop={CropType})", 
                    response.StatusCode, cropType);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OptimalConditionsResponse>(_jsonOptions, ct);
            _logger.LogDebug("Retrieved optimal conditions for crop {CropType}, stage {Stage}", cropType, growthStage);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ML service unavailable for optimal conditions");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("ML service request timed out for optimal conditions");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize ML service optimal conditions response");
            return null;
        }
    }
}
