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

    [BsonElement("target_type")]
    [JsonPropertyName("target_type")]
    public string TargetType { get; set; } = "node";

    [BsonElement("target_version")]
    [JsonPropertyName("target_version")]
    public string TargetVersion { get; set; } = string.Empty;

    [BsonElement("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
