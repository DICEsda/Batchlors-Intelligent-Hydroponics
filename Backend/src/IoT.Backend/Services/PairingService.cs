using System.Collections.Concurrent;
using IoT.Backend.Models;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Service for managing coordinator-tower pairing workflow.
/// Tracks active pairing sessions and processes tower requests.
/// </summary>
public class PairingService : IPairingService
{
    private readonly ITowerRepository _towerRepository;
    private readonly IMqttService _mqtt;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<PairingService> _logger;
    
    // In-memory cache of active sessions for fast lookup
    private readonly ConcurrentDictionary<string, PairingSession> _activeSessions = new();

    public PairingService(
        ITowerRepository towerRepository,
        IMqttService mqtt,
        IWsBroadcaster broadcaster,
        ILogger<PairingService> logger)
    {
        _towerRepository = towerRepository;
        _mqtt = mqtt;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PairingSession> StartPairingSessionAsync(
        string farmId,
        string coordId,
        int durationSeconds = 60,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";
        
        // Check if there's already an active session
        if (_activeSessions.TryGetValue(sessionKey, out var existingSession) 
            && existingSession.Status == "active" 
            && existingSession.ExpiresAt > DateTime.UtcNow)
        {
            _logger.LogWarning("Pairing session already active for {FarmId}/{CoordId}", farmId, coordId);
            return existingSession;
        }

        // Create new session
        var session = new PairingSession
        {
            Id = $"{farmId}-{coordId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            FarmId = farmId,
            CoordId = coordId,
            Status = "active",
            StartedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(durationSeconds),
            DurationS = durationSeconds,
            PendingRequests = new List<TowerPairingRequest>(),
            ApprovedTowers = new List<string>(),
            RejectedTowers = new List<string>()
        };

        // Store in cache
        _activeSessions[sessionKey] = session;

        // Send pairing command to coordinator via MQTT
        var topic = $"farm/{farmId}/coord/{coordId}/cmd";
        var command = new
        {
            cmd = "start_pairing",
            @params = new
            {
                duration_s = durationSeconds
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        _logger.LogInformation(
            "Started pairing session for {FarmId}/{CoordId} with duration {Duration}s",
            farmId, coordId, durationSeconds);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("pairing_status", new
        {
            farm_id = farmId,
            coord_id = coordId,
            status = "started",
            expires_at = session.ExpiresAt,
            duration_s = durationSeconds
        }, ct);

        return session;
    }

    /// <inheritdoc />
    public async Task<PairingSession?> StopPairingSessionAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (!_activeSessions.TryGetValue(sessionKey, out var session))
        {
            _logger.LogWarning("No active pairing session found for {FarmId}/{CoordId}", farmId, coordId);
            return null;
        }

        // Update session state
        session.Status = "cancelled";
        session.EndedAt = DateTime.UtcNow;

        // Remove from active sessions
        _activeSessions.TryRemove(sessionKey, out _);

        // Send stop command to coordinator via MQTT
        var topic = $"farm/{farmId}/coord/{coordId}/cmd";
        var command = new { cmd = "stop_pairing" };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        _logger.LogInformation("Stopped pairing session for {FarmId}/{CoordId}", farmId, coordId);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("pairing_status", new
        {
            farm_id = farmId,
            coord_id = coordId,
            status = "stopped"
        }, ct);

        return session;
    }

    /// <inheritdoc />
    public Task<PairingSession?> GetActiveSessionAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (_activeSessions.TryGetValue(sessionKey, out var session) 
            && session.Status == "active" 
            && session.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<PairingSession?>(session);
        }

        return Task.FromResult<PairingSession?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TowerPairingRequest>> GetPendingRequestsAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (_activeSessions.TryGetValue(sessionKey, out var session))
        {
            var pending = session.PendingRequests
                .Where(r => r.Status == "pending")
                .ToList();
            return Task.FromResult<IReadOnlyList<TowerPairingRequest>>(pending);
        }

        return Task.FromResult<IReadOnlyList<TowerPairingRequest>>(new List<TowerPairingRequest>());
    }

    /// <inheritdoc />
    public async Task<TowerPairingRequest?> ProcessPairingRequestAsync(
        string farmId,
        string coordId,
        TowerPairingRequest request,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (!_activeSessions.TryGetValue(sessionKey, out var session) 
            || session.Status != "active" 
            || session.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Received pairing request from tower {TowerId} but no active session for {FarmId}/{CoordId}",
                request.TowerId, farmId, coordId);
            return null;
        }

        // Check if we already have a request from this tower
        var existingRequest = session.PendingRequests.FirstOrDefault(r => r.TowerId == request.TowerId);
        if (existingRequest != null)
        {
            _logger.LogDebug("Updating existing pairing request from tower {TowerId}", request.TowerId);
            existingRequest.RequestedAt = DateTime.UtcNow;
            existingRequest.Rssi = request.Rssi;
            existingRequest.FwVersion = request.FwVersion;
            existingRequest.Capabilities = request.Capabilities;
            return existingRequest;
        }

        // Add new request
        request.RequestId = Guid.NewGuid().ToString("N")[..8];
        request.RequestedAt = DateTime.UtcNow;
        request.Status = "pending";
        session.PendingRequests.Add(request);

        _logger.LogInformation(
            "Received pairing request from tower {TowerId} (MAC: {Mac}) for {FarmId}/{CoordId}",
            request.TowerId, request.MacAddress, farmId, coordId);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("pairing_request", new
        {
            farm_id = farmId,
            coord_id = coordId,
            request = new
            {
                request_id = request.RequestId,
                tower_id = request.TowerId,
                mac_address = request.MacAddress,
                fw_version = request.FwVersion,
                capabilities = request.Capabilities,
                rssi = request.Rssi,
                requested_at = request.RequestedAt
            }
        }, ct);

        return request;
    }

    /// <inheritdoc />
    public async Task<Tower?> ApprovePairingRequestAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (!_activeSessions.TryGetValue(sessionKey, out var session))
        {
            _logger.LogWarning("No active session for approval: {FarmId}/{CoordId}", farmId, coordId);
            return null;
        }

        // Find the pending request
        var request = session.PendingRequests.FirstOrDefault(r => r.TowerId == towerId && r.Status == "pending");
        if (request == null)
        {
            _logger.LogWarning("No pending request found for tower {TowerId}", towerId);
            return null;
        }

        // Update request status
        request.Status = "approved";
        request.ResolvedAt = DateTime.UtcNow;
        session.ApprovedTowers.Add(towerId);

        // Send approval command to coordinator
        var topic = $"farm/{farmId}/coord/{coordId}/cmd";
        var command = new
        {
            cmd = "approve_pairing",
            @params = new
            {
                tower_id = towerId,
                mac_address = request.MacAddress
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        _logger.LogInformation("Approved pairing for tower {TowerId} on {FarmId}/{CoordId}", 
            towerId, farmId, coordId);

        // Create the tower entity
        var tower = new Tower
        {
            Id = $"{farmId}/{coordId}/{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = $"Tower {towerId[..Math.Min(4, towerId.Length)]}",
            MacAddress = request.MacAddress,
            StatusMode = "pairing",
            FwVersion = request.FwVersion ?? string.Empty,
            Capabilities = request.Capabilities,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };

        await _towerRepository.UpsertAsync(tower, ct);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("pairing_approved", new
        {
            farm_id = farmId,
            coord_id = coordId,
            tower_id = towerId,
            tower = tower
        }, ct);

        return tower;
    }

    /// <inheritdoc />
    public async Task<bool> RejectPairingRequestAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        if (!_activeSessions.TryGetValue(sessionKey, out var session))
        {
            _logger.LogWarning("No active session for rejection: {FarmId}/{CoordId}", farmId, coordId);
            return false;
        }

        // Find the pending request
        var request = session.PendingRequests.FirstOrDefault(r => r.TowerId == towerId && r.Status == "pending");
        if (request == null)
        {
            _logger.LogWarning("No pending request found for tower {TowerId}", towerId);
            return false;
        }

        // Update request status
        request.Status = "rejected";
        request.ResolvedAt = DateTime.UtcNow;
        session.RejectedTowers.Add(towerId);

        // Send rejection command to coordinator
        var topic = $"farm/{farmId}/coord/{coordId}/cmd";
        var command = new
        {
            cmd = "reject_pairing",
            @params = new
            {
                tower_id = towerId,
                mac_address = request.MacAddress
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        _logger.LogInformation("Rejected pairing for tower {TowerId} on {FarmId}/{CoordId}", 
            towerId, farmId, coordId);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("pairing_rejected", new
        {
            farm_id = farmId,
            coord_id = coordId,
            tower_id = towerId
        }, ct);

        return true;
    }

    /// <inheritdoc />
    public async Task ExpireTimedOutSessionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _activeSessions)
        {
            if (kvp.Value.Status == "active" && kvp.Value.ExpiresAt <= now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_activeSessions.TryRemove(key, out var session))
            {
                session.Status = "expired";
                session.EndedAt = now;

                _logger.LogInformation("Pairing session expired for {FarmId}/{CoordId}", 
                    session.FarmId, session.CoordId);

                // Broadcast expiration
                await _broadcaster.BroadcastAsync("pairing_status", new
                {
                    farm_id = session.FarmId,
                    coord_id = session.CoordId,
                    status = "expired"
                }, ct);
            }
        }
    }

    /// <inheritdoc />
    public async Task HandlePairingStatusAsync(
        string farmId,
        string coordId,
        PairingStatusUpdate status,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        _logger.LogDebug("Received pairing status from {FarmId}/{CoordId}: {Status}",
            farmId, coordId, status.Status);

        switch (status.Status.ToLowerInvariant())
        {
            case "started":
            case "active":
                // Coordinator confirmed it's in pairing mode
                break;

            case "stopped":
            case "timeout":
                // Coordinator exited pairing mode
                if (_activeSessions.TryRemove(sessionKey, out var session))
                {
                    session.Status = status.Status.ToLowerInvariant() == "timeout" ? "expired" : "completed";
                    session.EndedAt = DateTime.UtcNow;
                }
                break;
        }

        // Broadcast status update
        await _broadcaster.BroadcastAsync("pairing_status", new
        {
            farm_id = farmId,
            coord_id = coordId,
            status = status.Status,
            remaining_seconds = status.RemainingSeconds,
            pending_count = status.PendingCount
        }, ct);
    }

    /// <inheritdoc />
    public async Task HandlePairingCompleteAsync(
        string farmId,
        string coordId,
        PairingCompleteEvent completion,
        CancellationToken ct = default)
    {
        var sessionKey = $"{farmId}/{coordId}";

        _logger.LogInformation(
            "Pairing complete for tower {TowerId} on {FarmId}/{CoordId}: success={Success}",
            completion.TowerId, farmId, coordId, completion.Success);

        if (completion.Success)
        {
            // Update tower status to operational
            var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, completion.TowerId, ct);
            if (tower != null)
            {
                tower.StatusMode = "operational";
                tower.LastSeen = DateTime.UtcNow;
                tower.UpdatedAt = DateTime.UtcNow;
                
                if (completion.Capabilities != null)
                    tower.Capabilities = completion.Capabilities;
                if (!string.IsNullOrEmpty(completion.FwVersion))
                    tower.FwVersion = completion.FwVersion;

                await _towerRepository.UpsertAsync(tower, ct);
            }

            // Broadcast success
            await _broadcaster.BroadcastAsync("pairing_complete", new
            {
                farm_id = farmId,
                coord_id = coordId,
                tower_id = completion.TowerId,
                success = true,
                tower
            }, ct);
        }
        else
        {
            // Pairing failed - clean up if tower was created
            _logger.LogWarning("Pairing failed for tower {TowerId}: {Error}", 
                completion.TowerId, completion.Error);

            await _broadcaster.BroadcastAsync("pairing_complete", new
            {
                farm_id = farmId,
                coord_id = coordId,
                tower_id = completion.TowerId,
                success = false,
                error = completion.Error
            }, ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ForgetDeviceAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default)
    {
        // Check if the tower exists
        var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, towerId, ct);
        if (tower == null)
        {
            _logger.LogWarning("Cannot forget tower {TowerId}: not found in {FarmId}/{CoordId}",
                towerId, farmId, coordId);
            return false;
        }

        // Send forget command to coordinator via MQTT
        // The coordinator should forward this to the tower to wipe its credentials
        var topic = $"farm/{farmId}/coord/{coordId}/cmd";
        var command = new
        {
            cmd = "forget_device",
            @params = new
            {
                tower_id = towerId,
                mac_address = tower.MacAddress
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        _logger.LogInformation(
            "Sent forget command for tower {TowerId} to coordinator {FarmId}/{CoordId}",
            towerId, farmId, coordId);

        // Delete the tower from the repository
        await _towerRepository.DeleteAsync(farmId, coordId, towerId, ct);

        _logger.LogInformation(
            "Deleted tower {TowerId} from {FarmId}/{CoordId}",
            towerId, farmId, coordId);

        // Broadcast to WebSocket clients
        await _broadcaster.BroadcastAsync("device_forgotten", new
        {
            farm_id = farmId,
            coord_id = coordId,
            tower_id = towerId
        }, ct);

        return true;
    }
}
