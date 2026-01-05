using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// Metadata for tracking digital twin state and synchronization
/// </summary>
public class TwinMetadata
{
    /// <summary>
    /// Optimistic concurrency version - incremented on each state change
    /// </summary>
    [BsonElement("version")]
    [JsonPropertyName("version")]
    public long Version { get; set; } = 1;
    
    /// <summary>
    /// Current synchronization status between desired and reported state
    /// </summary>
    [BsonElement("sync_status")]
    [JsonPropertyName("sync_status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public SyncStatus SyncStatus { get; set; } = SyncStatus.InSync;
    
    /// <summary>
    /// Timestamp when device last reported its state
    /// </summary>
    [BsonElement("last_reported_at")]
    [JsonPropertyName("last_reported_at")]
    public DateTime? LastReportedAt { get; set; }
    
    /// <summary>
    /// Timestamp when desired state was last modified
    /// </summary>
    [BsonElement("last_desired_at")]
    [JsonPropertyName("last_desired_at")]
    public DateTime? LastDesiredAt { get; set; }
    
    /// <summary>
    /// Timestamp of last sync attempt (command sent to device)
    /// </summary>
    [BsonElement("last_sync_attempt")]
    [JsonPropertyName("last_sync_attempt")]
    public DateTime? LastSyncAttempt { get; set; }
    
    /// <summary>
    /// Number of failed sync attempts since last successful sync
    /// </summary>
    [BsonElement("sync_retry_count")]
    [JsonPropertyName("sync_retry_count")]
    public int SyncRetryCount { get; set; }
    
    /// <summary>
    /// When the twin document was created
    /// </summary>
    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the twin document was last updated (any field)
    /// </summary>
    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Whether the device is currently connected/online
    /// </summary>
    [BsonElement("is_connected")]
    [JsonPropertyName("is_connected")]
    public bool IsConnected { get; set; }
    
    /// <summary>
    /// Connection quality indicator (e.g., WiFi RSSI for coordinator, ESP-NOW quality for tower)
    /// </summary>
    [BsonElement("connection_quality")]
    [JsonPropertyName("connection_quality")]
    public int? ConnectionQuality { get; set; }
}
