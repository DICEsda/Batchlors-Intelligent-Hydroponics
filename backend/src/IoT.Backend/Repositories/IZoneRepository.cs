using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Zone data access operations.
/// </summary>
public interface IZoneRepository
{
    Task CreateAsync(Zone zone, CancellationToken ct = default);
    Task<Zone?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetBySiteAsync(string siteId, CancellationToken ct = default);
    Task<Zone?> GetByCoordinatorAsync(string siteId, string coordinatorId, CancellationToken ct = default);
    Task DeleteAsync(string zoneId, CancellationToken ct = default);
    Task UpdateAsync(Zone zone, CancellationToken ct = default);
}
