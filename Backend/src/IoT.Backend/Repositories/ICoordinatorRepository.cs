using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Coordinator data access operations.
/// </summary>
public interface ICoordinatorRepository
{
    Task<Coordinator?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Coordinator?> GetBySiteAndIdAsync(string siteId, string coordId, CancellationToken ct = default);
    Task<Coordinator?> GetByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct = default);
    Task<IReadOnlyList<Coordinator>> GetByFarmAsync(string farmId, CancellationToken ct = default);
    Task UpsertAsync(Coordinator coordinator, CancellationToken ct = default);
    
    /// <summary>
    /// Counts coordinators that have been seen within the specified threshold.
    /// </summary>
    Task<long> CountOnlineAsync(TimeSpan threshold, CancellationToken ct = default);
}
