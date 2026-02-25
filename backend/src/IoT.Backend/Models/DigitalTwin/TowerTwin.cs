using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// Digital Twin for a hydroponic tower node (ESP32-C3)
/// Separates reported state (from device) from desired state (from backend/user)
/// </summary>
public class TowerTwin
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Unique tower identifier assigned during pairing
    /// </summary>
    [BsonElement("tower_id")]
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Parent coordinator ID that this tower is paired with
    /// </summary>
    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Farm ID for hierarchical organization
    /// </summary>
    [BsonElement("farm_id")]
    [JsonPropertyName("farm_id")]
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the tower
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
    public TowerReportedState Reported { get; set; } = new();

    /// <summary>
    /// Desired state set by backend/user (commands to be synced to device)
    /// </summary>
    [BsonElement("desired")]
    [JsonPropertyName("desired")]
    public TowerDesiredState Desired { get; set; } = new();

    /// <summary>
    /// Twin metadata for synchronization tracking
    /// </summary>
    [BsonElement("metadata")]
    [JsonPropertyName("metadata")]
    public TwinMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Tower hardware capabilities, set during initial pairing
    /// </summary>
    [BsonElement("capabilities")]
    [JsonPropertyName("capabilities")]
    [BsonIgnoreIfNull]
    public TowerCapabilities? Capabilities { get; set; }

    // ============================================================================
    // Crop Tracking & Growth Monitoring
    // ============================================================================

    /// <summary>
    /// Type of crop currently growing in this tower
    /// </summary>
    [BsonElement("crop_type")]
    [JsonPropertyName("crop_type")]
    public CropType CropType { get; set; } = CropType.Unknown;

    /// <summary>
    /// Date when the current crop was planted
    /// </summary>
    [BsonElement("planting_date")]
    [JsonPropertyName("planting_date")]
    [BsonIgnoreIfNull]
    public DateTime? PlantingDate { get; set; }

    /// <summary>
    /// Last measured plant height in centimeters
    /// </summary>
    [BsonElement("last_height_cm")]
    [JsonPropertyName("last_height_cm")]
    [BsonIgnoreIfNull]
    public float? LastHeightCm { get; set; }

    /// <summary>
    /// Timestamp of the last height measurement
    /// </summary>
    [BsonElement("last_height_at")]
    [JsonPropertyName("last_height_at")]
    [BsonIgnoreIfNull]
    public DateTime? LastHeightAt { get; set; }

    /// <summary>
    /// ML-predicted height in centimeters (based on growth model)
    /// </summary>
    [BsonElement("predicted_height_cm")]
    [JsonPropertyName("predicted_height_cm")]
    [BsonIgnoreIfNull]
    public float? PredictedHeightCm { get; set; }

    /// <summary>
    /// Expected harvest date based on crop type and planting date
    /// </summary>
    [BsonElement("expected_harvest_date")]
    [JsonPropertyName("expected_harvest_date")]
    [BsonIgnoreIfNull]
    public DateTime? ExpectedHarvestDate { get; set; }

    /// <summary>
    /// ML predictions for this tower (growth, harvest date, health score, etc.)
    /// Updated periodically by the MlSchedulerBackgroundService.
    /// </summary>
    [BsonElement("ml_predictions")]
    [JsonPropertyName("ml_predictions")]
    [BsonIgnoreIfNull]
    public MlPredictions? MlPredictions { get; set; }
}

/// <summary>
/// State reported by the tower device - updated via telemetry
/// </summary>
public class TowerReportedState
{
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
    // Actuator States (as reported by device)
    // ============================================================================

    /// <summary>
    /// Current state of the tower's water pump/valve (as reported)
    /// </summary>
    [BsonElement("pump_on")]
    [JsonPropertyName("pump_on")]
    public bool PumpOn { get; set; }

    /// <summary>
    /// Current state of the grow light (as reported)
    /// </summary>
    [BsonElement("light_on")]
    [JsonPropertyName("light_on")]
    public bool LightOn { get; set; }

    /// <summary>
    /// Grow light brightness level 0-255 (as reported)
    /// </summary>
    [BsonElement("light_brightness")]
    [JsonPropertyName("light_brightness")]
    public int LightBrightness { get; set; }

    // ============================================================================
    // System Status
    // ============================================================================

    /// <summary>
    /// Tower status mode: "operational", "pairing", "ota", "error", "idle"
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    public string StatusMode { get; set; } = "operational";

    /// <summary>
    /// Battery/supply voltage in millivolts
    /// </summary>
    [BsonElement("vbat_mv")]
    [JsonPropertyName("vbat_mv")]
    public int VbatMv { get; set; }

    /// <summary>
    /// Firmware version string
    /// </summary>
    [BsonElement("fw_version")]
    [JsonPropertyName("fw_version")]
    public string FwVersion { get; set; } = string.Empty;

    /// <summary>
    /// Tower uptime in seconds
    /// </summary>
    [BsonElement("uptime_s")]
    [JsonPropertyName("uptime_s")]
    public long UptimeS { get; set; }

    /// <summary>
    /// ESP-NOW signal quality to coordinator (RSSI or similar)
    /// </summary>
    [BsonElement("signal_quality")]
    [JsonPropertyName("signal_quality")]
    public int? SignalQuality { get; set; }
}

/// <summary>
/// Desired state for tower - set by backend/user, to be synced to device
/// </summary>
public class TowerDesiredState
{
    /// <summary>
    /// Desired pump state
    /// </summary>
    [BsonElement("pump_on")]
    [JsonPropertyName("pump_on")]
    public bool? PumpOn { get; set; }

    /// <summary>
    /// Desired grow light state
    /// </summary>
    [BsonElement("light_on")]
    [JsonPropertyName("light_on")]
    public bool? LightOn { get; set; }

    /// <summary>
    /// Desired grow light brightness (0-255)
    /// </summary>
    [BsonElement("light_brightness")]
    [JsonPropertyName("light_brightness")]
    public int? LightBrightness { get; set; }

    /// <summary>
    /// Desired status mode (e.g., "operational", "ota")
    /// </summary>
    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    public string? StatusMode { get; set; }
}

// TowerCapabilities is defined in Models/Tower.cs - use that class instead of duplicating
