using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Tower (hydroponic system) data access operations.
/// </summary>
public interface ITowerRepository
{
    Task<Tower?> GetByIdAsync(string towerId, CancellationToken ct = default);
    Task<Tower?> GetByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);
    Task<IReadOnlyList<Tower>> GetByCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default);
    Task<IReadOnlyList<Tower>> GetByFarmAsync(string farmId, CancellationToken ct = default);
    Task UpsertAsync(Tower tower, CancellationToken ct = default);
    Task DeleteAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);
    Task UpdateNameAsync(string farmId, string coordId, string towerId, string name, CancellationToken ct = default);
}
