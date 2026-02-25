using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing system alerts.
/// </summary>
[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertRepository alertRepository,
        IWsBroadcaster broadcaster,
        ILogger<AlertsController> logger)
    {
        _alertRepository = alertRepository;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Get alerts with filtering and pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? severity = null,
        [FromQuery] string? status = null,
        [FromQuery] string? farmId = null,
        CancellationToken ct = default)
    {
        var (alerts, totalCount) = await _alertRepository.GetFilteredAsync(
            page, pageSize, severity, status, farmId, ct);

        return Ok(new
        {
            data = alerts,
            page,
            page_size = pageSize,
            total_count = totalCount,
            total_pages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Get a specific alert by ID.
    /// </summary>
    [HttpGet("{alertId}")]
    public async Task<ActionResult<Alert>> GetAlert(string alertId, CancellationToken ct)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, ct);
        if (alert == null)
        {
            return NotFound(new { error = "Alert not found" });
        }
        return Ok(alert);
    }

    /// <summary>
    /// Get all active alerts (unacknowledged).
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetActiveAlerts(CancellationToken ct)
    {
        var alerts = await _alertRepository.GetActiveAlertsAsync(ct);
        return Ok(alerts);
    }

    /// <summary>
    /// Get alerts for a specific farm.
    /// </summary>
    [HttpGet("farm/{farmId}")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetFarmAlerts(
        string farmId,
        CancellationToken ct)
    {
        var alerts = await _alertRepository.GetByFarmAsync(farmId, ct);
        return Ok(alerts);
    }

    /// <summary>
    /// Acknowledge an alert.
    /// </summary>
    [HttpPost("{alertId}/acknowledge")]
    public async Task<ActionResult<Alert>> AcknowledgeAlert(
        string alertId,
        [FromBody] AcknowledgeAlertRequest? request,
        CancellationToken ct)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, ct);
        if (alert == null)
        {
            return NotFound(new { error = "Alert not found" });
        }

        if (alert.Status != "active")
        {
            return BadRequest(new { error = "Only active alerts can be acknowledged" });
        }

        var acknowledgedBy = request?.AcknowledgedBy ?? "system";
        await _alertRepository.AcknowledgeAsync(alertId, acknowledgedBy, ct);

        // Fetch updated alert
        alert = await _alertRepository.GetByIdAsync(alertId, ct);
        _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAlertUpdatedAsync(new AlertPayload
        {
            AlertId = alert!.Id,
            FarmId = alert.FarmId,
            CoordId = alert.CoordId,
            TowerId = alert.TowerId,
            Severity = alert.Severity,
            Status = alert.Status,
            Message = alert.Message,
            Category = alert.Category,
            Timestamp = new DateTimeOffset(alert.CreatedAt).ToUnixTimeMilliseconds()
        }, ct);

        return Ok(alert);
    }

    /// <summary>
    /// Resolve an alert.
    /// </summary>
    [HttpPost("{alertId}/resolve")]
    public async Task<ActionResult<Alert>> ResolveAlert(
        string alertId,
        [FromBody] ResolveAlertRequest? request,
        CancellationToken ct)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, ct);
        if (alert == null)
        {
            return NotFound(new { error = "Alert not found" });
        }

        await _alertRepository.ResolveAsync(alertId, ct);

        // Fetch updated alert
        alert = await _alertRepository.GetByIdAsync(alertId, ct);
        _logger.LogInformation("Alert {AlertId} resolved", alertId);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAlertUpdatedAsync(new AlertPayload
        {
            AlertId = alert!.Id,
            FarmId = alert.FarmId,
            CoordId = alert.CoordId,
            TowerId = alert.TowerId,
            Severity = alert.Severity,
            Status = alert.Status,
            Message = alert.Message,
            Category = alert.Category,
            Timestamp = new DateTimeOffset(alert.CreatedAt).ToUnixTimeMilliseconds()
        }, ct);

        return Ok(alert);
    }
}
