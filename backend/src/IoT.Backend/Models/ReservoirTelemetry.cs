using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Time-series telemetry record for reservoir/coordinator water quality data.
/// Stored separately from the Coordinator model for historical analysis and trending.
/// </summary>
public class ReservoirTelemetry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Coordinator ID that this telemetry came from
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
    // Water Quality Sensors
    // ============================================================================

    /// <summary>
    /// Water pH level (0-14 scale, target typically 5.5-6.5 for hydroponics)
    /// </summary>
    [BsonElement("ph")]
    [JsonPropertyName("ph")]
    [BsonIgnoreIfNull]
    public float? Ph { get; set; }

    /// <summary>
    /// Electrical conductivity in mS/cm (nutrient concentration indicator)
    /// </summary>
    [BsonElement("ec_ms_cm")]
    [JsonPropertyName("ec_ms_cm")]
    [BsonIgnoreIfNull]
    public float? EcMsCm { get; set; }

    /// <summary>
    /// Total dissolved solids in parts per million (derived from EC)
    /// </summary>
    [BsonElement("tds_ppm")]
    [JsonPropertyName("tds_ppm")]
    [BsonIgnoreIfNull]
    public float? TdsPpm { get; set; }

    /// <summary>
    /// Water temperature in Celsius (target typically 18-24°C)
    /// </summary>
    [BsonElement("water_temp_c")]
    [JsonPropertyName("water_temp_c")]
    [BsonIgnoreIfNull]
    public float? WaterTempC { get; set; }

    // ============================================================================
    // Water Level
    // ============================================================================

    /// <summary>
    /// Water level as percentage of reservoir capacity
    /// </summary>
    [BsonElement("water_level_pct")]
    [JsonPropertyName("water_level_pct")]
    [BsonIgnoreIfNull]
    public float? WaterLevelPct { get; set; }

    /// <summary>
    /// Water level in centimeters (raw sensor reading)
    /// </summary>
    [BsonElement("water_level_cm")]
    [JsonPropertyName("water_level_cm")]
    [BsonIgnoreIfNull]
    public float? WaterLevelCm { get; set; }

    // ============================================================================
    // Actuator States (snapshot at telemetry time)
    // ============================================================================

    /// <summary>
    /// Main circulation pump state
    /// </summary>
    [BsonElement("main_pump_on")]
    [JsonPropertyName("main_pump_on")]
    [BsonIgnoreIfNull]
    public bool? MainPumpOn { get; set; }

    /// <summary>
    /// pH adjustment dosing pump state
    /// </summary>
    [BsonElement("dosing_pump_ph_on")]
    [JsonPropertyName("dosing_pump_ph_on")]
    [BsonIgnoreIfNull]
    public bool? DosingPumpPhOn { get; set; }

    /// <summary>
    /// Nutrient dosing pump state
    /// </summary>
    [BsonElement("dosing_pump_nutrient_on")]
    [JsonPropertyName("dosing_pump_nutrient_on")]
    [BsonIgnoreIfNull]
    public bool? DosingPumpNutrientOn { get; set; }

    // ============================================================================
    // System Status (snapshot at telemetry time)
    // ============================================================================

    /// <summary>
    /// WiFi signal strength in dBm
    /// </summary>
    [BsonElement("wifi_rssi")]
    [JsonPropertyName("wifi_rssi")]
    [BsonIgnoreIfNull]
    public int? WifiRssi { get; set; }

    /// <summary>
    /// Number of towers online at telemetry time
    /// </summary>
    [BsonElement("towers_online")]
    [JsonPropertyName("towers_online")]
    [BsonIgnoreIfNull]
    public int? TowersOnline { get; set; }
}
