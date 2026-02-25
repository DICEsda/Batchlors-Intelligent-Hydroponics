using System.Net.WebSockets;

namespace IoT.Backend.Services;

/// <summary>
/// Interface for the WebSocket-to-MQTT bridge handler.
/// Manages WebSocket connections and bridges MQTT messages to connected clients.
/// </summary>
public interface IMqttBridgeHandler
{
    /// <summary>
    /// Handles a new WebSocket connection, processing subscribe/unsubscribe/publish messages
    /// and bridging MQTT topics to the WebSocket client.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection to handle.</param>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    Task HandleAsync(WebSocket webSocket, CancellationToken ct);
}
