using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a firmware version available for OTA updates.
/// Used to track and manage firmware releases for coordinators and towers.
/// </summary>
public class FirmwareVersion
{
    /// <summary>
    /// Unique identifier for the firmware version record.
    /// </summary>
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Semantic version string (e.g., "1.2.3", "2.0.0-beta").
    /// </summary>
    [BsonElement("version")]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Type of device this firmware targets: "coordinator" or "tower".
    /// </summary>
    [BsonElement("device_type")]
    [JsonPropertyName("device_type")]
    public string DeviceType { get; set; } = "coordinator";

    /// <summary>
    /// Description of changes in this version.
    /// </summary>
    [BsonElement("changelog")]
    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }

    /// <summary>
    /// When this firmware version was released.
    /// </summary>
    [BsonElement("release_date")]
    [JsonPropertyName("release_date")]
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// URL to download the firmware binary.
    /// </summary>
    [BsonElement("download_url")]
    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 checksum of the firmware binary for verification.
    /// </summary>
    [BsonElement("checksum")]
    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Size of the firmware binary in bytes.
    /// </summary>
    [BsonElement("file_size_bytes")]
    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Whether this is a stable release (true) or pre-release/beta (false).
    /// </summary>
    [BsonElement("is_stable")]
    [JsonPropertyName("is_stable")]
    public bool IsStable { get; set; } = true;

    /// <summary>
    /// Minimum firmware version required to upgrade from.
    /// Null means any previous version can upgrade to this one.
    /// </summary>
    [BsonElement("min_version")]
    [JsonPropertyName("min_version")]
    public string? MinVersion { get; set; }

    /// <summary>
    /// When this firmware record was created in the database.
    /// </summary>
    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
