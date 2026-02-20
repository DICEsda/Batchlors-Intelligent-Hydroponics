using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Request model for anomaly detection.
/// </summary>
public class AnomalyDetectionRequest
{
    /// <summary>
    /// Optional tower identifier.
    /// </summary>
    [JsonPropertyName("tower_id")]
    public string? TowerId { get; set; }

    /// <summary>
    /// Optional coordinator identifier.
    /// </summary>
    [JsonPropertyName("coord_id")]
    public string? CoordId { get; set; }

    /// <summary>
    /// Telemetry data to analyze for anomalies.
    /// </summary>
    [JsonPropertyName("telemetry")]
    public required TelemetryInput Telemetry { get; set; }
}

/// <summary>
/// Single telemetry reading for anomaly detection.
/// </summary>
public class TelemetryInput
{
    /// <summary>
    /// Optional timestamp of the reading.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    /// <summary>
    /// Air temperature in Celsius.
    /// </summary>
    [JsonPropertyName("air_temp_c")]
    public double? AirTempC { get; set; }

    /// <summary>
    /// Humidity percentage (0-100).
    /// </summary>
    [JsonPropertyName("humidity_pct")]
    public double? HumidityPct { get; set; }

    /// <summary>
    /// Light intensity in lux.
    /// </summary>
    [JsonPropertyName("light_lux")]
    public double? LightLux { get; set; }

    /// <summary>
    /// pH level (0-14).
    /// </summary>
    [JsonPropertyName("ph")]
    public double? Ph { get; set; }

    /// <summary>
    /// Electrical conductivity in mS/cm.
    /// </summary>
    [JsonPropertyName("ec_ms_cm")]
    public double? EcMsCm { get; set; }

    /// <summary>
    /// Total dissolved solids in ppm.
    /// </summary>
    [JsonPropertyName("tds_ppm")]
    public double? TdsPpm { get; set; }

    /// <summary>
    /// Water temperature in Celsius.
    /// </summary>
    [JsonPropertyName("water_temp_c")]
    public double? WaterTempC { get; set; }

    /// <summary>
    /// Water level percentage (0-100).
    /// </summary>
    [JsonPropertyName("water_level_pct")]
    public double? WaterLevelPct { get; set; }
}
