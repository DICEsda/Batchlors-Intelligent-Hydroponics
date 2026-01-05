using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents an active pairing session for a coordinator.
/// Tracks when the coordinator enters pairing mode and any pending tower requests.
/// </summary>
public class PairingSession
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Coordinator ID that initiated the pairing session
    /// </summary>
    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// Farm ID for the coordinator
    /// </summary>
    [BsonElement("farm_id")]
    [JsonPropertyName("farm_id")]
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Session status: "active", "completed", "expired", "cancelled"
    /// </summary>
    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    /// <summary>
    /// When the pairing session started
    /// </summary>
    [BsonElement("started_at")]
    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the pairing session expires (auto-timeout)
    /// </summary>
    [BsonElement("expires_at")]
    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Duration of the pairing window in seconds
    /// </summary>
    [BsonElement("duration_s")]
    [JsonPropertyName("duration_s")]
    public int DurationS { get; set; }

    /// <summary>
    /// When the session was completed or cancelled
    /// </summary>
    [BsonElement("ended_at")]
    [JsonPropertyName("ended_at")]
    [BsonIgnoreIfNull]
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// List of pending tower pairing requests for this session
    /// </summary>
    [BsonElement("pending_requests")]
    [JsonPropertyName("pending_requests")]
    public List<TowerPairingRequest> PendingRequests { get; set; } = new();

    /// <summary>
    /// List of tower IDs that were approved during this session
    /// </summary>
    [BsonElement("approved_towers")]
    [JsonPropertyName("approved_towers")]
    public List<string> ApprovedTowers { get; set; } = new();

    /// <summary>
    /// List of tower IDs that were rejected during this session
    /// </summary>
    [BsonElement("rejected_towers")]
    [JsonPropertyName("rejected_towers")]
    public List<string> RejectedTowers { get; set; } = new();
}

/// <summary>
/// Represents a tower's request to pair with a coordinator.
/// Received via MQTT when a tower broadcasts its pairing request.
/// </summary>
public class TowerPairingRequest
{
    /// <summary>
    /// Unique identifier for this request (generated)
    /// </summary>
    [BsonElement("request_id")]
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Tower's unique ID (MAC-based or assigned)
    /// </summary>
    [BsonElement("tower_id")]
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Tower's MAC address
    /// </summary>
    [BsonElement("mac_address")]
    [JsonPropertyName("mac_address")]
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Request status: "pending", "approved", "rejected", "expired"
    /// </summary>
    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When the pairing request was received
    /// </summary>
    [BsonElement("requested_at")]
    [JsonPropertyName("requested_at")]
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the request was approved/rejected
    /// </summary>
    [BsonElement("resolved_at")]
    [JsonPropertyName("resolved_at")]
    [BsonIgnoreIfNull]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Firmware version reported by the tower
    /// </summary>
    [BsonElement("fw_version")]
    [JsonPropertyName("fw_version")]
    [BsonIgnoreIfNull]
    public string? FwVersion { get; set; }

    /// <summary>
    /// Hardware capabilities reported by the tower
    /// </summary>
    [BsonElement("capabilities")]
    [JsonPropertyName("capabilities")]
    [BsonIgnoreIfNull]
    public TowerCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Signal strength (RSSI) during the pairing request
    /// </summary>
    [BsonElement("rssi")]
    [JsonPropertyName("rssi")]
    [BsonIgnoreIfNull]
    public int? Rssi { get; set; }
}
