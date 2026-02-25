using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using IoT.Backend.Services;

namespace IoT.Backend.WebSockets;

/// <summary>
/// WebSocket message types matching the Go backend contract.
/// </summary>
public class WsMessage
{
    public string Type { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public JsonElement? Payload { get; set; }
}

/// <summary>
/// Manages WebSocket connections and bridges MQTT messages to connected clients.
/// Clients can subscribe to MQTT topics via WebSocket messages.
/// </summary>
public class MqttBridgeHandler : IMqttBridgeHandler
{
    private readonly IMqttService _mqtt;
    private readonly ILogger<MqttBridgeHandler> _logger;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

    /// <summary>
    /// The set of message types the backend accepts from frontend clients.
    /// </summary>
    private static readonly HashSet<string> ValidIncomingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping",
        "subscribe",
        "unsubscribe",
        "publish"
    };

    /// <summary>
    /// Message types that require a non-empty <c>topic</c> field.
    /// </summary>
    private static readonly HashSet<string> TypesRequiringTopic = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscribe",
        "unsubscribe",
        "publish"
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MqttBridgeHandler(IMqttService mqtt, ILogger<MqttBridgeHandler> logger)
    {
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Handles a new WebSocket connection.
    /// </summary>
    public async Task HandleAsync(WebSocket webSocket, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var connection = new WebSocketConnection(connectionId, webSocket);
        _connections[connectionId] = connection;

        _logger.LogInformation("WebSocket connected: {ConnectionId}", connectionId);

        try 
        {
            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessageAsync(connection, json, ct);
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("WebSocket closed prematurely: {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error: {ConnectionId}", connectionId);
        }
        finally
        {
            await CleanupConnectionAsync(connection);
        }
    }

    private async Task HandleMessageAsync(WebSocketConnection connection, string json, CancellationToken ct)
    {
        // ── Step 1: Validate JSON ──────────────────────────────────────
        WsMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<WsMessage>(json, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON from {ConnectionId}", connection.Id);
            await SendErrorAsync(connection.Socket, "Invalid message: malformed JSON", ct);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Null deserialization result from {ConnectionId}", connection.Id);
            await SendErrorAsync(connection.Socket, "Invalid message: empty payload", ct);
            return;
        }

        // ── Step 2: Validate 'type' field exists ───────────────────────
        if (string.IsNullOrWhiteSpace(message.Type))
        {
            _logger.LogWarning("Missing 'type' field from {ConnectionId}", connection.Id);
            await SendErrorAsync(connection.Socket, "Invalid message: missing 'type' field", ct);
            return;
        }

        // ── Step 3: Validate 'type' is a known value ──────────────────
        if (!ValidIncomingTypes.Contains(message.Type))
        {
            _logger.LogWarning("Unknown message type '{Type}' from {ConnectionId}", message.Type, connection.Id);
            await SendErrorAsync(connection.Socket,
                $"Invalid message: unknown type '{message.Type}'", ct);
            return;
        }

        // ── Step 4: Validate required fields per type ─────────────────
        if (TypesRequiringTopic.Contains(message.Type) && string.IsNullOrWhiteSpace(message.Topic))
        {
            _logger.LogWarning("Missing 'topic' for '{Type}' from {ConnectionId}", message.Type, connection.Id);
            await SendErrorAsync(connection.Socket,
                $"Invalid message: '{message.Type}' requires a 'topic' field", ct);
            return;
        }

        // ── Step 5: Dispatch to handler ───────────────────────────────
        switch (message.Type.ToLowerInvariant())
        {
            case "ping":
                await SendAsync(connection.Socket, new { type = "pong" }, ct);
                break;

            case "subscribe":
                await HandleSubscribeAsync(connection, message.Topic, ct);
                break;

            case "unsubscribe":
                await HandleUnsubscribeAsync(connection, message.Topic, ct);
                break;

            case "publish":
                await HandlePublishAsync(message.Topic, message.Payload, ct);
                break;
        }
    }

    /// <summary>
    /// Sends a structured error message back to the client.
    /// </summary>
    private Task SendErrorAsync(WebSocket socket, string errorMessage, CancellationToken ct)
    {
        return SendAsync(socket, new { type = "error", message = errorMessage }, ct);
    }

    private async Task HandleSubscribeAsync(WebSocketConnection connection, string? topic, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(topic))
        {
            _logger.LogWarning("Subscribe without topic from {ConnectionId}", connection.Id);
            return;
        }

        if (connection.Subscriptions.Add(topic))
        {
            _logger.LogDebug("Connection {ConnectionId} subscribed to {Topic}", connection.Id, topic);

            // Subscribe to MQTT if this is the first subscriber for this topic
            await _mqtt.SubscribeAsync(topic, async (t, payload) =>
            {
                await BroadcastToSubscribersAsync(t, payload);
            }, ct: ct);
        }

        // Send confirmation
        await SendAsync(connection.Socket, new
        {
            type = "subscribed",
            topic
        }, ct);
    }

    private async Task HandleUnsubscribeAsync(WebSocketConnection connection, string? topic, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(topic)) return;

        connection.Subscriptions.Remove(topic);
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from {Topic}", connection.Id, topic);

        await SendAsync(connection.Socket, new
        {
            type = "unsubscribed",
            topic
        }, ct);
    }

    private async Task HandlePublishAsync(string? topic, JsonElement? payload, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(topic) || payload == null) return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload.Value);
        await _mqtt.PublishAsync(topic, bytes, ct: ct);
        _logger.LogDebug("Published to {Topic} via WebSocket", topic);
    }

    private async Task BroadcastToSubscribersAsync(string topic, byte[] payload)
    {
        var message = new
        {
            type = "message",
            topic,
            payload = JsonSerializer.Deserialize<JsonElement>(payload)
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(message);

        foreach (var connection in _connections.Values)
        {
            if (connection.Socket.State != WebSocketState.Open) continue;

            // Check if connection is subscribed (including wildcards)
            if (connection.Subscriptions.Any(sub => TopicMatches(sub, topic)))
            {
                try
                {
                    await connection.Socket.SendAsync(
                        new ArraySegment<byte>(json),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send to {ConnectionId}", connection.Id);
                }
            }
        }
    }

    private async Task SendAsync<T>(WebSocket socket, T message, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        await socket.SendAsync(
            new ArraySegment<byte>(json),
            WebSocketMessageType.Text,
            true,
            ct);
    }

    private async Task CleanupConnectionAsync(WebSocketConnection connection)
    {
        _connections.TryRemove(connection.Id, out _);
        _logger.LogInformation("WebSocket disconnected: {ConnectionId}", connection.Id);

        if (connection.Socket.State == WebSocketState.Open)
        {
            try
            {
                await connection.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }
    }

    private static bool TopicMatches(string pattern, string topic)
    {
        if (pattern == topic) return true;
        if (!pattern.Contains('+') && !pattern.Contains('#')) return false;

        var patternParts = pattern.Split('/');
        var topicParts = topic.Split('/');

        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i] == "#") return true;
            if (i >= topicParts.Length) return false;
            if (patternParts[i] != "+" && patternParts[i] != topicParts[i]) return false;
        }

        return patternParts.Length == topicParts.Length;
    }

    private class WebSocketConnection
    {
        public string Id { get; }
        public WebSocket Socket { get; }
        public HashSet<string> Subscriptions { get; } = new();

        public WebSocketConnection(string id, WebSocket socket)
        {
            Id = id;
            Socket = socket;
        }
    }
}
