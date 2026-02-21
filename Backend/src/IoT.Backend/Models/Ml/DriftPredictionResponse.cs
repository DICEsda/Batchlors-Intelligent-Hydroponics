using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response from the ML service for reservoir drift prediction.
/// </summary>
public class DriftPredictionResponse
{
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    [JsonPropertyName("forecasts")]
    public List<MetricForecast> Forecasts { get; set; } = new();

    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;
}

/// <summary>
/// Forecast for a single reservoir metric at multiple time horizons.
/// </summary>
public class MetricForecast
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; } = string.Empty;

    [JsonPropertyName("current_value")]
    public double? CurrentValue { get; set; }

    [JsonPropertyName("predicted_1h")]
    public double? Predicted1H { get; set; }

    [JsonPropertyName("predicted_6h")]
    public double? Predicted6H { get; set; }

    [JsonPropertyName("predicted_24h")]
    public double? Predicted24H { get; set; }

    [JsonPropertyName("time_to_threshold_hours")]
    public double? TimeToThresholdHours { get; set; }
}
