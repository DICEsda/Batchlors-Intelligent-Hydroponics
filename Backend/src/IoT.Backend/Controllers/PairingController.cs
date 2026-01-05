using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing the V2 pairing workflow.
/// Handles pairing sessions, tower requests, and device management.
/// </summary>
[ApiController]
[Route("api/pairing")]
public class PairingController : ControllerBase
{
    private readonly IPairingService _pairingService;
    private readonly ILogger<PairingController> _logger;

    public PairingController(IPairingService pairingService, ILogger<PairingController> logger)
    {
        _pairingService = pairingService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new pairing session for a coordinator.
    /// Puts the coordinator into pairing mode for the specified duration.
    /// </summary>
    /// <param name="request">Pairing session configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created pairing session</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(PairingSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PairingSession>> StartPairingSession(
        [FromBody] StartPairingRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FarmId) || string.IsNullOrWhiteSpace(request.CoordId))
        {
            return BadRequest(new { error = "farm_id and coord_id are required" });
        }

        var session = await _pairingService.StartPairingSessionAsync(
            request.FarmId,
            request.CoordId,
            request.DurationSeconds,
            ct);

        _logger.LogInformation(
            "Pairing session started for {FarmId}/{CoordId} via REST API",
            request.FarmId, request.CoordId);

        return Ok(session);
    }

    /// <summary>
    /// Stop an active pairing session.
    /// Exits the coordinator from pairing mode.
    /// </summary>
    /// <param name="request">Farm and coordinator identification</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The stopped session or 404 if not found</returns>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(PairingSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PairingSession>> StopPairingSession(
        [FromBody] StopPairingRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FarmId) || string.IsNullOrWhiteSpace(request.CoordId))
        {
            return BadRequest(new { error = "farm_id and coord_id are required" });
        }

        var session = await _pairingService.StopPairingSessionAsync(
            request.FarmId,
            request.CoordId,
            ct);

        if (session == null)
        {
            return NotFound(new { error = "No active pairing session found" });
        }

        _logger.LogInformation(
            "Pairing session stopped for {FarmId}/{CoordId} via REST API",
            request.FarmId, request.CoordId);

        return Ok(session);
    }

    /// <summary>
    /// Get the active pairing session for a coordinator.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The active session or 404 if none</returns>
    [HttpGet("session/{farmId}/{coordId}")]
    [ProducesResponseType(typeof(PairingSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PairingSession>> GetActiveSession(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        var session = await _pairingService.GetActiveSessionAsync(farmId, coordId, ct);

        if (session == null)
        {
            return NotFound(new { error = "No active pairing session" });
        }

        return Ok(session);
    }

    /// <summary>
    /// Get all pending tower pairing requests for a coordinator.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending requests</returns>
    [HttpGet("requests/{farmId}/{coordId}")]
    [ProducesResponseType(typeof(IReadOnlyList<TowerPairingRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TowerPairingRequest>>> GetPendingRequests(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        var requests = await _pairingService.GetPendingRequestsAsync(farmId, coordId, ct);
        return Ok(requests);
    }

    /// <summary>
    /// Approve a pending tower pairing request.
    /// Creates the tower entity and sends approval to the coordinator.
    /// </summary>
    /// <param name="request">Approval request details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created tower entity</returns>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(Tower), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Tower>> ApprovePairing(
        [FromBody] ApproveTowerRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FarmId) ||
            string.IsNullOrWhiteSpace(request.CoordId) ||
            string.IsNullOrWhiteSpace(request.TowerId))
        {
            return BadRequest(new { error = "farm_id, coord_id, and tower_id are required" });
        }

        var tower = await _pairingService.ApprovePairingRequestAsync(
            request.FarmId,
            request.CoordId,
            request.TowerId,
            ct);

        if (tower == null)
        {
            return NotFound(new { error = "No pending request found for this tower" });
        }

        _logger.LogInformation(
            "Approved pairing for tower {TowerId} on {FarmId}/{CoordId} via REST API",
            request.TowerId, request.FarmId, request.CoordId);

        return Ok(tower);
    }

    /// <summary>
    /// Reject a pending tower pairing request.
    /// Sends rejection to the coordinator - tower will go to idle state.
    /// </summary>
    /// <param name="request">Rejection request details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectPairing(
        [FromBody] RejectTowerRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FarmId) ||
            string.IsNullOrWhiteSpace(request.CoordId) ||
            string.IsNullOrWhiteSpace(request.TowerId))
        {
            return BadRequest(new { error = "farm_id, coord_id, and tower_id are required" });
        }

        var success = await _pairingService.RejectPairingRequestAsync(
            request.FarmId,
            request.CoordId,
            request.TowerId,
            ct);

        if (!success)
        {
            return NotFound(new { error = "No pending request found for this tower" });
        }

        _logger.LogInformation(
            "Rejected pairing for tower {TowerId} on {FarmId}/{CoordId} via REST API",
            request.TowerId, request.FarmId, request.CoordId);

        return Ok(new { status = "success", message = "Pairing rejected" });
    }

    /// <summary>
    /// Forget a paired device.
    /// Removes the tower from the backend and sends a command to the coordinator
    /// to notify the tower to wipe its credentials.
    /// </summary>
    /// <param name="request">Forget device request details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("forget")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgetDevice(
        [FromBody] ForgetDeviceRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FarmId) ||
            string.IsNullOrWhiteSpace(request.CoordId) ||
            string.IsNullOrWhiteSpace(request.TowerId))
        {
            return BadRequest(new { error = "farm_id, coord_id, and tower_id are required" });
        }

        var success = await _pairingService.ForgetDeviceAsync(
            request.FarmId,
            request.CoordId,
            request.TowerId,
            ct);

        if (!success)
        {
            return NotFound(new { error = "Tower not found" });
        }

        _logger.LogInformation(
            "Device forgotten: tower {TowerId} on {FarmId}/{CoordId} via REST API",
            request.TowerId, request.FarmId, request.CoordId);

        return Ok(new { status = "success", message = "Device forgotten" });
    }
}
