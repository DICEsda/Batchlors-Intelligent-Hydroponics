using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for firmware version management.
/// Used for tracking available firmware releases for OTA updates.
/// </summary>
public interface IFirmwareRepository
{
    /// <summary>
    /// Gets a firmware version by its unique ID.
    /// </summary>
    Task<FirmwareVersion?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets a firmware version by version string and device type.
    /// </summary>
    Task<FirmwareVersion?> GetByVersionAndTypeAsync(string version, string deviceType, CancellationToken ct = default);

    /// <summary>
    /// Gets all firmware versions with optional filtering.
    /// </summary>
    /// <param name="deviceType">Filter by device type (coordinator/tower)</param>
    /// <param name="stableOnly">If true, only return stable releases</param>
    /// <param name="limit">Maximum number of results</param>
    Task<IReadOnlyList<FirmwareVersion>> GetAllAsync(
        string? deviceType = null, 
        bool? stableOnly = null, 
        int limit = 50, 
        CancellationToken ct = default);

    /// <summary>
    /// Gets the latest firmware version for a device type.
    /// </summary>
    /// <param name="deviceType">Device type (coordinator/tower)</param>
    /// <param name="stableOnly">If true, only consider stable releases</param>
    Task<FirmwareVersion?> GetLatestAsync(string deviceType, bool stableOnly = true, CancellationToken ct = default);

    /// <summary>
    /// Creates a new firmware version record.
    /// </summary>
    Task CreateAsync(FirmwareVersion firmware, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing firmware version record.
    /// </summary>
    Task UpdateAsync(FirmwareVersion firmware, CancellationToken ct = default);

    /// <summary>
    /// Deletes a firmware version by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
