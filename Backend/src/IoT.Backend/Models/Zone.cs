using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a zone within a site (e.g., Living Room, Kitchen)
/// </summary>
public class Zone
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [BsonElement("coordinator_id")]
    [JsonPropertyName("coordinator_id")]
    [BsonIgnoreIfNull]
    public string? CoordinatorId { get; set; }

    [BsonElement("description")]
    [JsonPropertyName("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; set; }

    [BsonElement("color")]
    [JsonPropertyName("color")]
    [BsonIgnoreIfNull]
    public string? Color { get; set; }

    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
