using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents an ESP32-S3 coordinator with integrated reservoir (hydroponic system)
/// The coordinator manages tower nodes via ESP-NOW and monitors reservoir water quality
/// </summary>
public class Coordinator
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Site ID (legacy) - use FarmId for new hydroponic systems
    /// </summary>
    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Farm ID for hierarchical organization (hydroponic system)
    /// </summary>
    [BsonElement("farm_id")]
    [JsonPropertyName("farm_id")]
    [BsonIgnoreIfNull]
    public string? FarmId { get; set; }

    /// <summary>
    /// Human-readable name for the coordinator/reservoir
    /// </summary>
    [BsonElement("name")]
    [JsonPropertyName("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("fw_version")]
    [JsonPropertyName("fw_version")]
    public string FwVersion { get; set; } = string.Empty;

    /// <summary>
    /// Number of tower nodes currently online (connected via ESP-NOW)
    /// </summary>
    [BsonElement("towers_online")]
    [JsonPropertyName("towers_online")]
    public int TowersOnline { get; set; }

    /// <summary>
    /// Legacy field for tile nodes - kept for backwards compatibility
    /// </summary>
    [BsonElement("nodes_online")]
    [JsonPropertyName("nodes_online")]
    public int NodesOnline { get; set; }

    [BsonElement("wifi_rssi")]
    [JsonPropertyName("wifi_rssi")]
    public int WifiRssi { get; set; }

    // ============================================================================
    // Legacy Smart Tile Fields (kept for backwards compatibility)
    // ============================================================================

    [BsonElement("light_lux")]
    [JsonPropertyName("light_lux")]
    [BsonIgnoreIfNull]
    public float? LightLux { get; set; }

    /// <summary>
    /// Ambient air temperature in Celsius
    /// </summary>
    [BsonElement("temp_c")]
    [JsonPropertyName("temp_c")]
    public float TempC { get; set; }

    // ============================================================================
    // Reservoir Water Quality Sensors (Hydroponic System)
    // ============================================================================

    /// <summary>
    /// Water pH level (0-14 scale, optimal range 5.5-6.5 for hydroponics)
    /// </summary>
    [BsonElement("ph")]
    [JsonPropertyName("ph")]
    [BsonIgnoreIfNull]
    public float? Ph { get; set; }

    /// <summary>
    /// Electrical conductivity in mS/cm (indicates nutrient concentration)
    /// </summary>
    [BsonElement("ec_ms_cm")]
    [JsonPropertyName("ec_ms_cm")]
    [BsonIgnoreIfNull]
    public float? EcMsCm { get; set; }

    /// <summary>
    /// Total dissolved solids in ppm (calculated from EC)
    /// </summary>
    [BsonElement("tds_ppm")]
    [JsonPropertyName("tds_ppm")]
    [BsonIgnoreIfNull]
    public float? TdsPpm { get; set; }

    /// <summary>
    /// Water temperature in Celsius (optimal range 18-24C)
    /// </summary>
    [BsonElement("water_temp_c")]
    [JsonPropertyName("water_temp_c")]
    [BsonIgnoreIfNull]
    public float? WaterTempC { get; set; }

    // ============================================================================
    // Reservoir Water Level
    // ============================================================================

    /// <summary>
    /// Water level as percentage of reservoir capacity (0-100)
    /// </summary>
    [BsonElement("water_level_pct")]
    [JsonPropertyName("water_level_pct")]
    [BsonIgnoreIfNull]
    public float? WaterLevelPct { get; set; }

    /// <summary>
    /// Water level in centimeters from bottom
    /// </summary>
    [BsonElement("water_level_cm")]
    [JsonPropertyName("water_level_cm")]
    [BsonIgnoreIfNull]
    public float? WaterLevelCm { get; set; }

    /// <summary>
    /// Low water alert flag - true if below minimum threshold
    /// </summary>
    [BsonElement("low_water_alert")]
    [JsonPropertyName("low_water_alert")]
    [BsonIgnoreIfNull]
    public bool? LowWaterAlert { get; set; }

    // ============================================================================
    // Reservoir Actuator States
    // ============================================================================

    /// <summary>
    /// Main circulation pump state
    /// </summary>
    [BsonElement("main_pump_on")]
    [JsonPropertyName("main_pump_on")]
    [BsonIgnoreIfNull]
    public bool? MainPumpOn { get; set; }

    /// <summary>
    /// Timestamp when main pump state last changed
    /// </summary>
    [BsonElement("last_pump_change_at")]
    [JsonPropertyName("last_pump_change_at")]
    [BsonIgnoreIfNull]
    public DateTime? LastPumpChangeAt { get; set; }

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
    // System Status
    // ============================================================================

    /// <summary>
    /// Status mode: "operational", "maintenance", "error", "pairing"
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    [BsonIgnoreIfNull]
    public string? StatusMode { get; set; }

    /// <summary>
    /// Coordinator uptime in seconds
    /// </summary>
    [BsonElement("uptime_s")]
    [JsonPropertyName("uptime_s")]
    [BsonIgnoreIfNull]
    public long? UptimeS { get; set; }

    [BsonElement("last_seen")]
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; }

    // ============================================================================
    // Setpoints and Configuration (for automation)
    // ============================================================================

    /// <summary>
    /// Reservoir setpoints for automated control
    /// </summary>
    [BsonElement("setpoints")]
    [JsonPropertyName("setpoints")]
    [BsonIgnoreIfNull]
    public ReservoirSetpoints? Setpoints { get; set; }
}

/// <summary>
/// Reservoir setpoints for automated pH and nutrient control
/// </summary>
public class ReservoirSetpoints
{
    /// <summary>
    /// Target pH value (default 6.0)
    /// </summary>
    [BsonElement("ph_target")]
    [JsonPropertyName("ph_target")]
    public float PhTarget { get; set; } = 6.0f;

    /// <summary>
    /// pH tolerance band (+/- from target before dosing)
    /// </summary>
    [BsonElement("ph_tolerance")]
    [JsonPropertyName("ph_tolerance")]
    public float PhTolerance { get; set; } = 0.3f;

    /// <summary>
    /// Target EC value in mS/cm
    /// </summary>
    [BsonElement("ec_target")]
    [JsonPropertyName("ec_target")]
    public float EcTarget { get; set; } = 1.5f;

    /// <summary>
    /// EC tolerance band before nutrient dosing
    /// </summary>
    [BsonElement("ec_tolerance")]
    [JsonPropertyName("ec_tolerance")]
    public float EcTolerance { get; set; } = 0.2f;

    /// <summary>
    /// Minimum water level percentage before low water alert
    /// </summary>
    [BsonElement("water_level_min_pct")]
    [JsonPropertyName("water_level_min_pct")]
    public float WaterLevelMinPct { get; set; } = 20.0f;

    /// <summary>
    /// Target water temperature in Celsius
    /// </summary>
    [BsonElement("water_temp_target_c")]
    [JsonPropertyName("water_temp_target_c")]
    public float WaterTempTargetC { get; set; } = 20.0f;
}
