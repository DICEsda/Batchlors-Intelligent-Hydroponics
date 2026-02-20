using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing zones.
/// </summary>
[ApiController]
[Route("api/zones")]
public class ZonesController : ControllerBase
{
    private readonly IZoneRepository _zoneRepository;
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<ZonesController> _logger;

    public ZonesController(IZoneRepository zoneRepository, ICoordinatorRepository coordinatorRepository, IMqttService mqtt, ILogger<ZonesController> logger)
    {
        _zoneRepository = zoneRepository;
        _coordinatorRepository = coordinatorRepository;
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get all zones (system-wide) for frontend /api/v1/zones endpoint.
    /// </summary>
    [HttpGet]
    [Route("/api/v1/zones")]
    public async Task<ActionResult<IEnumerable<Zone>>> GetAllZones(CancellationToken ct)
    {
        var zones = await _zoneRepository.GetAllAsync(ct);
        return Ok(zones);
    }

    /// <summary>
    /// Get all zones for a site.
    /// </summary>
    [HttpGet("site/{siteId}")]
    public async Task<ActionResult<IEnumerable<Zone>>> GetZonesBySite(string siteId, CancellationToken ct)
    {
        var zones = await _zoneRepository.GetBySiteAsync(siteId, ct);
        return Ok(new { zones });
    }

    /// <summary>
    /// Get a specific zone by ID.
    /// </summary>
    [HttpGet("{zoneId}")]
    public async Task<ActionResult<Zone>> GetZone(string zoneId, CancellationToken ct)
    {
        var zone = await _zoneRepository.GetByIdAsync(zoneId, ct);
        if (zone == null)
        {
            return NotFound();
        }
        return Ok(zone);
    }

    /// <summary>
    /// Create a new zone.
    /// Validates coordinator exists and is not already assigned.
    /// Flashes coordinator green to confirm creation.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Zone>> CreateZone([FromBody] CreateZoneRequest request, CancellationToken ct)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.SiteId) || string.IsNullOrEmpty(request.CoordinatorId))
        {
            return BadRequest(new { error = "Name, site_id, and coordinator_id are required" });
        }

        // Check if coordinator exists
        var coordinator = await _coordinatorRepository.GetBySiteAndIdAsync(request.SiteId, request.CoordinatorId, ct);
        if (coordinator == null)
        {
            return NotFound(new { error = "Coordinator not found" });
        }

        // Check if coordinator is already assigned to another zone
        var existingZone = await _zoneRepository.GetByCoordinatorAsync(request.SiteId, request.CoordinatorId, ct);
        if (existingZone != null)
        {
            return Conflict(new { error = "Coordinator is already assigned to a zone" });
        }

        // Create zone
        var zone = new Zone
        {
            Id = Guid.NewGuid().ToString("N"),
            SiteId = request.SiteId,
            Name = request.Name,
            Description = request.Description,
            CoordinatorId = request.CoordinatorId,
            Color = request.Color
        };

        await _zoneRepository.CreateAsync(zone, ct);
        _logger.LogInformation("Created zone {ZoneId} in site {SiteId} for coordinator {CoordinatorId}",
            zone.Id, zone.SiteId, zone.CoordinatorId);

        // Flash coordinator green to confirm zone creation
        await FlashCoordinatorGreenAsync(request.SiteId, request.CoordinatorId, ct);

        return CreatedAtAction(nameof(GetZone), new { zoneId = zone.Id }, new
        {
            status = "success",
            message = "Zone created successfully",
            zone
        });
    }

    /// <summary>
    /// Update a zone.
    /// If coordinator is changed, validates new coordinator exists and is not already assigned.
    /// Flashes coordinator green to confirm update.
    /// </summary>
    [HttpPut("{zoneId}")]
    public async Task<IActionResult> UpdateZone(string zoneId, [FromBody] UpdateZoneRequest request, CancellationToken ct)
    {
        var zone = await _zoneRepository.GetByIdAsync(zoneId, ct);
        if (zone == null)
        {
            return NotFound();
        }

        // Update name if provided
        if (!string.IsNullOrEmpty(request.Name))
        {
            zone.Name = request.Name;
        }

        // Update description if provided
        if (request.Description != null)
        {
            zone.Description = request.Description;
        }

        // Update color if provided
        if (request.Color != null)
        {
            zone.Color = request.Color;
        }

        // Handle coordinator change
        if (!string.IsNullOrEmpty(request.CoordinatorId) && request.CoordinatorId != zone.CoordinatorId)
        {
            // Check if new coordinator exists
            var newCoordinator = await _coordinatorRepository.GetBySiteAndIdAsync(zone.SiteId, request.CoordinatorId, ct);
            if (newCoordinator == null)
            {
                return NotFound(new { error = "Coordinator not found" });
            }

            // Check if new coordinator is already assigned to another zone
            var existingZone = await _zoneRepository.GetByCoordinatorAsync(zone.SiteId, request.CoordinatorId, ct);
            if (existingZone != null && existingZone.Id != zoneId)
            {
                return Conflict(new { error = "Coordinator is already assigned to another zone" });
            }

            zone.CoordinatorId = request.CoordinatorId;
        }

        await _zoneRepository.UpdateAsync(zone, ct);
        _logger.LogInformation("Updated zone {ZoneId}", zoneId);

        // Flash coordinator green to confirm update
        if (!string.IsNullOrEmpty(zone.CoordinatorId))
        {
            await FlashCoordinatorGreenAsync(zone.SiteId, zone.CoordinatorId, ct);
        }

        return Ok(new
        {
            status = "success",
            message = "Zone updated successfully",
            zone
        });
    }

    /// <summary>
    /// Delete a zone.
    /// Flashes coordinator green to confirm deletion.
    /// </summary>
    [HttpDelete("{zoneId}")]
    public async Task<IActionResult> DeleteZone(string zoneId, CancellationToken ct)
    {
        // Get zone before deleting to flash coordinator
        var zone = await _zoneRepository.GetByIdAsync(zoneId, ct);
        if (zone == null)
        {
            return NotFound();
        }

        await _zoneRepository.DeleteAsync(zoneId, ct);
        _logger.LogInformation("Deleted zone {ZoneId}", zoneId);

        // Flash coordinator green to confirm deletion
        if (!string.IsNullOrEmpty(zone.CoordinatorId))
        {
            await FlashCoordinatorGreenAsync(zone.SiteId, zone.CoordinatorId, ct);
        }

        return Ok(new
        {
            status = "success",
            message = "Zone deleted successfully"
        });
    }

    /// <summary>
    /// Send MQTT command to flash coordinator LED green 3 times.
    /// Used to provide visual confirmation of zone operations.
    /// </summary>
    private async Task FlashCoordinatorGreenAsync(string siteId, string coordinatorId, CancellationToken ct)
    {
        try
        {
            var topic = $"site/{siteId}/coord/{coordinatorId}/cmd";
            var payload = new
            {
                cmd = "flash_green",
                times = 3
            };

            await _mqtt.PublishJsonAsync(topic, payload, ct: ct);
            _logger.LogDebug("Sent flash_green command to coordinator {CoordinatorId}", coordinatorId);
        }
        catch (Exception ex)
        {
            // Log but don't fail the operation if flash command fails
            _logger.LogWarning(ex, "Failed to send flash_green command to coordinator {CoordinatorId}", coordinatorId);
        }
    }
}
