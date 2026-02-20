using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing farms (hydroponic farms/sites).
/// </summary>
[ApiController]
[Route("api/v1/farms")]
public class FarmsController : ControllerBase
{
    private readonly IFarmRepository _farmRepository;
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly ILogger<FarmsController> _logger;

    public FarmsController(
        IFarmRepository farmRepository,
        ICoordinatorRepository coordinatorRepository,
        ITowerRepository towerRepository,
        ILogger<FarmsController> logger)
    {
        _farmRepository = farmRepository;
        _coordinatorRepository = coordinatorRepository;
        _towerRepository = towerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all farms.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Farm>>> GetAllFarms(CancellationToken ct)
    {
        var farms = await _farmRepository.GetAllAsync(ct);
        return Ok(farms);
    }

    /// <summary>
    /// Get a specific farm by farm_id.
    /// </summary>
    [HttpGet("{farmId}")]
    public async Task<ActionResult<Farm>> GetFarm(string farmId, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByFarmIdAsync(farmId, ct);
        if (farm == null)
        {
            return NotFound(new { error = $"Farm '{farmId}' not found" });
        }
        return Ok(farm);
    }

    /// <summary>
    /// Create a new farm manually.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Farm>> CreateFarm([FromBody] CreateFarmRequest request, CancellationToken ct)
    {
        // Check if farm already exists
        var existing = await _farmRepository.GetByFarmIdAsync(request.FarmId, ct);
        if (existing != null)
        {
            return Conflict(new { error = $"Farm '{request.FarmId}' already exists" });
        }

        var farm = new Farm
        {
            Id = null!, // Let MongoDB generate the _id
            FarmId = request.FarmId,
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            AutoDiscovered = false,
            CreatedAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };

        await _farmRepository.UpsertAsync(farm, ct);
        _logger.LogInformation("Created farm {FarmId} manually", request.FarmId);

        return CreatedAtAction(nameof(GetFarm), new { farmId = farm.FarmId }, farm);
    }

    /// <summary>
    /// Update an existing farm.
    /// </summary>
    [HttpPut("{farmId}")]
    public async Task<ActionResult<Farm>> UpdateFarm(
        string farmId,
        [FromBody] UpdateFarmRequest request,
        CancellationToken ct)
    {
        var farm = await _farmRepository.GetByFarmIdAsync(farmId, ct);
        if (farm == null)
        {
            return NotFound(new { error = $"Farm '{farmId}' not found" });
        }

        if (request.Name != null)
            farm.Name = request.Name;
        if (request.Description != null)
            farm.Description = request.Description;
        if (request.Location != null)
            farm.Location = request.Location;

        await _farmRepository.UpsertAsync(farm, ct);
        _logger.LogInformation("Updated farm {FarmId}", farmId);

        return Ok(farm);
    }

    /// <summary>
    /// Delete a farm (only if no coordinators or towers exist).
    /// </summary>
    [HttpDelete("{farmId}")]
    public async Task<IActionResult> DeleteFarm(string farmId, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByFarmIdAsync(farmId, ct);
        if (farm == null)
        {
            return NotFound(new { error = $"Farm '{farmId}' not found" });
        }

        // Check if farm has coordinators or towers
        var coordinators = await _coordinatorRepository.GetByFarmAsync(farmId, ct);
        if (coordinators.Count > 0)
        {
            return BadRequest(new { error = "Cannot delete farm with active coordinators" });
        }

        var towers = await _towerRepository.GetByFarmAsync(farmId, ct);
        if (towers.Count > 0)
        {
            return BadRequest(new { error = "Cannot delete farm with active towers" });
        }

        await _farmRepository.DeleteAsync(farm.Id, ct);
        _logger.LogInformation("Deleted farm {FarmId}", farmId);

        return NoContent();
    }

    /// <summary>
    /// Get all coordinators in a farm.
    /// </summary>
    [HttpGet("{farmId}/coordinators")]
    public async Task<ActionResult<IEnumerable<Coordinator>>> GetFarmCoordinators(
        string farmId,
        CancellationToken ct)
    {
        var coordinators = await _coordinatorRepository.GetByFarmAsync(farmId, ct);
        return Ok(coordinators);
    }

    /// <summary>
    /// Get all towers in a farm.
    /// </summary>
    [HttpGet("{farmId}/towers")]
    public async Task<ActionResult<IEnumerable<Tower>>> GetFarmTowers(
        string farmId,
        CancellationToken ct)
    {
        var towers = await _towerRepository.GetByFarmAsync(farmId, ct);
        return Ok(towers);
    }
}
