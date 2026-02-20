using IoT.Backend.Models;

namespace IoT.Backend.Services;

/// <summary>
/// Service for managing alerts (auto-generation, resolution, and broadcasting).
/// Monitors telemetry data and creates alerts for anomalies like low battery,
/// temperature issues, connectivity problems, and sensor out-of-range values.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Checks coordinator telemetry data and generates alerts if thresholds are exceeded.
    /// Handles: temperature, connectivity timeout, water level, pH, EC, pump failure.
    /// </summary>
    Task CheckCoordinatorAlertsAsync(Coordinator coordinator, CancellationToken ct = default);

    /// <summary>
    /// Checks tower telemetry data and generates alerts if thresholds are exceeded.
    /// Handles: low battery, temperature, connectivity timeout.
    /// </summary>
    Task CheckTowerAlertsAsync(Tower tower, CancellationToken ct = default);

    /// <summary>
    /// Creates a new alert and broadcasts it via WebSocket.
    /// Prevents duplicate alerts by checking alert_key.
    /// </summary>
    Task CreateAlertAsync(
        string farmId,
        string? coordId,
        string? towerId,
        string severity,
        string category,
        string message,
        CancellationToken ct = default);

    /// <summary>
    /// Auto-resolves an alert if it exists and is active.
    /// Used when conditions return to normal (e.g., battery recharged, temperature normalized).
    /// </summary>
    Task AutoResolveAlertAsync(string alertKey, CancellationToken ct = default);
}
