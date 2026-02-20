using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response model containing growth predictions.
/// </summary>
public class GrowthPredictionResponse
{
    /// <summary>
    /// Tower identifier.
    /// </summary>
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Type of crop.
    /// </summary>
    [JsonPropertyName("crop_type")]
    public string CropType { get; set; } = string.Empty;

    /// <summary>
    /// Predicted height in centimeters.
    /// </summary>
    [JsonPropertyName("predicted_height_cm")]
    public double PredictedHeightCm { get; set; }

    /// <summary>
    /// Expected harvest date (ISO format).
    /// </summary>
    [JsonPropertyName("predicted_harvest_date")]
    public string PredictedHarvestDate { get; set; } = string.Empty;

    /// <summary>
    /// Days until harvest.
    /// </summary>
    [JsonPropertyName("days_to_harvest")]
    public int DaysToHarvest { get; set; }

    /// <summary>
    /// Estimated growth rate in cm per day.
    /// </summary>
    [JsonPropertyName("growth_rate_cm_per_day")]
    public double GrowthRateCmPerDay { get; set; }

    /// <summary>
    /// Plant health score (0-1).
    /// </summary>
    [JsonPropertyName("health_score")]
    public double HealthScore { get; set; }

    /// <summary>
    /// Prediction confidence (0-1).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Name of the model used for prediction.
    /// </summary>
    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the model used for prediction.
    /// </summary>
    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the prediction was generated (ISO format).
    /// </summary>
    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;
}
