using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a site/location containing coordinators and nodes
/// </summary>
public class Site
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    [JsonPropertyName("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; set; }

    [BsonElement("location")]
    [JsonPropertyName("location")]
    [BsonIgnoreIfNull]
    public string? Location { get; set; }

    [BsonElement("timezone")]
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";

    [BsonElement("config")]
    [JsonPropertyName("config")]
    [BsonIgnoreIfNull]
    public string? Config { get; set; }

    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
