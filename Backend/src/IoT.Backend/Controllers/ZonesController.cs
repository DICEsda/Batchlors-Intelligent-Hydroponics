using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing zones.
/// </summary>
[ApiController]
[Route("api/zones")]
public class ZonesController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ILogger<ZonesController> _logger;

    public ZonesController(IRepository repository, ILogger<ZonesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all zones for a site.
    /// </summary>
    [HttpGet("site/{siteId}")]
    public async Task<ActionResult<IEnumerable<Zone>>> GetZonesBySite(string siteId, CancellationToken ct)
    {
        var zones = await _repository.GetZonesBySiteAsync(siteId, ct);
        return Ok(zones);
    }

    /// <summary>
    /// Get a specific zone by ID.
    /// </summary>
    [HttpGet("{zoneId}")]
    public async Task<ActionResult<Zone>> GetZone(string zoneId, CancellationToken ct)
    {
        var zone = await _repository.GetZoneByIdAsync(zoneId, ct);
        if (zone == null)
        {
            return NotFound();
        }
        return Ok(zone);
    }

    /// <summary>
    /// Create a new zone.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Zone>> CreateZone([FromBody] CreateZoneRequest request, CancellationToken ct)
    {
        var zone = new Zone
        {
            Id = Guid.NewGuid().ToString("N"),
            SiteId = request.SiteId,
            Name = request.Name,
            Description = request.Description,
            CoordinatorId = request.CoordinatorId,
            Color = request.Color
        };

        await _repository.CreateZoneAsync(zone, ct);
        _logger.LogInformation("Created zone {ZoneId} in site {SiteId}", zone.Id, zone.SiteId);
        
        return CreatedAtAction(nameof(GetZone), new { zoneId = zone.Id }, zone);
    }

    /// <summary>
    /// Update a zone.
    /// </summary>
    [HttpPut("{zoneId}")]
    public async Task<IActionResult> UpdateZone(string zoneId, [FromBody] UpdateZoneRequest request, CancellationToken ct)
    {
        var zone = await _repository.GetZoneByIdAsync(zoneId, ct);
        if (zone == null)
        {
            return NotFound();
        }

        zone.Name = request.Name ?? zone.Name;
        zone.Description = request.Description ?? zone.Description;
        zone.Color = request.Color ?? zone.Color;

        await _repository.UpdateZoneAsync(zone, ct);
        _logger.LogInformation("Updated zone {ZoneId}", zoneId);
        
        return NoContent();
    }

    /// <summary>
    /// Delete a zone.
    /// </summary>
    [HttpDelete("{zoneId}")]
    public async Task<IActionResult> DeleteZone(string zoneId, CancellationToken ct)
    {
        await _repository.DeleteZoneAsync(zoneId, ct);
        _logger.LogInformation("Deleted zone {ZoneId}", zoneId);
        return NoContent();
    }
}

public class CreateZoneRequest
{
    public string SiteId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoordinatorId { get; set; }
    public string? Color { get; set; }
}

public class UpdateZoneRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
}
