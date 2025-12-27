using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for IoT data access operations.
/// Matches the Go backend contract exactly.
/// </summary>
public interface IRepository
{
    // Coordinator operations
    Task<Coordinator?> GetCoordinatorByIdAsync(string id, CancellationToken ct = default);
    Task<Coordinator?> GetCoordinatorBySiteAndIdAsync(string siteId, string coordId, CancellationToken ct = default);
    Task UpsertCoordinatorAsync(Coordinator coordinator, CancellationToken ct = default);

    // Node operations
    Task<Node?> GetNodeByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Node>> GetNodesByCoordinatorAsync(string siteId, string coordId, CancellationToken ct = default);
    Task UpsertNodeAsync(Node node, CancellationToken ct = default);
    Task DeleteNodeAsync(string siteId, string coordId, string nodeId, CancellationToken ct = default);
    Task UpdateNodeZoneAsync(string siteId, string coordId, string nodeId, string zoneId, CancellationToken ct = default);
    Task UpdateNodeNameAsync(string siteId, string coordId, string nodeId, string name, CancellationToken ct = default);

    // mmWave operations
    Task InsertMmwaveFrameAsync(MmwaveFrame frame, CancellationToken ct = default);
    Task<IReadOnlyList<MmwaveFrame>> GetMmwaveFramesAsync(string siteId, string coordinatorId, int limit, CancellationToken ct = default);

    // OTA operations
    Task<OtaJob?> GetOtaJobByIdAsync(string id, CancellationToken ct = default);
    Task CreateOtaJobAsync(OtaJob job, CancellationToken ct = default);
    Task UpdateOtaJobStatusAsync(string id, string status, CancellationToken ct = default);

    // Site operations
    Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken ct = default);
    Task<Site?> GetSiteByIdAsync(string id, CancellationToken ct = default);
    Task CreateSiteAsync(Site site, CancellationToken ct = default);
    Task UpsertSiteAsync(Site site, CancellationToken ct = default);

    // Settings operations
    Task<Settings?> GetSettingsAsync(string siteId, CancellationToken ct = default);
    Task SaveSettingsAsync(Settings settings, CancellationToken ct = default);

    // Zone operations
    Task CreateZoneAsync(Zone zone, CancellationToken ct = default);
    Task<Zone?> GetZoneByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetZonesBySiteAsync(string siteId, CancellationToken ct = default);
    Task<Zone?> GetZoneByCoordinatorAsync(string siteId, string coordinatorId, CancellationToken ct = default);
    Task DeleteZoneAsync(string zoneId, CancellationToken ct = default);
    Task UpdateZoneAsync(Zone zone, CancellationToken ct = default);
}
