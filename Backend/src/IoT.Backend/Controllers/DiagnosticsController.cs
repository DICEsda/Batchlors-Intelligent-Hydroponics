using IoT.Backend.Models.Diagnostics;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// Exposes backend performance metrics (throughput, latency, errors)
/// for the frontend diagnostics dashboard.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly IWsBroadcaster _broadcaster;

    public DiagnosticsController(IDiagnosticsService diagnostics, IWsBroadcaster broadcaster)
    {
        _diagnostics = diagnostics;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Returns the most recent performance metrics snapshot.
    /// </summary>
    [HttpGet]
    public ActionResult<SystemMetricsSnapshot> GetCurrent()
    {
        var snapshot = _diagnostics.GetCurrentSnapshot(_broadcaster.ConnectedClientCount);
        return Ok(snapshot);
    }

    /// <summary>
    /// Returns historical performance snapshots for the last N minutes.
    /// </summary>
    /// <param name="minutes">Number of minutes of history to return (default 30, max 60).</param>
    [HttpGet("history")]
    public ActionResult<IReadOnlyList<SystemMetricsSnapshot>> GetHistory([FromQuery] int minutes = 30)
    {
        if (minutes < 1) minutes = 1;
        if (minutes > 60) minutes = 60;

        var history = _diagnostics.GetHistory(minutes);
        return Ok(history);
    }

    /// <summary>
    /// Resets all diagnostics counters and history.
    /// Useful between test runs or after deployments.
    /// </summary>
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        _diagnostics.Reset();
        return Ok(new { message = "Diagnostics counters reset" });
    }
}
