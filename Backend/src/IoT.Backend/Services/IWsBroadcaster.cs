using IoT.Backend.Models.Diagnostics;

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
    /// Broadcasts coordinator serial log messages to all connected clients.
    /// </summary>
    Task BroadcastCoordinatorLogAsync(CoordinatorLogPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts farm update (statistics changed).
    /// </summary>
    Task BroadcastFarmUpdateAsync(FarmUpdatePayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts alert created event.
    /// </summary>
    Task BroadcastAlertCreatedAsync(AlertPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts alert updated event (acknowledged or resolved).
    /// </summary>
    Task BroadcastAlertUpdatedAsync(AlertPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts tower status change (online/offline).
    /// </summary>
    Task BroadcastTowerStatusAsync(TowerStatusPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts coordinator connection status events (WiFi/MQTT connect/disconnect).
    /// </summary>
    Task BroadcastConnectionStatusAsync(ConnectionStatusPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a coordinator registration request to the frontend.
    /// Triggered when an unknown coordinator is detected on MQTT.
    /// </summary>
    Task BroadcastCoordinatorRegistrationRequestAsync(CoordinatorRegistrationPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a coordinator registered event to the frontend.
    /// Triggered when a coordinator registration is approved.
    /// </summary>
    Task BroadcastCoordinatorRegisteredAsync(CoordinatorRegisteredPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Broadcasts a diagnostics metrics snapshot to all connected clients.
    /// </summary>
    Task BroadcastDiagnosticsAsync(SystemMetricsSnapshot snapshot, CancellationToken ct = default);

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
    // Water quality — previously missing, needed by frontend charts
    public float Ph { get; set; }
    public float EcMsCm { get; set; }
    public float WaterLevelPct { get; set; }
    public float WaterTempC { get; set; }
    public bool MainPumpOn { get; set; }
}

/// <summary>
/// Coordinator serial log payload for WebSocket broadcast.
/// Real-time log streaming from coordinator firmware to frontend.
/// </summary>
public class CoordinatorLogPayload
{
    public string CoordId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

/// <summary>
/// Farm update payload for WebSocket broadcast.
/// </summary>
public class FarmUpdatePayload
{
    public string FarmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CoordinatorCount { get; set; }
    public int TowerCount { get; set; }
    public int ActiveAlertCount { get; set; }
}

/// <summary>
/// Alert payload for WebSocket broadcast (created or updated).
/// </summary>
public class AlertPayload
{
    public string AlertId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string? CoordId { get; set; }
    public string? TowerId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}

/// <summary>
/// Tower status payload for WebSocket broadcast.
/// </summary>
public class TowerStatusPayload
{
    public string TowerId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string CoordId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // online, offline, error
    public long Timestamp { get; set; }
}

/// <summary>
/// Coordinator connection status payload for WebSocket broadcast.
/// Tracks WiFi and MQTT connection lifecycle events.
/// </summary>
public class ConnectionStatusPayload
{
    public long Ts { get; set; }
    public string CoordId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty; // wifi_connected, wifi_disconnected, mqtt_connected, mqtt_disconnected, wifi_got_ip, wifi_lost_ip
    public bool WifiConnected { get; set; }
    public int WifiRssi { get; set; }
    public bool MqttConnected { get; set; }
    public long UptimeMs { get; set; }
    public int FreeHeap { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Coordinator registration request payload for WebSocket broadcast.
/// Sent to frontend when an unknown coordinator is detected on MQTT.
/// </summary>
public class CoordinatorRegistrationPayload
{
    public string CoordId { get; set; } = string.Empty;
    public string? FwVersion { get; set; }
    public string? ChipModel { get; set; }
    public int WifiRssi { get; set; }
    public string? Ip { get; set; }
    public int FreeHeap { get; set; }
    public DateTime FirstSeenAt { get; set; }
}

/// <summary>
/// Coordinator registered payload for WebSocket broadcast.
/// Sent to frontend when a coordinator registration is approved.
/// </summary>
public class CoordinatorRegisteredPayload
{
    public string CoordId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Location { get; set; }
    public DateTime RegisteredAt { get; set; }
}
