using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response from the ML service for nutrient consumption prediction.
/// </summary>
public class ConsumptionPredictionResponse
{
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    [JsonPropertyName("rates")]
    public List<ConsumptionRate> Rates { get; set; } = new();

    [JsonPropertyName("water_change_recommended_in_days")]
    public double? WaterChangeRecommendedInDays { get; set; }

    [JsonPropertyName("nutrient_top_up_recommended")]
    public bool NutrientTopUpRecommended { get; set; }

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
/// Predicted depletion rate for a single reservoir metric.
/// </summary>
public class ConsumptionRate
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; } = string.Empty;

    [JsonPropertyName("rate_per_hour")]
    public double RatePerHour { get; set; }

    [JsonPropertyName("hours_until_critical")]
    public double? HoursUntilCritical { get; set; }
}
