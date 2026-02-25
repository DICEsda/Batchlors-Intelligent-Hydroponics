using IoT.Backend.Models;

namespace IoT.Backend.Services;

/// <summary>
/// Service for managing coordinator registration workflow.
/// Unknown coordinators must be approved before their messages are processed.
/// </summary>
public interface ICoordinatorRegistrationService
{
    /// <summary>
    /// Check if a coordinator is registered (exists in the database).
    /// Uses in-memory cache for fast lookups.
    /// </summary>
    Task<bool> IsRegisteredAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Called when any MQTT message is received from an unregistered coordinator.
    /// Extracts metadata and broadcasts a registration request to the frontend.
    /// Only broadcasts once per coordinator (deduplicates).
    /// </summary>
    Task ProcessUnknownCoordinatorAsync(string coordId, string topic, byte[] payload, CancellationToken ct = default);

    /// <summary>
    /// Handle an explicit coordinator announce message.
    /// Topic: coordinator/{mac}/announce
    /// </summary>
    Task HandleCoordinatorAnnounceAsync(string coordId, CoordinatorAnnounceDto announce, CancellationToken ct = default);

    /// <summary>
    /// Approve a pending coordinator registration.
    /// Creates the coordinator in the database and notifies the coordinator via MQTT.
    /// </summary>
    Task<Coordinator> ApproveRegistrationAsync(ApproveCoordinatorRegistrationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reject a pending coordinator registration.
    /// Adds to rejected list and optionally notifies the coordinator.
    /// </summary>
    Task RejectRegistrationAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Remove/forget a registered coordinator.
    /// Deletes from database and clears cache.
    /// </summary>
    Task<bool> RemoveCoordinatorAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get all pending registration requests.
    /// </summary>
    Task<IReadOnlyList<PendingCoordinatorRegistration>> GetPendingRegistrationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Refresh the in-memory cache of registered coordinators from the database.
    /// Called on startup.
    /// </summary>
    Task RefreshCacheAsync(CancellationToken ct = default);
}

/// <summary>
/// DTO for coordinator announce message from firmware.
/// </summary>
public class CoordinatorAnnounceDto
{
    public string Mac { get; set; } = string.Empty;
    public string? FwVersion { get; set; }
    public string? ChipModel { get; set; }
    public int FreeHeap { get; set; }
    public int WifiRssi { get; set; }
    public string? Ip { get; set; }
}

/// <summary>
/// Request to approve a coordinator registration.
/// </summary>
public class ApproveCoordinatorRegistrationRequest
{
    public string CoordId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public List<string>? Tags { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Represents a pending coordinator registration.
/// </summary>
public class PendingCoordinatorRegistration
{
    public string CoordId { get; set; } = string.Empty;
    public string? FwVersion { get; set; }
    public string? ChipModel { get; set; }
    public int WifiRssi { get; set; }
    public string? Ip { get; set; }
    public int FreeHeap { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int MessageCount { get; set; }
}
