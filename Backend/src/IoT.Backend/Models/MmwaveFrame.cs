using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a single mmWave radar target
/// </summary>
public class MmwaveTarget
{
    [BsonElement("id")]
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [BsonElement("distance_mm")]
    [JsonPropertyName("distance_mm")]
    public int DistanceMm { get; set; }

    [BsonElement("speed_cm_s")]
    [JsonPropertyName("speed_cm_s")]
    public int SpeedCmS { get; set; }

    [BsonElement("resolution_mm")]
    [JsonPropertyName("resolution_mm")]
    public int ResolutionMm { get; set; }

    [BsonElement("position_x_mm")]
    [JsonPropertyName("position_x_mm")]
    public int PositionXMm { get; set; }

    [BsonElement("position_y_mm")]
    [JsonPropertyName("position_y_mm")]
    public int PositionYMm { get; set; }

    [BsonElement("velocity_x_m_s")]
    [JsonPropertyName("velocity_x_m_s")]
    public float VelocityXMps { get; set; }

    [BsonElement("velocity_y_m_s")]
    [JsonPropertyName("velocity_y_m_s")]
    public float VelocityYMps { get; set; }
}

/// <summary>
/// Represents a frame of mmWave radar data
/// </summary>
public class MmwaveFrame
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [BsonElement("coordinator_id")]
    [JsonPropertyName("coordinator_id")]
    public string CoordinatorId { get; set; } = string.Empty;

    [BsonElement("sensor_id")]
    [JsonPropertyName("sensor_id")]
    public string SensorId { get; set; } = string.Empty;

    [BsonElement("presence")]
    [JsonPropertyName("presence")]
    public bool Presence { get; set; }

    [BsonElement("confidence")]
    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [BsonElement("targets")]
    [JsonPropertyName("targets")]
    public List<MmwaveTarget> Targets { get; set; } = new();

    [BsonElement("timestamp")]
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
