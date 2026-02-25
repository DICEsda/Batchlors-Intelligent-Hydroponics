using IoT.Backend.Models;

namespace IoT.Backend.Services;

/// <summary>
/// Interface for MQTT operations.
/// Provides publish/subscribe functionality for IoT messaging.
/// </summary>
public interface IMqttService
{
    /// <summary>
    /// Indicates whether the client is connected to the MQTT broker.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Publishes a message to an MQTT topic.
    /// </summary>
    Task PublishAsync(string topic, byte[] payload, byte qos = 1, CancellationToken ct = default);

    /// <summary>
    /// Publishes a JSON-serialized message to an MQTT topic.
    /// </summary>
    Task PublishJsonAsync<T>(string topic, T payload, byte qos = 1, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to an MQTT topic with a message handler.
    /// </summary>
    Task SubscribeAsync(string topic, Func<string, byte[], Task> handler, byte qos = 1, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribes from an MQTT topic.
    /// </summary>
    Task UnsubscribeAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Starts the MQTT client and establishes connection.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the MQTT client and disconnects.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}

/// <summary>
/// Callback for MQTT message received events.
/// </summary>
public delegate Task MqttMessageHandler(string topic, byte[] payload);
