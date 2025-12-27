using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing sites.
/// </summary>
[ApiController]
[Route("api/sites")]
public class SitesController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ILogger<SitesController> _logger;

    public SitesController(IRepository repository, ILogger<SitesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all sites.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Site>>> GetSites(CancellationToken ct)
    {
        var sites = await _repository.GetSitesAsync(ct);
        return Ok(sites);
    }

    /// <summary>
    /// Get a specific site by ID.
    /// </summary>
    [HttpGet("{siteId}")]
    public async Task<ActionResult<Site>> GetSite(string siteId, CancellationToken ct)
    {
        var site = await _repository.GetSiteByIdAsync(siteId, ct);
        if (site == null)
        {
            return NotFound();
        }
        return Ok(site);
    }

    /// <summary>
    /// Get site with all coordinators and zones.
    /// </summary>
    [HttpGet("{siteId}/full")]
    public async Task<ActionResult<SiteFullResponse>> GetSiteFull(string siteId, CancellationToken ct)
    {
        var site = await _repository.GetSiteByIdAsync(siteId, ct);
        if (site == null)
        {
            return NotFound();
        }

        var zones = await _repository.GetZonesBySiteAsync(siteId, ct);

        return Ok(new SiteFullResponse
        {
            Site = site,
            Zones = zones.ToList()
        });
    }

    /// <summary>
    /// Create a new site.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Site>> CreateSite([FromBody] CreateSiteRequest request, CancellationToken ct)
    {
        var site = new Site
        {
            Id = request.Id ?? Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            Timezone = request.Timezone ?? "UTC"
        };

        await _repository.CreateSiteAsync(site, ct);
        _logger.LogInformation("Created site {SiteId}", site.Id);
        
        return CreatedAtAction(nameof(GetSite), new { siteId = site.Id }, site);
    }

    /// <summary>
    /// Update a site.
    /// </summary>
    [HttpPut("{siteId}")]
    public async Task<IActionResult> UpdateSite(string siteId, [FromBody] UpdateSiteRequest request, CancellationToken ct)
    {
        var site = await _repository.GetSiteByIdAsync(siteId, ct);
        if (site == null)
        {
            return NotFound();
        }

        site.Name = request.Name ?? site.Name;
        site.Description = request.Description ?? site.Description;
        site.Location = request.Location ?? site.Location;
        site.Timezone = request.Timezone ?? site.Timezone;

        await _repository.UpsertSiteAsync(site, ct);
        _logger.LogInformation("Updated site {SiteId}", siteId);
        
        return NoContent();
    }

    /// <summary>
    /// Get site settings.
    /// </summary>
    [HttpGet("{siteId}/settings")]
    public async Task<ActionResult<Settings>> GetSettings(string siteId, CancellationToken ct)
    {
        var settings = await _repository.GetSettingsAsync(siteId, ct);
        if (settings == null)
        {
            // Return default settings
            settings = new Settings { SiteId = siteId };
        }
        return Ok(settings);
    }

    /// <summary>
    /// Update site settings.
    /// </summary>
    [HttpPut("{siteId}/settings")]
    public async Task<IActionResult> UpdateSettings(string siteId, [FromBody] Settings settings, CancellationToken ct)
    {
        settings.SiteId = siteId;
        await _repository.SaveSettingsAsync(settings, ct);
        _logger.LogInformation("Updated settings for site {SiteId}", siteId);
        return NoContent();
    }
}

public class SiteFullResponse
{
    public Site Site { get; set; } = null!;
    public List<Zone> Zones { get; set; } = new();
}

public class CreateSiteRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Timezone { get; set; }
}

public class UpdateSiteRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Timezone { get; set; }
}
