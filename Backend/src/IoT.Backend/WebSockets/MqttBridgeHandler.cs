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
        try
        {
            var message = JsonSerializer.Deserialize<WsMessage>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (message == null)
            {
                _logger.LogWarning("Invalid WebSocket message from {ConnectionId}", connection.Id);
                return;
            }

            switch (message.Type.ToLowerInvariant())
            {
                case "subscribe":
                    await HandleSubscribeAsync(connection, message.Topic, ct);
                    break;

                case "unsubscribe":
                    await HandleUnsubscribeAsync(connection, message.Topic, ct);
                    break;

                case "publish":
                    await HandlePublishAsync(message.Topic, message.Payload, ct);
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {Type}", message.Type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message");
        }
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
