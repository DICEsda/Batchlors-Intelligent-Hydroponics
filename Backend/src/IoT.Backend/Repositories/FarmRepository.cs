using IoT.Backend.Models;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// MongoDB implementation of Farm repository.
/// </summary>
public class FarmRepository : MongoRepository<Farm>, IFarmRepository
{
    public FarmRepository(IMongoDatabase database, ILogger<FarmRepository> logger)
        : base(database, "farms", logger)
    {
        // Create index on farm_id for fast lookups
        var indexKeys = Builders<Farm>.IndexKeys.Ascending(f => f.FarmId);
        var indexModel = new CreateIndexModel<Farm>(indexKeys, new CreateIndexOptions { Unique = true });
        Collection.Indexes.CreateOneAsync(indexModel).GetAwaiter().GetResult();
    }
    
    public async Task<Farm?> GetByFarmIdAsync(string farmId, CancellationToken ct = default)
    {
        var filter = Builders<Farm>.Filter.Eq(f => f.FarmId, farmId);
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }
    
    public async Task UpdateStatisticsAsync(string farmId, int coordinatorCount, int towerCount, int activeAlertCount, CancellationToken ct = default)
    {
        var filter = Builders<Farm>.Filter.Eq(f => f.FarmId, farmId);
        var update = Builders<Farm>.Update
            .Set(f => f.CoordinatorCount, coordinatorCount)
            .Set(f => f.TowerCount, towerCount)
            .Set(f => f.ActiveAlertCount, activeAlertCount);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
    
    public async Task UpdateLastSeenAsync(string farmId, DateTime lastSeen, CancellationToken ct = default)
    {
        var filter = Builders<Farm>.Filter.Eq(f => f.FarmId, farmId);
        var update = Builders<Farm>.Update.Set(f => f.LastSeen, lastSeen);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
    
    public async Task IncrementAlertCountAsync(string farmId, CancellationToken ct = default)
    {
        var filter = Builders<Farm>.Filter.Eq(f => f.FarmId, farmId);
        var update = Builders<Farm>.Update.Inc(f => f.ActiveAlertCount, 1);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
    
    public async Task DecrementAlertCountAsync(string farmId, CancellationToken ct = default)
    {
        // Only decrement if count is greater than 0
        var filter = Builders<Farm>.Filter.And(
            Builders<Farm>.Filter.Eq(f => f.FarmId, farmId),
            Builders<Farm>.Filter.Gt(f => f.ActiveAlertCount, 0)
        );
        var update = Builders<Farm>.Update.Inc(f => f.ActiveAlertCount, -1);
        
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
