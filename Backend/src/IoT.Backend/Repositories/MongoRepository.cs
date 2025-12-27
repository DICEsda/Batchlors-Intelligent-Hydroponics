using IoT.Backend.Models;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// MongoDB implementation of the repository interface.
/// Matches the Go backend behavior exactly for contract compatibility.
/// </summary>
public sealed class MongoRepository : IRepository
{
    private readonly IMongoDatabase _db;
    private readonly ILogger<MongoRepository> _logger;

    // Collection names matching Go backend
    private const string CoordinatorsCollection = "coordinators";
    private const string NodesCollection = "nodes";
    private const string SitesCollection = "sites";
    private const string ZonesCollection = "zones";
    private const string SettingsCollection = "settings";
    private const string MmwaveFramesCollection = "mmwave_frames";
    private const string OtaJobsCollection = "ota_jobs";

    public MongoRepository(IMongoDatabase database, ILogger<MongoRepository> logger)
    {
        _db = database;
        _logger = logger;
    }

    #region Coordinator Operations

    public async Task<Coordinator?> GetCoordinatorByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        
        // Try both _id and coord_id for backwards compatibility (matching Go behavior)
        var filter = Builders<Coordinator>.Filter.Or(
            Builders<Coordinator>.Filter.Eq("_id", id),
            Builders<Coordinator>.Filter.Eq("coord_id", id)
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Coordinator?> GetCoordinatorBySiteAndIdAsync(string siteId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        var filter = Builders<Coordinator>.Filter.And(
            Builders<Coordinator>.Filter.Eq("site_id", siteId),
            Builders<Coordinator>.Filter.Eq("_id", coordId)
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpsertCoordinatorAsync(Coordinator coordinator, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        
        // Use coord_id as the unique key for matching (matching Go behavior)
        var filter = Builders<Coordinator>.Filter.Eq("coord_id", coordinator.CoordId);
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            await collection.ReplaceOneAsync(filter, coordinator, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert coordinator {CoordId}", coordinator.CoordId);
            throw;
        }
    }

    #endregion

    #region Node Operations

    public async Task<Node?> GetNodeByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Node>> GetNodesByCoordinatorAsync(string siteId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.And(
            Builders<Node>.Filter.Eq("site_id", siteId),
            Builders<Node>.Filter.Eq("coordinator_id", coordId)
        );

        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpsertNodeAsync(Node node, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.Eq("_id", node.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            await collection.ReplaceOneAsync(filter, node, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert node {NodeId}", node.Id);
            throw;
        }
    }

    public async Task DeleteNodeAsync(string siteId, string coordId, string nodeId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.And(
            Builders<Node>.Filter.Eq("site_id", siteId),
            Builders<Node>.Filter.Eq("coordinator_id", coordId),
            Builders<Node>.Filter.Eq("node_id", nodeId)
        );

        await collection.DeleteOneAsync(filter, ct);
    }

    public async Task UpdateNodeZoneAsync(string siteId, string coordId, string nodeId, string zoneId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.And(
            Builders<Node>.Filter.Eq("site_id", siteId),
            Builders<Node>.Filter.Eq("coordinator_id", coordId),
            Builders<Node>.Filter.Eq("node_id", nodeId)
        );
        var update = Builders<Node>.Update
            .Set("zone_id", zoneId)
            .Set("updated_at", DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task UpdateNodeNameAsync(string siteId, string coordId, string nodeId, string name, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Node>(NodesCollection);
        var filter = Builders<Node>.Filter.And(
            Builders<Node>.Filter.Eq("site_id", siteId),
            Builders<Node>.Filter.Eq("coordinator_id", coordId),
            Builders<Node>.Filter.Eq("node_id", nodeId)
        );
        var update = Builders<Node>.Update
            .Set("name", name)
            .Set("updated_at", DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    #endregion

    #region mmWave Operations

    public async Task InsertMmwaveFrameAsync(MmwaveFrame frame, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<MmwaveFrame>(MmwaveFramesCollection);
        
        if (frame.Timestamp == default)
        {
            frame.Timestamp = DateTime.UtcNow;
        }

        try
        {
            await collection.InsertOneAsync(frame, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert mmWave frame");
            throw;
        }
    }

    public async Task<IReadOnlyList<MmwaveFrame>> GetMmwaveFramesAsync(string siteId, string coordinatorId, int limit, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<MmwaveFrame>(MmwaveFramesCollection);
        
        var filterBuilder = Builders<MmwaveFrame>.Filter;
        var filters = new List<FilterDefinition<MmwaveFrame>>();

        if (!string.IsNullOrEmpty(siteId))
            filters.Add(filterBuilder.Eq("site_id", siteId));
        
        if (!string.IsNullOrEmpty(coordinatorId))
            filters.Add(filterBuilder.Eq("coordinator_id", coordinatorId));

        var filter = filters.Count > 0 
            ? filterBuilder.And(filters) 
            : filterBuilder.Empty;

        var sort = Builders<MmwaveFrame>.Sort.Descending("timestamp");

        var query = collection.Find(filter).Sort(sort);
        
        if (limit > 0)
            query = query.Limit(limit);

        return await query.ToListAsync(ct);
    }

    #endregion

    #region OTA Operations

    public async Task<OtaJob?> GetOtaJobByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        var filter = Builders<OtaJob>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task CreateOtaJobAsync(OtaJob job, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);

        try
        {
            await collection.InsertOneAsync(job, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OTA job");
            throw;
        }
    }

    public async Task UpdateOtaJobStatusAsync(string id, string status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        var filter = Builders<OtaJob>.Filter.Eq("_id", id);
        var update = Builders<OtaJob>.Update
            .Set("status", status)
            .Set("updated_at", DateTime.UtcNow);

        try
        {
            await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OTA job status");
            throw;
        }
    }

    #endregion

    #region Site Operations

    public async Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Site>(SitesCollection);
        return await collection.Find(Builders<Site>.Filter.Empty).ToListAsync(ct);
    }

    public async Task<Site?> GetSiteByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Site>(SitesCollection);
        var filter = Builders<Site>.Filter.Eq("_id", id);
        var site = await collection.Find(filter).FirstOrDefaultAsync(ct);

        // Auto-create site001 if requested and missing (Development convenience - matching Go behavior)
        if (site == null && id == "site001")
        {
            _logger.LogInformation("Auto-creating default site001");
            var defaultSite = new Site
            {
                Id = "site001",
                Name = "Default Site",
                Location = "Local",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await CreateSiteAsync(defaultSite, ct);
                return defaultSite;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-create site001");
            }
        }

        return site;
    }

    public async Task CreateSiteAsync(Site site, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Site>(SitesCollection);

        try
        {
            await collection.InsertOneAsync(site, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create site");
            throw;
        }
    }

    public async Task UpsertSiteAsync(Site site, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Site>(SitesCollection);
        var filter = Builders<Site>.Filter.Eq("_id", site.Id);
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            await collection.ReplaceOneAsync(filter, site, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert site");
            throw;
        }
    }

    #endregion

    #region Settings Operations

    public async Task<Settings?> GetSettingsAsync(string siteId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Settings>(SettingsCollection);
        var filter = Builders<Settings>.Filter.Eq("site_id", siteId);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task SaveSettingsAsync(Settings settings, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Settings>(SettingsCollection);
        var filter = Builders<Settings>.Filter.Eq("site_id", settings.SiteId);
        var options = new ReplaceOptions { IsUpsert = true };

        await collection.ReplaceOneAsync(filter, settings, options, ct);
    }

    #endregion

    #region Zone Operations

    public async Task CreateZoneAsync(Zone zone, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        zone.CreatedAt = DateTime.UtcNow;
        zone.UpdatedAt = DateTime.UtcNow;

        await collection.InsertOneAsync(zone, cancellationToken: ct);
    }

    public async Task<Zone?> GetZoneByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        var filter = Builders<Zone>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Zone>> GetZonesBySiteAsync(string siteId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        var filter = Builders<Zone>.Filter.Eq("site_id", siteId);
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<Zone?> GetZoneByCoordinatorAsync(string siteId, string coordinatorId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        var filter = Builders<Zone>.Filter.And(
            Builders<Zone>.Filter.Eq("site_id", siteId),
            Builders<Zone>.Filter.Eq("coordinator_id", coordinatorId)
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task DeleteZoneAsync(string zoneId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        await collection.DeleteOneAsync(Builders<Zone>.Filter.Eq("_id", zoneId), ct);

        // Remove zone assignment from all nodes in this zone (matching Go behavior)
        var nodesCollection = _db.GetCollection<Node>(NodesCollection);
        var nodeFilter = Builders<Node>.Filter.Eq("zone_id", zoneId);
        var nodeUpdate = Builders<Node>.Update.Unset("zone_id");
        await nodesCollection.UpdateManyAsync(nodeFilter, nodeUpdate, cancellationToken: ct);
    }

    public async Task UpdateZoneAsync(Zone zone, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        zone.UpdatedAt = DateTime.UtcNow;
        
        var filter = Builders<Zone>.Filter.Eq("_id", zone.Id);
        var options = new ReplaceOptions { IsUpsert = false };

        await collection.ReplaceOneAsync(filter, zone, options, ct);
    }

    #endregion
}
