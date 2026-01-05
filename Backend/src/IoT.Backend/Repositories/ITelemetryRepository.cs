using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for time-series telemetry data access operations.
/// Handles reservoir telemetry, tower telemetry, and height measurements.
/// </summary>
public interface ITelemetryRepository
{
    #region Reservoir Telemetry

    /// <summary>
    /// Insert a new reservoir telemetry record.
    /// </summary>
    Task InsertReservoirTelemetryAsync(ReservoirTelemetry telemetry, CancellationToken ct = default);

    /// <summary>
    /// Get reservoir telemetry for a coordinator within a time range.
    /// </summary>
    Task<IReadOnlyList<ReservoirTelemetry>> GetReservoirTelemetryAsync(
        string farmId,
        string coordId,
        DateTime from,
        DateTime to,
        int limit = 1000,
        CancellationToken ct = default);

    /// <summary>
    /// Get the latest reservoir telemetry for a coordinator.
    /// </summary>
    Task<ReservoirTelemetry?> GetLatestReservoirTelemetryAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default);

    #endregion

    #region Tower Telemetry

    /// <summary>
    /// Insert a new tower telemetry record.
    /// </summary>
    Task InsertTowerTelemetryAsync(TowerTelemetry telemetry, CancellationToken ct = default);

    /// <summary>
    /// Get tower telemetry for a specific tower within a time range.
    /// </summary>
    Task<IReadOnlyList<TowerTelemetry>> GetTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        DateTime from,
        DateTime to,
        int limit = 1000,
        CancellationToken ct = default);

    /// <summary>
    /// Get the latest tower telemetry for a specific tower.
    /// </summary>
    Task<TowerTelemetry?> GetLatestTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default);

    /// <summary>
    /// Get the latest tower telemetry for all towers under a coordinator.
    /// </summary>
    Task<IReadOnlyList<TowerTelemetry>> GetLatestTowerTelemetryByCoordinatorAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default);

    #endregion

    #region Height Measurements

    /// <summary>
    /// Insert a new height measurement record.
    /// </summary>
    Task InsertHeightMeasurementAsync(HeightMeasurement measurement, CancellationToken ct = default);

    /// <summary>
    /// Get height measurements for a specific tower slot within a time range.
    /// </summary>
    Task<IReadOnlyList<HeightMeasurement>> GetHeightMeasurementsAsync(
        string farmId,
        string towerId,
        int? slotIndex = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 500,
        CancellationToken ct = default);

    /// <summary>
    /// Get the latest height measurement for each slot in a tower.
    /// </summary>
    Task<IReadOnlyList<HeightMeasurement>> GetLatestHeightMeasurementsByTowerAsync(
        string farmId,
        string towerId,
        CancellationToken ct = default);

    /// <summary>
    /// Delete height measurements for a specific tower and slot (used when replanting).
    /// </summary>
    Task DeleteHeightMeasurementsAsync(
        string farmId,
        string towerId,
        int slotIndex,
        CancellationToken ct = default);

    #endregion

    #region Aggregations

    /// <summary>
    /// Get daily average reservoir telemetry for charting.
    /// </summary>
    Task<IReadOnlyList<ReservoirTelemetry>> GetDailyAverageReservoirTelemetryAsync(
        string farmId,
        string coordId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Get daily average tower telemetry for charting.
    /// </summary>
    Task<IReadOnlyList<TowerTelemetry>> GetDailyAverageTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    #endregion
}
