using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// Stores ML prediction results for a tower.
/// This is embedded in the TowerTwin document to track prediction history.
/// </summary>
public class MlPredictions
{
    /// <summary>
    /// Predicted plant height in centimeters based on growth model.
    /// </summary>
    [BsonElement("predicted_height_cm")]
    [JsonPropertyName("predicted_height_cm")]
    [BsonIgnoreIfNull]
    public double? PredictedHeightCm { get; set; }

    /// <summary>
    /// Expected harvest date based on crop type and current growth.
    /// </summary>
    [BsonElement("expected_harvest_date")]
    [JsonPropertyName("expected_harvest_date")]
    [BsonIgnoreIfNull]
    public DateTime? ExpectedHarvestDate { get; set; }

    /// <summary>
    /// Days until expected harvest.
    /// </summary>
    [BsonElement("days_to_harvest")]
    [JsonPropertyName("days_to_harvest")]
    [BsonIgnoreIfNull]
    public int? DaysToHarvest { get; set; }

    /// <summary>
    /// Estimated growth rate in cm per day.
    /// </summary>
    [BsonElement("growth_rate_cm_per_day")]
    [JsonPropertyName("growth_rate_cm_per_day")]
    [BsonIgnoreIfNull]
    public double? GrowthRateCmPerDay { get; set; }

    /// <summary>
    /// Plant health score (0-1).
    /// </summary>
    [BsonElement("health_score")]
    [JsonPropertyName("health_score")]
    [BsonIgnoreIfNull]
    public double? HealthScore { get; set; }

    /// <summary>
    /// Prediction confidence (0-1).
    /// </summary>
    [BsonElement("confidence")]
    [JsonPropertyName("confidence")]
    [BsonIgnoreIfNull]
    public double? Confidence { get; set; }

    /// <summary>
    /// Name of the ML model used for prediction.
    /// </summary>
    [BsonElement("model_name")]
    [JsonPropertyName("model_name")]
    [BsonIgnoreIfNull]
    public string? ModelName { get; set; }

    /// <summary>
    /// Version of the ML model used for prediction.
    /// </summary>
    [BsonElement("model_version")]
    [JsonPropertyName("model_version")]
    [BsonIgnoreIfNull]
    public string? ModelVersion { get; set; }

    /// <summary>
    /// Timestamp when the prediction was last updated.
    /// </summary>
    [BsonElement("last_updated_at")]
    [JsonPropertyName("last_updated_at")]
    [BsonIgnoreIfNull]
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    /// Average temperature used for the prediction (in Celsius).
    /// </summary>
    [BsonElement("input_avg_temp_c")]
    [JsonPropertyName("input_avg_temp_c")]
    [BsonIgnoreIfNull]
    public double? InputAvgTempC { get; set; }

    /// <summary>
    /// Average humidity used for the prediction (percentage).
    /// </summary>
    [BsonElement("input_avg_humidity_pct")]
    [JsonPropertyName("input_avg_humidity_pct")]
    [BsonIgnoreIfNull]
    public double? InputAvgHumidityPct { get; set; }

    /// <summary>
    /// Average light level used for the prediction (lux).
    /// </summary>
    [BsonElement("input_avg_light_lux")]
    [JsonPropertyName("input_avg_light_lux")]
    [BsonIgnoreIfNull]
    public double? InputAvgLightLux { get; set; }
}
