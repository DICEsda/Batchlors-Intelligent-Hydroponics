using IoT.Backend.Models;
using IoT.Backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for mmWave radar data.
/// Returns JSON only - rendering moved to frontend.
/// </summary>
[ApiController]
[Route("api/radar")]
public class RadarController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ILogger<RadarController> _logger;

    public RadarController(IRepository repository, ILogger<RadarController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get recent mmWave frames for a coordinator.
    /// </summary>
    /// <param name="siteId">Site ID</param>
    /// <param name="coordId">Coordinator ID</param>
    /// <param name="limit">Max frames to return (default 100)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{siteId}/{coordId}")]
    public async Task<ActionResult<RadarResponse>> GetRadarData(
        string siteId, 
        string coordId, 
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var frames = await _repository.GetMmwaveFramesAsync(siteId, coordId, limit, ct);
        
        // Get the latest frame for current status
        var latest = frames.FirstOrDefault();

        return Ok(new RadarResponse
        {
            SiteId = siteId,
            CoordinatorId = coordId,
            Presence = latest?.Presence ?? false,
            Confidence = latest?.Confidence ?? 0,
            Targets = latest?.Targets ?? new List<MmwaveTarget>(),
            Timestamp = latest?.Timestamp ?? DateTime.UtcNow,
            HistoricalFrames = frames.ToList()
        });
    }

    /// <summary>
    /// Get aggregated presence stats for a time window.
    /// </summary>
    [HttpGet("{siteId}/{coordId}/stats")]
    public async Task<ActionResult<RadarStats>> GetRadarStats(
        string siteId,
        string coordId,
        [FromQuery] int windowMinutes = 60,
        CancellationToken ct = default)
    {
        // Fetch enough frames for the window
        var limit = windowMinutes * 10; // Assuming ~10 frames per minute
        var frames = await _repository.GetMmwaveFramesAsync(siteId, coordId, limit, ct);

        var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var relevantFrames = frames.Where(f => f.Timestamp >= cutoff).ToList();

        if (!relevantFrames.Any())
        {
            return Ok(new RadarStats
            {
                SiteId = siteId,
                CoordinatorId = coordId,
                WindowMinutes = windowMinutes,
                FrameCount = 0,
                PresencePercentage = 0,
                AvgConfidence = 0,
                AvgTargetCount = 0
            });
        }

        return Ok(new RadarStats
        {
            SiteId = siteId,
            CoordinatorId = coordId,
            WindowMinutes = windowMinutes,
            FrameCount = relevantFrames.Count,
            PresencePercentage = (float)relevantFrames.Count(f => f.Presence) / relevantFrames.Count * 100,
            AvgConfidence = relevantFrames.Average(f => f.Confidence),
            AvgTargetCount = (float)relevantFrames.Average(f => f.Targets.Count)
        });
    }
}

public class RadarResponse
{
    public string SiteId { get; set; } = string.Empty;
    public string CoordinatorId { get; set; } = string.Empty;
    public bool Presence { get; set; }
    public float Confidence { get; set; }
    public List<MmwaveTarget> Targets { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public List<MmwaveFrame> HistoricalFrames { get; set; } = new();
}

public class RadarStats
{
    public string SiteId { get; set; } = string.Empty;
    public string CoordinatorId { get; set; } = string.Empty;
    public int WindowMinutes { get; set; }
    public int FrameCount { get; set; }
    public float PresencePercentage { get; set; }
    public float AvgConfidence { get; set; }
    public float AvgTargetCount { get; set; }
}
