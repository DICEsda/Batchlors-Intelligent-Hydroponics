using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Helpers;

/// <summary>
/// WebSocket test client for testing real-time updates from the backend.
/// </summary>
public class WebSocketTestClient : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly ITestOutputHelper? _output;
    private readonly List<WebSocketMessage> _receivedMessages = new();
    private readonly SemaphoreSlim _messageLock = new(1, 1);
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;
    public IReadOnlyList<WebSocketMessage> ReceivedMessages => _receivedMessages.AsReadOnly();

    public WebSocketTestClient(ITestOutputHelper? output = null)
    {
        _output = output;
        _webSocket = new ClientWebSocket();
    }

    /// <summary>
    /// Connects to the WebSocket endpoint.
    /// </summary>
    public async Task ConnectAsync(string url)
    {
        _output?.WriteLine($"WebSocket connecting to {url}...");
        await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
        _output?.WriteLine("WebSocket connected");

        // Start receiving messages in background
        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
    }

    /// <summary>
    /// Connects to the broadcast WebSocket endpoint on the test server.
    /// </summary>
    public Task ConnectBroadcastAsync(HttpClient httpClient)
    {
        var baseAddress = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
        var wsUrl = baseAddress.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws/broadcast";
        return ConnectAsync(wsUrl);
    }

    /// <summary>
    /// Connects to the subscription-based WebSocket endpoint on the test server.
    /// </summary>
    public Task ConnectSubscriptionAsync(HttpClient httpClient)
    {
        var baseAddress = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
        var wsUrl = baseAddress.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws";
        return ConnectAsync(wsUrl);
    }

    /// <summary>
    /// Sends a JSON message over the WebSocket.
    /// </summary>
    public async Task SendJsonAsync<T>(T message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        var buffer = Encoding.UTF8.GetBytes(json);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        _output?.WriteLine($"Sent: {json}");
    }

    /// <summary>
    /// Sends a subscription request for a coordinator.
    /// </summary>
    public Task SubscribeToCoordinatorAsync(string coordId)
    {
        return SendJsonAsync(new { type = "subscribe", target = "coordinator", id = coordId });
    }

    /// <summary>
    /// Sends a subscription request for a tower.
    /// </summary>
    public Task SubscribeToTowerAsync(string towerId)
    {
        return SendJsonAsync(new { type = "subscribe", target = "tower", id = towerId });
    }

    /// <summary>
    /// Waits for a message matching the predicate.
    /// </summary>
    public async Task<WebSocketMessage?> WaitForMessageAsync(
        Func<WebSocketMessage, bool> predicate,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            await _messageLock.WaitAsync();
            try
            {
                var match = _receivedMessages.FirstOrDefault(predicate);
                if (match != null)
                {
                    return match;
                }
            }
            finally
            {
                _messageLock.Release();
            }

            await Task.Delay(100);
        }

        return null;
    }

    /// <summary>
    /// Waits for a message of a specific type.
    /// </summary>
    public Task<WebSocketMessage?> WaitForMessageTypeAsync(string messageType, TimeSpan? timeout = null)
    {
        return WaitForMessageAsync(m => m.Type == messageType, timeout);
    }

    /// <summary>
    /// Clears all received messages.
    /// </summary>
    public async Task ClearMessagesAsync()
    {
        await _messageLock.WaitAsync();
        try
        {
            _receivedMessages.Clear();
        }
        finally
        {
            _messageLock.Release();
        }
    }

    /// <summary>
    /// Closes the WebSocket connection.
    /// </summary>
    public async Task CloseAsync()
    {
        _receiveCts?.Cancel();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Test completed",
                    CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _output?.WriteLine("WebSocket closed");
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                messageBuffer.AddRange(buffer.Take(result.Count));

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();

                    var message = WebSocketMessage.Parse(json);

                    await _messageLock.WaitAsync(ct);
                    try
                    {
                        _receivedMessages.Add(message);
                    }
                    finally
                    {
                        _messageLock.Release();
                    }

                    _output?.WriteLine($"Received [{message.Type}]: {json.Substring(0, Math.Min(200, json.Length))}...");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when closing
        }
        catch (WebSocketException ex)
        {
            _output?.WriteLine($"WebSocket error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _receiveCts?.Dispose();
        _messageLock.Dispose();
        _webSocket.Dispose();
    }
}

/// <summary>
/// Represents a message received over WebSocket.
/// </summary>
public class WebSocketMessage
{
    public string Type { get; init; } = string.Empty;
    public JsonElement? Payload { get; init; }
    public string RawJson { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    public static WebSocketMessage Parse(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString() ?? "unknown"
                : "unknown";

            JsonElement? payload = root.TryGetProperty("payload", out var payloadProp)
                ? payloadProp
                : root.TryGetProperty("data", out var dataProp)
                    ? dataProp
                    : null;

            return new WebSocketMessage
            {
                Type = type,
                Payload = payload,
                RawJson = json
            };
        }
        catch
        {
            return new WebSocketMessage
            {
                Type = "parse_error",
                RawJson = json
            };
        }
    }

    public T? DeserializePayload<T>() where T : class
    {
        if (Payload == null) return null;
        return JsonSerializer.Deserialize<T>(Payload.Value.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }
}
