using IoT.Backend.Models;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// MongoDB implementation of Alert repository.
/// </summary>
public class AlertRepository : MongoRepository<Alert>, IAlertRepository
{
    public AlertRepository(IMongoDatabase database, ILogger<AlertRepository> logger)
        : base(database, "alerts", logger)
    {
        // Create indexes for common queries
        var farmIdIndex = Builders<Alert>.IndexKeys.Ascending(a => a.FarmId);
        var statusIndex = Builders<Alert>.IndexKeys.Ascending(a => a.Status);
        var alertKeyIndex = Builders<Alert>.IndexKeys.Ascending(a => a.AlertKey);
        var createdAtIndex = Builders<Alert>.IndexKeys.Descending(a => a.CreatedAt);
        
        Collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Alert>(farmIdIndex),
            new CreateIndexModel<Alert>(statusIndex),
            new CreateIndexModel<Alert>(alertKeyIndex),
            new CreateIndexModel<Alert>(createdAtIndex)
        }).GetAwaiter().GetResult();
    }
    
    public async Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.Status, "active");
        return await Collection.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }
    
    public async Task<IReadOnlyList<Alert>> GetByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.FarmId, farmId);
        return await Collection.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }
    
    public async Task<(IReadOnlyList<Alert> Alerts, int TotalCount)> GetFilteredAsync(
        int page, 
        int pageSize, 
        string? severity = null, 
        string? status = null, 
        string? farmId = null,
        CancellationToken ct = default)
    {
        var filterBuilder = Builders<Alert>.Filter;
        var filters = new List<FilterDefinition<Alert>>();
        
        if (!string.IsNullOrEmpty(severity))
            filters.Add(filterBuilder.Eq(a => a.Severity, severity));
        
        if (!string.IsNullOrEmpty(status))
            filters.Add(filterBuilder.Eq(a => a.Status, status));
        
        if (!string.IsNullOrEmpty(farmId))
            filters.Add(filterBuilder.Eq(a => a.FarmId, farmId));
        
        var filter = filters.Count > 0 
            ? filterBuilder.And(filters) 
            : filterBuilder.Empty;
        
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        
        var alerts = await Collection.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);
        
        return (alerts, (int)totalCount);
    }
    
    public async Task AcknowledgeAsync(string alertId, string acknowledgedBy, CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.Id, alertId);
        var update = Builders<Alert>.Update
            .Set(a => a.Status, "acknowledged")
            .Set(a => a.AcknowledgedAt, DateTime.UtcNow)
            .Set(a => a.AcknowledgedBy, acknowledgedBy);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
    
    public async Task ResolveAsync(string alertId, CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.Id, alertId);
        var update = Builders<Alert>.Update
            .Set(a => a.Status, "resolved")
            .Set(a => a.ResolvedAt, DateTime.UtcNow);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
    
    public async Task<Alert?> GetActiveAlertByKeyAsync(string alertKey, CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.And(
            Builders<Alert>.Filter.Eq(a => a.AlertKey, alertKey),
            Builders<Alert>.Filter.Eq(a => a.Status, "active")
        );
        
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }
    
    public async Task<int> GetActiveAlertCountByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var filter = Builders<Alert>.Filter.And(
            Builders<Alert>.Filter.Eq(a => a.FarmId, farmId),
            Builders<Alert>.Filter.Eq(a => a.Status, "active")
        );
        
        return (int)await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }
}
