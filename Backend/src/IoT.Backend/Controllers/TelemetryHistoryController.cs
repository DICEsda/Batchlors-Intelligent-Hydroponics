using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// Provides REST endpoints for querying historical telemetry data
/// (reservoir and tower) from the time-series MongoDB collections.
/// </summary>
[ApiController]
[Route("api/telemetry")]
public class TelemetryHistoryController : ControllerBase
{
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly ILogger<TelemetryHistoryController> _logger;

    public TelemetryHistoryController(
        ITelemetryRepository telemetryRepository,
        ILogger<TelemetryHistoryController> logger)
    {
        _telemetryRepository = telemetryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get historical reservoir telemetry for a coordinator within a time window.
    /// </summary>
    /// <param name="coordId">Coordinator ID (required).</param>
    /// <param name="farmId">Farm ID (defaults to "farm-001" if not provided).</param>
    /// <param name="minutes">Number of minutes of history to return (default 60).</param>
    [HttpGet("reservoir/history")]
    public async Task<ActionResult<IReadOnlyList<ReservoirTelemetry>>> GetReservoirHistory(
        [FromQuery] string? coordId,
        [FromQuery] string? farmId,
        [FromQuery] int minutes = 60)
    {
        if (string.IsNullOrWhiteSpace(coordId))
        {
            return BadRequest(new { error = "coordId query parameter is required" });
        }

        farmId ??= "farm-001";
        if (minutes < 1) minutes = 1;
        if (minutes > 1440) minutes = 1440; // max 24 hours

        var from = DateTime.UtcNow.AddMinutes(-minutes);
        var to = DateTime.UtcNow;

        _logger.LogDebug("Fetching reservoir history for {FarmId}/{CoordId} from {From} to {To}",
            farmId, coordId, from, to);

        var data = await _telemetryRepository.GetReservoirTelemetryAsync(farmId, coordId, from, to);
        return Ok(data);
    }

    /// <summary>
    /// Get historical tower telemetry for a specific tower within a time window.
    /// </summary>
    /// <param name="towerId">Tower ID (required).</param>
    /// <param name="farmId">Farm ID (defaults to "farm-001" if not provided).</param>
    /// <param name="coordId">Coordinator ID (defaults to "coord-001" if not provided).</param>
    /// <param name="minutes">Number of minutes of history to return (default 60).</param>
    [HttpGet("tower/history")]
    public async Task<ActionResult<IReadOnlyList<TowerTelemetry>>> GetTowerHistory(
        [FromQuery] string? towerId,
        [FromQuery] string? farmId,
        [FromQuery] string? coordId,
        [FromQuery] int minutes = 60)
    {
        if (string.IsNullOrWhiteSpace(towerId))
        {
            return BadRequest(new { error = "towerId query parameter is required" });
        }

        farmId ??= "farm-001";
        coordId ??= "coord-001";
        if (minutes < 1) minutes = 1;
        if (minutes > 1440) minutes = 1440; // max 24 hours

        var from = DateTime.UtcNow.AddMinutes(-minutes);
        var to = DateTime.UtcNow;

        _logger.LogDebug("Fetching tower history for {FarmId}/{CoordId}/{TowerId} from {From} to {To}",
            farmId, coordId, towerId, from, to);

        var data = await _telemetryRepository.GetTowerTelemetryAsync(farmId, coordId, towerId, from, to);
        return Ok(data);
    }

    /// <summary>
    /// Get the latest reservoir telemetry for a coordinator.
    /// </summary>
    /// <param name="farmId">Farm ID (required).</param>
    /// <param name="coordId">Coordinator ID (required).</param>
    [HttpGet("reservoir/latest")]
    public async Task<ActionResult<ReservoirTelemetry>> GetLatestReservoir(
        [FromQuery] string? farmId,
        [FromQuery] string? coordId)
    {
        if (string.IsNullOrWhiteSpace(farmId))
        {
            return BadRequest(new { error = "farmId query parameter is required" });
        }

        if (string.IsNullOrWhiteSpace(coordId))
        {
            return BadRequest(new { error = "coordId query parameter is required" });
        }

        var latest = await _telemetryRepository.GetLatestReservoirTelemetryAsync(farmId, coordId);
        if (latest == null)
        {
            return NotFound(new { error = $"No reservoir telemetry found for {farmId}/{coordId}" });
        }

        return Ok(latest);
    }
}
