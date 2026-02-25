using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for nodes (towers) - provides /api/nodes endpoints.
/// This is a wrapper around TowerRepository to match frontend expectations.
/// </summary>
[ApiController]
[Route("api/nodes")]
public class NodesController : ControllerBase
{
    private readonly ITowerRepository _towerRepository;
    private readonly ILogger<NodesController> _logger;

    public NodesController(ITowerRepository towerRepository, ILogger<NodesController> logger)
    {
        _towerRepository = towerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all nodes/towers (system-wide).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tower>>> GetAllNodes(CancellationToken ct)
    {
        var towers = await _towerRepository.GetAllAsync(ct);
        return Ok(towers);
    }

    /// <summary>
    /// Get a specific node/tower by ID.
    /// </summary>
    [HttpGet("{nodeId}")]
    public async Task<ActionResult<Tower>> GetNode(string nodeId, CancellationToken ct)
    {
        var tower = await _towerRepository.GetByIdAsync(nodeId, ct);
        if (tower == null)
        {
            return NotFound(new { error = $"Node '{nodeId}' not found" });
        }
        return Ok(tower);
    }

    /// <summary>
    /// Get all nodes for a specific farm.
    /// </summary>
    [HttpGet("farm/{farmId}")]
    public async Task<ActionResult<IEnumerable<Tower>>> GetNodesByFarm(
        string farmId,
        CancellationToken ct)
    {
        var towers = await _towerRepository.GetByFarmAsync(farmId, ct);
        return Ok(towers);
    }

    /// <summary>
    /// Get all nodes for a specific coordinator.
    /// </summary>
    [HttpGet("farm/{farmId}/coord/{coordId}")]
    public async Task<ActionResult<IEnumerable<Tower>>> GetNodesByCoordinator(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        var towers = await _towerRepository.GetByCoordinatorAsync(farmId, coordId, ct);
        return Ok(towers);
    }
}
