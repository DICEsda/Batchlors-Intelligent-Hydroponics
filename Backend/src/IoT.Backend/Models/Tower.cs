using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a hydroponic tower node (ESP32-C3)
/// Tower nodes connect to the coordinator via ESP-NOW and report
/// air temperature, humidity, light levels, and control grow lights/pumps
/// </summary>
public class Tower
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
    /// Tower's MAC address (stored during pairing)
    /// </summary>
    [BsonElement("mac_address")]
    [JsonPropertyName("mac_address")]
    [BsonIgnoreIfNull]
    public string? MacAddress { get; set; }

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
    // Actuator States
    // ============================================================================

    /// <summary>
    /// Current state of the tower's water pump/valve
    /// </summary>
    [BsonElement("pump_on")]
    [JsonPropertyName("pump_on")]
    public bool PumpOn { get; set; }

    /// <summary>
    /// Current state of the grow light
    /// </summary>
    [BsonElement("light_on")]
    [JsonPropertyName("light_on")]
    public bool LightOn { get; set; }

    /// <summary>
    /// Grow light brightness level (0-255, if PWM supported)
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
    /// Last time telemetry was received from this tower
    /// </summary>
    [BsonElement("last_seen")]
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// When the tower was first registered
    /// </summary>
    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the tower document was last updated
    /// </summary>
    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // ============================================================================
    // Tower Capabilities (set during pairing)
    // ============================================================================

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
}

/// <summary>
/// Hardware capabilities reported by tower during pairing
/// </summary>
public class TowerCapabilities
{
    /// <summary>
    /// Has DHT22 temperature/humidity sensor
    /// </summary>
    [BsonElement("dht_sensor")]
    [JsonPropertyName("dht_sensor")]
    public bool DhtSensor { get; set; }

    /// <summary>
    /// Has ambient light sensor
    /// </summary>
    [BsonElement("light_sensor")]
    [JsonPropertyName("light_sensor")]
    public bool LightSensor { get; set; }

    /// <summary>
    /// Has pump/valve relay output
    /// </summary>
    [BsonElement("pump_relay")]
    [JsonPropertyName("pump_relay")]
    public bool PumpRelay { get; set; }

    /// <summary>
    /// Has grow light output (PWM or on/off)
    /// </summary>
    [BsonElement("grow_light")]
    [JsonPropertyName("grow_light")]
    public bool GrowLight { get; set; }

    /// <summary>
    /// Number of plant slots (default 6)
    /// </summary>
    [BsonElement("slot_count")]
    [JsonPropertyName("slot_count")]
    public int SlotCount { get; set; } = 6;
}
