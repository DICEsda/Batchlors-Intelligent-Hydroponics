using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for IoT data access operations.
/// Supports both legacy smart tile system and new hydroponic system.
/// </summary>
public interface IRepository
{
    /// <summary>
    /// Checks if the database connection is healthy.
    /// </summary>
    Task<bool> CheckConnectionAsync(CancellationToken ct = default);

    // Coordinator operations (also serves as reservoir in hydroponic system)
    Task<Coordinator?> GetCoordinatorByIdAsync(string id, CancellationToken ct = default);
    Task<Coordinator?> GetCoordinatorBySiteAndIdAsync(string siteId, string coordId, CancellationToken ct = default);
    Task<Coordinator?> GetCoordinatorByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct = default);
    Task<IReadOnlyList<Coordinator>> GetCoordinatorsByFarmAsync(string farmId, CancellationToken ct = default);
    Task UpsertCoordinatorAsync(Coordinator coordinator, CancellationToken ct = default);

    // Tower operations (hydroponic system)
    Task<Tower?> GetTowerByIdAsync(string towerId, CancellationToken ct = default);
    Task<Tower?> GetTowerByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);
    Task<IReadOnlyList<Tower>> GetTowersByCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default);
    Task<IReadOnlyList<Tower>> GetTowersByFarmAsync(string farmId, CancellationToken ct = default);
    Task UpsertTowerAsync(Tower tower, CancellationToken ct = default);
    Task DeleteTowerAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);
    Task UpdateTowerNameAsync(string farmId, string coordId, string towerId, string name, CancellationToken ct = default);

    // OTA operations
    Task<OtaJob?> GetOtaJobByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<OtaJob>> GetOtaJobsAsync(string? farmId = null, int limit = 50, CancellationToken ct = default);
    Task CreateOtaJobAsync(OtaJob job, CancellationToken ct = default);
    Task UpdateOtaJobAsync(OtaJob job, CancellationToken ct = default);
    Task UpdateOtaJobStatusAsync(string id, string status, CancellationToken ct = default);

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

    // Reservoir telemetry operations (hydroponic system)
    Task InsertReservoirTelemetryAsync(ReservoirTelemetry telemetry, CancellationToken ct = default);
    Task<IReadOnlyList<ReservoirTelemetry>> GetReservoirTelemetryAsync(string farmId, string coordId, DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default);
    Task<ReservoirTelemetry?> GetLatestReservoirTelemetryAsync(string farmId, string coordId, CancellationToken ct = default);

    // Tower telemetry operations (hydroponic system)
    Task InsertTowerTelemetryAsync(TowerTelemetry telemetry, CancellationToken ct = default);
    Task<IReadOnlyList<TowerTelemetry>> GetTowerTelemetryAsync(string farmId, string coordId, string towerId, DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default);
    Task<TowerTelemetry?> GetLatestTowerTelemetryAsync(string farmId, string coordId, string towerId, CancellationToken ct = default);

    // Height measurement operations (hydroponic system)
    Task InsertHeightMeasurementAsync(HeightMeasurement measurement, CancellationToken ct = default);
    Task<IReadOnlyList<HeightMeasurement>> GetHeightMeasurementsAsync(string farmId, string towerId, int? slotIndex = null, DateTime? from = null, DateTime? to = null, int limit = 500, CancellationToken ct = default);
    Task<IReadOnlyList<HeightMeasurement>> GetLatestHeightMeasurementsByTowerAsync(string farmId, string towerId, CancellationToken ct = default);
}
