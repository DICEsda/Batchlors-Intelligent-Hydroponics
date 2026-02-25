using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Request model for plant growth prediction.
/// </summary>
public class GrowthPredictionRequest
{
    /// <summary>
    /// Tower identifier.
    /// </summary>
    [JsonPropertyName("tower_id")]
    public required string TowerId { get; set; }

    /// <summary>
    /// Type of crop (e.g., 'Lettuce', 'Basil').
    /// </summary>
    [JsonPropertyName("crop_type")]
    public required string CropType { get; set; }

    /// <summary>
    /// Current plant height in centimeters.
    /// </summary>
    [JsonPropertyName("current_height_cm")]
    public required double CurrentHeightCm { get; set; }

    /// <summary>
    /// Days since planting.
    /// </summary>
    [JsonPropertyName("days_since_planting")]
    public required int DaysSincePlanting { get; set; }

    /// <summary>
    /// Optional average temperature in Celsius.
    /// </summary>
    [JsonPropertyName("avg_temp_c")]
    public double? AvgTempC { get; set; }

    /// <summary>
    /// Optional average humidity percentage (0-100).
    /// </summary>
    [JsonPropertyName("avg_humidity_pct")]
    public double? AvgHumidityPct { get; set; }

    /// <summary>
    /// Optional average light intensity in lux.
    /// </summary>
    [JsonPropertyName("avg_light_lux")]
    public double? AvgLightLux { get; set; }

    /// <summary>
    /// Optional average pH level (0-14).
    /// </summary>
    [JsonPropertyName("avg_ph")]
    public double? AvgPh { get; set; }

    /// <summary>
    /// Optional average electrical conductivity in mS/cm.
    /// </summary>
    [JsonPropertyName("avg_ec_ms_cm")]
    public double? AvgEcMsCm { get; set; }
}
