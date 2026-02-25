using IoT.Backend.Models.DigitalTwin;

namespace IoT.Backend.Services;

/// <summary>
/// Service interface for Digital Twin state management and synchronization
/// </summary>
public interface ITwinService
{
    // ============================================================================
    // Tower Twin Operations
    // ============================================================================

    /// <summary>
    /// Process incoming telemetry and update the tower's reported state.
    /// If the twin does not yet exist, it is auto-created from the first telemetry message.
    /// </summary>
    Task ProcessTowerTelemetryAsync(string towerId, string coordId, string farmId, TowerReportedState reported, CancellationToken ct = default);

    /// <summary>
    /// Set the desired state for a tower and trigger sync
    /// </summary>
    Task SetTowerDesiredStateAsync(string towerId, TowerDesiredState desired, CancellationToken ct = default);

    /// <summary>
    /// Get the current state delta between desired and reported for a tower
    /// Returns null if states are in sync
    /// </summary>
    Task<TowerDesiredState?> GetTowerStateDeltaAsync(string towerId, CancellationToken ct = default);

    /// <summary>
    /// Get a tower twin by ID
    /// </summary>
    Task<TowerTwin?> GetTowerTwinAsync(string towerId, CancellationToken ct = default);

    /// <summary>
    /// Get all tower twins for a coordinator
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetTowerTwinsForCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default);

    // ============================================================================
    // Coordinator Twin Operations
    // ============================================================================

    /// <summary>
    /// Process incoming telemetry and update the coordinator's reported state.
    /// If the twin does not yet exist, it is auto-created from the first telemetry message.
    /// </summary>
    Task ProcessCoordinatorTelemetryAsync(string coordId, string farmId, CoordinatorReportedState reported, CancellationToken ct = default);

    /// <summary>
    /// Set the desired state for a coordinator and trigger sync
    /// </summary>
    Task SetCoordinatorDesiredStateAsync(string coordId, CoordinatorDesiredState desired, CancellationToken ct = default);

    /// <summary>
    /// Get the current state delta between desired and reported for a coordinator
    /// Returns null if states are in sync
    /// </summary>
    Task<CoordinatorDesiredState?> GetCoordinatorStateDeltaAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get a coordinator twin by ID
    /// </summary>
    Task<CoordinatorTwin?> GetCoordinatorTwinAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get all coordinator twins for a farm
    /// </summary>
    Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsForFarmAsync(string farmId, CancellationToken ct = default);

    // ============================================================================
    // Sync Operations
    // ============================================================================

    /// <summary>
    /// Process all pending sync operations - called periodically by background service
    /// </summary>
    Task ProcessPendingSyncsAsync(CancellationToken ct = default);

    /// <summary>
    /// Mark a tower sync as successful (called when device acknowledges command)
    /// </summary>
    Task MarkTowerSyncSuccessAsync(string towerId, CancellationToken ct = default);

    /// <summary>
    /// Mark a coordinator sync as successful (called when device acknowledges command)
    /// </summary>
    Task MarkCoordinatorSyncSuccessAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Check for stale twins and update their status
    /// </summary>
    Task CheckAndMarkStaleTwinsAsync(TimeSpan staleThreshold, CancellationToken ct = default);
}
