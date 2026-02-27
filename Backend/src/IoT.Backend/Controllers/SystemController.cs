using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace IoT.Backend.Controllers;

/// <summary>
/// System-level operations (factory reset, diagnostics, etc.).
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IMongoDatabase _db;
    private readonly ICoordinatorRegistrationService _registrationService;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<SystemController> _logger;

    /// <summary>
    /// All known MongoDB collection names used by the application.
    /// </summary>
    private static readonly string[] AllCollections =
    [
        "coordinators",
        "towers",
        "farms",
        "zones",
        "alerts",
        "coordinator_twins",
        "tower_twins",
        "reservoir_telemetry",
        "tower_telemetry",
        "height_measurements",
        "settings",
        "ota_jobs",
        "firmware_versions",
        "nodes"
    ];

    public SystemController(
        IMongoDatabase db,
        ICoordinatorRegistrationService registrationService,
        IWsBroadcaster broadcaster,
        ILogger<SystemController> logger)
    {
        _db = db;
        _registrationService = registrationService;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Factory reset — drops all MongoDB collections and clears in-memory caches.
    /// This is a destructive operation intended for development/testing.
    /// </summary>
    [HttpPost("factory-reset")]
    [ProducesResponseType(typeof(FactoryResetResponse), 200)]
    public async Task<IActionResult> FactoryReset(CancellationToken ct)
    {
        _logger.LogWarning("Factory reset initiated — dropping all collections and clearing caches");

        var dropped = new List<string>();

        // Get the list of collections that actually exist in the database
        var existingCollections = new HashSet<string>();
        using (var cursor = await _db.ListCollectionNamesAsync(cancellationToken: ct))
        {
            await cursor.ForEachAsync(name => existingCollections.Add(name), ct);
        }

        // Drop each known collection
        foreach (var collection in AllCollections)
        {
            if (existingCollections.Contains(collection))
            {
                await _db.DropCollectionAsync(collection, ct);
                dropped.Add(collection);
                _logger.LogInformation("Dropped collection: {Collection}", collection);
            }
        }

        // Clear in-memory registration caches
        _registrationService.ResetAll();

        // Broadcast to all connected frontend clients so they clear their state
        await _broadcaster.BroadcastAsync("system_reset", new
        {
            reset_at = DateTime.UtcNow,
            collections_dropped = dropped
        }, ct);

        _logger.LogWarning("Factory reset complete — dropped {Count} collections", dropped.Count);

        return Ok(new FactoryResetResponse
        {
            Success = true,
            CollectionsDropped = dropped,
            Message = $"Factory reset complete. Dropped {dropped.Count} collections and cleared registration caches."
        });
    }
}

public class FactoryResetResponse
{
    public bool Success { get; set; }
    public List<string> CollectionsDropped { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}
