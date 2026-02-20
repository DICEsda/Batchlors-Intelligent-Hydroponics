using System.Collections.Concurrent;
using IoT.Backend.Models;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Service for managing coordinator registration workflow.
/// Unknown coordinators must be approved via the frontend before their MQTT messages are processed.
/// Mirrors the tower pairing flow but at the coordinator level.
/// </summary>
public class CoordinatorRegistrationService : ICoordinatorRegistrationService
{
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly IMqttService _mqtt;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<CoordinatorRegistrationService> _logger;

    /// <summary>
    /// In-memory cache of registered coordinator IDs for fast IsRegistered lookups.
    /// Loaded from DB on startup and updated on approve/remove.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _registeredCache = new();

    /// <summary>
    /// In-memory store of pending (unregistered) coordinator registrations.
    /// Keyed by coordId (MAC address).
    /// </summary>
    private readonly ConcurrentDictionary<string, PendingCoordinatorRegistration> _pendingRegistrations = new();

    /// <summary>
    /// Set of rejected coordinator IDs. Messages from these are silently dropped.
    /// </summary>
    private readonly HashSet<string> _rejectedCoordinators = new();
    private readonly object _rejectedLock = new();

    public CoordinatorRegistrationService(
        ICoordinatorRepository coordinatorRepository,
        IMqttService mqtt,
        IWsBroadcaster broadcaster,
        ILogger<CoordinatorRegistrationService> logger)
    {
        _coordinatorRepository = coordinatorRepository;
        _mqtt = mqtt;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> IsRegisteredAsync(string coordId, CancellationToken ct = default)
    {
        return Task.FromResult(_registeredCache.ContainsKey(coordId));
    }

    /// <inheritdoc />
    public async Task ProcessUnknownCoordinatorAsync(string coordId, string topic, byte[] payload, CancellationToken ct = default)
    {
        // Skip if already rejected
        lock (_rejectedLock)
        {
            if (_rejectedCoordinators.Contains(coordId))
            {
                _logger.LogDebug("Dropping message from rejected coordinator {CoordId} on {Topic}", coordId, topic);
                return;
            }
        }

        var now = DateTime.UtcNow;
        var isNew = false;

        _pendingRegistrations.AddOrUpdate(
            coordId,
            _ =>
            {
                isNew = true;
                return new PendingCoordinatorRegistration
                {
                    CoordId = coordId,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    MessageCount = 1
                };
            },
            (_, existing) =>
            {
                existing.LastSeenAt = now;
                existing.MessageCount++;
                return existing;
            });

        // Only broadcast the registration request the first time we see this coordinator
        if (isNew)
        {
            _logger.LogInformation(
                "Unknown coordinator {CoordId} detected on topic {Topic}. Broadcasting registration request to frontend.",
                coordId, topic);

            var pending = _pendingRegistrations[coordId];
            var wsPayload = new CoordinatorRegistrationPayload
            {
                CoordId = coordId,
                FwVersion = pending.FwVersion,
                ChipModel = pending.ChipModel,
                WifiRssi = pending.WifiRssi,
                Ip = pending.Ip,
                FreeHeap = pending.FreeHeap,
                FirstSeenAt = pending.FirstSeenAt
            };

            await _broadcaster.BroadcastCoordinatorRegistrationRequestAsync(wsPayload, ct);
        }
        else
        {
            _logger.LogDebug("Dropping message from unregistered coordinator {CoordId} on {Topic} (seen {Count} times)",
                coordId, topic, _pendingRegistrations[coordId].MessageCount);
        }
    }

    /// <inheritdoc />
    public async Task HandleCoordinatorAnnounceAsync(string coordId, CoordinatorAnnounceDto announce, CancellationToken ct = default)
    {
        // If already registered, just log and return
        if (_registeredCache.ContainsKey(coordId))
        {
            _logger.LogDebug("Registered coordinator {CoordId} sent announce (FW: {Fw})", coordId, announce.FwVersion);
            return;
        }

        // Skip if rejected
        lock (_rejectedLock)
        {
            if (_rejectedCoordinators.Contains(coordId))
            {
                _logger.LogDebug("Rejected coordinator {CoordId} sent announce, ignoring", coordId);
                return;
            }
        }

        var now = DateTime.UtcNow;
        var isNew = false;

        _pendingRegistrations.AddOrUpdate(
            coordId,
            _ =>
            {
                isNew = true;
                return new PendingCoordinatorRegistration
                {
                    CoordId = coordId,
                    FwVersion = announce.FwVersion,
                    ChipModel = announce.ChipModel,
                    WifiRssi = announce.WifiRssi,
                    Ip = announce.Ip,
                    FreeHeap = announce.FreeHeap,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    MessageCount = 1
                };
            },
            (_, existing) =>
            {
                // Update with richer announce data
                existing.FwVersion = announce.FwVersion ?? existing.FwVersion;
                existing.ChipModel = announce.ChipModel ?? existing.ChipModel;
                existing.WifiRssi = announce.WifiRssi;
                existing.Ip = announce.Ip ?? existing.Ip;
                existing.FreeHeap = announce.FreeHeap;
                existing.LastSeenAt = now;
                existing.MessageCount++;
                return existing;
            });

        _logger.LogInformation(
            "Coordinator announce from unregistered {CoordId}: FW={Fw}, Chip={Chip}, IP={Ip}",
            coordId, announce.FwVersion, announce.ChipModel, announce.Ip);

        // Always broadcast on announce (it has richer data than a generic message)
        var pending = _pendingRegistrations[coordId];
        var wsPayload = new CoordinatorRegistrationPayload
        {
            CoordId = coordId,
            FwVersion = pending.FwVersion,
            ChipModel = pending.ChipModel,
            WifiRssi = pending.WifiRssi,
            Ip = pending.Ip,
            FreeHeap = pending.FreeHeap,
            FirstSeenAt = pending.FirstSeenAt
        };

        await _broadcaster.BroadcastCoordinatorRegistrationRequestAsync(wsPayload, ct);
    }

    /// <inheritdoc />
    public async Task<Coordinator> ApproveRegistrationAsync(ApproveCoordinatorRegistrationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CoordId))
            throw new InvalidOperationException("CoordId is required.");
        if (string.IsNullOrWhiteSpace(request.FarmId))
            throw new InvalidOperationException("FarmId is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        // Check if already registered
        if (_registeredCache.ContainsKey(request.CoordId))
            throw new InvalidOperationException($"Coordinator {request.CoordId} is already registered.");

        // Get pending info if available
        _pendingRegistrations.TryGetValue(request.CoordId, out var pending);

        // Create the coordinator model
        var coordinator = new Coordinator
        {
            Id = request.CoordId,
            CoordId = request.CoordId,
            FarmId = request.FarmId,
            SiteId = request.FarmId, // Backwards compatibility
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            Tags = request.Tags,
            Color = request.Color,
            FwVersion = pending?.FwVersion ?? string.Empty,
            StatusMode = "operational",
            LastSeen = DateTime.UtcNow
        };

        // Persist to database
        await _coordinatorRepository.UpsertAsync(coordinator, ct);

        // Add to registered cache
        _registeredCache[request.CoordId] = true;

        // Remove from pending
        _pendingRegistrations.TryRemove(request.CoordId, out _);

        // Remove from rejected if it was there
        lock (_rejectedLock)
        {
            _rejectedCoordinators.Remove(request.CoordId);
        }

        _logger.LogInformation(
            "Coordinator {CoordId} approved and registered as '{Name}' in farm {FarmId}",
            request.CoordId, request.Name, request.FarmId);

        // Notify the coordinator via MQTT that it has been registered
        await _mqtt.PublishJsonAsync(
            $"coordinator/{request.CoordId}/registered",
            new { farm_id = request.FarmId },
            ct: ct);

        // Broadcast to WebSocket clients
        var wsPayload = new CoordinatorRegisteredPayload
        {
            CoordId = request.CoordId,
            FarmId = request.FarmId,
            Name = request.Name,
            Description = request.Description,
            Color = request.Color,
            Location = request.Location,
            RegisteredAt = DateTime.UtcNow
        };
        await _broadcaster.BroadcastCoordinatorRegisteredAsync(wsPayload, ct);

        return coordinator;
    }

    /// <inheritdoc />
    public async Task RejectRegistrationAsync(string coordId, CancellationToken ct = default)
    {
        // Add to rejected set
        lock (_rejectedLock)
        {
            _rejectedCoordinators.Add(coordId);
        }

        // Remove from pending
        _pendingRegistrations.TryRemove(coordId, out _);

        _logger.LogInformation("Coordinator {CoordId} registration rejected", coordId);

        // Broadcast rejection to WebSocket clients
        await _broadcaster.BroadcastAsync("coordinator_rejected", new
        {
            coord_id = coordId,
            rejected_at = DateTime.UtcNow
        }, ct);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCoordinatorAsync(string coordId, CancellationToken ct = default)
    {
        // Delete from database
        var deleted = await _coordinatorRepository.DeleteAsync(coordId, ct);
        if (!deleted)
        {
            _logger.LogWarning("Coordinator {CoordId} not found for removal", coordId);
            return false;
        }

        // Remove from registered cache
        _registeredCache.TryRemove(coordId, out _);

        _logger.LogInformation("Coordinator {CoordId} removed from system", coordId);

        // Broadcast removal to WebSocket clients
        await _broadcaster.BroadcastAsync("coordinator_removed", new
        {
            coord_id = coordId,
            removed_at = DateTime.UtcNow
        }, ct);

        return true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingCoordinatorRegistration>> GetPendingRegistrationsAsync(CancellationToken ct = default)
    {
        var pending = _pendingRegistrations.Values
            .OrderByDescending(p => p.LastSeenAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PendingCoordinatorRegistration>>(pending);
    }

    /// <inheritdoc />
    public async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        try
        {
            var coordinators = await _coordinatorRepository.GetAllAsync(ct);

            _registeredCache.Clear();
            foreach (var coord in coordinators)
            {
                if (!string.IsNullOrEmpty(coord.CoordId))
                {
                    _registeredCache[coord.CoordId] = true;
                }
            }

            _logger.LogInformation(
                "Coordinator registration cache refreshed: {Count} registered coordinators loaded",
                _registeredCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh coordinator registration cache from database");
        }
    }
}
