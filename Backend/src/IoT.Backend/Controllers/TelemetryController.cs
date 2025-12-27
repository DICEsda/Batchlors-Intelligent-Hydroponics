using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for retrieving telemetry data.
/// </summary>
[ApiController]
[Route("api/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(IRepository repository, ILogger<TelemetryController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get latest telemetry for a coordinator.
    /// </summary>
    [HttpGet("coordinator/{siteId}/{coordId}")]
    public async Task<ActionResult<Coordinator>> GetCoordinatorTelemetry(string siteId, string coordId, CancellationToken ct)
    {
        var coordinator = await _repository.GetCoordinatorBySiteAndIdAsync(siteId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound();
        }
        return Ok(coordinator);
    }

    /// <summary>
    /// Get latest telemetry for all nodes under a coordinator.
    /// </summary>
    [HttpGet("nodes/{siteId}/{coordId}")]
    public async Task<ActionResult<IEnumerable<Node>>> GetNodesTelemetry(string siteId, string coordId, CancellationToken ct)
    {
        var nodes = await _repository.GetNodesByCoordinatorAsync(siteId, coordId, ct);
        return Ok(nodes);
    }

    /// <summary>
    /// Get telemetry for a specific node.
    /// </summary>
    [HttpGet("node/{siteId}/{coordId}/{nodeId}")]
    public async Task<ActionResult<Node>> GetNodeTelemetry(string siteId, string coordId, string nodeId, CancellationToken ct)
    {
        var node = await _repository.GetNodeByIdAsync($"{siteId}/{coordId}/{nodeId}", ct);
        if (node == null)
        {
            return NotFound();
        }
        return Ok(node);
    }
}
