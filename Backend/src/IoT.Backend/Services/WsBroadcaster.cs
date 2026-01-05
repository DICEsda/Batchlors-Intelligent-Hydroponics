using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace IoT.Backend.Services;

/// <summary>
/// WebSocket broadcaster that sends messages to all connected clients.
/// Used for real-time telemetry updates (tower/reservoir data) to the frontend.
/// </summary>
public class WsBroadcaster : IWsBroadcaster
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<WsBroadcaster> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public int ConnectedClientCount => _clients.Count;

    public WsBroadcaster(ILogger<WsBroadcaster> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

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

    /// <summary>
    /// Broadcasts a message to all connected WebSocket clients.
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

    /// <summary>
    /// Broadcasts tower (node) telemetry to all connected clients.
    /// </summary>
    public Task BroadcastTowerTelemetryAsync(TowerTelemetryPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("tower_telemetry", payload, ct);
    }

    /// <summary>
    /// Broadcasts reservoir (coordinator) telemetry to all connected clients.
    /// </summary>
    public Task BroadcastReservoirTelemetryAsync(ReservoirTelemetryPayload payload, CancellationToken ct = default)
    {
        return BroadcastAsync("reservoir_telemetry", payload, ct);
    }

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
}

/// <summary>
/// WebSocket broadcast message wrapper.
/// </summary>
public class WsBroadcastMessage<T>
{
    public string Type { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
}
