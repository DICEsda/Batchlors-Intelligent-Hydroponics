using IoT.Backend.Models;

namespace IoT.Backend.Repositories;

/// <summary>
/// Repository interface for Alert entities.
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Get all alerts.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get an alert by its MongoDB _id.
    /// </summary>
    Task<Alert?> GetByIdAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Insert a new alert.
    /// </summary>
    Task<Alert> InsertAsync(Alert alert, CancellationToken ct = default);
    
    /// <summary>
    /// Upsert an alert (insert or update).
    /// </summary>
    Task<Alert> UpsertAsync(Alert alert, CancellationToken ct = default);
    
    /// <summary>
    /// Delete an alert by its MongoDB _id.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
    
    /// <summary>
    /// Get all active alerts (status = "active").
    /// </summary>
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get alerts for a specific farm.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetByFarmAsync(string farmId, CancellationToken ct = default);
    
    /// <summary>
    /// Get alerts with filtering and pagination.
    /// </summary>
    Task<(IReadOnlyList<Alert> Alerts, int TotalCount)> GetFilteredAsync(
        int page, 
        int pageSize, 
        string? severity = null, 
        string? status = null, 
        string? farmId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Acknowledge an alert.
    /// </summary>
    Task AcknowledgeAsync(string alertId, string acknowledgedBy, CancellationToken ct = default);
    
    /// <summary>
    /// Resolve an alert.
    /// </summary>
    Task ResolveAsync(string alertId, CancellationToken ct = default);
    
    /// <summary>
    /// Check if an alert with the given key already exists and is active.
    /// </summary>
    Task<Alert?> GetActiveAlertByKeyAsync(string alertKey, CancellationToken ct = default);
    
    /// <summary>
    /// Get count of active alerts for a farm.
    /// </summary>
    Task<int> GetActiveAlertCountByFarmAsync(string farmId, CancellationToken ct = default);
}
