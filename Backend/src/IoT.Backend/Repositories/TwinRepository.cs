using IoT.Backend.Models.DigitalTwin;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// MongoDB implementation of Digital Twin repository.
/// Uses partial document updates for efficient state synchronization.
/// </summary>
public sealed class TwinRepository : ITwinRepository
{
    private readonly IMongoDatabase _db;
    private readonly ILogger<TwinRepository> _logger;

    // Collection names for digital twins
    private const string TowerTwinsCollection = "tower_twins";
    private const string CoordinatorTwinsCollection = "coordinator_twins";

    public TwinRepository(IMongoDatabase database, ILogger<TwinRepository> logger)
    {
        _db = database;
        _logger = logger;

        // Ensure indexes for efficient queries
        EnsureIndexesAsync().ConfigureAwait(false);
    }

    private async Task EnsureIndexesAsync()
    {
        try
        {
            // Tower twin indexes
            var towerCollection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
            var towerIndexes = new[]
            {
                new CreateIndexModel<TowerTwin>(
                    Builders<TowerTwin>.IndexKeys.Ascending(t => t.FarmId).Ascending(t => t.CoordId),
                    new CreateIndexOptions { Name = "farm_coord_idx" }),
                new CreateIndexModel<TowerTwin>(
                    Builders<TowerTwin>.IndexKeys.Ascending(t => t.FarmId),
                    new CreateIndexOptions { Name = "farm_idx" }),
                new CreateIndexModel<TowerTwin>(
                    Builders<TowerTwin>.IndexKeys.Ascending("metadata.sync_status"),
                    new CreateIndexOptions { Name = "sync_status_idx" }),
                new CreateIndexModel<TowerTwin>(
                    Builders<TowerTwin>.IndexKeys.Ascending("metadata.last_reported_at"),
                    new CreateIndexOptions { Name = "last_reported_idx" })
            };
            await towerCollection.Indexes.CreateManyAsync(towerIndexes);

            // Coordinator twin indexes
            var coordCollection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
            var coordIndexes = new[]
            {
                new CreateIndexModel<CoordinatorTwin>(
                    Builders<CoordinatorTwin>.IndexKeys.Ascending(c => c.FarmId),
                    new CreateIndexOptions { Name = "farm_idx" }),
                new CreateIndexModel<CoordinatorTwin>(
                    Builders<CoordinatorTwin>.IndexKeys.Ascending("metadata.sync_status"),
                    new CreateIndexOptions { Name = "sync_status_idx" }),
                new CreateIndexModel<CoordinatorTwin>(
                    Builders<CoordinatorTwin>.IndexKeys.Ascending("metadata.last_reported_at"),
                    new CreateIndexOptions { Name = "last_reported_idx" })
            };
            await coordCollection.Indexes.CreateManyAsync(coordIndexes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create indexes for twin collections (may already exist)");
        }
    }

    // ============================================================================
    // Tower Twin Operations
    // ============================================================================

    public async Task<TowerTwin?> GetTowerTwinByIdAsync(string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TowerTwin?> GetTowerTwinByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.And(
            Builders<TowerTwin>.Filter.Eq(t => t.FarmId, farmId),
            Builders<TowerTwin>.Filter.Eq(t => t.CoordId, coordId),
            Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId)
        );
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TowerTwin>> GetTowerTwinsByCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.And(
            Builders<TowerTwin>.Filter.Eq(t => t.FarmId, farmId),
            Builders<TowerTwin>.Filter.Eq(t => t.CoordId, coordId)
        );
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TowerTwin>> GetTowerTwinsByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq(t => t.FarmId, farmId);
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TowerTwin>> GetTowerTwinsBySyncStatusAsync(SyncStatus status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq("metadata.sync_status", status.ToString().ToLowerInvariant());
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> UpdateTowerReportedStateAsync(string towerId, TowerReportedState reported, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId);

        // Partial update: only update reported state and metadata
        var update = Builders<TowerTwin>.Update
            .Set(t => t.Reported, reported)
            .Set("metadata.last_reported_at", DateTime.UtcNow)
            .Set("metadata.is_connected", true)
            .Inc("metadata.version", 1);

        try
        {
            var result = await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            
            if (result.MatchedCount == 0)
            {
                _logger.LogDebug("Tower twin {TowerId} not found for reported state update", towerId);
                return false;
            }
            
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tower {TowerId} reported state", towerId);
            throw;
        }
    }

    public async Task<bool> UpdateTowerDesiredStateAsync(string towerId, TowerDesiredState desired, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId);

        // Partial update: only update desired state, set sync status to pending
        var update = Builders<TowerTwin>.Update
            .Set(t => t.Desired, desired)
            .Set("metadata.last_desired_at", DateTime.UtcNow)
            .Set("metadata.sync_status", SyncStatus.Pending.ToString().ToLowerInvariant())
            .Set("metadata.sync_retry_count", 0)
            .Inc("metadata.version", 1);

        try
        {
            var result = await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            
            if (result.MatchedCount == 0)
            {
                _logger.LogWarning("Tower twin {TowerId} not found for desired state update", towerId);
                return false;
            }
            
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tower {TowerId} desired state", towerId);
            throw;
        }
    }

    public async Task<bool> UpdateTowerSyncStatusAsync(string towerId, SyncStatus status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId);

        var updateBuilder = Builders<TowerTwin>.Update
            .Set("metadata.sync_status", status.ToString().ToLowerInvariant())
            .Inc("metadata.version", 1);

        // Reset retry count on successful sync
        if (status == SyncStatus.InSync)
        {
            updateBuilder = updateBuilder.Set("metadata.sync_retry_count", 0);
        }
        // Increment retry count on pending
        else if (status == SyncStatus.Pending)
        {
            updateBuilder = updateBuilder.Inc("metadata.sync_retry_count", 1);
        }

        try
        {
            var result = await collection.UpdateOneAsync(filter, updateBuilder, cancellationToken: ct);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tower {TowerId} sync status", towerId);
            throw;
        }
    }

    public async Task UpsertTowerTwinAsync(TowerTwin twin, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        
        var filter = Builders<TowerTwin>.Filter.And(
            Builders<TowerTwin>.Filter.Eq(t => t.FarmId, twin.FarmId),
            Builders<TowerTwin>.Filter.Eq(t => t.CoordId, twin.CoordId),
            Builders<TowerTwin>.Filter.Eq(t => t.TowerId, twin.TowerId)
        );
        
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            await collection.ReplaceOneAsync(filter, twin, options, ct);
            _logger.LogDebug("Upserted tower twin {TowerId}", twin.TowerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert tower twin {TowerId}", twin.TowerId);
            throw;
        }
    }

    public async Task DeleteTowerTwinAsync(string farmId, string coordId, string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var filter = Builders<TowerTwin>.Filter.And(
            Builders<TowerTwin>.Filter.Eq(t => t.FarmId, farmId),
            Builders<TowerTwin>.Filter.Eq(t => t.CoordId, coordId),
            Builders<TowerTwin>.Filter.Eq(t => t.TowerId, towerId)
        );

        await collection.DeleteOneAsync(filter, ct);
        _logger.LogInformation("Deleted tower twin {TowerId}", towerId);
    }

    // ============================================================================
    // Coordinator Twin Operations
    // ============================================================================

    public async Task<CoordinatorTwin?> GetCoordinatorTwinByIdAsync(string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<CoordinatorTwin?> GetCoordinatorTwinByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.And(
            Builders<CoordinatorTwin>.Filter.Eq(c => c.FarmId, farmId),
            Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId)
        );
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq(c => c.FarmId, farmId);
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CoordinatorTwin>> GetCoordinatorTwinsBySyncStatusAsync(SyncStatus status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq("metadata.sync_status", status.ToString().ToLowerInvariant());
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> UpdateCoordinatorReportedStateAsync(string coordId, CoordinatorReportedState reported, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId);

        // Partial update: only update reported state and metadata
        var update = Builders<CoordinatorTwin>.Update
            .Set(c => c.Reported, reported)
            .Set("metadata.last_reported_at", DateTime.UtcNow)
            .Set("metadata.is_connected", true)
            .Inc("metadata.version", 1);

        try
        {
            var result = await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            
            if (result.MatchedCount == 0)
            {
                _logger.LogDebug("Coordinator twin {CoordId} not found for reported state update", coordId);
                return false;
            }
            
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update coordinator {CoordId} reported state", coordId);
            throw;
        }
    }

    public async Task<bool> UpdateCoordinatorDesiredStateAsync(string coordId, CoordinatorDesiredState desired, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId);

        // Partial update: only update desired state, set sync status to pending
        var update = Builders<CoordinatorTwin>.Update
            .Set(c => c.Desired, desired)
            .Set("metadata.last_desired_at", DateTime.UtcNow)
            .Set("metadata.sync_status", SyncStatus.Pending.ToString().ToLowerInvariant())
            .Set("metadata.sync_retry_count", 0)
            .Inc("metadata.version", 1);

        try
        {
            var result = await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            
            if (result.MatchedCount == 0)
            {
                _logger.LogWarning("Coordinator twin {CoordId} not found for desired state update", coordId);
                return false;
            }
            
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update coordinator {CoordId} desired state", coordId);
            throw;
        }
    }

    public async Task<bool> UpdateCoordinatorSyncStatusAsync(string coordId, SyncStatus status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId);

        var updateBuilder = Builders<CoordinatorTwin>.Update
            .Set("metadata.sync_status", status.ToString().ToLowerInvariant())
            .Inc("metadata.version", 1);

        // Reset retry count on successful sync
        if (status == SyncStatus.InSync)
        {
            updateBuilder = updateBuilder.Set("metadata.sync_retry_count", 0);
        }
        // Increment retry count on pending
        else if (status == SyncStatus.Pending)
        {
            updateBuilder = updateBuilder.Inc("metadata.sync_retry_count", 1);
        }

        try
        {
            var result = await collection.UpdateOneAsync(filter, updateBuilder, cancellationToken: ct);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update coordinator {CoordId} sync status", coordId);
            throw;
        }
    }

    public async Task UpsertCoordinatorTwinAsync(CoordinatorTwin twin, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        
        var filter = Builders<CoordinatorTwin>.Filter.And(
            Builders<CoordinatorTwin>.Filter.Eq(c => c.FarmId, twin.FarmId),
            Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, twin.CoordId)
        );
        
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            await collection.ReplaceOneAsync(filter, twin, options, ct);
            _logger.LogDebug("Upserted coordinator twin {CoordId}", twin.CoordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert coordinator twin {CoordId}", twin.CoordId);
            throw;
        }
    }

    public async Task DeleteCoordinatorTwinAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var filter = Builders<CoordinatorTwin>.Filter.And(
            Builders<CoordinatorTwin>.Filter.Eq(c => c.FarmId, farmId),
            Builders<CoordinatorTwin>.Filter.Eq(c => c.CoordId, coordId)
        );

        await collection.DeleteOneAsync(filter, ct);
        _logger.LogInformation("Deleted coordinator twin {CoordId}", coordId);
    }

    // ============================================================================
    // Bulk Operations
    // ============================================================================

    public async Task<(IReadOnlyList<TowerTwin> Towers, IReadOnlyList<CoordinatorTwin> Coordinators)> GetPendingSyncTwinsAsync(CancellationToken ct = default)
    {
        var towerCollection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var coordCollection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);

        var pendingFilter = SyncStatus.Pending.ToString().ToLowerInvariant();

        var towerFilter = Builders<TowerTwin>.Filter.Eq("metadata.sync_status", pendingFilter);
        var coordFilter = Builders<CoordinatorTwin>.Filter.Eq("metadata.sync_status", pendingFilter);

        var towers = await towerCollection.Find(towerFilter).ToListAsync(ct);
        var coordinators = await coordCollection.Find(coordFilter).ToListAsync(ct);

        _logger.LogDebug("Found {TowerCount} towers and {CoordCount} coordinators pending sync", 
            towers.Count, coordinators.Count);

        return (towers, coordinators);
    }

    public async Task<int> MarkStaleTwinsAsync(TimeSpan staleThreshold, CancellationToken ct = default)
    {
        var cutoffTime = DateTime.UtcNow - staleThreshold;
        var staleStatus = SyncStatus.Stale.ToString().ToLowerInvariant();
        var offlineStatus = SyncStatus.Offline.ToString().ToLowerInvariant();
        int totalMarked = 0;

        // Mark stale tower twins
        var towerCollection = _db.GetCollection<TowerTwin>(TowerTwinsCollection);
        var towerFilter = Builders<TowerTwin>.Filter.And(
            Builders<TowerTwin>.Filter.Lt("metadata.last_reported_at", cutoffTime),
            Builders<TowerTwin>.Filter.Ne("metadata.sync_status", staleStatus),
            Builders<TowerTwin>.Filter.Ne("metadata.sync_status", offlineStatus)
        );
        var towerUpdate = Builders<TowerTwin>.Update
            .Set("metadata.sync_status", staleStatus)
            .Set("metadata.is_connected", false);

        var towerResult = await towerCollection.UpdateManyAsync(towerFilter, towerUpdate, cancellationToken: ct);
        totalMarked += (int)towerResult.ModifiedCount;

        // Mark stale coordinator twins
        var coordCollection = _db.GetCollection<CoordinatorTwin>(CoordinatorTwinsCollection);
        var coordFilter = Builders<CoordinatorTwin>.Filter.And(
            Builders<CoordinatorTwin>.Filter.Lt("metadata.last_reported_at", cutoffTime),
            Builders<CoordinatorTwin>.Filter.Ne("metadata.sync_status", staleStatus),
            Builders<CoordinatorTwin>.Filter.Ne("metadata.sync_status", offlineStatus)
        );
        var coordUpdate = Builders<CoordinatorTwin>.Update
            .Set("metadata.sync_status", staleStatus)
            .Set("metadata.is_connected", false);

        var coordResult = await coordCollection.UpdateManyAsync(coordFilter, coordUpdate, cancellationToken: ct);
        totalMarked += (int)coordResult.ModifiedCount;

        if (totalMarked > 0)
        {
            _logger.LogInformation("Marked {Count} twins as stale (no data for {Threshold})", 
                totalMarked, staleThreshold);
        }

        return totalMarked;
    }
}
