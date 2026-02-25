using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Settings data access operations.
/// </summary>
public interface ISettingsRepository
{
    Task<Settings?> GetAsync(string siteId, CancellationToken ct = default);
    Task SaveAsync(Settings settings, CancellationToken ct = default);
}
