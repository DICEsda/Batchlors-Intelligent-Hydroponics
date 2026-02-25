using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Farm entity - represents a hydroponic farm with coordinators and towers.
/// Auto-discovered from coordinator telemetry or manually created.
/// </summary>
public class Farm
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    
    /// <summary>
    /// Unique farm identifier (matches farmId from MQTT topics and coordinator data).
    /// </summary>
    [BsonElement("farm_id")]
    public string FarmId { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable farm name.
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description.
    /// </summary>
    [BsonElement("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Physical location or address.
    /// </summary>
    [BsonElement("location")]
    public string? Location { get; set; }
    
    /// <summary>
    /// When the farm was created (auto-discovery or manual).
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last time any coordinator from this farm sent telemetry.
    /// </summary>
    [BsonElement("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Number of coordinators registered to this farm.
    /// </summary>
    [BsonElement("coordinator_count")]
    public int CoordinatorCount { get; set; }
    
    /// <summary>
    /// Number of towers registered to this farm.
    /// </summary>
    [BsonElement("tower_count")]
    public int TowerCount { get; set; }
    
    /// <summary>
    /// Number of active alerts for this farm.
    /// </summary>
    [BsonElement("active_alert_count")]
    public int ActiveAlertCount { get; set; }
    
    /// <summary>
    /// Whether this farm was auto-discovered (true) or manually created (false).
    /// </summary>
    [BsonElement("auto_discovered")]
    public bool AutoDiscovered { get; set; }
}
