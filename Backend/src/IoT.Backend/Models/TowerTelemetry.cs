using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Time-series telemetry record for tower environmental and actuator data.
/// Stored separately from the Tower model for historical analysis and trending.
/// </summary>
public class TowerTelemetry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Tower ID that this telemetry came from
    /// </summary>
    [BsonElement("tower_id")]
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Parent coordinator ID
    /// </summary>
    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Farm ID for hierarchical organization and querying
    /// </summary>
    [BsonElement("farm_id")]
    [JsonPropertyName("farm_id")]
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this telemetry was recorded (UTC)
    /// </summary>
    [BsonElement("timestamp")]
    [JsonPropertyName("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; }

    // ============================================================================
    // Environmental Sensors (from DHT22 and light sensor)
    // ============================================================================

    /// <summary>
    /// Air temperature in Celsius (from DHT22 sensor)
    /// </summary>
    [BsonElement("air_temp_c")]
    [JsonPropertyName("air_temp_c")]
    public float AirTempC { get; set; }

    /// <summary>
    /// Relative humidity percentage (from DHT22 sensor)
    /// </summary>
    [BsonElement("humidity_pct")]
    [JsonPropertyName("humidity_pct")]
    public float HumidityPct { get; set; }

    /// <summary>
    /// Ambient light level in lux (from optional light sensor)
    /// </summary>
    [BsonElement("light_lux")]
    [JsonPropertyName("light_lux")]
    public float LightLux { get; set; }

    // ============================================================================
    // Actuator States (snapshot at telemetry time)
    // ============================================================================

    /// <summary>
    /// Water pump/valve state at telemetry time
    /// </summary>
    [BsonElement("pump_on")]
    [JsonPropertyName("pump_on")]
    public bool PumpOn { get; set; }

    /// <summary>
    /// Grow light state at telemetry time
    /// </summary>
    [BsonElement("light_on")]
    [JsonPropertyName("light_on")]
    public bool LightOn { get; set; }

    /// <summary>
    /// Grow light brightness level (0-255)
    /// </summary>
    [BsonElement("light_brightness")]
    [JsonPropertyName("light_brightness")]
    public int LightBrightness { get; set; }

    // ============================================================================
    // System Status (snapshot at telemetry time)
    // ============================================================================

    /// <summary>
    /// Battery/supply voltage in millivolts
    /// </summary>
    [BsonElement("vbat_mv")]
    [JsonPropertyName("vbat_mv")]
    [BsonIgnoreIfNull]
    public int? VbatMv { get; set; }

    /// <summary>
    /// ESP-NOW signal quality to coordinator (RSSI or similar)
    /// </summary>
    [BsonElement("signal_quality")]
    [JsonPropertyName("signal_quality")]
    [BsonIgnoreIfNull]
    public int? SignalQuality { get; set; }

    /// <summary>
    /// Tower status mode at telemetry time
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    [BsonIgnoreIfNull]
    public string? StatusMode { get; set; }
}
