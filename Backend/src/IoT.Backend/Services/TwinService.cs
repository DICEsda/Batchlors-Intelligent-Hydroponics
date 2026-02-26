using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;
using Microsoft.Extensions.Logging;

namespace IoT.Backend.Services;

/// <summary>
/// Implementation of Digital Twin state management and synchronization
/// </summary>
public class TwinService : ITwinService
{
    private readonly ITwinRepository _twinRepository;
    private readonly IMqttService _mqttService;
    private readonly TwinChangeChannel _changeChannel;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<TwinService> _logger;

    public TwinService(
        ITwinRepository twinRepository,
        IMqttService mqttService,
        TwinChangeChannel changeChannel,
        IWsBroadcaster broadcaster,
        ILogger<TwinService> logger)
    {
        _twinRepository = twinRepository;
        _mqttService = mqttService;
        _changeChannel = changeChannel;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    // ============================================================================
    // Tower Twin Operations
    // ============================================================================

    public async Task ProcessTowerTelemetryAsync(string towerId, string coordId, string farmId, TowerReportedState reported, CancellationToken ct = default)
    {
        _logger.LogDebug("Processing telemetry for tower {TowerId}", towerId);

        var updated = await _twinRepository.UpdateTowerReportedStateAsync(towerId, reported, ct);
        
        if (!updated)
        {
            // Twin doesn't exist yet — auto-create from first telemetry
            _logger.LogInformation("Auto-creating tower twin for {TowerId} from first telemetry", towerId);
            var newTwin = new TowerTwin
            {
                Id = towerId,
                TowerId = towerId,
                CoordId = coordId,
                FarmId = farmId,
                Reported = reported,
                Desired = new TowerDesiredState(),
                Metadata = new TwinMetadata
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastReportedAt = DateTime.UtcNow,
                    IsConnected = true,
                    SyncStatus = SyncStatus.InSync
                }
            };
            await _twinRepository.UpsertTowerTwinAsync(newTwin, ct);
        }

        // Emit ADT sync event (fire-and-forget via channel)
        var twinChangeEvt = new TwinChangeEvent
        {
            ChangeType = TwinChangeType.TowerTelemetry,
            DeviceId = towerId,
            TowerReported = reported
        };
        _changeChannel.TryWrite(twinChangeEvt);

        // Broadcast to WebSocket clients for real-time UI updates
        _ = _broadcaster.BroadcastAsync("digital_twin_update", new
        {
            changeType = "TowerTelemetry",
            deviceId = towerId,
            towerReported = reported
        });

        // Check if reported state now matches desired state
        var twin = await _twinRepository.GetTowerTwinByIdAsync(towerId, ct);
        if (twin != null && twin.Metadata.SyncStatus == SyncStatus.Pending)
        {
            var delta = CalculateTowerDelta(twin.Desired, twin.Reported);
            if (delta == null)
            {
                // States are now in sync
                await _twinRepository.UpdateTowerSyncStatusAsync(towerId, SyncStatus.InSync, ct);
                _logger.LogInformation("Tower {TowerId} is now in sync", towerId);
            }
        }
    }

    public async Task SetTowerDesiredStateAsync(string towerId, TowerDesiredState desired, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting desired state for tower {TowerId}", towerId);

        var updated = await _twinRepository.UpdateTowerDesiredStateAsync(towerId, desired, ct);
        
        if (!updated)
        {
            _logger.LogWarning("Failed to update desired state for tower {TowerId}", towerId);
            return;
        }

        // Emit ADT sync event
        _changeChannel.TryWrite(new TwinChangeEvent
        {
            ChangeType = TwinChangeType.TowerDesiredStateChanged,
            DeviceId = towerId
        });

        // Broadcast to WebSocket clients
        _ = _broadcaster.BroadcastAsync("digital_twin_update", new
        {
            changeType = "TowerDesiredStateChanged",
            deviceId = towerId
        });

        // Publish command to device immediately
        var twin = await _twinRepository.GetTowerTwinByIdAsync(towerId, ct);
        if (twin != null)
        {
            await PublishTowerCommandAsync(twin, ct);
        }
    }

    public async Task<TowerDesiredState?> GetTowerStateDeltaAsync(string towerId, CancellationToken ct = default)
    {
        var twin = await _twinRepository.GetTowerTwinByIdAsync(towerId, ct);
        if (twin == null) return null;

        return CalculateTowerDelta(twin.Desired, twin.Reported);
    }

    public Task<TowerTwin?> GetTowerTwinAsync(string towerId, CancellationToken ct = default)
    {
        return _twinRepository.GetTowerTwinByIdAsync(towerId, ct);
    }

    public Task<IReadOnlyList<TowerTwin>> GetTowerTwinsForCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        return _twinRepository.GetTowerTwinsByCoordinatorAsync(farmId, coordId, ct);
    }

    // ============================================================================
    // Coordinator Twin Operations
    // ============================================================================

    public async Task ProcessCoordinatorTelemetryAsync(string coordId, string farmId, CoordinatorReportedState reported, CancellationToken ct = default)
    {
        _logger.LogDebug("Processing telemetry for coordinator {CoordId}", coordId);

        var updated = await _twinRepository.UpdateCoordinatorReportedStateAsync(coordId, reported, ct);
        
        if (!updated)
        {
            // Twin doesn't exist yet — auto-create from first telemetry
            _logger.LogInformation("Auto-creating coordinator twin for {CoordId} from first telemetry", coordId);
            var newTwin = new CoordinatorTwin
            {
                Id = coordId,
                CoordId = coordId,
                FarmId = farmId,
                SiteId = farmId,
                Reported = reported,
                Desired = new CoordinatorDesiredState(),
                Metadata = new TwinMetadata
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastReportedAt = DateTime.UtcNow,
                    IsConnected = true,
                    SyncStatus = SyncStatus.InSync
                }
            };
            await _twinRepository.UpsertCoordinatorTwinAsync(newTwin, ct);
        }

        // Emit ADT sync event
        _changeChannel.TryWrite(new TwinChangeEvent
        {
            ChangeType = TwinChangeType.CoordinatorTelemetry,
            DeviceId = coordId,
            CoordinatorReported = reported
        });

        // Broadcast to WebSocket clients
        _ = _broadcaster.BroadcastAsync("digital_twin_update", new
        {
            changeType = "CoordinatorTelemetry",
            deviceId = coordId,
            coordinatorReported = reported
        });

        // Check if reported state now matches desired state
        var twin = await _twinRepository.GetCoordinatorTwinByIdAsync(coordId, ct);
        if (twin != null && twin.Metadata.SyncStatus == SyncStatus.Pending)
        {
            var delta = CalculateCoordinatorDelta(twin.Desired, twin.Reported);
            if (delta == null)
            {
                // States are now in sync
                await _twinRepository.UpdateCoordinatorSyncStatusAsync(coordId, SyncStatus.InSync, ct);
                _logger.LogInformation("Coordinator {CoordId} is now in sync", coordId);
            }
        }
    }

    public async Task SetCoordinatorDesiredStateAsync(string coordId, CoordinatorDesiredState desired, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting desired state for coordinator {CoordId}", coordId);

        var updated = await _twinRepository.UpdateCoordinatorDesiredStateAsync(coordId, desired, ct);
        
        if (!updated)
        {
            _logger.LogWarning("Failed to update desired state for coordinator {CoordId}", coordId);
            return;
        }

        // Emit ADT sync event
        _changeChannel.TryWrite(new TwinChangeEvent
        {
            ChangeType = TwinChangeType.CoordinatorDesiredStateChanged,
            DeviceId = coordId
        });

        // Broadcast to WebSocket clients
        _ = _broadcaster.BroadcastAsync("digital_twin_update", new
        {
            changeType = "CoordinatorDesiredStateChanged",
            deviceId = coordId
        });

        // Publish command to device immediately
        var twin = await _twinRepository.GetCoordinatorTwinByIdAsync(coordId, ct);
        if (twin != null)
        {
            await PublishCoordinatorCommandAsync(twin, ct);
        }
    }

    public async Task<CoordinatorDesiredState?> GetCoordinatorStateDeltaAsync(string coordId, CancellationToken ct = default)
    {
        var twin = await _twinRepository.GetCoordinatorTwinByIdAsync(coordId, ct);
        if (twin == null) return null;

        return CalculateCoordinatorDelta(twin.Desired, twin.Reported);
    }

    public Task<CoordinatorTwin?> GetCoordinatorTwinAsync(string coordId, CancellationToken ct = default)
    {
        return _twinRepository.GetCoordinatorTwinByIdAsync(coordId, ct);
    }

    public Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsForFarmAsync(string farmId, CancellationToken ct = default)
    {
        return _twinRepository.GetCoordinatorTwinsByFarmAsync(farmId, ct);
    }

    // ============================================================================
    // Sync Operations
    // ============================================================================

    public async Task ProcessPendingSyncsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Processing pending syncs");

        var (towers, coordinators) = await _twinRepository.GetPendingSyncTwinsAsync(ct);

        foreach (var tower in towers)
        {
            try
            {
                await PublishTowerCommandAsync(tower, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync tower {TowerId}", tower.TowerId);
            }
        }

        foreach (var coord in coordinators)
        {
            try
            {
                await PublishCoordinatorCommandAsync(coord, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync coordinator {CoordId}", coord.CoordId);
            }
        }
    }

    public async Task MarkTowerSyncSuccessAsync(string towerId, CancellationToken ct = default)
    {
        await _twinRepository.UpdateTowerSyncStatusAsync(towerId, SyncStatus.InSync, ct);
        _logger.LogInformation("Tower {TowerId} sync marked as successful", towerId);
    }

    public async Task MarkCoordinatorSyncSuccessAsync(string coordId, CancellationToken ct = default)
    {
        await _twinRepository.UpdateCoordinatorSyncStatusAsync(coordId, SyncStatus.InSync, ct);
        _logger.LogInformation("Coordinator {CoordId} sync marked as successful", coordId);
    }

    public async Task CheckAndMarkStaleTwinsAsync(TimeSpan staleThreshold, CancellationToken ct = default)
    {
        var staleCount = await _twinRepository.MarkStaleTwinsAsync(staleThreshold, ct);
        if (staleCount > 0)
        {
            _logger.LogWarning("Marked {Count} twins as stale", staleCount);
        }
    }

    // ============================================================================
    // Private Helpers
    // ============================================================================

    /// <summary>
    /// Calculate the delta between desired and reported state for a tower
    /// Returns null if states are equivalent (in sync)
    /// </summary>
    private static TowerDesiredState? CalculateTowerDelta(TowerDesiredState desired, TowerReportedState reported)
    {
        var delta = new TowerDesiredState();
        bool hasDelta = false;

        if (desired.PumpOn.HasValue && desired.PumpOn.Value != reported.PumpOn)
        {
            delta.PumpOn = desired.PumpOn;
            hasDelta = true;
        }

        if (desired.LightOn.HasValue && desired.LightOn.Value != reported.LightOn)
        {
            delta.LightOn = desired.LightOn;
            hasDelta = true;
        }

        if (desired.LightBrightness.HasValue && desired.LightBrightness.Value != reported.LightBrightness)
        {
            delta.LightBrightness = desired.LightBrightness;
            hasDelta = true;
        }

        if (!string.IsNullOrEmpty(desired.StatusMode) && desired.StatusMode != reported.StatusMode)
        {
            delta.StatusMode = desired.StatusMode;
            hasDelta = true;
        }

        return hasDelta ? delta : null;
    }

    /// <summary>
    /// Calculate the delta between desired and reported state for a coordinator
    /// Returns null if states are equivalent (in sync)
    /// </summary>
    private static CoordinatorDesiredState? CalculateCoordinatorDelta(CoordinatorDesiredState desired, CoordinatorReportedState reported)
    {
        var delta = new CoordinatorDesiredState();
        bool hasDelta = false;

        if (desired.MainPumpOn.HasValue && desired.MainPumpOn != reported.MainPumpOn)
        {
            delta.MainPumpOn = desired.MainPumpOn;
            hasDelta = true;
        }

        if (desired.DosingPumpPhOn.HasValue && desired.DosingPumpPhOn != reported.DosingPumpPhOn)
        {
            delta.DosingPumpPhOn = desired.DosingPumpPhOn;
            hasDelta = true;
        }

        if (desired.DosingPumpNutrientOn.HasValue && desired.DosingPumpNutrientOn != reported.DosingPumpNutrientOn)
        {
            delta.DosingPumpNutrientOn = desired.DosingPumpNutrientOn;
            hasDelta = true;
        }

        if (!string.IsNullOrEmpty(desired.StatusMode) && desired.StatusMode != reported.StatusMode)
        {
            delta.StatusMode = desired.StatusMode;
            hasDelta = true;
        }

        // Setpoints are always included in delta if they exist
        if (desired.Setpoints != null)
        {
            delta.Setpoints = desired.Setpoints;
            hasDelta = true;
        }

        return hasDelta ? delta : null;
    }

    /// <summary>
    /// Publish a command to a tower via MQTT (through coordinator)
    /// </summary>
    private async Task PublishTowerCommandAsync(TowerTwin twin, CancellationToken ct)
    {
        var delta = CalculateTowerDelta(twin.Desired, twin.Reported);
        if (delta == null)
        {
            _logger.LogDebug("Tower {TowerId} has no delta to sync", twin.TowerId);
            return;
        }

        var topic = MqttTopics.TowerCmd(twin.FarmId, twin.CoordId, twin.TowerId);
        var payload = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(delta));

        await _mqttService.PublishAsync(topic, payload, ct: ct);
        _logger.LogInformation("Published command to tower {TowerId} on topic {Topic}", twin.TowerId, topic);
    }

    /// <summary>
    /// Publish a command to a coordinator via MQTT
    /// </summary>
    private async Task PublishCoordinatorCommandAsync(CoordinatorTwin twin, CancellationToken ct)
    {
        var delta = CalculateCoordinatorDelta(twin.Desired, twin.Reported);
        if (delta == null)
        {
            _logger.LogDebug("Coordinator {CoordId} has no delta to sync", twin.CoordId);
            return;
        }

        var topic = MqttTopics.CoordinatorCmd(twin.FarmId, twin.CoordId);
        var payload = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(delta));

        await _mqttService.PublishAsync(topic, payload, ct: ct);
        _logger.LogInformation("Published command to coordinator {CoordId} on topic {Topic}", twin.CoordId, topic);
    }
}
