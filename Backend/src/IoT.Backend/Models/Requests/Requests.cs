using IoT.Backend.Models;

namespace IoT.Backend.Models.Requests;

// ============================================================================
// Coordinator Requests
// ============================================================================

/// <summary>
/// Command to send to a coordinator via MQTT.
/// </summary>
public class CoordinatorCommand
{
    public string Cmd { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}

/// <summary>
/// Request to put coordinator into pairing mode.
/// </summary>
public class PairRequest
{
    public int DurationSeconds { get; set; } = 60;
}

/// <summary>
/// Request to approve a tower pairing (legacy endpoint).
/// </summary>
public class ApprovePairingRequest
{
    public string TowerId { get; set; } = string.Empty;
}

/// <summary>
/// Request to update coordinator WiFi configuration.
/// </summary>
public class WifiConfigRequest
{
    public string Ssid { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Command to send to a tower via coordinator broadcast.
/// </summary>
public class TowerCommand
{
    public string Cmd { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}

// ============================================================================
// Zone Requests
// ============================================================================

/// <summary>
/// Request to create a new zone.
/// </summary>
public class CreateZoneRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoordinatorId { get; set; } = string.Empty;
    public string? Color { get; set; }
}

/// <summary>
/// Request to update an existing zone.
/// </summary>
public class UpdateZoneRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? CoordinatorId { get; set; }
}

// ============================================================================
// Pairing Requests
// ============================================================================

/// <summary>
/// Request to start a pairing session.
/// </summary>
public class StartPairingRequest
{
    /// <summary>
    /// Farm identifier
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier
    /// </summary>
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Duration of the pairing window in seconds (default: 60)
    /// </summary>
    public int DurationSeconds { get; set; } = 60;
}

/// <summary>
/// Request to stop a pairing session.
/// </summary>
public class StopPairingRequest
{
    /// <summary>
    /// Farm identifier
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier
    /// </summary>
    public string CoordId { get; set; } = string.Empty;
}

/// <summary>
/// Request to approve a tower pairing.
/// </summary>
public class ApproveTowerRequest
{
    /// <summary>
    /// Farm identifier
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier
    /// </summary>
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Tower identifier to approve
    /// </summary>
    public string TowerId { get; set; } = string.Empty;
}

/// <summary>
/// Request to reject a tower pairing.
/// </summary>
public class RejectTowerRequest
{
    /// <summary>
    /// Farm identifier
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier
    /// </summary>
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Tower identifier to reject
    /// </summary>
    public string TowerId { get; set; } = string.Empty;
}

/// <summary>
/// Request to forget (unpair) a device.
/// </summary>
public class ForgetDeviceRequest
{
    /// <summary>
    /// Farm identifier
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier
    /// </summary>
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Tower identifier to forget
    /// </summary>
    public string TowerId { get; set; } = string.Empty;
}

// ============================================================================
// OTA Requests
// ============================================================================

/// <summary>
/// Request to start an OTA update job.
/// </summary>
public class StartOtaRequest
{
    /// <summary>
    /// Farm identifier (required)
    /// </summary>
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator identifier (required - identifies which coordinator will manage the OTA)
    /// </summary>
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Device type to update: "tower" or "coordinator" (default: tower)
    /// </summary>
    public string? TargetType { get; set; } = "tower";

    /// <summary>
    /// Optional - specific device ID to update. Null means update all devices of the target type
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Target firmware version string (e.g., "1.2.0")
    /// </summary>
    public string TargetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Reference to FirmwareVersion record (optional - can be used instead of FirmwareUrl)
    /// </summary>
    public string? FirmwareId { get; set; }

    /// <summary>
    /// Direct URL to firmware binary (optional - used if FirmwareId is not provided)
    /// </summary>
    public string? FirmwareUrl { get; set; }

    /// <summary>
    /// SHA256 checksum of the firmware binary for verification
    /// </summary>
    public string? FirmwareChecksum { get; set; }

    // Rollout Strategy Options

    /// <summary>
    /// Rollout strategy: "immediate", "staged", or "canary" (default: immediate)
    /// </summary>
    public string? RolloutStrategy { get; set; } = "immediate";

    /// <summary>
    /// For staged/canary rollout: percentage of devices to update (0-100, default: 100)
    /// </summary>
    public int? RolloutPercentage { get; set; }

    /// <summary>
    /// Number of devices to update per batch (0 = all at once)
    /// </summary>
    public int? RolloutBatchSize { get; set; }

    /// <summary>
    /// Delay in seconds between batches (default: 0)
    /// </summary>
    public int? RolloutDelaySeconds { get; set; }

    /// <summary>
    /// Percentage of failures before triggering auto-rollback (default: 20)
    /// </summary>
    public int? FailureThreshold { get; set; }

    /// <summary>
    /// Whether to automatically rollback on failure threshold breach (default: true)
    /// </summary>
    public bool? AutoRollback { get; set; }
}

/// <summary>
/// Request to update OTA job progress.
/// </summary>
public class UpdateProgressRequest
{
    public string? Status { get; set; }
    public int? Progress { get; set; }
    public int? DevicesTotal { get; set; }
    public int? DevicesUpdated { get; set; }
    public int? DevicesFailed { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to create a new firmware version.
/// </summary>
public class CreateFirmwareRequest
{
    /// <summary>
    /// Semantic version string (e.g., "1.0.0", "2.1.3-beta")
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Device type this firmware is for (e.g., "coordinator", "node", "tower")
    /// </summary>
    public string? DeviceType { get; set; }

    /// <summary>
    /// Description of changes in this version
    /// </summary>
    public string? Changelog { get; set; }

    /// <summary>
    /// Release date of this firmware version
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// URL to download the firmware binary
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 checksum of the firmware binary
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Size of the firmware binary in bytes
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Whether this is a stable release (vs beta/dev)
    /// </summary>
    public bool IsStable { get; set; } = true;

    /// <summary>
    /// Minimum firmware version required to upgrade to this version
    /// </summary>
    public string? MinVersion { get; set; }
}

// ============================================================================
// Customize Requests
// ============================================================================

/// <summary>
/// Request to update device configuration.
/// </summary>
public class UpdateConfigRequest
{
    public string? SiteId { get; set; }
    public Dictionary<string, object> Config { get; set; } = new();
}

/// <summary>
/// Request to reset device configuration to defaults.
/// </summary>
public class ResetConfigRequest
{
    public string? SiteId { get; set; }
    public string? Section { get; set; }
}

/// <summary>
/// Request for LED preview on a device.
/// </summary>
public class LedPreviewRequest
{
    public string? SiteId { get; set; }
    public string? Color { get; set; }
    public int Brightness { get; set; } = 80;
    public string? Effect { get; set; }
    public int Duration { get; set; } = 5;
}

// ============================================================================
// Settings Requests
// ============================================================================

/// <summary>
/// Partial settings update request.
/// </summary>
public class SettingsPatchRequest
{
    public bool? AutoMode { get; set; }
    public int? MotionSensitivity { get; set; }
    public int? LightIntensity { get; set; }
    public int? AutoOffDelay { get; set; }
    public List<string>? Zones { get; set; }
}

// ============================================================================
// Tower Requests
// ============================================================================

/// <summary>
/// Request to create or update a tower.
/// </summary>
public class UpsertTowerRequest
{
    /// <summary>
    /// Display name for the tower
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Type of crop being grown
    /// </summary>
    public CropType? CropType { get; set; }

    /// <summary>
    /// Date when the crop was planted
    /// </summary>
    public DateTime? PlantingDate { get; set; }
}

/// <summary>
/// Request to update a tower's name.
/// </summary>
public class UpdateNameRequest
{
    /// <summary>
    /// New display name for the tower
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request to control grow light on a tower.
/// </summary>
public class SetLightRequest
{
    /// <summary>
    /// Turn light on (true) or off (false)
    /// </summary>
    public bool On { get; set; }

    /// <summary>
    /// Optional brightness level (0-100)
    /// </summary>
    public int? Brightness { get; set; }
}

/// <summary>
/// Request to control pump on a tower.
/// </summary>
public class SetPumpRequest
{
    /// <summary>
    /// Turn pump on (true) or off (false)
    /// </summary>
    public bool On { get; set; }

    /// <summary>
    /// Optional duration in seconds (auto-off after this time)
    /// </summary>
    public int? DurationSeconds { get; set; }
}

/// <summary>
/// Request to record a plant height measurement.
/// </summary>
public class RecordHeightRequest
{
    /// <summary>
    /// Index of the plant slot (0-based)
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>
    /// Measured height in centimeters
    /// </summary>
    public float HeightCm { get; set; }

    /// <summary>
    /// Method used for measurement
    /// </summary>
    public MeasurementMethod Method { get; set; } = MeasurementMethod.Manual;

    /// <summary>
    /// Optional notes about the measurement
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request to set crop information on a tower.
/// </summary>
public class SetCropRequest
{
    /// <summary>
    /// Type of crop being grown
    /// </summary>
    public CropType CropType { get; set; } = CropType.Unknown;

    /// <summary>
    /// Date when the crop was planted
    /// </summary>
    public DateTime? PlantingDate { get; set; }

    /// <summary>
    /// Expected harvest date (can be calculated or manually set)
    /// </summary>
    public DateTime? ExpectedHarvestDate { get; set; }
}

// ============================================================================
// Reservoir Requests
// ============================================================================

/// <summary>
/// Request to control reservoir nutrient dosing.
/// </summary>
public class DosingRequest
{
    /// <summary>
    /// Amount of nutrient A to dose in mL
    /// </summary>
    public float? NutrientAMl { get; set; }

    /// <summary>
    /// Amount of nutrient B to dose in mL
    /// </summary>
    public float? NutrientBMl { get; set; }

    /// <summary>
    /// Amount of pH-up solution in mL
    /// </summary>
    public float? PhUpMl { get; set; }

    /// <summary>
    /// Amount of pH-down solution in mL
    /// </summary>
    public float? PhDownMl { get; set; }
}

/// <summary>
/// Request to control reservoir main pump.
/// </summary>
public class ReservoirPumpRequest
{
    /// <summary>
    /// Turn pump on (true) or off (false)
    /// </summary>
    public bool On { get; set; }

    /// <summary>
    /// Optional duration in seconds (auto-off after this time)
    /// </summary>
    public int? DurationSeconds { get; set; }
}

/// <summary>
/// Request to set reservoir target ranges.
/// </summary>
public class ReservoirTargetsRequest
{
    /// <summary>
    /// Target pH minimum
    /// </summary>
    public float? PhMin { get; set; }

    /// <summary>
    /// Target pH maximum
    /// </summary>
    public float? PhMax { get; set; }

    /// <summary>
    /// Target EC minimum in mS/cm
    /// </summary>
    public float? EcMin { get; set; }

    /// <summary>
    /// Target EC maximum in mS/cm
    /// </summary>
    public float? EcMax { get; set; }

    /// <summary>
    /// Target water temperature minimum in Celsius
    /// </summary>
    public float? TempMinC { get; set; }

    /// <summary>
    /// Target water temperature maximum in Celsius
    /// </summary>
    public float? TempMaxC { get; set; }
}
