using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// ML prediction results for a reservoir / coordinator.
/// Embedded in CoordinatorTwin to store drift forecasts and consumption rates.
/// </summary>
public class ReservoirMlPredictions
{
    // ========================================================================
    // Drift Forecasts (predicted future values at multiple horizons)
    // ========================================================================

    /// <summary>
    /// Predicted pH value in 1 hour.
    /// </summary>
    [BsonElement("predicted_ph_1h")]
    [JsonPropertyName("predicted_ph_1h")]
    [BsonIgnoreIfNull]
    public double? PredictedPh1H { get; set; }

    /// <summary>
    /// Predicted pH value in 6 hours.
    /// </summary>
    [BsonElement("predicted_ph_6h")]
    [JsonPropertyName("predicted_ph_6h")]
    [BsonIgnoreIfNull]
    public double? PredictedPh6H { get; set; }

    /// <summary>
    /// Predicted pH value in 24 hours.
    /// </summary>
    [BsonElement("predicted_ph_24h")]
    [JsonPropertyName("predicted_ph_24h")]
    [BsonIgnoreIfNull]
    public double? PredictedPh24H { get; set; }

    /// <summary>
    /// Predicted EC (mS/cm) in 1 hour.
    /// </summary>
    [BsonElement("predicted_ec_1h")]
    [JsonPropertyName("predicted_ec_1h")]
    [BsonIgnoreIfNull]
    public double? PredictedEc1H { get; set; }

    /// <summary>
    /// Predicted EC (mS/cm) in 6 hours.
    /// </summary>
    [BsonElement("predicted_ec_6h")]
    [JsonPropertyName("predicted_ec_6h")]
    [BsonIgnoreIfNull]
    public double? PredictedEc6H { get; set; }

    /// <summary>
    /// Predicted EC (mS/cm) in 24 hours.
    /// </summary>
    [BsonElement("predicted_ec_24h")]
    [JsonPropertyName("predicted_ec_24h")]
    [BsonIgnoreIfNull]
    public double? PredictedEc24H { get; set; }

    /// <summary>
    /// Predicted water level (%) in 1 hour.
    /// </summary>
    [BsonElement("predicted_water_level_1h")]
    [JsonPropertyName("predicted_water_level_1h")]
    [BsonIgnoreIfNull]
    public double? PredictedWaterLevel1H { get; set; }

    /// <summary>
    /// Predicted water level (%) in 6 hours.
    /// </summary>
    [BsonElement("predicted_water_level_6h")]
    [JsonPropertyName("predicted_water_level_6h")]
    [BsonIgnoreIfNull]
    public double? PredictedWaterLevel6H { get; set; }

    /// <summary>
    /// Predicted water level (%) in 24 hours.
    /// </summary>
    [BsonElement("predicted_water_level_24h")]
    [JsonPropertyName("predicted_water_level_24h")]
    [BsonIgnoreIfNull]
    public double? PredictedWaterLevel24H { get; set; }

    // ========================================================================
    // Time-to-threshold estimates
    // ========================================================================

    /// <summary>
    /// Estimated hours until pH drifts outside acceptable range (5.5-7.0).
    /// </summary>
    [BsonElement("hours_to_ph_threshold")]
    [JsonPropertyName("hours_to_ph_threshold")]
    [BsonIgnoreIfNull]
    public double? HoursToPhThreshold { get; set; }

    /// <summary>
    /// Estimated hours until EC drifts outside acceptable range.
    /// </summary>
    [BsonElement("hours_to_ec_threshold")]
    [JsonPropertyName("hours_to_ec_threshold")]
    [BsonIgnoreIfNull]
    public double? HoursToEcThreshold { get; set; }

    // ========================================================================
    // Consumption / Depletion Rates
    // ========================================================================

    /// <summary>
    /// EC depletion rate in mS/cm per hour (negative = depleting).
    /// </summary>
    [BsonElement("ec_depletion_rate_per_hour")]
    [JsonPropertyName("ec_depletion_rate_per_hour")]
    [BsonIgnoreIfNull]
    public double? EcDepletionRatePerHour { get; set; }

    /// <summary>
    /// pH drift rate in units per hour.
    /// </summary>
    [BsonElement("ph_drift_rate_per_hour")]
    [JsonPropertyName("ph_drift_rate_per_hour")]
    [BsonIgnoreIfNull]
    public double? PhDriftRatePerHour { get; set; }

    /// <summary>
    /// Water consumption rate in % per hour.
    /// </summary>
    [BsonElement("water_consumption_rate_per_hour")]
    [JsonPropertyName("water_consumption_rate_per_hour")]
    [BsonIgnoreIfNull]
    public double? WaterConsumptionRatePerHour { get; set; }

    // ========================================================================
    // Derived Recommendations
    // ========================================================================

    /// <summary>
    /// Recommended days until next full water change.
    /// </summary>
    [BsonElement("water_change_recommended_in_days")]
    [JsonPropertyName("water_change_recommended_in_days")]
    [BsonIgnoreIfNull]
    public double? WaterChangeRecommendedInDays { get; set; }

    /// <summary>
    /// Whether a nutrient top-up is recommended soon.
    /// </summary>
    [BsonElement("nutrient_top_up_recommended")]
    [JsonPropertyName("nutrient_top_up_recommended")]
    [BsonIgnoreIfNull]
    public bool? NutrientTopUpRecommended { get; set; }

    // ========================================================================
    // Metadata
    // ========================================================================

    /// <summary>
    /// Name of the ML model that produced these predictions.
    /// </summary>
    [BsonElement("model_name")]
    [JsonPropertyName("model_name")]
    [BsonIgnoreIfNull]
    public string? ModelName { get; set; }

    /// <summary>
    /// Model version string.
    /// </summary>
    [BsonElement("model_version")]
    [JsonPropertyName("model_version")]
    [BsonIgnoreIfNull]
    public string? ModelVersion { get; set; }

    /// <summary>
    /// Prediction confidence (0-1).
    /// </summary>
    [BsonElement("confidence")]
    [JsonPropertyName("confidence")]
    [BsonIgnoreIfNull]
    public double? Confidence { get; set; }

    /// <summary>
    /// Timestamp when predictions were last updated.
    /// </summary>
    [BsonElement("last_updated_at")]
    [JsonPropertyName("last_updated_at")]
    [BsonIgnoreIfNull]
    public DateTime? LastUpdatedAt { get; set; }
}
