using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response model for ML service health check.
/// </summary>
public class MlHealthResponse
{
    /// <summary>
    /// Health status (e.g., "healthy").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// API version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Whether MongoDB is connected.
    /// </summary>
    [JsonPropertyName("mongodb_connected")]
    public bool MongoDbConnected { get; set; }

    /// <summary>
    /// Whether MQTT is connected.
    /// </summary>
    [JsonPropertyName("mqtt_connected")]
    public bool MqttConnected { get; set; }

    /// <summary>
    /// List of loaded ML models.
    /// </summary>
    [JsonPropertyName("models_loaded")]
    public List<string> ModelsLoaded { get; set; } = new();

    /// <summary>
    /// Service uptime in seconds.
    /// </summary>
    [JsonPropertyName("uptime_seconds")]
    public double UptimeSeconds { get; set; }
}
