using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Helpers;

/// <summary>
/// MQTT test client for simulating device messages in integration tests.
/// Provides easy methods to publish telemetry and subscribe to command topics.
/// </summary>
public class MqttTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly ITestOutputHelper? _output;
    private readonly List<ReceivedMessage> _receivedMessages = new();
    private readonly SemaphoreSlim _messageLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public bool IsConnected => _client.IsConnected;
    public IReadOnlyList<ReceivedMessage> ReceivedMessages => _receivedMessages.AsReadOnly();

    public MqttTestClient(ITestOutputHelper? output = null)
    {
        _output = output;
        _client = new MqttFactory().CreateMqttClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        _client.ApplicationMessageReceivedAsync += OnMessageReceived;
    }

    /// <summary>
    /// Connects to the MQTT broker.
    /// </summary>
    public async Task ConnectAsync(string host, int port, string? clientId = null)
    {
        clientId ??= $"test-device-{Guid.NewGuid():N}";

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCleanSession(true)
            .Build();

        _output?.WriteLine($"MQTT Test Client connecting to {host}:{port} as {clientId}...");
        await _client.ConnectAsync(options);
        _output?.WriteLine("MQTT Test Client connected");
    }

    /// <summary>
    /// Disconnects from the MQTT broker.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
            _output?.WriteLine("MQTT Test Client disconnected");
        }
    }

    /// <summary>
    /// Subscribes to a topic and records all received messages.
    /// </summary>
    public async Task SubscribeAsync(string topic)
    {
        await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build());
        _output?.WriteLine($"Subscribed to: {topic}");
    }

    /// <summary>
    /// Publishes a raw message to a topic.
    /// </summary>
    public async Task PublishAsync(string topic, byte[] payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message);
        _output?.WriteLine($"Published {payload.Length} bytes to: {topic}");
    }

    /// <summary>
    /// Publishes a JSON-serialized message to a topic.
    /// </summary>
    public async Task PublishJsonAsync<T>(string topic, T payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
        await PublishAsync(topic, json);
        _output?.WriteLine($"Published JSON: {JsonSerializer.Serialize(payload, _jsonOptions)}");
    }

    /// <summary>
    /// Publishes tower telemetry message (simulates ESP32-C3 tower node).
    /// </summary>
    public Task PublishTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        float airTempC = 25.0f,
        float humidityPct = 65.0f,
        float lightLux = 500.0f,
        bool pumpOn = false,
        bool lightOn = true,
        int lightBrightness = 128,
        int vbatMv = 3300,
        string statusMode = "operational")
    {
        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry";
        var telemetry = new
        {
            air_temp_c = airTempC,
            humidity_pct = humidityPct,
            light_lux = lightLux,
            pump_on = pumpOn,
            light_on = lightOn,
            light_brightness = lightBrightness,
            vbat_mv = vbatMv,
            status_mode = statusMode,
            fw_version = "1.0.0-test",
            uptime_s = 3600,
            signal_quality = -50
        };
        return PublishJsonAsync(topic, telemetry);
    }

    /// <summary>
    /// Publishes reservoir telemetry message (simulates ESP32-S3 coordinator).
    /// </summary>
    public Task PublishReservoirTelemetryAsync(
        string farmId,
        string coordId,
        float? ph = 6.0f,
        float? ecMsCm = 1.5f,
        float? tdsPpm = 750f,
        float? waterTempC = 22.0f,
        float? waterLevelPct = 85.0f,
        bool? mainPumpOn = true,
        int towersOnline = 2,
        int wifiRssi = -45,
        string statusMode = "operational")
    {
        var topic = $"farm/{farmId}/coord/{coordId}/reservoir/telemetry";
        var telemetry = new
        {
            ph,
            ec_ms_cm = ecMsCm,
            tds_ppm = tdsPpm,
            water_temp_c = waterTempC,
            water_level_pct = waterLevelPct,
            water_level_cm = waterLevelPct.HasValue ? waterLevelPct.Value * 0.5f : (float?)null,
            low_water_alert = waterLevelPct.HasValue && waterLevelPct.Value < 20,
            main_pump_on = mainPumpOn,
            dosing_pump_ph_on = false,
            dosing_pump_nutrient_on = false,
            towers_online = towersOnline,
            wifi_rssi = wifiRssi,
            status_mode = statusMode,
            fw_version = "1.0.0-test",
            uptime_s = 7200,
            temp_c = 28.0f
        };
        return PublishJsonAsync(topic, telemetry);
    }

    /// <summary>
    /// Publishes a pairing request (simulates tower wanting to pair).
    /// </summary>
    public Task PublishPairingRequestAsync(
        string farmId,
        string coordId,
        string towerId,
        string macAddress,
        string? fwVersion = "1.0.0-test",
        int? rssi = -55)
    {
        var topic = $"farm/{farmId}/coord/{coordId}/pairing/request";
        var request = new
        {
            tower_id = towerId,
            mac_address = macAddress,
            fw_version = fwVersion,
            rssi,
            capabilities = new
            {
                dht_sensor = true,
                light_sensor = true,
                pump_relay = true,
                grow_light = true,
                slot_count = 6
            }
        };
        return PublishJsonAsync(topic, request);
    }

    /// <summary>
    /// Publishes a pairing status update (simulates coordinator response).
    /// </summary>
    public Task PublishPairingStatusAsync(
        string farmId,
        string coordId,
        string status,
        int? remainingSeconds = null,
        int? pendingCount = null)
    {
        var topic = $"farm/{farmId}/coord/{coordId}/pairing/status";
        var update = new
        {
            status,
            remaining_seconds = remainingSeconds,
            pending_count = pendingCount
        };
        return PublishJsonAsync(topic, update);
    }

    /// <summary>
    /// Publishes a pairing complete event (simulates coordinator confirmation).
    /// </summary>
    public Task PublishPairingCompleteAsync(
        string farmId,
        string coordId,
        string towerId,
        bool success,
        string? error = null)
    {
        var topic = $"farm/{farmId}/coord/{coordId}/pairing/complete";
        var completion = new
        {
            tower_id = towerId,
            success,
            error,
            capabilities = success ? new
            {
                dht_sensor = true,
                light_sensor = true,
                pump_relay = true,
                grow_light = true,
                slot_count = 6
            } : null,
            fw_version = success ? "1.0.0-test" : null
        };
        return PublishJsonAsync(topic, completion);
    }

    /// <summary>
    /// Waits for a message on the subscribed topics with a specific condition.
    /// </summary>
    public async Task<ReceivedMessage?> WaitForMessageAsync(
        Func<ReceivedMessage, bool> predicate,
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
    /// Waits for a message on a specific topic.
    /// </summary>
    public Task<ReceivedMessage?> WaitForMessageOnTopicAsync(string topic, TimeSpan? timeout = null)
    {
        return WaitForMessageAsync(m => m.Topic == topic, timeout);
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

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        var message = new ReceivedMessage
        {
            Topic = e.ApplicationMessage.Topic,
            Payload = e.ApplicationMessage.PayloadSegment.ToArray(),
            ReceivedAt = DateTime.UtcNow
        };

        await _messageLock.WaitAsync();
        try
        {
            _receivedMessages.Add(message);
        }
        finally
        {
            _messageLock.Release();
        }

        _output?.WriteLine($"Received message on: {message.Topic}");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _client.Dispose();
        _messageLock.Dispose();
    }
}

/// <summary>
/// Represents a message received by the MQTT test client.
/// </summary>
public class ReceivedMessage
{
    public string Topic { get; init; } = string.Empty;
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public DateTime ReceivedAt { get; init; }

    public string PayloadString => Encoding.UTF8.GetString(Payload);

    public T? DeserializePayload<T>() =>
        JsonSerializer.Deserialize<T>(Payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
}
