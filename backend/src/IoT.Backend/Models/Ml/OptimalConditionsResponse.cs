using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response model containing optimal growing conditions for a crop.
/// </summary>
public class OptimalConditionsResponse
{
    /// <summary>
    /// Type of crop.
    /// </summary>
    [JsonPropertyName("crop_type")]
    public string CropType { get; set; } = string.Empty;

    /// <summary>
    /// Growth stage: seedling, vegetative, flowering, fruiting.
    /// </summary>
    [JsonPropertyName("growth_stage")]
    public string GrowthStage { get; set; } = string.Empty;

    /// <summary>
    /// Minimum temperature in Celsius.
    /// </summary>
    [JsonPropertyName("temp_min_c")]
    public double TempMinC { get; set; }

    /// <summary>
    /// Maximum temperature in Celsius.
    /// </summary>
    [JsonPropertyName("temp_max_c")]
    public double TempMaxC { get; set; }

    /// <summary>
    /// Optimal temperature in Celsius.
    /// </summary>
    [JsonPropertyName("temp_optimal_c")]
    public double TempOptimalC { get; set; }

    /// <summary>
    /// Minimum humidity percentage.
    /// </summary>
    [JsonPropertyName("humidity_min_pct")]
    public double HumidityMinPct { get; set; }

    /// <summary>
    /// Maximum humidity percentage.
    /// </summary>
    [JsonPropertyName("humidity_max_pct")]
    public double HumidityMaxPct { get; set; }

    /// <summary>
    /// Optimal humidity percentage.
    /// </summary>
    [JsonPropertyName("humidity_optimal_pct")]
    public double HumidityOptimalPct { get; set; }

    /// <summary>
    /// Minimum light intensity in lux.
    /// </summary>
    [JsonPropertyName("light_min_lux")]
    public double LightMinLux { get; set; }

    /// <summary>
    /// Maximum light intensity in lux.
    /// </summary>
    [JsonPropertyName("light_max_lux")]
    public double LightMaxLux { get; set; }

    /// <summary>
    /// Recommended light hours per day.
    /// </summary>
    [JsonPropertyName("light_hours_per_day")]
    public int LightHoursPerDay { get; set; }

    /// <summary>
    /// Minimum pH level.
    /// </summary>
    [JsonPropertyName("ph_min")]
    public double PhMin { get; set; }

    /// <summary>
    /// Maximum pH level.
    /// </summary>
    [JsonPropertyName("ph_max")]
    public double PhMax { get; set; }

    /// <summary>
    /// Optimal pH level.
    /// </summary>
    [JsonPropertyName("ph_optimal")]
    public double PhOptimal { get; set; }

    /// <summary>
    /// Minimum EC in mS/cm.
    /// </summary>
    [JsonPropertyName("ec_min_ms_cm")]
    public double EcMinMsCm { get; set; }

    /// <summary>
    /// Maximum EC in mS/cm.
    /// </summary>
    [JsonPropertyName("ec_max_ms_cm")]
    public double EcMaxMsCm { get; set; }

    /// <summary>
    /// Optimal EC in mS/cm.
    /// </summary>
    [JsonPropertyName("ec_optimal_ms_cm")]
    public double EcOptimalMsCm { get; set; }

    /// <summary>
    /// Expected days to harvest.
    /// </summary>
    [JsonPropertyName("expected_days_to_harvest")]
    public int ExpectedDaysToHarvest { get; set; }

    /// <summary>
    /// Expected height in centimeters at harvest.
    /// </summary>
    [JsonPropertyName("expected_height_cm")]
    public double ExpectedHeightCm { get; set; }
}
