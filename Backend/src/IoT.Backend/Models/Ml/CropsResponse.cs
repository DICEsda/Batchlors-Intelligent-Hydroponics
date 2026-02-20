using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response model containing list of supported crops.
/// </summary>
public class CropsResponse
{
    /// <summary>
    /// List of supported crops.
    /// </summary>
    [JsonPropertyName("crops")]
    public List<CropInfo> Crops { get; set; } = new();

    /// <summary>
    /// Total count of supported crops.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Information about a supported crop type.
/// </summary>
public class CropInfo
{
    /// <summary>
    /// Name of the crop.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Expected days from planting to harvest.
    /// </summary>
    [JsonPropertyName("days_to_harvest")]
    public int DaysToHarvest { get; set; }

    /// <summary>
    /// Expected height at harvest in centimeters.
    /// </summary>
    [JsonPropertyName("expected_height_cm")]
    public double ExpectedHeightCm { get; set; }

    /// <summary>
    /// Acceptable pH range (e.g., "5.5-6.5").
    /// </summary>
    [JsonPropertyName("ph_range")]
    public string PhRange { get; set; } = string.Empty;

    /// <summary>
    /// Acceptable EC range in mS/cm (e.g., "1.0-2.0").
    /// </summary>
    [JsonPropertyName("ec_range_ms_cm")]
    public string EcRangeMsCm { get; set; } = string.Empty;
}
