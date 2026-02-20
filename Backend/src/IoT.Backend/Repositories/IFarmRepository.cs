using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Farm entities.
/// </summary>
public interface IFarmRepository
{
    /// <summary>
    /// Get all farms.
    /// </summary>
    Task<IReadOnlyList<Farm>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get a farm by its MongoDB _id.
    /// </summary>
    Task<Farm?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Insert a new farm.
    /// </summary>
    Task<Farm> InsertAsync(Farm farm, CancellationToken ct = default);
    
    /// <summary>
    /// Upsert a farm (insert or update).
    /// </summary>
    Task<Farm> UpsertAsync(Farm farm, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a farm by its MongoDB _id.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Get a farm by its farm_id (not MongoDB _id).
    /// </summary>
    Task<Farm?> GetByFarmIdAsync(string farmId, CancellationToken ct = default);
    
    /// <summary>
    /// Update farm statistics (coordinator count, tower count, alert count).
    /// </summary>
    Task UpdateStatisticsAsync(string farmId, int coordinatorCount, int towerCount, int activeAlertCount, CancellationToken ct = default);
    
    /// <summary>
    /// Update last seen timestamp for a farm.
    /// </summary>
    Task UpdateLastSeenAsync(string farmId, DateTime lastSeen, CancellationToken ct = default);
    
    /// <summary>
    /// Increment the active alert count for a farm.
    /// </summary>
    Task IncrementAlertCountAsync(string farmId, CancellationToken ct = default);
    
    /// <summary>
    /// Decrement the active alert count for a farm.
    /// </summary>
    Task DecrementAlertCountAsync(string farmId, CancellationToken ct = default);
}
