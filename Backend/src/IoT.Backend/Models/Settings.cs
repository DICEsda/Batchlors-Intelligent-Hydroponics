using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// System settings for a site
/// </summary>
public class Settings
{
    [BsonElement("site_id")]
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [BsonElement("auto_mode")]
    [JsonPropertyName("auto_mode")]
    public bool AutoMode { get; set; } = true;

    [BsonElement("motion_sensitivity")]
    [JsonPropertyName("motion_sensitivity")]
    public int MotionSensitivity { get; set; } = 50;

    [BsonElement("light_intensity")]
    [JsonPropertyName("light_intensity")]
    public int LightIntensity { get; set; } = 80;

    [BsonElement("auto_off_delay")]
    [JsonPropertyName("auto_off_delay")]
    public int AutoOffDelay { get; set; } = 30;

    [BsonElement("zones")]
    [JsonPropertyName("zones")]
    public List<string> Zones { get; set; } = new() { "Living Room", "Bedroom", "Kitchen", "Bathroom", "Office", "Hallway" };

    // WiFi credentials (populated from environment)
    [BsonElement("wifi_ssid")]
    [JsonPropertyName("wifi_ssid")]
    [BsonIgnoreIfNull]
    public string? WifiSsid { get; set; }

    [BsonElement("wifi_password")]
    [JsonPropertyName("wifi_password")]
    [BsonIgnoreIfNull]
    public string? WifiPassword { get; set; }

    // MQTT credentials (populated from environment)
    [BsonElement("mqtt_broker")]
    [JsonPropertyName("mqtt_broker")]
    [BsonIgnoreIfNull]
    public string? MqttBroker { get; set; }

    [BsonElement("mqtt_username")]
    [JsonPropertyName("mqtt_username")]
    [BsonIgnoreIfNull]
    public string? MqttUsername { get; set; }

    [BsonElement("mqtt_password")]
    [JsonPropertyName("mqtt_password")]
    [BsonIgnoreIfNull]
    public string? MqttPassword { get; set; }
}
