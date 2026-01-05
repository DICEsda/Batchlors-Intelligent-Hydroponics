using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Requests;

// ============================================================================
// Legacy Smart Tile Telemetry DTOs
// ============================================================================

/// <summary>
/// DTO for coordinator telemetry from legacy smart tile system.
/// Topic: site/{siteId}/coord/{coordId}/telemetry
/// </summary>
public class CoordinatorTelemetryDto
{
    [JsonPropertyName("fw_version")]
    public string? FwVersion { get; set; }

    [JsonPropertyName("nodes_online")]
    public int NodesOnline { get; set; }

    [JsonPropertyName("wifi_rssi")]
    public int WifiRssi { get; set; }

    [JsonPropertyName("light_lux")]
    public float LightLux { get; set; }

    [JsonPropertyName("temp_c")]
    public float TempC { get; set; }
}

/// <summary>
/// DTO for status updates from devices.
/// </summary>
public class StatusUpdateDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

/// <summary>
/// DTO for OTA progress updates.
/// Topic: site/{siteId}/ota/progress
/// </summary>
public class OtaProgressUpdateDto
{
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }

    [JsonPropertyName("devices_total")]
    public int? DevicesTotal { get; set; }

    [JsonPropertyName("devices_updated")]
    public int? DevicesUpdated { get; set; }

    [JsonPropertyName("devices_failed")]
    public int? DevicesFailed { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

// ============================================================================
// Hydroponic System Telemetry DTOs
// ============================================================================

/// <summary>
/// DTO for reservoir telemetry from coordinator with water quality sensors.
/// Topic: farm/{farmId}/coord/{coordId}/reservoir/telemetry
/// </summary>
public class ReservoirTelemetryDto
{
    // ---- System Status ----

    [JsonPropertyName("fw_version")]
    public string? FwVersion { get; set; }

    [JsonPropertyName("towers_online")]
    public int TowersOnline { get; set; }

    [JsonPropertyName("wifi_rssi")]
    public int WifiRssi { get; set; }

    [JsonPropertyName("status_mode")]
    public string? StatusMode { get; set; }

    [JsonPropertyName("uptime_s")]
    public long UptimeS { get; set; }

    [JsonPropertyName("temp_c")]
    public float TempC { get; set; }

    // ---- Water Quality Sensors ----

    /// <summary>
    /// Water pH level (0-14 scale, target typically 5.5-6.5 for hydroponics)
    /// </summary>
    [JsonPropertyName("ph")]
    public float? Ph { get; set; }

    /// <summary>
    /// Electrical conductivity in mS/cm (nutrient concentration indicator)
    /// </summary>
    [JsonPropertyName("ec_ms_cm")]
    public float? EcMsCm { get; set; }

    /// <summary>
    /// Total dissolved solids in parts per million (derived from EC)
    /// </summary>
    [JsonPropertyName("tds_ppm")]
    public float? TdsPpm { get; set; }

    /// <summary>
    /// Water temperature in Celsius (target typically 18-24C)
    /// </summary>
    [JsonPropertyName("water_temp_c")]
    public float? WaterTempC { get; set; }

    // ---- Water Level ----

    /// <summary>
    /// Water level as percentage of reservoir capacity
    /// </summary>
    [JsonPropertyName("water_level_pct")]
    public float? WaterLevelPct { get; set; }

    /// <summary>
    /// Water level in centimeters (raw sensor reading)
    /// </summary>
    [JsonPropertyName("water_level_cm")]
    public float? WaterLevelCm { get; set; }

    /// <summary>
    /// True if water level is below minimum threshold
    /// </summary>
    [JsonPropertyName("low_water_alert")]
    public bool? LowWaterAlert { get; set; }

    // ---- Actuator States (as reported) ----

    /// <summary>
    /// Main circulation pump state
    /// </summary>
    [JsonPropertyName("main_pump_on")]
    public bool? MainPumpOn { get; set; }

    /// <summary>
    /// pH adjustment dosing pump state
    /// </summary>
    [JsonPropertyName("dosing_pump_ph_on")]
    public bool? DosingPumpPhOn { get; set; }

    /// <summary>
    /// Nutrient dosing pump state
    /// </summary>
    [JsonPropertyName("dosing_pump_nutrient_on")]
    public bool? DosingPumpNutrientOn { get; set; }
}

/// <summary>
/// DTO for tower telemetry (DHT22, light sensor, actuator states).
/// Topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry
/// </summary>
public class TowerTelemetryDto
{
    // ---- Environmental Sensors (from DHT22 and light sensor) ----

    /// <summary>
    /// Air temperature in Celsius (from DHT22 sensor)
    /// </summary>
    [JsonPropertyName("air_temp_c")]
    public float AirTempC { get; set; }

    /// <summary>
    /// Relative humidity percentage (from DHT22 sensor)
    /// </summary>
    [JsonPropertyName("humidity_pct")]
    public float HumidityPct { get; set; }

    /// <summary>
    /// Ambient light level in lux (from optional light sensor)
    /// </summary>
    [JsonPropertyName("light_lux")]
    public float LightLux { get; set; }

    // ---- Actuator States (as reported) ----

    /// <summary>
    /// Water pump/valve state at telemetry time
    /// </summary>
    [JsonPropertyName("pump_on")]
    public bool PumpOn { get; set; }

    /// <summary>
    /// Grow light state at telemetry time
    /// </summary>
    [JsonPropertyName("light_on")]
    public bool LightOn { get; set; }

    /// <summary>
    /// Grow light brightness level (0-255)
    /// </summary>
    [JsonPropertyName("light_brightness")]
    public int LightBrightness { get; set; }

    // ---- System Status ----

    /// <summary>
    /// Tower status mode (operational, pairing, ota, error)
    /// </summary>
    [JsonPropertyName("status_mode")]
    public string? StatusMode { get; set; }

    /// <summary>
    /// Battery/supply voltage in millivolts
    /// </summary>
    [JsonPropertyName("vbat_mv")]
    public int VbatMv { get; set; }

    /// <summary>
    /// Firmware version string
    /// </summary>
    [JsonPropertyName("fw_version")]
    public string? FwVersion { get; set; }

    /// <summary>
    /// System uptime in seconds
    /// </summary>
    [JsonPropertyName("uptime_s")]
    public long UptimeS { get; set; }

    /// <summary>
    /// ESP-NOW signal quality to coordinator (RSSI or similar)
    /// </summary>
    [JsonPropertyName("signal_quality")]
    public int? SignalQuality { get; set; }
}

/// <summary>
/// DTO for tower status updates (online/offline, mode changes).
/// Topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/status
/// </summary>
public class TowerStatusDto
{
    /// <summary>
    /// Whether the tower is currently online
    /// </summary>
    [JsonPropertyName("online")]
    public bool? Online { get; set; }

    /// <summary>
    /// Current status mode (operational, pairing, ota, error)
    /// </summary>
    [JsonPropertyName("status_mode")]
    public string? StatusMode { get; set; }

    /// <summary>
    /// Command acknowledgment - true if tower has applied the commanded desired state
    /// </summary>
    [JsonPropertyName("cmd_ack")]
    public bool? CmdAck { get; set; }
}

// ============================================================================
// Pairing Workflow DTOs
// ============================================================================

/// <summary>
/// DTO for incoming tower pairing request from MQTT.
/// Topic: farm/{farmId}/coord/{coordId}/pairing/request
/// </summary>
public class PairingRequestDto
{
    /// <summary>
    /// Tower's unique identifier (typically derived from MAC)
    /// </summary>
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Tower's MAC address
    /// </summary>
    [JsonPropertyName("mac_address")]
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Tower's firmware version
    /// </summary>
    [JsonPropertyName("fw_version")]
    public string? FwVersion { get; set; }

    /// <summary>
    /// Tower's hardware capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public TowerCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Signal strength (RSSI) at time of request
    /// </summary>
    [JsonPropertyName("rssi")]
    public int? Rssi { get; set; }
}

/// <summary>
/// DTO for pairing status updates from coordinator.
/// Topic: farm/{farmId}/coord/{coordId}/pairing/status
/// </summary>
public class PairingStatusUpdateDto
{
    /// <summary>
    /// Current pairing status (active, inactive, timeout)
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Remaining time in seconds for pairing window
    /// </summary>
    [JsonPropertyName("remaining_seconds")]
    public int? RemainingSeconds { get; set; }

    /// <summary>
    /// Number of pending pairing requests
    /// </summary>
    [JsonPropertyName("pending_count")]
    public int? PendingCount { get; set; }
}

/// <summary>
/// DTO for pairing completion events from coordinator.
/// Topic: farm/{farmId}/coord/{coordId}/pairing/complete
/// </summary>
public class PairingCompleteEventDto
{
    /// <summary>
    /// Tower ID that was paired
    /// </summary>
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Whether pairing was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if pairing failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

// ============================================================================
// Command DTOs (for MQTT command publishing)
// ============================================================================

/// <summary>
/// Generic command payload for MQTT command topics.
/// </summary>
public class MqttCommandDto
{
    /// <summary>
    /// Command name (e.g., "set_light", "set_pump", "reboot")
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

    /// <summary>
    /// Command parameters as key-value pairs
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }

    /// <summary>
    /// Optional timestamp for command sequencing
    /// </summary>
    [JsonPropertyName("ts")]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Optional correlation ID for tracking command responses
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Light control command for towers.
/// </summary>
public class LightCommandDto
{
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "set_light";

    [JsonPropertyName("on")]
    public bool On { get; set; }

    [JsonPropertyName("brightness")]
    public int? Brightness { get; set; }

    [JsonPropertyName("r")]
    public int? R { get; set; }

    [JsonPropertyName("g")]
    public int? G { get; set; }

    [JsonPropertyName("b")]
    public int? B { get; set; }

    [JsonPropertyName("w")]
    public int? W { get; set; }
}

/// <summary>
/// Pump control command for towers or reservoirs.
/// </summary>
public class PumpCommandDto
{
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "set_pump";

    [JsonPropertyName("on")]
    public bool On { get; set; }

    [JsonPropertyName("duration_s")]
    public int? DurationSeconds { get; set; }
}
