using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// Digital Twin for a coordinator/reservoir unit (ESP32-S3)
/// The coordinator manages tower nodes via ESP-NOW and monitors reservoir water quality
/// </summary>
public class CoordinatorTwin
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

    // ============================================================================
    // Digital Twin State
    // ============================================================================

    /// <summary>
    /// State reported by the physical device (read-only from device perspective)
    /// </summary>
    [BsonElement("reported")]
    [JsonPropertyName("reported")]
    public CoordinatorReportedState Reported { get; set; } = new();

    /// <summary>
    /// Desired state set by backend/user (commands to be synced to device)
    /// </summary>
    [BsonElement("desired")]
    [JsonPropertyName("desired")]
    public CoordinatorDesiredState Desired { get; set; } = new();

    /// <summary>
    /// Twin metadata for synchronization tracking
    /// </summary>
    [BsonElement("metadata")]
    [JsonPropertyName("metadata")]
    public TwinMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Coordinator hardware capabilities, set during initial registration
    /// </summary>
    [BsonElement("capabilities")]
    [JsonPropertyName("capabilities")]
    [BsonIgnoreIfNull]
    public CoordinatorCapabilities? Capabilities { get; set; }

    /// <summary>
    /// ML predictions for the reservoir: drift forecasts, consumption rates,
    /// and maintenance recommendations. Updated by the ML scheduler.
    /// </summary>
    [BsonElement("reservoir_ml_predictions")]
    [JsonPropertyName("reservoir_ml_predictions")]
    [BsonIgnoreIfNull]
    public ReservoirMlPredictions? ReservoirMlPredictions { get; set; }
}

/// <summary>
/// State reported by the coordinator device - updated via telemetry
/// </summary>
public class CoordinatorReportedState
{
    // ============================================================================
    // System Status
    // ============================================================================

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

    /// <summary>
    /// Status mode: "operational", "maintenance", "error", "pairing"
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    public string StatusMode { get; set; } = "operational";

    /// <summary>
    /// Coordinator uptime in seconds
    /// </summary>
    [BsonElement("uptime_s")]
    [JsonPropertyName("uptime_s")]
    public long UptimeS { get; set; }

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
    // Reservoir Actuator States (as reported by device)
    // ============================================================================

    /// <summary>
    /// Main circulation pump state (as reported)
    /// </summary>
    [BsonElement("main_pump_on")]
    [JsonPropertyName("main_pump_on")]
    [BsonIgnoreIfNull]
    public bool? MainPumpOn { get; set; }

    /// <summary>
    /// pH adjustment dosing pump state (as reported)
    /// </summary>
    [BsonElement("dosing_pump_ph_on")]
    [JsonPropertyName("dosing_pump_ph_on")]
    [BsonIgnoreIfNull]
    public bool? DosingPumpPhOn { get; set; }

    /// <summary>
    /// Nutrient dosing pump state (as reported)
    /// </summary>
    [BsonElement("dosing_pump_nutrient_on")]
    [JsonPropertyName("dosing_pump_nutrient_on")]
    [BsonIgnoreIfNull]
    public bool? DosingPumpNutrientOn { get; set; }
}

/// <summary>
/// Desired state for coordinator - set by backend/user, to be synced to device
/// </summary>
public class CoordinatorDesiredState
{
    // ============================================================================
    // Actuator Commands
    // ============================================================================

    /// <summary>
    /// Desired main circulation pump state
    /// </summary>
    [BsonElement("main_pump_on")]
    [JsonPropertyName("main_pump_on")]
    public bool? MainPumpOn { get; set; }

    /// <summary>
    /// Desired pH adjustment dosing pump state
    /// </summary>
    [BsonElement("dosing_pump_ph_on")]
    [JsonPropertyName("dosing_pump_ph_on")]
    public bool? DosingPumpPhOn { get; set; }

    /// <summary>
    /// Desired nutrient dosing pump state
    /// </summary>
    [BsonElement("dosing_pump_nutrient_on")]
    [JsonPropertyName("dosing_pump_nutrient_on")]
    public bool? DosingPumpNutrientOn { get; set; }

    /// <summary>
    /// Desired status mode (e.g., "operational", "maintenance", "pairing")
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    public string? StatusMode { get; set; }

    // ============================================================================
    // Setpoints for automated control
    // ============================================================================

    /// <summary>
    /// Reservoir setpoints for automated pH and nutrient control
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

/// <summary>
/// Hardware capabilities reported by coordinator during registration
/// </summary>
public class CoordinatorCapabilities
{
    /// <summary>
    /// Has pH sensor
    /// </summary>
    [BsonElement("ph_sensor")]
    [JsonPropertyName("ph_sensor")]
    public bool PhSensor { get; set; }

    /// <summary>
    /// Has EC/TDS sensor
    /// </summary>
    [BsonElement("ec_sensor")]
    [JsonPropertyName("ec_sensor")]
    public bool EcSensor { get; set; }

    /// <summary>
    /// Has water temperature sensor
    /// </summary>
    [BsonElement("water_temp_sensor")]
    [JsonPropertyName("water_temp_sensor")]
    public bool WaterTempSensor { get; set; }

    /// <summary>
    /// Has water level sensor
    /// </summary>
    [BsonElement("water_level_sensor")]
    [JsonPropertyName("water_level_sensor")]
    public bool WaterLevelSensor { get; set; }

    /// <summary>
    /// Has main circulation pump
    /// </summary>
    [BsonElement("main_pump")]
    [JsonPropertyName("main_pump")]
    public bool MainPump { get; set; }

    /// <summary>
    /// Has pH dosing pump
    /// </summary>
    [BsonElement("ph_dosing_pump")]
    [JsonPropertyName("ph_dosing_pump")]
    public bool PhDosingPump { get; set; }

    /// <summary>
    /// Has nutrient dosing pump
    /// </summary>
    [BsonElement("nutrient_dosing_pump")]
    [JsonPropertyName("nutrient_dosing_pump")]
    public bool NutrientDosingPump { get; set; }

    /// <summary>
    /// Maximum number of tower nodes supported
    /// </summary>
    [BsonElement("max_towers")]
    [JsonPropertyName("max_towers")]
    public int MaxTowers { get; set; } = 8;

    /// <summary>
    /// Has ambient light sensor (legacy smart tile)
    /// </summary>
    [BsonElement("light_sensor")]
    [JsonPropertyName("light_sensor")]
    public bool LightSensor { get; set; }
}
