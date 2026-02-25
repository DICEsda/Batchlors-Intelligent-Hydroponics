using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents an OTA (Over-The-Air) update job
/// </summary>
public class OtaJob
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("farm_id")]
    [JsonPropertyName("farm_id")]
    public string FarmId { get; set; } = string.Empty;

    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string? CoordId { get; set; }

    [BsonElement("target_type")]
    [JsonPropertyName("target_type")]
    public string TargetType { get; set; } = "tower"; // "tower" or "coordinator"

    [BsonElement("target_id")]
    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; } // Optional - specific device ID, null means all of type

    [BsonElement("target_version")]
    [JsonPropertyName("target_version")]
    public string TargetVersion { get; set; } = string.Empty;

    [BsonElement("firmware_id")]
    [JsonPropertyName("firmware_id")]
    public string? FirmwareId { get; set; } // Reference to FirmwareVersion

    [BsonElement("firmware_url")]
    [JsonPropertyName("firmware_url")]
    public string? FirmwareUrl { get; set; }

    [BsonElement("firmware_checksum")]
    [JsonPropertyName("firmware_checksum")]
    public string? FirmwareChecksum { get; set; } // SHA256 for verification

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, in_progress, completed, failed, cancelled, rolling_back

    [BsonElement("progress")]
    [JsonPropertyName("progress")]
    public int Progress { get; set; } // 0-100

    [BsonElement("error_message")]
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [BsonElement("devices_total")]
    [JsonPropertyName("devices_total")]
    public int DevicesTotal { get; set; }

    [BsonElement("devices_updated")]
    [JsonPropertyName("devices_updated")]
    public int DevicesUpdated { get; set; }

    [BsonElement("devices_failed")]
    [JsonPropertyName("devices_failed")]
    public int DevicesFailed { get; set; }

    [BsonElement("devices_pending")]
    [JsonPropertyName("devices_pending")]
    public int DevicesPending { get; set; }

    // Rollout Strategy
    [BsonElement("rollout_strategy")]
    [JsonPropertyName("rollout_strategy")]
    public string RolloutStrategy { get; set; } = "immediate"; // immediate, staged, canary

    [BsonElement("rollout_percentage")]
    [JsonPropertyName("rollout_percentage")]
    public int RolloutPercentage { get; set; } = 100; // For staged rollout (0-100)

    [BsonElement("rollout_batch_size")]
    [JsonPropertyName("rollout_batch_size")]
    public int RolloutBatchSize { get; set; } = 0; // Number of devices per batch (0 = all at once)

    [BsonElement("rollout_delay_seconds")]
    [JsonPropertyName("rollout_delay_seconds")]
    public int RolloutDelaySeconds { get; set; } = 0; // Delay between batches

    [BsonElement("failure_threshold")]
    [JsonPropertyName("failure_threshold")]
    public int FailureThreshold { get; set; } = 20; // Percentage of failures before auto-rollback

    [BsonElement("auto_rollback")]
    [JsonPropertyName("auto_rollback")]
    public bool AutoRollback { get; set; } = true;

    // Device tracking
    [BsonElement("device_statuses")]
    [JsonPropertyName("device_statuses")]
    public List<OtaDeviceStatus> DeviceStatuses { get; set; } = new();

    // Timestamps
    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("started_at")]
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [BsonElement("completed_at")]
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("created_by")]
    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; } // User/API key that initiated the job
}

/// <summary>
/// Tracks OTA status for individual devices within a job
/// </summary>
public class OtaDeviceStatus
{
    [BsonElement("device_id")]
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("device_type")]
    [JsonPropertyName("device_type")]
    public string DeviceType { get; set; } = string.Empty; // tower, coordinator

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, downloading, flashing, verifying, success, failed, rolled_back

    [BsonElement("progress")]
    [JsonPropertyName("progress")]
    public int Progress { get; set; } // 0-100

    [BsonElement("previous_version")]
    [JsonPropertyName("previous_version")]
    public string? PreviousVersion { get; set; }

    [BsonElement("error_message")]
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [BsonElement("error_code")]
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; } // e.g., "DOWNLOAD_FAILED", "CHECKSUM_MISMATCH", "FLASH_ERROR"

    [BsonElement("started_at")]
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [BsonElement("completed_at")]
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("retries")]
    [JsonPropertyName("retries")]
    public int Retries { get; set; }
}
