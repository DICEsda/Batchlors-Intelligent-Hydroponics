using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a serial log entry from a coordinator
/// </summary>
public class SerialLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("site_id")]
    public string SiteId { get; set; } = string.Empty;
    
    [BsonElement("coord_id")]
    public string CoordId { get; set; } = string.Empty;
    
    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;
    
    [BsonElement("level")]
    public string Level { get; set; } = "INFO";
    
    [BsonElement("tag")]
    public string? Tag { get; set; }
    
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
