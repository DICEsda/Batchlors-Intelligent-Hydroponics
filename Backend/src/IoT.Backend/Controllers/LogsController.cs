using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// API controller for serial logs from coordinators
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IRepository repository, ILogger<LogsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // TODO: SerialLog persistence not yet implemented in repository
    // These endpoints are commented out until GetSerialLogsAsync is added to IRepository
    
    // /// <summary>
    // /// Get serial logs with optional filtering
    // /// </summary>
    // /// <param name="siteId">Optional site ID filter</param>
    // /// <param name="coordId">Optional coordinator ID filter</param>
    // /// <param name="limit">Maximum number of logs to return (default 100, max 1000)</param>
    // /// <param name="ct">Cancellation token</param>
    // [HttpGet]
    // public async Task<ActionResult<IReadOnlyList<SerialLog>>> GetLogs(
    //     [FromQuery] string? siteId = null,
    //     [FromQuery] string? coordId = null,
    //     [FromQuery] int limit = 100,
    //     CancellationToken ct = default)
    // {
    //     if (limit <= 0 || limit > 1000)
    //     {
    //         limit = 100;
    //     }

    //     try
    //     {
    //         var logs = await _repository.GetSerialLogsAsync(siteId, coordId, limit, ct);
    //         return Ok(logs);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to retrieve serial logs");
    //         return StatusCode(500, "Failed to retrieve logs");
    //     }
    // }

    // /// <summary>
    // /// Get serial logs for a specific coordinator
    // /// </summary>
    // /// <param name="coordId">Coordinator ID</param>
    // /// <param name="limit">Maximum number of logs to return (default 100, max 1000)</param>
    // /// <param name="ct">Cancellation token</param>
    // [HttpGet("coordinator/{coordId}")]
    // public async Task<ActionResult<IReadOnlyList<SerialLog>>> GetCoordinatorLogs(
    //     string coordId,
    //     [FromQuery] int limit = 100,
    //     CancellationToken ct = default)
    // {
    //     if (limit <= 0 || limit > 1000)
    //     {
    //         limit = 100;
    //     }

    //     try
    //     {
    //         var logs = await _repository.GetSerialLogsAsync(null, coordId, limit, ct);
    //         return Ok(logs);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to retrieve coordinator logs for {CoordId}", coordId);
    //         return StatusCode(500, "Failed to retrieve logs");
    //     }
    // }
}
