using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for OTA Job data access operations.
/// </summary>
public interface IOtaJobRepository
{
    Task<OtaJob?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<OtaJob>> GetAllAsync(string? farmId = null, int limit = 50, CancellationToken ct = default);
    Task CreateAsync(OtaJob job, CancellationToken ct = default);
    Task UpdateAsync(OtaJob job, CancellationToken ct = default);
    Task UpdateStatusAsync(string id, string status, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent active (non-terminal) OTA job for a given coordinator.
    /// Returns null if no active job exists.
    /// </summary>
    Task<OtaJob?> GetActiveByCoordIdAsync(string coordId, CancellationToken ct = default);
}
