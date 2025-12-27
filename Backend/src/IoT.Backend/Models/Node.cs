using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents an ESP32-C3 tile node
/// </summary>
public class Node
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("light_id")]
    [JsonPropertyName("light_id")]
    public string LightId { get; set; } = string.Empty;

    [BsonElement("status_mode")]
    [JsonPropertyName("status_mode")]
    public string StatusMode { get; set; } = "operational";

    [BsonElement("avg_r")]
    [JsonPropertyName("avg_r")]
    public int AvgR { get; set; }

    [BsonElement("avg_g")]
    [JsonPropertyName("avg_g")]
    public int AvgG { get; set; }

    [BsonElement("avg_b")]
    [JsonPropertyName("avg_b")]
    public int AvgB { get; set; }

    [BsonElement("avg_w")]
    [JsonPropertyName("avg_w")]
    public int AvgW { get; set; }

    [BsonElement("temp_c")]
    [JsonPropertyName("temp_c")]
    public float TempC { get; set; }

    [BsonElement("vbat_mv")]
    [JsonPropertyName("vbat_mv")]
    public int VbatMv { get; set; }

    [BsonElement("fw_version")]
    [JsonPropertyName("fw_version")]
    public string FwVersion { get; set; } = string.Empty;

    [BsonElement("last_seen")]
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; }

    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [BsonElement("coordinator_id")]
    [JsonPropertyName("coordinator_id")]
    public string CoordinatorId { get; set; } = string.Empty;

    [BsonElement("zone_id")]
    [JsonPropertyName("zone_id")]
    [BsonIgnoreIfNull]
    public string? ZoneId { get; set; }

    [BsonElement("name")]
    [JsonPropertyName("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }
}
