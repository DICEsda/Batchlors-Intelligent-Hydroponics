using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

[ApiController]
[Route("api/coordinators")]
public class CoordinatorRegistrationController : ControllerBase
{
    private readonly ICoordinatorRegistrationService _registrationService;
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<CoordinatorRegistrationController> _logger;

    public CoordinatorRegistrationController(
        ICoordinatorRegistrationService registrationService,
        ICoordinatorRepository coordinatorRepository,
        IMqttService mqtt,
        ILogger<CoordinatorRegistrationController> logger)
    {
        _registrationService = registrationService;
        _coordinatorRepository = coordinatorRepository;
        _mqtt = mqtt;
        _logger = logger;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingCoordinatorRegistration>>> GetPendingRegistrations(CancellationToken ct)
    {
        var pending = await _registrationService.GetPendingRegistrationsAsync(ct);
        return Ok(pending);
    }

    [HttpPost("register/approve")]
    public async Task<IActionResult> ApproveRegistration(
        [FromBody] ApproveCoordinatorRegistrationRequest request,
        CancellationToken ct)
    {
        try
        {
            var coordinator = await _registrationService.ApproveRegistrationAsync(request, ct);
            _logger.LogInformation("Coordinator {CoordId} registered as '{Name}' in farm {FarmId}",
                request.CoordId, request.Name, request.FarmId);
            return Ok(coordinator);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("register/reject")]
    public async Task<IActionResult> RejectRegistration(
        [FromBody] RejectCoordinatorRegistrationRequest request,
        CancellationToken ct)
    {
        await _registrationService.RejectRegistrationAsync(request.CoordId, ct);
        _logger.LogInformation("Coordinator {CoordId} registration rejected", request.CoordId);
        return Ok(new { status = "rejected", coordId = request.CoordId });
    }

    /// <summary>
    /// Update coordinator metadata (name, description, location, color, tags) in the database only.
    /// Used by the edit dialog — does NOT send any MQTT commands to the firmware.
    /// </summary>
    [HttpPut("{coordId}")]
    public async Task<IActionResult> UpdateCoordinator(
        string coordId,
        [FromBody] UpdateCoordinatorRequest request,
        CancellationToken ct)
    {
        var coordinator = await _coordinatorRepository.GetByIdAsync(coordId, ct);
        if (coordinator is null)
            return NotFound(new { error = $"Coordinator {coordId} not found" });

        // Apply metadata updates
        if (request.Name is not null) coordinator.Name = request.Name;
        if (request.Description is not null) coordinator.Description = request.Description;
        if (request.Location is not null) coordinator.Location = request.Location;
        if (request.Color is not null) coordinator.Color = request.Color;
        if (request.Tags is not null) coordinator.Tags = request.Tags;

        await _coordinatorRepository.UpsertAsync(coordinator, ct);

        _logger.LogInformation("Coordinator {CoordId} metadata updated (Name={Name})",
            coordId, coordinator.Name);

        return Ok(coordinator);
    }

    /// <summary>
    /// Update coordinator configuration: persists metadata changes to the database AND
    /// publishes operational settings to MQTT topic <c>coordinator/{coordId}/config</c>
    /// so the firmware can pick them up.
    /// </summary>
    [HttpPut("{coordId}/config")]
    public async Task<IActionResult> UpdateCoordinatorConfig(
        string coordId,
        [FromBody] UpdateCoordinatorConfigRequest request,
        CancellationToken ct)
    {
        var coordinator = await _coordinatorRepository.GetByIdAsync(coordId, ct);
        if (coordinator is null)
            return NotFound(new { error = $"Coordinator {coordId} not found" });

        // Apply metadata updates to the database document
        if (request.Name is not null) coordinator.Name = request.Name;
        if (request.Description is not null) coordinator.Description = request.Description;
        if (request.Location is not null) coordinator.Location = request.Location;
        if (request.Color is not null) coordinator.Color = request.Color;
        if (request.Tags is not null) coordinator.Tags = request.Tags;

        await _coordinatorRepository.UpsertAsync(coordinator, ct);

        // Build the MQTT config payload with only the operational settings that were provided
        var mqttConfig = new Dictionary<string, object>();

        if (request.NodeListeningEnabled.HasValue)
            mqttConfig["node_listening_enabled"] = request.NodeListeningEnabled.Value;
        if (request.LogPublishFrequencySeconds.HasValue)
            mqttConfig["log_publish_frequency_s"] = request.LogPublishFrequencySeconds.Value;
        if (request.StatusReportIntervalSeconds.HasValue)
            mqttConfig["status_report_interval_s"] = request.StatusReportIntervalSeconds.Value;
        if (request.TelemetryIntervalSeconds.HasValue)
            mqttConfig["telemetry_interval_s"] = request.TelemetryIntervalSeconds.Value;

        // Only publish to MQTT if there are operational settings to send
        if (mqttConfig.Count > 0)
        {
            var topic = MqttTopics.CoordinatorConfig(coordId);
            await _mqtt.PublishJsonAsync(topic, mqttConfig, ct: ct);

            _logger.LogInformation(
                "Published config to {Topic} with {Count} settings: {@Config}",
                topic, mqttConfig.Count, mqttConfig);
        }

        _logger.LogInformation("Coordinator {CoordId} config updated (Name={Name})",
            coordId, coordinator.Name);

        return Ok(coordinator);
    }

    /// <summary>
    /// Send a restart command to a coordinator via MQTT.
    /// Publishes <c>{"action":"restart"}</c> to <c>coordinator/{coordId}/cmd</c>.
    /// </summary>
    [HttpPost("{coordId}/restart")]
    public async Task<IActionResult> RestartCoordinator(string coordId, CancellationToken ct)
    {
        var coordinator = await _coordinatorRepository.GetByIdAsync(coordId, ct);
        if (coordinator is null)
            return NotFound(new { error = $"Coordinator {coordId} not found" });

        var topic = MqttTopics.CoordinatorDirectCmd(coordId);
        await _mqtt.PublishJsonAsync(topic, new { action = "restart" }, ct: ct);

        _logger.LogInformation("Restart command sent to coordinator {CoordId} on {Topic}", coordId, topic);

        return Ok(new { status = "restart_sent", coordId });
    }

    [HttpDelete("{coordId}")]
    public async Task<IActionResult> RemoveCoordinator(string coordId, CancellationToken ct)
    {
        var removed = await _registrationService.RemoveCoordinatorAsync(coordId, ct);
        if (!removed) return NotFound(new { error = $"Coordinator {coordId} not found" });
        _logger.LogInformation("Coordinator {CoordId} removed", coordId);
        return Ok(new { status = "removed", coordId });
    }
}

public class RejectCoordinatorRegistrationRequest
{
    public string CoordId { get; set; } = string.Empty;
}
