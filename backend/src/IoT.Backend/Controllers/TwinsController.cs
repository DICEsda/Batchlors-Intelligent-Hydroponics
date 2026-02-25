using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Models.Responses;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for Digital Twin operations - managing desired/reported state for towers and coordinators
/// </summary>
[ApiController]
[Route("api/twins")]
public class TwinsController : ControllerBase
{
    private readonly ITwinService _twinService;
    private readonly ILogger<TwinsController> _logger;

    public TwinsController(ITwinService twinService, ILogger<TwinsController> logger)
    {
        _twinService = twinService;
        _logger = logger;
    }

    // ============================================================================
    // Tower Twin Endpoints
    // ============================================================================

    /// <summary>
    /// Get a tower twin by ID
    /// </summary>
    [HttpGet("towers/{towerId}")]
    [ProducesResponseType(typeof(TowerTwin), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TowerTwin>> GetTowerTwin(string towerId, CancellationToken ct)
    {
        var twin = await _twinService.GetTowerTwinAsync(towerId, ct);
        if (twin == null)
        {
            return NotFound(new { error = "Tower twin not found", tower_id = towerId });
        }
        return Ok(twin);
    }

    /// <summary>
    /// Get all tower twins for a coordinator
    /// </summary>
    [HttpGet("towers")]
    [ProducesResponseType(typeof(IReadOnlyList<TowerTwin>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TowerTwin>>> GetTowerTwins(
        [FromQuery] string farmId,
        [FromQuery] string coordId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(farmId) || string.IsNullOrEmpty(coordId))
        {
            return BadRequest(new { error = "Both farm_id and coord_id query parameters are required" });
        }

        var twins = await _twinService.GetTowerTwinsForCoordinatorAsync(farmId, coordId, ct);
        return Ok(twins);
    }

    /// <summary>
    /// Set the desired state for a tower (triggers command sync to device)
    /// </summary>
    [HttpPut("towers/{towerId}/desired")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetTowerDesiredState(
        string towerId,
        [FromBody] TowerDesiredState desired,
        CancellationToken ct)
    {
        if (desired == null)
        {
            return BadRequest(new { error = "Desired state is required" });
        }

        await _twinService.SetTowerDesiredStateAsync(towerId, desired, ct);
        _logger.LogInformation("Set desired state for tower {TowerId}", towerId);

        return Accepted(new
        {
            status = "pending",
            message = "Desired state set, command queued for delivery",
            tower_id = towerId
        });
    }

    /// <summary>
    /// Get the state delta between desired and reported for a tower
    /// Returns null/empty if states are in sync
    /// </summary>
    [HttpGet("towers/{towerId}/delta")]
    [ProducesResponseType(typeof(TowerDesiredState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TowerDeltaResponse>> GetTowerStateDelta(string towerId, CancellationToken ct)
    {
        var twin = await _twinService.GetTowerTwinAsync(towerId, ct);
        if (twin == null)
        {
            return NotFound(new { error = "Tower twin not found", tower_id = towerId });
        }

        var delta = await _twinService.GetTowerStateDeltaAsync(towerId, ct);
        return Ok(new TowerDeltaResponse
        {
            TowerId = towerId,
            SyncStatus = twin.Metadata.SyncStatus.ToString().ToLowerInvariant(),
            IsInSync = delta == null,
            Delta = delta
        });
    }

    /// <summary>
    /// Mark a tower sync as successful (typically called by command acknowledgment handler)
    /// </summary>
    [HttpPost("towers/{towerId}/sync/success")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkTowerSyncSuccess(string towerId, CancellationToken ct)
    {
        await _twinService.MarkTowerSyncSuccessAsync(towerId, ct);
        return Ok(new { status = "success", tower_id = towerId });
    }

    // ============================================================================
    // Coordinator Twin Endpoints
    // ============================================================================

    /// <summary>
    /// Get a coordinator twin by ID
    /// </summary>
    [HttpGet("coordinators/{coordId}")]
    [ProducesResponseType(typeof(CoordinatorTwin), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoordinatorTwin>> GetCoordinatorTwin(string coordId, CancellationToken ct)
    {
        var twin = await _twinService.GetCoordinatorTwinAsync(coordId, ct);
        if (twin == null)
        {
            return NotFound(new { error = "Coordinator twin not found", coord_id = coordId });
        }
        return Ok(twin);
    }

    /// <summary>
    /// Get all coordinator twins for a farm
    /// </summary>
    [HttpGet("coordinators")]
    [ProducesResponseType(typeof(IReadOnlyList<CoordinatorTwin>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CoordinatorTwin>>> GetCoordinatorTwins(
        [FromQuery] string farmId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(farmId))
        {
            return BadRequest(new { error = "farm_id query parameter is required" });
        }

        var twins = await _twinService.GetCoordinatorTwinsForFarmAsync(farmId, ct);
        return Ok(twins);
    }

    /// <summary>
    /// Set the desired state for a coordinator (triggers command sync to device)
    /// </summary>
    [HttpPut("coordinators/{coordId}/desired")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetCoordinatorDesiredState(
        string coordId,
        [FromBody] CoordinatorDesiredState desired,
        CancellationToken ct)
    {
        if (desired == null)
        {
            return BadRequest(new { error = "Desired state is required" });
        }

        await _twinService.SetCoordinatorDesiredStateAsync(coordId, desired, ct);
        _logger.LogInformation("Set desired state for coordinator {CoordId}", coordId);

        return Accepted(new
        {
            status = "pending",
            message = "Desired state set, command queued for delivery",
            coord_id = coordId
        });
    }

    /// <summary>
    /// Get the state delta between desired and reported for a coordinator
    /// Returns null/empty if states are in sync
    /// </summary>
    [HttpGet("coordinators/{coordId}/delta")]
    [ProducesResponseType(typeof(CoordinatorDeltaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoordinatorDeltaResponse>> GetCoordinatorStateDelta(string coordId, CancellationToken ct)
    {
        var twin = await _twinService.GetCoordinatorTwinAsync(coordId, ct);
        if (twin == null)
        {
            return NotFound(new { error = "Coordinator twin not found", coord_id = coordId });
        }

        var delta = await _twinService.GetCoordinatorStateDeltaAsync(coordId, ct);
        return Ok(new CoordinatorDeltaResponse
        {
            CoordId = coordId,
            SyncStatus = twin.Metadata.SyncStatus.ToString().ToLowerInvariant(),
            IsInSync = delta == null,
            Delta = delta
        });
    }

    /// <summary>
    /// Mark a coordinator sync as successful (typically called by command acknowledgment handler)
    /// </summary>
    [HttpPost("coordinators/{coordId}/sync/success")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkCoordinatorSyncSuccess(string coordId, CancellationToken ct)
    {
        await _twinService.MarkCoordinatorSyncSuccessAsync(coordId, ct);
        return Ok(new { status = "success", coord_id = coordId });
    }

    // ============================================================================
    // Convenience Endpoints for Farm Overview
    // ============================================================================

    /// <summary>
    /// Get all twins (coordinators and their towers) for a farm
    /// </summary>
    [HttpGet("farms/{farmId}")]
    [ProducesResponseType(typeof(FarmTwinsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FarmTwinsResponse>> GetFarmTwins(string farmId, CancellationToken ct)
    {
        var coordinators = await _twinService.GetCoordinatorTwinsForFarmAsync(farmId, ct);
        var allTowers = new List<TowerTwin>();

        foreach (var coord in coordinators)
        {
            var towers = await _twinService.GetTowerTwinsForCoordinatorAsync(farmId, coord.CoordId, ct);
            allTowers.AddRange(towers);
        }

        return Ok(new FarmTwinsResponse
        {
            FarmId = farmId,
            Coordinators = coordinators,
            Towers = allTowers
        });
    }
}
