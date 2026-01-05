using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for retrieving time-series telemetry data.
/// Provides endpoints for querying historical reservoir and tower telemetry.
/// </summary>
[ApiController]
[Route("api/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        ICoordinatorRepository coordinatorRepository,
        ITelemetryRepository telemetryRepository,
        ILogger<TelemetryController> logger)
    {
        _coordinatorRepository = coordinatorRepository;
        _telemetryRepository = telemetryRepository;
        _logger = logger;
    }

    // ============================================================================
    // Coordinator/Legacy Endpoints
    // ============================================================================

    /// <summary>
    /// Get latest telemetry for a coordinator (legacy endpoint).
    /// </summary>
    [HttpGet("coordinator/{siteId}/{coordId}")]
    public async Task<ActionResult<Coordinator>> GetCoordinatorTelemetry(
        string siteId, 
        string coordId, 
        CancellationToken ct)
    {
        var coordinator = await _coordinatorRepository.GetBySiteAndIdAsync(siteId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound();
        }
        return Ok(coordinator);
    }

    // ============================================================================
    // Reservoir Telemetry Endpoints
    // ============================================================================

    /// <summary>
    /// Get reservoir telemetry history for a coordinator.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="from">Start of time range (defaults to 24 hours ago)</param>
    /// <param name="to">End of time range (defaults to now)</param>
    /// <param name="limit">Maximum number of records to return (default 1000)</param>
    [HttpGet("reservoir/{farmId}/{coordId}")]
    public async Task<ActionResult<IEnumerable<ReservoirTelemetry>>> GetReservoirTelemetry(
        string farmId,
        string coordId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 1000,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
        var toDate = to ?? DateTime.UtcNow;
        
        var telemetry = await _telemetryRepository.GetReservoirTelemetryAsync(
            farmId, coordId, fromDate, toDate, limit, ct);
        
        return Ok(telemetry);
    }

    /// <summary>
    /// Get the latest reservoir telemetry for a coordinator.
    /// </summary>
    [HttpGet("reservoir/{farmId}/{coordId}/latest")]
    public async Task<ActionResult<ReservoirTelemetry>> GetLatestReservoirTelemetry(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        var telemetry = await _telemetryRepository.GetLatestReservoirTelemetryAsync(farmId, coordId, ct);
        if (telemetry == null)
        {
            return NotFound();
        }
        return Ok(telemetry);
    }

    /// <summary>
    /// Get daily averaged reservoir telemetry for charting.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="from">Start of time range (defaults to 30 days ago)</param>
    /// <param name="to">End of time range (defaults to now)</param>
    [HttpGet("reservoir/{farmId}/{coordId}/daily")]
    public async Task<ActionResult<IEnumerable<ReservoirTelemetry>>> GetDailyReservoirTelemetry(
        string farmId,
        string coordId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        
        var telemetry = await _telemetryRepository.GetDailyAverageReservoirTelemetryAsync(
            farmId, coordId, fromDate, toDate, ct);
        
        return Ok(telemetry);
    }

    // ============================================================================
    // Tower Telemetry Endpoints
    // ============================================================================

    /// <summary>
    /// Get tower telemetry history for a specific tower.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="towerId">Tower identifier</param>
    /// <param name="from">Start of time range (defaults to 24 hours ago)</param>
    /// <param name="to">End of time range (defaults to now)</param>
    /// <param name="limit">Maximum number of records to return (default 1000)</param>
    [HttpGet("tower/{farmId}/{coordId}/{towerId}")]
    public async Task<ActionResult<IEnumerable<TowerTelemetry>>> GetTowerTelemetry(
        string farmId,
        string coordId,
        string towerId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 1000,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
        var toDate = to ?? DateTime.UtcNow;
        
        var telemetry = await _telemetryRepository.GetTowerTelemetryAsync(
            farmId, coordId, towerId, fromDate, toDate, limit, ct);
        
        return Ok(telemetry);
    }

    /// <summary>
    /// Get the latest tower telemetry for a specific tower.
    /// </summary>
    [HttpGet("tower/{farmId}/{coordId}/{towerId}/latest")]
    public async Task<ActionResult<TowerTelemetry>> GetLatestTowerTelemetry(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct)
    {
        var telemetry = await _telemetryRepository.GetLatestTowerTelemetryAsync(farmId, coordId, towerId, ct);
        if (telemetry == null)
        {
            return NotFound();
        }
        return Ok(telemetry);
    }

    /// <summary>
    /// Get the latest telemetry for all towers under a coordinator.
    /// </summary>
    [HttpGet("tower/{farmId}/{coordId}/latest")]
    public async Task<ActionResult<IEnumerable<TowerTelemetry>>> GetLatestTowerTelemetryByCoordinator(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        var telemetry = await _telemetryRepository.GetLatestTowerTelemetryByCoordinatorAsync(farmId, coordId, ct);
        return Ok(telemetry);
    }

    /// <summary>
    /// Get daily averaged tower telemetry for charting.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="towerId">Tower identifier</param>
    /// <param name="from">Start of time range (defaults to 30 days ago)</param>
    /// <param name="to">End of time range (defaults to now)</param>
    [HttpGet("tower/{farmId}/{coordId}/{towerId}/daily")]
    public async Task<ActionResult<IEnumerable<TowerTelemetry>>> GetDailyTowerTelemetry(
        string farmId,
        string coordId,
        string towerId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        
        var telemetry = await _telemetryRepository.GetDailyAverageTowerTelemetryAsync(
            farmId, coordId, towerId, fromDate, toDate, ct);
        
        return Ok(telemetry);
    }

    // ============================================================================
    // Height Measurements Endpoints
    // ============================================================================

    /// <summary>
    /// Get height measurements for a tower.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="towerId">Tower identifier</param>
    /// <param name="slotIndex">Optional slot index filter</param>
    /// <param name="from">Start of time range</param>
    /// <param name="to">End of time range</param>
    /// <param name="limit">Maximum number of records to return (default 500)</param>
    [HttpGet("height/{farmId}/{towerId}")]
    public async Task<ActionResult<IEnumerable<HeightMeasurement>>> GetHeightMeasurements(
        string farmId,
        string towerId,
        [FromQuery] int? slotIndex = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        var measurements = await _telemetryRepository.GetHeightMeasurementsAsync(
            farmId, towerId, slotIndex, from, to, limit, ct);
        
        return Ok(measurements);
    }

    /// <summary>
    /// Get the latest height measurements for each slot in a tower.
    /// </summary>
    [HttpGet("height/{farmId}/{towerId}/latest")]
    public async Task<ActionResult<IEnumerable<HeightMeasurement>>> GetLatestHeightMeasurements(
        string farmId,
        string towerId,
        CancellationToken ct)
    {
        var measurements = await _telemetryRepository.GetLatestHeightMeasurementsByTowerAsync(farmId, towerId, ct);
        return Ok(measurements);
    }
}
