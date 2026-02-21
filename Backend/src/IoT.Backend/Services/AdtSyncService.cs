using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Background service that reads twin change events from the <see cref="TwinChangeChannel"/>
/// and syncs them to Azure Digital Twins in the background.
/// 
/// This decouples the hot telemetry path from ADT latency — MQTT→MongoDB updates
/// remain fast, and ADT sync happens asynchronously.
/// 
/// DI Registration in Program.cs:
/// <code>
/// builder.Services.AddSingleton&lt;TwinChangeChannel&gt;();
/// builder.Services.AddHostedService&lt;AdtSyncService&gt;();
/// </code>
/// </summary>
public class AdtSyncService : BackgroundService
{
    private readonly TwinChangeChannel _channel;
    private readonly IAzureDigitalTwinsService _adtService;
    private readonly AdtTwinMapper _mapper;
    private readonly ITwinRepository _twinRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly ILogger<AdtSyncService> _logger;
    private readonly AzureDigitalTwinsConfig _adtConfig;

    /// <summary>
    /// Tracks consecutive failures for backoff logic.
    /// </summary>
    private int _consecutiveFailures;

    public AdtSyncService(
        TwinChangeChannel channel,
        IAzureDigitalTwinsService adtService,
        AdtTwinMapper mapper,
        ITwinRepository twinRepository,
        IFarmRepository farmRepository,
        Microsoft.Extensions.Options.IOptions<AzureDigitalTwinsConfig> adtConfig,
        ILogger<AdtSyncService> logger)
    {
        _channel = channel;
        _adtService = adtService;
        _mapper = mapper;
        _twinRepository = twinRepository;
        _farmRepository = farmRepository;
        _adtConfig = adtConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_adtConfig.IsConfigured)
        {
            _logger.LogInformation(
                "AdtSyncService: Azure Digital Twins is not configured — sync is disabled. " +
                "Events will still be consumed from the channel to prevent backpressure.");
        }

        _logger.LogInformation("AdtSyncService started, reading from TwinChangeChannel");

        // Initial sync: push all existing MongoDB twins to ADT on startup
        if (_adtConfig.IsConfigured)
        {
            // Delay to let other services start up (MongoDB, etc.)
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await PerformInitialSyncAsync(stoppingToken);
        }

        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessEventAsync(evt, stoppingToken);
                    _consecutiveFailures = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    _logger.LogError(ex,
                        "AdtSyncService: error processing {ChangeType} for {DeviceId} (failure #{Count})",
                        evt.ChangeType, evt.DeviceId, _consecutiveFailures);

                    // Backoff on repeated failures to avoid hammering a down ADT instance
                    if (_consecutiveFailures >= 5)
                    {
                        var backoff = TimeSpan.FromSeconds(Math.Min(30, _consecutiveFailures * 2));
                        _logger.LogWarning(
                            "AdtSyncService: {Count} consecutive failures, backing off {Backoff}s",
                            _consecutiveFailures, backoff.TotalSeconds);
                        await Task.Delay(backoff, stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        _logger.LogInformation("AdtSyncService stopped");
    }

    /// <summary>
    /// Routes a twin change event to the appropriate ADT operation.
    /// If ADT is not configured, events are silently consumed (no-op).
    /// </summary>
    private async Task ProcessEventAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (!_adtConfig.IsConfigured)
        {
            // ADT not configured — consume event silently to keep channel drained
            if (_adtConfig.EnableVerboseLogging)
            {
                _logger.LogDebug("AdtSyncService: ADT not configured, dropping {ChangeType} for {DeviceId}",
                    evt.ChangeType, evt.DeviceId);
            }
            return;
        }

        switch (evt.ChangeType)
        {
            case TwinChangeType.TowerTelemetry:
                await HandleTowerTelemetryAsync(evt, ct);
                break;

            case TwinChangeType.TowerDesiredStateChanged:
                // Desired state changes are reflected via the full twin upsert on next telemetry.
                // For now, log it. Phase 3 will add targeted desired-state patches.
                _logger.LogDebug("AdtSyncService: TowerDesiredStateChanged for {DeviceId} (deferred to next telemetry sync)",
                    evt.DeviceId);
                break;

            case TwinChangeType.CoordinatorTelemetry:
                await HandleCoordinatorTelemetryAsync(evt, ct);
                break;

            case TwinChangeType.CoordinatorDesiredStateChanged:
                _logger.LogDebug("AdtSyncService: CoordinatorDesiredStateChanged for {DeviceId} (deferred to next telemetry sync)",
                    evt.DeviceId);
                break;

            case TwinChangeType.TowerPaired:
                await HandleTowerPairedAsync(evt, ct);
                break;

            case TwinChangeType.TowerRemoved:
                await HandleTowerRemovedAsync(evt, ct);
                break;

            case TwinChangeType.TowerUpsert:
                await HandleTowerUpsertAsync(evt, ct);
                break;

            case TwinChangeType.CoordinatorUpsert:
                await HandleCoordinatorUpsertAsync(evt, ct);
                break;

            case TwinChangeType.CoordinatorRegistered:
                await HandleCoordinatorRegisteredAsync(evt, ct);
                break;

            case TwinChangeType.CoordinatorRemoved:
                await HandleCoordinatorRemovedAsync(evt, ct);
                break;

            case TwinChangeType.FarmUpsert:
                await HandleFarmUpsertAsync(evt, ct);
                break;

            default:
                _logger.LogWarning("AdtSyncService: unhandled change type {ChangeType}", evt.ChangeType);
                break;
        }
    }

    // ============================================================================
    // Event Handlers
    // ============================================================================

    /// <summary>
    /// Patches tower reported state + sends telemetry event to ADT.
    /// </summary>
    private async Task HandleTowerTelemetryAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.TowerReported == null) return;

        var twinId = _mapper.GetTowerTwinId(evt.DeviceId);

        // 1. Patch reported-state properties on the ADT twin
        var patch = _mapper.CreateTowerTelemetryPatch(evt.TowerReported);
        await _adtService.UpdateTwinPropertyAsync(twinId, patch, ct);

        // 2. Also publish telemetry event for ADT time-series (if ADT routes are configured)
        var telemetry = _mapper.CreateTowerEnvironmentalTelemetry(evt.TowerReported);
        await _adtService.SendTelemetryAsync(twinId, telemetry, ct);

        if (_adtConfig.EnableVerboseLogging)
        {
            _logger.LogDebug("AdtSyncService: synced tower telemetry for {TwinId}", twinId);
        }
    }

    /// <summary>
    /// Patches coordinator reported state + sends telemetry event to ADT.
    /// </summary>
    private async Task HandleCoordinatorTelemetryAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.CoordinatorReported == null) return;

        var twinId = _mapper.GetCoordinatorTwinId(evt.DeviceId);

        // 1. Patch reported-state properties on the ADT twin
        var patch = _mapper.CreateCoordinatorTelemetryPatch(evt.CoordinatorReported);
        await _adtService.UpdateTwinPropertyAsync(twinId, patch, ct);

        // 2. Also publish telemetry event
        var telemetry = _mapper.CreateCoordinatorAmbientTelemetry(evt.CoordinatorReported);
        await _adtService.SendTelemetryAsync(twinId, telemetry, ct);

        if (_adtConfig.EnableVerboseLogging)
        {
            _logger.LogDebug("AdtSyncService: synced coordinator telemetry for {TwinId}", twinId);
        }
    }

    /// <summary>
    /// Creates tower twin + coordinator→tower relationship in ADT when a tower is paired.
    /// </summary>
    private async Task HandleTowerPairedAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.TowerTwin == null) return;

        var twin = _mapper.MapTowerTwin(evt.TowerTwin);
        await _adtService.UpsertTwinAsync(twin.Id, twin, ct);

        // Create relationship: coordinator -[hasTower]-> tower
        if (!string.IsNullOrEmpty(evt.CoordId))
        {
            var coordTwinId = _mapper.GetCoordinatorTwinId(evt.CoordId);
            await _adtService.CreateRelationshipAsync(coordTwinId, twin.Id, "hasTower", ct);
        }

        _logger.LogInformation("AdtSyncService: created tower twin {TwinId} with hasTower relationship", twin.Id);
    }

    /// <summary>
    /// Deletes tower twin (and relationships) from ADT when a tower is forgotten/removed.
    /// </summary>
    private async Task HandleTowerRemovedAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        var twinId = _mapper.GetTowerTwinId(evt.DeviceId);
        await _adtService.DeleteTwinAsync(twinId, ct);

        _logger.LogInformation("AdtSyncService: deleted tower twin {TwinId}", twinId);
    }

    /// <summary>
    /// Full upsert of a tower twin in ADT (create or replace).
    /// </summary>
    private async Task HandleTowerUpsertAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.TowerTwin == null) return;

        var twin = _mapper.MapTowerTwin(evt.TowerTwin);
        await _adtService.UpsertTwinAsync(twin.Id, twin, ct);

        if (_adtConfig.EnableVerboseLogging)
        {
            _logger.LogDebug("AdtSyncService: upserted tower twin {TwinId}", twin.Id);
        }
    }

    /// <summary>
    /// Full upsert of a coordinator twin in ADT (create or replace).
    /// </summary>
    private async Task HandleCoordinatorUpsertAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.CoordinatorTwin == null) return;

        var twin = _mapper.MapCoordinatorTwin(evt.CoordinatorTwin);
        await _adtService.UpsertTwinAsync(twin.Id, twin, ct);

        if (_adtConfig.EnableVerboseLogging)
        {
            _logger.LogDebug("AdtSyncService: upserted coordinator twin {TwinId}", twin.Id);
        }
    }

    /// <summary>
    /// Creates coordinator twin + Farm→Coordinator relationship when a coordinator is registered.
    /// Also ensures the Farm twin exists.
    /// </summary>
    private async Task HandleCoordinatorRegisteredAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.CoordinatorTwin == null) return;

        // 1. Upsert the coordinator twin
        var coordTwin = _mapper.MapCoordinatorTwin(evt.CoordinatorTwin);
        await _adtService.UpsertTwinAsync(coordTwin.Id, coordTwin, ct);

        // 2. Create Farm→Coordinator relationship (if farmId is available)
        if (!string.IsNullOrEmpty(evt.FarmId))
        {
            var farmTwinId = _mapper.GetFarmTwinId(evt.FarmId);
            await _adtService.CreateRelationshipAsync(farmTwinId, coordTwin.Id, "hasCoordinator", ct);
        }

        _logger.LogInformation(
            "AdtSyncService: created coordinator twin {TwinId} with hasCoordinator relationship",
            coordTwin.Id);
    }

    /// <summary>
    /// Deletes coordinator twin (and all relationships) from ADT when a coordinator is removed.
    /// </summary>
    private async Task HandleCoordinatorRemovedAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        var twinId = _mapper.GetCoordinatorTwinId(evt.DeviceId);
        await _adtService.DeleteTwinAsync(twinId, ct);

        _logger.LogInformation("AdtSyncService: deleted coordinator twin {TwinId}", twinId);
    }

    /// <summary>
    /// Creates or replaces a Farm twin in ADT.
    /// </summary>
    private async Task HandleFarmUpsertAsync(TwinChangeEvent evt, CancellationToken ct)
    {
        if (evt.Farm == null) return;

        var twin = _mapper.MapFarmTwin(evt.Farm);
        await _adtService.UpsertTwinAsync(twin.Id, twin, ct);

        _logger.LogInformation("AdtSyncService: upserted farm twin {TwinId}", twin.Id);
    }

    // ============================================================================
    // Initial Sync (on startup)
    // ============================================================================

    /// <summary>
    /// Pushes all existing MongoDB twins to ADT on startup.
    /// Ensures ADT is in sync with the local database after a restart.
    /// Runs once before the channel-reading loop begins.
    /// </summary>
    private async Task PerformInitialSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("AdtSyncService: performing initial ADT sync from MongoDB...");

        var stats = new { farms = 0, coordinators = 0, towers = 0, relationships = 0, errors = 0 };
        int farms = 0, coordinators = 0, towers = 0, relationships = 0, errors = 0;

        try
        {
            // 1. Sync all farms
            var allFarms = await _farmRepository.GetAllAsync(ct);
            foreach (var farm in allFarms)
            {
                try
                {
                    var farmTwin = _mapper.MapFarmTwin(farm);
                    await _adtService.UpsertTwinAsync(farmTwin.Id, farmTwin, ct);
                    farms++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AdtSyncService: initial sync failed for farm {FarmId}", farm.FarmId);
                    errors++;
                }
            }

            // 2. Sync all coordinator twins + Farm→Coordinator relationships
            var allTowers = await _twinRepository.GetAllTowerTwinsAsync(ct);
            var coordIds = new HashSet<string>();

            // Collect unique coordinator IDs from tower twins
            foreach (var tower in allTowers)
            {
                if (!string.IsNullOrEmpty(tower.CoordId))
                    coordIds.Add(tower.CoordId);
            }

            // Sync coordinators
            foreach (var coordId in coordIds)
            {
                try
                {
                    var coordTwin = await _twinRepository.GetCoordinatorTwinByIdAsync(coordId, ct);
                    if (coordTwin == null) continue;

                    var adtCoord = _mapper.MapCoordinatorTwin(coordTwin);
                    await _adtService.UpsertTwinAsync(adtCoord.Id, adtCoord, ct);
                    coordinators++;

                    // Create Farm→Coordinator relationship
                    if (!string.IsNullOrEmpty(coordTwin.FarmId))
                    {
                        var farmTwinId = _mapper.GetFarmTwinId(coordTwin.FarmId);
                        await _adtService.CreateRelationshipAsync(farmTwinId, adtCoord.Id, "hasCoordinator", ct);
                        relationships++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AdtSyncService: initial sync failed for coordinator {CoordId}", coordId);
                    errors++;
                }
            }

            // 3. Sync all tower twins + Coordinator→Tower relationships
            foreach (var tower in allTowers)
            {
                try
                {
                    var adtTower = _mapper.MapTowerTwin(tower);
                    await _adtService.UpsertTwinAsync(adtTower.Id, adtTower, ct);
                    towers++;

                    // Create Coordinator→Tower relationship
                    if (!string.IsNullOrEmpty(tower.CoordId))
                    {
                        var coordTwinId = _mapper.GetCoordinatorTwinId(tower.CoordId);
                        await _adtService.CreateRelationshipAsync(coordTwinId, adtTower.Id, "hasTower", ct);
                        relationships++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AdtSyncService: initial sync failed for tower {TowerId}", tower.TowerId);
                    errors++;
                }
            }

            _logger.LogInformation(
                "AdtSyncService: initial sync complete. Farms={Farms}, Coordinators={Coordinators}, Towers={Towers}, Relationships={Relationships}, Errors={Errors}",
                farms, coordinators, towers, relationships, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdtSyncService: initial sync failed with unrecoverable error");
        }
    }
}
