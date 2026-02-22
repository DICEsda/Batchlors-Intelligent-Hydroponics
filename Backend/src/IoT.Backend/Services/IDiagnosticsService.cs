using IoT.Backend.Models.Diagnostics;

namespace IoT.Backend.Services;

/// <summary>
/// Collects and exposes backend performance metrics (throughput, latency, errors).
/// Thread-safe – designed to be called from hot MQTT handler paths.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Record timing for a fully-processed tower telemetry message.
    /// </summary>
    void RecordTowerMessage(TimeSpan total, TimeSpan mongoWrite, TimeSpan twinUpsert, TimeSpan wsBroadcast);

    /// <summary>
    /// Record timing for a fully-processed reservoir telemetry message.
    /// </summary>
    void RecordReservoirMessage(TimeSpan total, TimeSpan mongoWrite, TimeSpan twinUpsert, TimeSpan wsBroadcast);

    /// <summary>
    /// Increment an error counter.
    /// </summary>
    /// <param name="component">One of: "mongo_write", "twin_upsert", "ws_broadcast", "processing".</param>
    void RecordError(string component);

    /// <summary>
    /// Returns the most recent metrics snapshot, enriched with the current WebSocket client count.
    /// </summary>
    SystemMetricsSnapshot GetCurrentSnapshot(int wsClientCount);

    /// <summary>
    /// Returns historical snapshots for the last <paramref name="minutes"/> minutes.
    /// </summary>
    IReadOnlyList<SystemMetricsSnapshot> GetHistory(int minutes = 30);

    /// <summary>
    /// Resets all counters and history (useful between test runs).
    /// </summary>
    void Reset();
}
