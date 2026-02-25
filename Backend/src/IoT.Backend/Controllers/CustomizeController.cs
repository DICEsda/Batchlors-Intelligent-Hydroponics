using IoT.Backend.Models.Requests;
using IoT.Backend.Models.Responses;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for device customization - configuration, LED settings, preview, and reset.
/// Supports both reservoir (coordinator) and tower (node) devices.
/// </summary>
[ApiController]
[Route("api/customize")]
public class CustomizeController : ControllerBase
{
    private readonly IMqttService _mqtt;
    private readonly ILogger<CustomizeController> _logger;

    public CustomizeController(IMqttService mqtt, ILogger<CustomizeController> logger)
    {
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get default configuration schema for a device type.
    /// </summary>
    [HttpGet("{deviceType}")]
    public ActionResult<DeviceConfigSchema> GetConfigSchema(string deviceType)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var config = BuildDefaultConfig(deviceType);
        return Ok(config);
    }

    /// <summary>
    /// Update device configuration via MQTT command.
    /// </summary>
    [HttpPut("{deviceType}/{deviceId}/config")]
    public async Task<IActionResult> UpdateConfig(
        string deviceType, 
        string deviceId, 
        [FromBody] UpdateConfigRequest request, 
        CancellationToken ct)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var siteId = request.SiteId ?? "site001";
        var mqttDeviceType = deviceType == "reservoir" ? "coord" : "node";
        var topic = MqttTopics.DeviceCmd(siteId, mqttDeviceType, deviceId);

        var payload = new
        {
            cmd = "update_config",
            config = request.Config
        };

        await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
        _logger.LogInformation("Sent config update to {DeviceType} {DeviceId}", deviceType, deviceId);

        return Ok(new { status = "success", message = "Configuration update sent" });
    }

    /// <summary>
    /// Update light sensor configuration.
    /// </summary>
    [HttpPut("{deviceType}/{deviceId}/light")]
    public async Task<IActionResult> UpdateLightConfig(
        string deviceType, 
        string deviceId, 
        [FromBody] UpdateConfigRequest request, 
        CancellationToken ct)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var siteId = request.SiteId ?? "site001";
        var mqttDeviceType = deviceType == "reservoir" ? "coord" : "node";
        var topic = MqttTopics.DeviceCmd(siteId, mqttDeviceType, deviceId);

        var payload = new
        {
            cmd = "config_light",
            config = request.Config
        };

        await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
        _logger.LogInformation("Sent light config to {DeviceType} {DeviceId}", deviceType, deviceId);

        return Ok(new { status = "success", message = "Light configuration updated" });
    }

    /// <summary>
    /// Update LED strip configuration.
    /// </summary>
    [HttpPut("{deviceType}/{deviceId}/led")]
    public async Task<IActionResult> UpdateLedConfig(
        string deviceType, 
        string deviceId, 
        [FromBody] UpdateConfigRequest request, 
        CancellationToken ct)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var siteId = request.SiteId ?? "site001";
        var mqttDeviceType = deviceType == "reservoir" ? "coord" : "node";
        var topic = MqttTopics.DeviceCmd(siteId, mqttDeviceType, deviceId);

        var payload = new
        {
            cmd = "config_led",
            config = request.Config
        };

        await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
        _logger.LogInformation("Sent LED config to {DeviceType} {DeviceId}", deviceType, deviceId);

        return Ok(new { status = "success", message = "LED configuration updated" });
    }

    /// <summary>
    /// Reset device configuration to factory defaults.
    /// </summary>
    [HttpPost("{deviceType}/{deviceId}/reset")]
    public async Task<IActionResult> ResetToDefaults(
        string deviceType, 
        string deviceId, 
        [FromBody] ResetConfigRequest request, 
        CancellationToken ct)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var siteId = request.SiteId ?? "site001";
        var mqttDeviceType = deviceType == "reservoir" ? "coord" : "node";
        var topic = MqttTopics.DeviceCmd(siteId, mqttDeviceType, deviceId);

        var payload = new
        {
            cmd = "reset_config",
            section = request.Section
        };

        await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
        _logger.LogInformation("Sent reset command to {DeviceType} {DeviceId}, section: {Section}", 
            deviceType, deviceId, request.Section);

        return Ok(new { status = "success", message = "Configuration reset to defaults" });
    }

    /// <summary>
    /// Send a temporary LED preview command (for testing colors/effects).
    /// </summary>
    [HttpPost("{deviceType}/{deviceId}/led/preview")]
    public async Task<IActionResult> LedPreview(
        string deviceType, 
        string deviceId, 
        [FromBody] LedPreviewRequest request, 
        CancellationToken ct)
    {
        if (deviceType != "reservoir" && deviceType != "tower")
        {
            return BadRequest(new { error = "Invalid device type. Use 'reservoir' or 'tower'." });
        }

        var siteId = request.SiteId ?? "site001";
        var mqttDeviceType = deviceType == "reservoir" ? "coord" : "node";
        var topic = MqttTopics.DeviceCmd(siteId, mqttDeviceType, deviceId);

        var payload = new
        {
            cmd = "led_preview",
            color = request.Color,
            brightness = request.Brightness,
            effect = request.Effect,
            duration = request.Duration
        };

        await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
        _logger.LogInformation("Sent LED preview to {DeviceType} {DeviceId}", deviceType, deviceId);

        return Ok(new { status = "success", message = "LED preview started" });
    }

    /// <summary>
    /// Builds default configuration schema based on device type.
    /// </summary>
    private static DeviceConfigSchema BuildDefaultConfig(string deviceType)
    {
        var schema = new DeviceConfigSchema();

        if (deviceType == "reservoir")
        {
            // Reservoir (coordinator) configuration from ConfigManager defaults
            schema.Device = new Dictionary<string, object>
            {
                ["presence_debounce_ms"] = 150,
                ["occupancy_hold_ms"] = 5000,
                ["fade_in_ms"] = 150,
                ["fade_out_ms"] = 1000,
                ["pairing_window_s"] = 120
            };

            schema.Light = new Dictionary<string, object>
            {
                ["enabled"] = true
            };
        }
        else if (deviceType == "tower")
        {
            // Tower (node) configuration from ConfigManager defaults
            schema.Device = new Dictionary<string, object>
            {
                ["pwm_freq_hz"] = 1000,
                ["pwm_res_bits"] = 12,
                ["telemetry_s"] = 5,
                ["rx_window_ms"] = 20,
                ["rx_period_ms"] = 100,
                ["derate_start_c"] = 70.0,
                ["derate_min_duty_pct"] = 30,
                ["retry_count"] = 3,
                ["cmd_ttl_ms"] = 1500
            };

            schema.Led = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["brightness"] = 80,
                ["color"] = "#00FFBF"
            };
        }

        return schema;
    }
}
