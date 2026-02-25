using IoT.Backend.Models;

namespace IoT.Backend.Services;

/// <summary>
/// Service interface for managing coordinator-tower pairing workflow.
/// Handles pairing sessions, tower requests, and state management.
/// </summary>
public interface IPairingService
{
    /// <summary>
    /// Start a new pairing session for a coordinator.
    /// Puts the coordinator into pairing mode and tracks the session.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="durationSeconds">Duration of the pairing window (default 60s)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created pairing session</returns>
    Task<PairingSession> StartPairingSessionAsync(
        string farmId, 
        string coordId, 
        int durationSeconds = 60, 
        CancellationToken ct = default);

    /// <summary>
    /// Stop an active pairing session.
    /// Exits the coordinator from pairing mode.
    /// </summary>
    Task<PairingSession?> StopPairingSessionAsync(
        string farmId, 
        string coordId, 
        CancellationToken ct = default);

    /// <summary>
    /// Get the active pairing session for a coordinator, if any.
    /// </summary>
    Task<PairingSession?> GetActiveSessionAsync(
        string farmId, 
        string coordId, 
        CancellationToken ct = default);

    /// <summary>
    /// Get all pending pairing requests for a coordinator's active session.
    /// </summary>
    Task<IReadOnlyList<TowerPairingRequest>> GetPendingRequestsAsync(
        string farmId, 
        string coordId, 
        CancellationToken ct = default);

    /// <summary>
    /// Process an incoming tower pairing request from MQTT.
    /// Adds the request to the active session's pending list.
    /// </summary>
    Task<TowerPairingRequest?> ProcessPairingRequestAsync(
        string farmId,
        string coordId,
        TowerPairingRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Approve a pending tower pairing request.
    /// Sends approval command to coordinator and creates the tower entity.
    /// </summary>
    Task<Tower?> ApprovePairingRequestAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default);

    /// <summary>
    /// Reject a pending tower pairing request.
    /// Sends rejection command to coordinator.
    /// </summary>
    Task<bool> RejectPairingRequestAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default);

    /// <summary>
    /// Check and expire any timed-out pairing sessions.
    /// Called periodically by background service.
    /// </summary>
    Task ExpireTimedOutSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Handle pairing status update from coordinator (via MQTT).
    /// Updates session state based on coordinator feedback.
    /// </summary>
    Task HandlePairingStatusAsync(
        string farmId,
        string coordId,
        PairingStatusUpdate status,
        CancellationToken ct = default);

    /// <summary>
    /// Handle pairing completion notification from coordinator (via MQTT).
    /// Finalizes the tower registration and updates session.
    /// </summary>
    Task HandlePairingCompleteAsync(
        string farmId,
        string coordId,
        PairingCompleteEvent completion,
        CancellationToken ct = default);

    /// <summary>
    /// Forget (unpair) a device.
    /// Removes the tower from the backend and sends a command to the coordinator
    /// to notify the tower to wipe its credentials.
    /// </summary>
    /// <param name="farmId">Farm identifier</param>
    /// <param name="coordId">Coordinator identifier</param>
    /// <param name="towerId">Tower identifier to forget</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the device was found and removed</returns>
    Task<bool> ForgetDeviceAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default);
}

/// <summary>
/// Status update received from coordinator about pairing mode.
/// </summary>
public class PairingStatusUpdate
{
    /// <summary>
    /// Pairing mode status: "started", "active", "stopped", "timeout"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Remaining time in seconds (if active)
    /// </summary>
    public int? RemainingSeconds { get; set; }

    /// <summary>
    /// Number of pending tower requests
    /// </summary>
    public int? PendingCount { get; set; }
}

/// <summary>
/// Pairing completion event received from coordinator.
/// </summary>
public class PairingCompleteEvent
{
    /// <summary>
    /// Tower ID that completed pairing
    /// </summary>
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Tower's MAC address
    /// </summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Whether pairing was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if pairing failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Tower capabilities reported during pairing
    /// </summary>
    public TowerCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Firmware version of the tower
    /// </summary>
    public string? FwVersion { get; set; }
}
