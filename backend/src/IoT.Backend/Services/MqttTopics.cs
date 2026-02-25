namespace IoT.Backend.Services;

/// <summary>
/// Centralized MQTT topic definitions. All topic patterns used by the backend
/// are defined here to ensure consistency between subscriptions and publications.
/// </summary>
public static class MqttTopics
{
    // ========================================================================
    // Subscription patterns (used in TelemetryHandler)
    // ========================================================================

    /// <summary>Coordinator ambient sensors, mmWave radar, WiFi status.</summary>
    public const string CoordinatorTelemetry = "farm/+/coord/+/telemetry";

    /// <summary>Reservoir water-quality sensors (pH, EC, water level).</summary>
    public const string ReservoirTelemetry = "farm/+/coord/+/reservoir/telemetry";

    /// <summary>Tower environmental sensors (DHT22, light, actuator states).</summary>
    public const string TowerTelemetry = "farm/+/coord/+/tower/+/telemetry";

    /// <summary>Tower online/offline and mode-change status.</summary>
    public const string TowerStatus = "farm/+/coord/+/tower/+/status";

    /// <summary>OTA progress updates from coordinators.</summary>
    public const string OtaStatus = "farm/+/coord/+/ota/status";

    /// <summary>Tower pairing request from coordinator.</summary>
    public const string PairingRequest = "farm/+/coord/+/pairing/request";

    /// <summary>Pairing mode status updates from coordinator.</summary>
    public const string PairingStatus = "farm/+/coord/+/pairing/status";

    /// <summary>Pairing completion events from coordinator.</summary>
    public const string PairingComplete = "farm/+/coord/+/pairing/complete";

    /// <summary>Coordinator serial log stream (debug/diagnostics).</summary>
    public const string SerialLog = "farm/+/coord/+/serial";

    /// <summary>WiFi/MQTT connection lifecycle events.</summary>
    public const string ConnectionStatus = "farm/+/coord/+/status/connection";

    /// <summary>Coordinator announce / discovery (registration workflow).</summary>
    public const string CoordinatorAnnounce = "coordinator/+/announce";

    // ========================================================================
    // Publication topic builders (used in controllers / services)
    // ========================================================================

    /// <summary>General command channel for a coordinator.</summary>
    public static string CoordinatorCmd(string farmId, string coordId)
        => $"farm/{farmId}/coord/{coordId}/cmd";

    /// <summary>Command channel for a specific tower (routed through coordinator).</summary>
    public static string TowerCmd(string farmId, string coordId, string towerId)
        => $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

    /// <summary>Command channel for the reservoir subsystem on a coordinator.</summary>
    public static string ReservoirCmd(string farmId, string coordId)
        => $"farm/{farmId}/coord/{coordId}/reservoir/cmd";

    /// <summary>Trigger an OTA update on a coordinator.</summary>
    public static string OtaStart(string farmId, string coordId)
        => $"farm/{farmId}/coord/{coordId}/ota/start";

    /// <summary>Cancel an in-progress OTA update on a coordinator.</summary>
    public static string OtaCancel(string farmId, string coordId)
        => $"farm/{farmId}/coord/{coordId}/ota/cancel";

    /// <summary>Push operational configuration to a coordinator.</summary>
    public static string CoordinatorConfig(string coordId)
        => $"coordinator/{coordId}/config";

    /// <summary>Direct command channel for a coordinator (restart, etc.).</summary>
    public static string CoordinatorDirectCmd(string coordId)
        => $"coordinator/{coordId}/cmd";

    /// <summary>Notify a coordinator that it has been registered.</summary>
    public static string CoordinatorRegistered(string coordId)
        => $"coordinator/{coordId}/registered";

    /// <summary>
    /// Generic device command channel used by the customize controller.
    /// <paramref name="mqttDeviceType"/> is "coord" or "node".
    /// </summary>
    public static string DeviceCmd(string farmId, string mqttDeviceType, string deviceId)
        => $"farm/{farmId}/{mqttDeviceType}/{deviceId}/cmd";
}
