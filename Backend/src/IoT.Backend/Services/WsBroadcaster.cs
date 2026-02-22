using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using IoT.Backend.Models.Diagnostics;

namespace IoT.Backend.Services;

/// <summary>
/// WebSocket broadcaster that sends messages to all connected clients.
/// Used for real-time telemetry updates (tower/reservoir data) to the frontend.
///
/// Telemetry messages (tower_telemetry, reservoir_telemetry) are throttled:
/// incoming payloads are buffered and flushed to clients at most every 500ms,
/// with latest-value deduplication per device ID. This collapses 1000+ msg/s
/// into ~2 batched frames/s, dramatically reducing browser-side pressure.
///
/// Non-telemetry broadcasts (alerts, OTA, diagnostics, etc.) are sent immediately.
/// </summary>
public class WsBroadcaster : IWsBroadcaster, IDisposable
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<WsBroadcaster> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // --- Telemetry throttle state ---
    // Key = device ID (tower or reservoir), Value = the envelope to broadcast.
    // Latest write wins, so rapid updates for the same device collapse into one.
    private readonly ConcurrentDictionary<string, TelemetryEnvelope> _pendingTelemetry = new();
    private readonly Timer _flushTimer;
    private const int FlushIntervalMs = 500;
    private int _flushing; // 0 = idle, 1 = flushing (used as a re-entrancy guard)
    private bool _disposed;

    public int ConnectedClientCount => _clients.Count;

    public WsBroadcaster(ILogger<WsBroadcaster> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Start the periodic flush timer. It fires every 500ms.
        _flushTimer = new Timer(
            callback: _ => _ = FlushTelemetryBufferAsync(),
            state: null,
            dueTime: FlushIntervalMs,
            period: FlushIntervalMs);
    }

    // ----------------------------------------------------------------
    //  Client registration
    // ----------------------------------------------------------------

    /// <summary>
    /// Registers a WebSocket client for broadcasts.
    /// </summary>
    public string RegisterClient(WebSocket socket)
    {
        var clientId = Guid.NewGuid().ToString("N");
        _clients[clientId] = socket;
        _logger.LogInformation("WebSocket client {ClientId} registered. Total clients: {Count}",
            clientId, _clients.Count);
        return clientId;
    }

    /// <summary>
    /// Unregisters a WebSocket client.
    /// </summary>
    public void UnregisterClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out _))
        {
            _logger.LogInformation("WebSocket client {ClientId} unregistered. Total clients: {Count}",
                clientId, _clients.Count);
        }
    }

    // ----------------------------------------------------------------
    //  Immediate (non-throttled) broadcast
    // ----------------------------------------------------------------

    /// <summary>
    /// Broadcasts a message to all connected WebSocket clients immediately.
    /// </summary>
    public async Task BroadcastAsync<T>(string messageType, T payload, CancellationToken ct = default)
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        var message = new WsBroadcastMessage<T>
        {
            Type = messageType,
            Payload = payload
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        var segment = new ArraySegment<byte>(json);

        var deadClients = new List<string>();

        foreach (var (clientId, socket) in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                deadClients.Add(clientId);
                continue;
            }

            try
            {
                await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send to client {ClientId}", clientId);
                deadClients.Add(clientId);
            }
        }

        // Clean up dead connections
        foreach (var clientId in deadClients)
        {
            UnregisterClient(clientId);
        }
    }

    // ----------------------------------------------------------------
    //  Throttled telemetry broadcasts
    // ----------------------------------------------------------------

    /// <summary>
    /// Buffers tower (node) telemetry for the next throttled flush.
    /// The latest payload per TowerId wins (deduplication).
    /// </summary>
    public Task BroadcastTowerTelemetryAsync(TowerTelemetryPayload payload, CancellationToken ct = default)
    {
        var key = $"tower:{payload.TowerId}";
        var envelope = new TelemetryEnvelope
        {
            Type = "tower_telemetry",
            Payload = payload
        };
        _pendingTelemetry[key] = envelope;

        // Return completed task; actual send happens on the flush timer.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Buffers reservoir (coordinator) telemetry for the next throttled flush.
    /// The latest payload per ReservoirId wins (deduplication).
    /// </summary>
    public Task BroadcastReservoirTelemetryAsync(ReservoirTelemetryPayload payload, CancellationToken ct = default)
    {
        var key = $"reservoir:{payload.ReservoirId}";
        var envelope = new TelemetryEnvelope
        {
            Type = "reservoir_telemetry",
            Payload = payload
        };
        _pendingTelemetry[key] = envelope;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Timer callback: snapshots and clears the pending telemetry buffer,
    /// then broadcasts a single "telemetry_batch" message containing all
    /// accumulated payloads. No-ops when the buffer is empty or no clients
    /// are connected.
    /// </summary>
    private async Task FlushTelemetryBufferAsync()
    {
        // Re-entrancy guard: if a previous flush is still running, skip.
        if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0)
            return;

        try
        {
            if (_pendingTelemetry.IsEmpty || _clients.IsEmpty)
                return;

            // Snapshot: grab all entries and remove them atomically per key.
            var batch = new List<TelemetryEnvelope>(_pendingTelemetry.Count);
            foreach (var key in _pendingTelemetry.Keys)
            {
                if (_pendingTelemetry.TryRemove(key, out var envelope))
                {
                    batch.Add(envelope);
                }
            }

            if (batch.Count == 0)
                return;

            _logger.LogDebug("Flushing telemetry batch: {Count} payloads to {Clients} clients",
                batch.Count, _clients.Count);

            // Build the batch message:
            // { "type": "telemetry_batch", "payload": [ { "type": "tower_telemetry", "payload": {...} }, ... ] }
            await BroadcastAsync("telemetry_batch", batch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing telemetry buffer");
        }
        finally
        {
            Interlocked.Exchange(ref _flushing, 0);
        }
    }

    // ----------------------------------------------------------------
    //  Immediate (non-telemetry) broadcast methods
    // ----------------------------------------------------------------

    /// <summary>
    /// Broadcasts a zone change notification.
    /// </summary>
    public Task BroadcastZoneChangeAsync(string zoneId, string action, CancellationToken ct = default)
    {
        var payload = new { ZoneId = zoneId, Action = action };
        return BroadcastAsync("zone_change", payload, ct);
    }

    /// <summary>
    /// Broadcasts an OTA update status change.
    /// </summary>
    public Task BroadcastOtaStatusAsync(string jobId, string status, CancellationToken ct = default)
    {
        var payload = new { JobId = jobId, Status = status };
        return BroadcastAsync("ota_status", payload, ct);
    }

    /// <summary>
    /// Broadcasts coordinator serial log messages to all connected clients.
    /// Used for real-time log streaming from coordinator firmware to frontend.
    /// </summary>
    public Task BroadcastCoordinatorLogAsync(CoordinatorLogPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("coordinator_log", payload, ct);
    }

    /// <summary>
    /// Broadcasts farm statistics update to all connected clients.
    /// </summary>
    public Task BroadcastFarmUpdateAsync(FarmUpdatePayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("farm_update", payload, ct);
    }

    /// <summary>
    /// Broadcasts a newly created alert to all connected clients.
    /// </summary>
    public Task BroadcastAlertCreatedAsync(AlertPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("alert_created", payload, ct);
    }

    /// <summary>
    /// Broadcasts an alert update (acknowledged or resolved) to all connected clients.
    /// </summary>
    public Task BroadcastAlertUpdatedAsync(AlertPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("alert_updated", payload, ct);
    }

    /// <summary>
    /// Broadcasts tower status change to all connected clients.
    /// </summary>
    public Task BroadcastTowerStatusAsync(TowerStatusPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("tower_status", payload, ct);
    }

    /// <summary>
    /// Broadcasts coordinator connection status events (WiFi/MQTT connect/disconnect) to all connected clients.
    /// Used for real-time connection monitoring in the frontend.
    /// </summary>
    public Task BroadcastConnectionStatusAsync(ConnectionStatusPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("connection_status", payload, ct);
    }

    /// <summary>
    /// Broadcasts a coordinator registration request to all connected clients.
    /// Triggered when an unknown coordinator is detected on MQTT.
    /// </summary>
    public Task BroadcastCoordinatorRegistrationRequestAsync(CoordinatorRegistrationPayload payload, CancellationToken ct = default)
        => BroadcastAsync("coordinator_registration_request", payload, ct);

    /// <summary>
    /// Broadcasts a coordinator registered event to all connected clients.
    /// Triggered when a coordinator registration is approved.
    /// </summary>
    public Task BroadcastCoordinatorRegisteredAsync(CoordinatorRegisteredPayload payload, CancellationToken ct = default)
        => BroadcastAsync("coordinator_registered", payload, ct);

    /// <summary>
    /// Broadcasts a diagnostics metrics snapshot to all connected clients.
    /// </summary>
    public Task BroadcastDiagnosticsAsync(SystemMetricsSnapshot snapshot, CancellationToken ct = default)
        => BroadcastAsync("diagnostics_update", snapshot, ct);

    // ----------------------------------------------------------------
    //  Disposal
    // ----------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _flushTimer.Dispose();

        // Best-effort final flush (fire-and-forget).
        try
        {
            FlushTelemetryBufferAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Swallow; we're shutting down.
        }
    }
}

/// <summary>
/// WebSocket broadcast message wrapper.
/// </summary>
public class WsBroadcastMessage<T>
{
    public string Type { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
}

/// <summary>
/// Envelope for a single telemetry payload inside a throttled batch.
/// Serialized as { "type": "tower_telemetry"|"reservoir_telemetry", "payload": {...} }.
/// </summary>
public class TelemetryEnvelope
{
    public string Type { get; set; } = string.Empty;
    public object Payload { get; set; } = default!;
}
