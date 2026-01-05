namespace IoT.Backend.Services;

/// <summary>
/// Interface for WebSocket broadcasting to all connected clients.
/// Used for real-time telemetry updates to the frontend.
/// </summary>
public interface IWsBroadcaster
{
    /// <summary>
    /// Broadcasts a message to all connected WebSocket clients.
    /// </summary>
    Task BroadcastAsync<T>(string messageType, T payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts tower (node) telemetry to all connected clients.
    /// </summary>
    Task BroadcastTowerTelemetryAsync(TowerTelemetryPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts reservoir (coordinator) telemetry to all connected clients.
    /// </summary>
    Task BroadcastReservoirTelemetryAsync(ReservoirTelemetryPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a zone change notification.
    /// </summary>
    Task BroadcastZoneChangeAsync(string zoneId, string action, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts an OTA update status change.
    /// </summary>
    Task BroadcastOtaStatusAsync(string jobId, string status, CancellationToken ct = default);

    /// <summary>
    /// Registers a WebSocket client for broadcasts.
    /// </summary>
    string RegisterClient(System.Net.WebSockets.WebSocket socket);

    /// <summary>
    /// Unregisters a WebSocket client.
    /// </summary>
    void UnregisterClient(string clientId);

    /// <summary>
    /// Gets the count of connected clients.
    /// </summary>
    int ConnectedClientCount { get; }
}

/// <summary>
/// Tower (node) telemetry payload for WebSocket broadcast.
/// </summary>
public class TowerTelemetryPayload
{
    public string TowerId { get; set; } = string.Empty;
    public string LightId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public float TempC { get; set; }
    public TowerLightState Light { get; set; } = new();
    public int VbatMv { get; set; }
    public string StatusMode { get; set; } = "operational";
}

/// <summary>
/// Light state for tower telemetry.
/// </summary>
public class TowerLightState
{
    public bool On { get; set; }
    public int Brightness { get; set; }
    public int AvgR { get; set; }
    public int AvgG { get; set; }
    public int AvgB { get; set; }
    public int AvgW { get; set; }
}

/// <summary>
/// Reservoir (coordinator) telemetry payload for WebSocket broadcast.
/// </summary>
public class ReservoirTelemetryPayload
{
    public string ReservoirId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public float LightLux { get; set; }
    public float TempC { get; set; }
    public int WifiRssi { get; set; }
}
