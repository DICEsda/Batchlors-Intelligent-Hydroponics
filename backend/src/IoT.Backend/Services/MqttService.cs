using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoT.Backend.Services;

/// <summary>
/// Configuration options for MQTT connection.
/// </summary>
public class MqttOptions
{
    public const string Section = "Mqtt";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "iot-backend";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int ReconnectDelayMs { get; set; } = 5000;
}

/// <summary>
/// MQTTnet-based implementation of IMqttService.
/// Provides reliable pub/sub with automatic reconnection.
/// </summary>
public class MqttService : IMqttService, IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttService> _logger;
    private readonly ConcurrentDictionary<string, List<Func<string, byte[], Task>>> _handlers = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public bool IsConnected => _client.IsConnected;

    public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrEmpty(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        _clientOptions = builder.Build();

        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await ConnectAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _reconnectCts?.Cancel();
        
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder()
                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                .Build(), ct);
        }
    }

    public async Task PublishAsync(string topic, byte[] payload, byte qos = 1, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
        {
            _logger.LogWarning("Cannot publish to {Topic}: not connected", topic);
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
            .Build();

        await _client.PublishAsync(message, ct);
        _logger.LogDebug("Published {Bytes} bytes to {Topic}", payload.Length, topic);
    }

    public async Task PublishJsonAsync<T>(string topic, T payload, byte qos = 1, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        await PublishAsync(topic, json, qos, ct);
    }

    public async Task SubscribeAsync(string topic, Func<string, byte[], Task> handler, byte qos = 1, CancellationToken ct = default)
    {
        var handlers = _handlers.GetOrAdd(topic, _ => new List<Func<string, byte[], Task>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }

        if (_client.IsConnected)
        {
            await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic, (MqttQualityOfServiceLevel)qos)
                .Build(), ct);
            _logger.LogInformation("Subscribed to {Topic}", topic);
        }
    }

    public async Task UnsubscribeAsync(string topic, CancellationToken ct = default)
    {
        _handlers.TryRemove(topic, out _);

        if (_client.IsConnected)
        {
            await _client.UnsubscribeAsync(new MqttClientUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build(), ct);
            _logger.LogInformation("Unsubscribed from {Topic}", topic);
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        await _connectLock.WaitAsync(ct);
        try
        {
            if (_client.IsConnected) return;

            _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}", _options.Host, _options.Port);
            await _client.ConnectAsync(_clientOptions, ct);
            _logger.LogInformation("Connected to MQTT broker");

            // Re-subscribe to all topics
            foreach (var topic in _handlers.Keys)
            {
                await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build(), ct);
                _logger.LogDebug("Re-subscribed to {Topic}", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        if (_disposed || _reconnectCts?.IsCancellationRequested == true)
        {
            return;
        }

        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", e.ReasonString);

        // Attempt reconnection with backoff
        while (!_reconnectCts?.IsCancellationRequested ?? false)
        {
            try
            {
                await Task.Delay(_options.ReconnectDelayMs, _reconnectCts!.Token);
                await ConnectAsync(_reconnectCts.Token);
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconnection attempt failed, retrying...");
            }
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.PayloadSegment.ToArray();

        _logger.LogDebug("Received message on {Topic}: {Bytes} bytes", topic, payload.Length);

        // Find matching handlers (support wildcards)
        foreach (var (pattern, handlers) in _handlers)
        {
            if (TopicMatches(pattern, topic))
            {
                List<Func<string, byte[], Task>> handlersCopy;
                lock (handlers)
                {
                    handlersCopy = handlers.ToList();
                }

                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        await handler(topic, payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Handler error for topic {Topic}", topic);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Matches MQTT topic patterns including wildcards (+ and #).
    /// </summary>
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }

        _client.Dispose();
        _connectLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
