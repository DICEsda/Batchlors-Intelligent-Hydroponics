using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for site settings management.
/// Supports retrieving and saving site configuration including MQTT/WiFi credentials.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsRepository settingsRepository, 
        IConfiguration configuration,
        ILogger<SettingsController> logger)
    {
        _settingsRepository = settingsRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get settings for a site. Returns default settings if none exist.
    /// MQTT/WiFi credentials are populated from environment variables.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Settings>> GetSettings(
        [FromQuery(Name = "site_id")] string? siteId, 
        CancellationToken ct)
    {
        siteId ??= "site001";

        var settings = await _settingsRepository.GetAsync(siteId, ct);

        if (settings == null)
        {
            // Return default settings if not found
            settings = new Settings
            {
                SiteId = siteId,
                AutoMode = true,
                MotionSensitivity = 50,
                LightIntensity = 80,
                AutoOffDelay = 30,
                Zones = new List<string> { "Living Room", "Bedroom", "Kitchen", "Bathroom", "Office", "Hallway" }
            };
        }

        // Populate MQTT credentials from environment/configuration
        var mqttBroker = Environment.GetEnvironmentVariable("MQTT_BROKER") 
            ?? _configuration["Mqtt:Host"];
        if (!string.IsNullOrEmpty(mqttBroker))
        {
            var mqttPort = Environment.GetEnvironmentVariable("MQTT_PORT") 
                ?? _configuration["Mqtt:Port"] ?? "1883";
            settings.MqttBroker = $"tcp://{mqttBroker}:{mqttPort}";
        }
        else
        {
            settings.MqttBroker = "tcp://mosquitto:1883";
        }

        settings.MqttUsername = Environment.GetEnvironmentVariable("MQTT_USERNAME") 
            ?? _configuration["Mqtt:Username"];
        settings.MqttPassword = Environment.GetEnvironmentVariable("MQTT_PASSWORD") 
            ?? _configuration["Mqtt:Password"];

        // Populate WiFi credentials from environment
        settings.WifiSsid = Environment.GetEnvironmentVariable("ESP32_WIFI_SSID");
        settings.WifiPassword = Environment.GetEnvironmentVariable("ESP32_WIFI_PASSWORD");

        _logger.LogDebug("Retrieved settings for site {SiteId}", siteId);
        return Ok(settings);
    }

    /// <summary>
    /// Save or update settings for a site.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> SaveSettings([FromBody] Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.SiteId))
        {
            settings.SiteId = "site001";
        }

        // Don't persist sensitive credentials - they come from environment
        settings.MqttBroker = null;
        settings.MqttUsername = null;
        settings.MqttPassword = null;
        settings.WifiSsid = null;
        settings.WifiPassword = null;

        await _settingsRepository.SaveAsync(settings, ct);
        _logger.LogInformation("Saved settings for site {SiteId}", settings.SiteId);

        return Ok(new { status = "success", message = "Settings saved" });
    }

    /// <summary>
    /// Partially update settings for a site.
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> PatchSettings(
        [FromQuery(Name = "site_id")] string? siteId,
        [FromBody] SettingsPatchRequest patch, 
        CancellationToken ct)
    {
        siteId ??= "site001";

        var settings = await _settingsRepository.GetAsync(siteId, ct);
        if (settings == null)
        {
            settings = new Settings { SiteId = siteId };
        }

        // Apply patches
        if (patch.AutoMode.HasValue)
            settings.AutoMode = patch.AutoMode.Value;
        if (patch.MotionSensitivity.HasValue)
            settings.MotionSensitivity = patch.MotionSensitivity.Value;
        if (patch.LightIntensity.HasValue)
            settings.LightIntensity = patch.LightIntensity.Value;
        if (patch.AutoOffDelay.HasValue)
            settings.AutoOffDelay = patch.AutoOffDelay.Value;
        if (patch.Zones != null)
            settings.Zones = patch.Zones;

        await _settingsRepository.SaveAsync(settings, ct);
        _logger.LogInformation("Patched settings for site {SiteId}", siteId);

        return Ok(new { status = "success", message = "Settings updated" });
    }
}
