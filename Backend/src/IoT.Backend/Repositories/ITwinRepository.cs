using IoT.Backend.Models.DigitalTwin;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Digital Twin operations.
/// Provides specialized methods for twin state management with optimistic concurrency.
/// </summary>
public interface ITwinRepository
{
    // ============================================================================
    // Tower Twin Operations
    // ============================================================================

    /// <summary>
    /// Get a tower twin by its unique tower ID
    /// </summary>
    Task<TowerTwin?> GetTowerTwinByIdAsync(string towerId, CancellationToken ct = default);

    /// <summary>
    /// Get a tower twin by farm, coordinator, and tower ID
    /// </summary>
    Task<TowerTwin?> GetTowerTwinByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);

    /// <summary>
    /// Get all tower twins for a specific coordinator
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetTowerTwinsByCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get all tower twins for a farm
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetTowerTwinsByFarmAsync(string farmId, CancellationToken ct = default);

    /// <summary>
    /// Get all tower twins with a specific sync status
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetTowerTwinsBySyncStatusAsync(SyncStatus status, CancellationToken ct = default);

    /// <summary>
    /// Update only the reported state of a tower twin (from device telemetry)
    /// Automatically updates metadata.LastReportedAt and increments version
    /// </summary>
    Task<bool> UpdateTowerReportedStateAsync(string towerId, TowerReportedState reported, CancellationToken ct = default);

    /// <summary>
    /// Update only the desired state of a tower twin (from user/backend command)
    /// Automatically updates metadata.LastDesiredAt, sets SyncStatus to Pending, and increments version
    /// </summary>
    Task<bool> UpdateTowerDesiredStateAsync(string towerId, TowerDesiredState desired, CancellationToken ct = default);

    /// <summary>
    /// Update the sync status of a tower twin
    /// </summary>
    Task<bool> UpdateTowerSyncStatusAsync(string towerId, SyncStatus status, CancellationToken ct = default);

    /// <summary>
    /// Create or replace a tower twin document
    /// </summary>
    Task UpsertTowerTwinAsync(TowerTwin twin, CancellationToken ct = default);

    /// <summary>
    /// Delete a tower twin
    /// </summary>
    Task DeleteTowerTwinAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);

    // ============================================================================
    // Coordinator Twin Operations
    // ============================================================================

    /// <summary>
    /// Get a coordinator twin by its unique coordinator ID
    /// </summary>
    Task<CoordinatorTwin?> GetCoordinatorTwinByIdAsync(string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get a coordinator twin by farm and coordinator ID
    /// </summary>
    Task<CoordinatorTwin?> GetCoordinatorTwinByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct = default);

    /// <summary>
    /// Get all coordinator twins for a farm
    /// </summary>
    Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsByFarmAsync(string farmId, CancellationToken ct = default);

    /// <summary>
    /// Get all coordinator twins with a specific sync status
    /// </summary>
    Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsBySyncStatusAsync(SyncStatus status, CancellationToken ct = default);

    /// <summary>
    /// Update only the reported state of a coordinator twin (from device telemetry)
    /// Automatically updates metadata.LastReportedAt and increments version
    /// </summary>
    Task<bool> UpdateCoordinatorReportedStateAsync(string coordId, CoordinatorReportedState reported, CancellationToken ct = default);

    /// <summary>
    /// Update only the desired state of a coordinator twin (from user/backend command)
    /// Automatically updates metadata.LastDesiredAt, sets SyncStatus to Pending, and increments version
    /// </summary>
    Task<bool> UpdateCoordinatorDesiredStateAsync(string coordId, CoordinatorDesiredState desired, CancellationToken ct = default);

    /// <summary>
    /// Update the sync status of a coordinator twin
    /// </summary>
    Task<bool> UpdateCoordinatorSyncStatusAsync(string coordId, SyncStatus status, CancellationToken ct = default);

    /// <summary>
    /// Create or replace a coordinator twin document
    /// </summary>
    Task UpsertCoordinatorTwinAsync(CoordinatorTwin twin, CancellationToken ct = default);

    /// <summary>
    /// Delete a coordinator twin
    /// </summary>
    Task DeleteCoordinatorTwinAsync(string farmId, string coordId, CancellationToken ct = default);

    // ============================================================================
    // Bulk Operations
    // ============================================================================

    /// <summary>
    /// Get all tower twins (across all farms)
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetAllTowerTwinsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all tower twins with an active crop (CropType != Unknown and PlantingDate is set)
    /// </summary>
    Task<IReadOnlyList<TowerTwin>> GetTowerTwinsWithActiveCropAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all twins (tower and coordinator) that need sync (status = Pending)
    /// Used by the sync service to find twins needing command dispatch
    /// </summary>
    Task<(IReadOnlyList<TowerTwin> Towers, IReadOnlyList<CoordinatorTwin> Coordinators)> GetPendingSyncTwinsAsync(CancellationToken ct = default);

    /// <summary>
    /// Update only the ML predictions section of a tower twin
    /// </summary>
    Task<bool> UpdateTowerMlPredictionsAsync(string towerId, MlPredictions predictions, CancellationToken ct = default);

    /// <summary>
    /// Mark twins as stale if they haven't reported within the threshold
    /// </summary>
    Task<int> MarkStaleTwinsAsync(TimeSpan staleThreshold, CancellationToken ct = default);
}
