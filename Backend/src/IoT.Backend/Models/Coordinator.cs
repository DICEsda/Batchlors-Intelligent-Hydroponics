using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents an ESP32-S3 coordinator
/// </summary>
public class Coordinator
{
    [BsonId]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("coord_id")]
    [JsonPropertyName("coord_id")]
    public string CoordId { get; set; } = string.Empty;

    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [BsonElement("fw_version")]
    [JsonPropertyName("fw_version")]
    public string FwVersion { get; set; } = string.Empty;

    [BsonElement("nodes_online")]
    [JsonPropertyName("nodes_online")]
    public int NodesOnline { get; set; }

    [BsonElement("wifi_rssi")]
    [JsonPropertyName("wifi_rssi")]
    public int WifiRssi { get; set; }

    [BsonElement("mmwave_event_rate")]
    [JsonPropertyName("mmwave_event_rate")]
    public float MmwaveEventRate { get; set; }

    [BsonElement("light_lux")]
    [JsonPropertyName("light_lux")]
    public float LightLux { get; set; }

    [BsonElement("temp_c")]
    [JsonPropertyName("temp_c")]
    public float TempC { get; set; }

    [BsonElement("last_seen")]
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; }
}
