using IoT.Backend.Models;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// MongoDB implementation of the repository interface.
/// Matches the Go backend behavior exactly for contract compatibility.
/// </summary>
public sealed class MongoRepository : IRepository, ICoordinatorRepository, ITowerRepository, IOtaJobRepository, ISettingsRepository, IZoneRepository, ITelemetryRepository, IFirmwareRepository
{
    private readonly IMongoDatabase _db;
    private readonly ILogger<MongoRepository> _logger;

    // Collection names matching Go backend
    private const string CoordinatorsCollection = "coordinators";
    private const string TowersCollection = "towers";  // Hydroponic tower twins
    private const string ZonesCollection = "zones";
    private const string SettingsCollection = "settings";
    private const string OtaJobsCollection = "ota_jobs";
    
    // Hydroponic time-series collections
    private const string ReservoirTelemetryCollection = "reservoir_telemetry";
    private const string TowerTelemetryCollection = "tower_telemetry";
    private const string HeightMeasurementsCollection = "height_measurements";
    private const string FirmwareVersionsCollection = "firmware_versions";

    public MongoRepository(IMongoDatabase database, ILogger<MongoRepository> logger)
    {
        _db = database;
        _logger = logger;

        // Ensure TTL indexes for telemetry time-series collections
        EnsureTelemetryTtlIndexesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates TTL indexes on telemetry collections so MongoDB automatically
    /// deletes documents older than 7 days (604 800 seconds).
    /// Safe to call on every startup — MongoDB is a no-op when the index already exists
    /// with the same key and options.
    /// </summary>
    private async Task EnsureTelemetryTtlIndexesAsync()
    {
        try
        {
            var ttlExpiry = TimeSpan.FromDays(7);

            // Tower telemetry — expire after 7 days on the "timestamp" field
            var towerTelemetryCollection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);
            var towerTtlIndex = new CreateIndexModel<TowerTelemetry>(
                Builders<TowerTelemetry>.IndexKeys.Ascending(t => t.Timestamp),
                new CreateIndexOptions { Name = "timestamp_ttl_7d", ExpireAfter = ttlExpiry });
            await towerTelemetryCollection.Indexes.CreateOneAsync(towerTtlIndex);

            // Reservoir telemetry — expire after 7 days on the "timestamp" field
            var reservoirTelemetryCollection = _db.GetCollection<ReservoirTelemetry>(ReservoirTelemetryCollection);
            var reservoirTtlIndex = new CreateIndexModel<ReservoirTelemetry>(
                Builders<ReservoirTelemetry>.IndexKeys.Ascending(r => r.Timestamp),
                new CreateIndexOptions { Name = "timestamp_ttl_7d", ExpireAfter = ttlExpiry });
            await reservoirTelemetryCollection.Indexes.CreateOneAsync(reservoirTtlIndex);

            _logger.LogInformation("Ensured 7-day TTL indexes on {TowerCollection} and {ReservoirCollection}",
                TowerTelemetryCollection, ReservoirTelemetryCollection);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create TTL indexes for telemetry collections (may already exist with different options)");
        }
    }

    #region Database Operations

    public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var command = new MongoDB.Bson.BsonDocument("ping", 1);
            await _db.RunCommandAsync<MongoDB.Bson.BsonDocument>(command, cancellationToken: ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

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

    public async Task<Coordinator?> GetCoordinatorByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        var filter = Builders<Coordinator>.Filter.And(
            Builders<Coordinator>.Filter.Eq("farm_id", farmId),
            Builders<Coordinator>.Filter.Or(
                Builders<Coordinator>.Filter.Eq("_id", coordId),
                Builders<Coordinator>.Filter.Eq("coord_id", coordId)
            )
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Coordinator>> GetCoordinatorsByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        var filter = Builders<Coordinator>.Filter.Eq("farm_id", farmId);
        return await collection.Find(filter).ToListAsync(ct);
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

    public async Task<long> CountOnlineCoordinatorsAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        var cutoff = DateTime.UtcNow - threshold;
        var filter = Builders<Coordinator>.Filter.Gte("last_seen", cutoff);
        return await collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<bool> DeleteCoordinatorAsync(string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        var filter = Builders<Coordinator>.Filter.Or(
            Builders<Coordinator>.Filter.Eq("_id", coordId),
            Builders<Coordinator>.Filter.Eq("coord_id", coordId)
        );

        try
        {
            var result = await collection.DeleteOneAsync(filter, ct);
            _logger.LogInformation("Deleted coordinator {CoordId}, count: {Count}", coordId, result.DeletedCount);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete coordinator {CoordId}", coordId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Coordinator>> GetAllCoordinatorsAsync(CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Coordinator>(CoordinatorsCollection);
        return await collection.Find(Builders<Coordinator>.Filter.Empty).ToListAsync(ct);
    }

    #endregion

    #region Tower Operations (Hydroponic System)

    public async Task<Tower?> GetTowerByIdAsync(string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        
        // Try both _id and tower_id for flexibility
        var filter = Builders<Tower>.Filter.Or(
            Builders<Tower>.Filter.Eq("_id", towerId),
            Builders<Tower>.Filter.Eq("tower_id", towerId)
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Tower?> GetTowerByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        var filter = Builders<Tower>.Filter.And(
            Builders<Tower>.Filter.Eq("farm_id", farmId),
            Builders<Tower>.Filter.Eq("coord_id", coordId),
            Builders<Tower>.Filter.Or(
                Builders<Tower>.Filter.Eq("_id", towerId),
                Builders<Tower>.Filter.Eq("tower_id", towerId)
            )
        );

        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Tower>> GetTowersByCoordinatorAsync(string farmId, string coordId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        var filter = Builders<Tower>.Filter.And(
            Builders<Tower>.Filter.Eq("farm_id", farmId),
            Builders<Tower>.Filter.Eq("coord_id", coordId)
        );

        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Tower>> GetTowersByFarmAsync(string farmId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        var filter = Builders<Tower>.Filter.Eq("farm_id", farmId);
        return await collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpsertTowerAsync(Tower tower, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        
        // Use composite key: farm_id + coord_id + tower_id for uniqueness
        var filter = Builders<Tower>.Filter.And(
            Builders<Tower>.Filter.Eq("farm_id", tower.FarmId),
            Builders<Tower>.Filter.Eq("coord_id", tower.CoordId),
            Builders<Tower>.Filter.Eq("tower_id", tower.TowerId)
        );
        var options = new ReplaceOptions { IsUpsert = true };

        try
        {
            tower.UpdatedAt = DateTime.UtcNow;
            await collection.ReplaceOneAsync(filter, tower, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert tower {TowerId} for coordinator {CoordId}", tower.TowerId, tower.CoordId);
            throw;
        }
    }

    public async Task DeleteTowerAsync(string farmId, string coordId, string towerId, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        var filter = Builders<Tower>.Filter.And(
            Builders<Tower>.Filter.Eq("farm_id", farmId),
            Builders<Tower>.Filter.Eq("coord_id", coordId),
            Builders<Tower>.Filter.Eq("tower_id", towerId)
        );

        await collection.DeleteOneAsync(filter, ct);
    }

    public async Task UpdateTowerNameAsync(string farmId, string coordId, string towerId, string name, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        var filter = Builders<Tower>.Filter.And(
            Builders<Tower>.Filter.Eq("farm_id", farmId),
            Builders<Tower>.Filter.Eq("coord_id", coordId),
            Builders<Tower>.Filter.Eq("tower_id", towerId)
        );
        var update = Builders<Tower>.Update
            .Set("name", name)
            .Set("updated_at", DateTime.UtcNow);

        await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Tower>> GetAllTowersAsync(CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Tower>(TowersCollection);
        return await collection.Find(Builders<Tower>.Filter.Empty).ToListAsync(ct);
    }

    #endregion

    #region OTA Operations

    public async Task<OtaJob?> GetOtaJobByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        var filter = Builders<OtaJob>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<OtaJob>> GetOtaJobsAsync(string? farmId = null, int limit = 50, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        
        var filter = string.IsNullOrEmpty(farmId)
            ? Builders<OtaJob>.Filter.Empty
            : Builders<OtaJob>.Filter.Eq("farm_id", farmId);
        
        var sort = Builders<OtaJob>.Sort.Descending("created_at");
        
        return await collection.Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);
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

    public async Task UpdateOtaJobAsync(OtaJob job, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        var filter = Builders<OtaJob>.Filter.Eq("_id", job.Id);
        job.UpdatedAt = DateTime.UtcNow;
        
        var options = new ReplaceOptions { IsUpsert = false };

        try
        {
            await collection.ReplaceOneAsync(filter, job, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OTA job");
            throw;
        }
    }

    public async Task UpdateOtaJobStatusAsync(string id, string status, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<OtaJob>(OtaJobsCollection);
        var filter = Builders<OtaJob>.Filter.Eq("_id", id);
        
        var updateDef = Builders<OtaJob>.Update
            .Set("status", status)
            .Set("updated_at", DateTime.UtcNow);
        
        // Set completed_at if status is terminal
        if (status is "completed" or "failed" or "cancelled")
        {
            updateDef = updateDef.Set("completed_at", DateTime.UtcNow);
        }

        try
        {
            await collection.UpdateOneAsync(filter, updateDef, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OTA job status");
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
    }

    public async Task UpdateZoneAsync(Zone zone, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        zone.UpdatedAt = DateTime.UtcNow;
        
        var filter = Builders<Zone>.Filter.Eq("_id", zone.Id);
        var options = new ReplaceOptions { IsUpsert = false };

        await collection.ReplaceOneAsync(filter, zone, options, ct);
    }

    public async Task<IReadOnlyList<Zone>> GetAllZonesAsync(CancellationToken ct = default)
    {
        var collection = _db.GetCollection<Zone>(ZonesCollection);
        return await collection.Find(Builders<Zone>.Filter.Empty).ToListAsync(ct);
    }

    #endregion

    #region Reservoir Telemetry Operations

    public async Task InsertReservoirTelemetryAsync(ReservoirTelemetry telemetry, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<ReservoirTelemetry>(ReservoirTelemetryCollection);

        try
        {
            telemetry.Timestamp = telemetry.Timestamp == default ? DateTime.UtcNow : telemetry.Timestamp;
            await collection.InsertOneAsync(telemetry, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert reservoir telemetry for coordinator {CoordId}", telemetry.CoordId);
            throw;
        }
    }

    public async Task<IReadOnlyList<ReservoirTelemetry>> GetReservoirTelemetryAsync(
        string farmId,
        string coordId,
        DateTime from,
        DateTime to,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<ReservoirTelemetry>(ReservoirTelemetryCollection);
        var filter = Builders<ReservoirTelemetry>.Filter.And(
            Builders<ReservoirTelemetry>.Filter.Eq("farm_id", farmId),
            Builders<ReservoirTelemetry>.Filter.Eq("coord_id", coordId),
            Builders<ReservoirTelemetry>.Filter.Gte("timestamp", from),
            Builders<ReservoirTelemetry>.Filter.Lte("timestamp", to)
        );

        var sort = Builders<ReservoirTelemetry>.Sort.Descending("timestamp");

        return await collection.Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<ReservoirTelemetry?> GetLatestReservoirTelemetryAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<ReservoirTelemetry>(ReservoirTelemetryCollection);
        var filter = Builders<ReservoirTelemetry>.Filter.And(
            Builders<ReservoirTelemetry>.Filter.Eq("farm_id", farmId),
            Builders<ReservoirTelemetry>.Filter.Eq("coord_id", coordId)
        );

        var sort = Builders<ReservoirTelemetry>.Sort.Descending("timestamp");

        return await collection.Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ReservoirTelemetry>> GetDailyAverageReservoirTelemetryAsync(
        string farmId,
        string coordId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<ReservoirTelemetry>(ReservoirTelemetryCollection);

        var pipeline = new[]
        {
            new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
            {
                { "farm_id", farmId },
                { "coord_id", coordId },
                { "timestamp", new MongoDB.Bson.BsonDocument
                {
                    { "$gte", from },
                    { "$lte", to }
                }}
            }),
            new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
            {
                { "_id", new MongoDB.Bson.BsonDocument("$dateToString", new MongoDB.Bson.BsonDocument
                {
                    { "format", "%Y-%m-%d" },
                    { "date", "$timestamp" }
                })},
                { "farm_id", new MongoDB.Bson.BsonDocument("$first", "$farm_id") },
                { "coord_id", new MongoDB.Bson.BsonDocument("$first", "$coord_id") },
                { "ph", new MongoDB.Bson.BsonDocument("$avg", "$ph") },
                { "ec_ms_cm", new MongoDB.Bson.BsonDocument("$avg", "$ec_ms_cm") },
                { "tds_ppm", new MongoDB.Bson.BsonDocument("$avg", "$tds_ppm") },
                { "water_temp_c", new MongoDB.Bson.BsonDocument("$avg", "$water_temp_c") },
                { "water_level_pct", new MongoDB.Bson.BsonDocument("$avg", "$water_level_pct") }
            }),
            new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("_id", 1))
        };

        var results = await collection.Aggregate<MongoDB.Bson.BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        
        return results.Select(doc => new ReservoirTelemetry
        {
            FarmId = doc.GetValue("farm_id", "").AsString,
            CoordId = doc.GetValue("coord_id", "").AsString,
            Timestamp = DateTime.Parse(doc["_id"].AsString),
            Ph = (float)doc.GetValue("ph", 0.0).ToDouble(),
            EcMsCm = (float)doc.GetValue("ec_ms_cm", 0.0).ToDouble(),
            TdsPpm = (float)doc.GetValue("tds_ppm", 0.0).ToDouble(),
            WaterTempC = (float)doc.GetValue("water_temp_c", 0.0).ToDouble(),
            WaterLevelPct = (float)doc.GetValue("water_level_pct", 0.0).ToDouble()
        }).ToList();
    }

    #endregion

    #region Tower Telemetry Operations

    public async Task InsertTowerTelemetryAsync(TowerTelemetry telemetry, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);

        try
        {
            telemetry.Timestamp = telemetry.Timestamp == default ? DateTime.UtcNow : telemetry.Timestamp;
            await collection.InsertOneAsync(telemetry, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert tower telemetry for tower {TowerId}", telemetry.TowerId);
            throw;
        }
    }

    public async Task<IReadOnlyList<TowerTelemetry>> GetTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        DateTime from,
        DateTime to,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);
        var filter = Builders<TowerTelemetry>.Filter.And(
            Builders<TowerTelemetry>.Filter.Eq("farm_id", farmId),
            Builders<TowerTelemetry>.Filter.Eq("coord_id", coordId),
            Builders<TowerTelemetry>.Filter.Eq("tower_id", towerId),
            Builders<TowerTelemetry>.Filter.Gte("timestamp", from),
            Builders<TowerTelemetry>.Filter.Lte("timestamp", to)
        );

        var sort = Builders<TowerTelemetry>.Sort.Descending("timestamp");

        return await collection.Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<TowerTelemetry?> GetLatestTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);
        var filter = Builders<TowerTelemetry>.Filter.And(
            Builders<TowerTelemetry>.Filter.Eq("farm_id", farmId),
            Builders<TowerTelemetry>.Filter.Eq("coord_id", coordId),
            Builders<TowerTelemetry>.Filter.Eq("tower_id", towerId)
        );

        var sort = Builders<TowerTelemetry>.Sort.Descending("timestamp");

        return await collection.Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TowerTelemetry>> GetLatestTowerTelemetryByCoordinatorAsync(
        string farmId,
        string coordId,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);

        // Aggregation to get latest telemetry per tower
        var pipeline = new[]
        {
            new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
            {
                { "farm_id", farmId },
                { "coord_id", coordId }
            }),
            new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("timestamp", -1)),
            new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
            {
                { "_id", "$tower_id" },
                { "doc", new MongoDB.Bson.BsonDocument("$first", "$$ROOT") }
            }),
            new MongoDB.Bson.BsonDocument("$replaceRoot", new MongoDB.Bson.BsonDocument("newRoot", "$doc"))
        };

        return await collection.Aggregate<TowerTelemetry>(pipeline, cancellationToken: ct).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TowerTelemetry>> GetDailyAverageTowerTelemetryAsync(
        string farmId,
        string coordId,
        string towerId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<TowerTelemetry>(TowerTelemetryCollection);

        var pipeline = new[]
        {
            new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
            {
                { "farm_id", farmId },
                { "coord_id", coordId },
                { "tower_id", towerId },
                { "timestamp", new MongoDB.Bson.BsonDocument
                {
                    { "$gte", from },
                    { "$lte", to }
                }}
            }),
            new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
            {
                { "_id", new MongoDB.Bson.BsonDocument("$dateToString", new MongoDB.Bson.BsonDocument
                {
                    { "format", "%Y-%m-%d" },
                    { "date", "$timestamp" }
                })},
                { "farm_id", new MongoDB.Bson.BsonDocument("$first", "$farm_id") },
                { "coord_id", new MongoDB.Bson.BsonDocument("$first", "$coord_id") },
                { "tower_id", new MongoDB.Bson.BsonDocument("$first", "$tower_id") },
                { "air_temp_c", new MongoDB.Bson.BsonDocument("$avg", "$air_temp_c") },
                { "humidity_pct", new MongoDB.Bson.BsonDocument("$avg", "$humidity_pct") },
                { "light_lux", new MongoDB.Bson.BsonDocument("$avg", "$light_lux") }
            }),
            new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("_id", 1))
        };

        var results = await collection.Aggregate<MongoDB.Bson.BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        
        return results.Select(doc => new TowerTelemetry
        {
            FarmId = doc.GetValue("farm_id", "").AsString,
            CoordId = doc.GetValue("coord_id", "").AsString,
            TowerId = doc.GetValue("tower_id", "").AsString,
            Timestamp = DateTime.Parse(doc["_id"].AsString),
            AirTempC = (float)doc.GetValue("air_temp_c", 0.0).ToDouble(),
            HumidityPct = (float)doc.GetValue("humidity_pct", 0.0).ToDouble(),
            LightLux = (float)doc.GetValue("light_lux", 0.0).ToDouble()
        }).ToList();
    }

    #endregion

    #region Height Measurement Operations

    public async Task InsertHeightMeasurementAsync(HeightMeasurement measurement, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<HeightMeasurement>(HeightMeasurementsCollection);

        try
        {
            measurement.Timestamp = measurement.Timestamp == default ? DateTime.UtcNow : measurement.Timestamp;
            await collection.InsertOneAsync(measurement, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert height measurement for tower {TowerId}, slot {SlotIndex}", 
                measurement.TowerId, measurement.SlotIndex);
            throw;
        }
    }

    public async Task<IReadOnlyList<HeightMeasurement>> GetHeightMeasurementsAsync(
        string farmId,
        string towerId,
        int? slotIndex = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<HeightMeasurement>(HeightMeasurementsCollection);
        
        var filterBuilder = Builders<HeightMeasurement>.Filter;
        var filters = new List<FilterDefinition<HeightMeasurement>>
        {
            filterBuilder.Eq("farm_id", farmId),
            filterBuilder.Eq("tower_id", towerId)
        };

        if (slotIndex.HasValue)
            filters.Add(filterBuilder.Eq("slot_index", slotIndex.Value));
        
        if (from.HasValue)
            filters.Add(filterBuilder.Gte("timestamp", from.Value));
        
        if (to.HasValue)
            filters.Add(filterBuilder.Lte("timestamp", to.Value));

        var filter = filterBuilder.And(filters);
        var sort = Builders<HeightMeasurement>.Sort.Descending("timestamp");

        return await collection.Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HeightMeasurement>> GetLatestHeightMeasurementsByTowerAsync(
        string farmId,
        string towerId,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<HeightMeasurement>(HeightMeasurementsCollection);

        // Aggregation to get latest measurement per slot
        var pipeline = new[]
        {
            new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
            {
                { "farm_id", farmId },
                { "tower_id", towerId }
            }),
            new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("timestamp", -1)),
            new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
            {
                { "_id", "$slot_index" },
                { "doc", new MongoDB.Bson.BsonDocument("$first", "$$ROOT") }
            }),
            new MongoDB.Bson.BsonDocument("$replaceRoot", new MongoDB.Bson.BsonDocument("newRoot", "$doc")),
            new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("slot_index", 1))
        };

        return await collection.Aggregate<HeightMeasurement>(pipeline, cancellationToken: ct).ToListAsync(ct);
    }

    public async Task DeleteHeightMeasurementsAsync(
        string farmId,
        string towerId,
        int slotIndex,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<HeightMeasurement>(HeightMeasurementsCollection);
        var filter = Builders<HeightMeasurement>.Filter.And(
            Builders<HeightMeasurement>.Filter.Eq("farm_id", farmId),
            Builders<HeightMeasurement>.Filter.Eq("tower_id", towerId),
            Builders<HeightMeasurement>.Filter.Eq("slot_index", slotIndex)
        );

        var result = await collection.DeleteManyAsync(filter, ct);
        _logger.LogInformation("Deleted {Count} height measurements for tower {TowerId}, slot {SlotIndex}", 
            result.DeletedCount, towerId, slotIndex);
    }

    #endregion

    #region Firmware Version Operations

    public async Task<FirmwareVersion?> GetFirmwareByIdAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        var filter = Builders<FirmwareVersion>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<FirmwareVersion?> GetFirmwareByVersionAndTypeAsync(string version, string deviceType, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        var filter = Builders<FirmwareVersion>.Filter.And(
            Builders<FirmwareVersion>.Filter.Eq("version", version),
            Builders<FirmwareVersion>.Filter.Eq("device_type", deviceType.ToLowerInvariant())
        );
        return await collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<FirmwareVersion>> GetAllFirmwareAsync(
        string? deviceType = null,
        bool? stableOnly = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        
        var filterBuilder = Builders<FirmwareVersion>.Filter;
        var filters = new List<FilterDefinition<FirmwareVersion>>();

        if (!string.IsNullOrEmpty(deviceType))
            filters.Add(filterBuilder.Eq("device_type", deviceType.ToLowerInvariant()));
        
        if (stableOnly == true)
            filters.Add(filterBuilder.Eq("is_stable", true));

        var filter = filters.Count > 0 
            ? filterBuilder.And(filters) 
            : filterBuilder.Empty;

        var sort = Builders<FirmwareVersion>.Sort.Descending("release_date");

        return await collection.Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<FirmwareVersion?> GetLatestFirmwareAsync(string deviceType, bool stableOnly = true, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        
        var filterBuilder = Builders<FirmwareVersion>.Filter;
        var filters = new List<FilterDefinition<FirmwareVersion>>
        {
            filterBuilder.Eq("device_type", deviceType.ToLowerInvariant())
        };

        if (stableOnly)
            filters.Add(filterBuilder.Eq("is_stable", true));

        var filter = filterBuilder.And(filters);
        var sort = Builders<FirmwareVersion>.Sort.Descending("release_date");

        return await collection.Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(ct);
    }

    public async Task CreateFirmwareAsync(FirmwareVersion firmware, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);

        try
        {
            firmware.CreatedAt = DateTime.UtcNow;
            firmware.DeviceType = firmware.DeviceType.ToLowerInvariant();
            await collection.InsertOneAsync(firmware, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create firmware version {Version} for {DeviceType}", 
                firmware.Version, firmware.DeviceType);
            throw;
        }
    }

    public async Task UpdateFirmwareAsync(FirmwareVersion firmware, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        var filter = Builders<FirmwareVersion>.Filter.Eq("_id", firmware.Id);
        
        firmware.DeviceType = firmware.DeviceType.ToLowerInvariant();
        var options = new ReplaceOptions { IsUpsert = false };

        try
        {
            await collection.ReplaceOneAsync(filter, firmware, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update firmware version {Id}", firmware.Id);
            throw;
        }
    }

    public async Task DeleteFirmwareAsync(string id, CancellationToken ct = default)
    {
        var collection = _db.GetCollection<FirmwareVersion>(FirmwareVersionsCollection);
        var filter = Builders<FirmwareVersion>.Filter.Eq("_id", id);
        
        var result = await collection.DeleteOneAsync(filter, ct);
        _logger.LogInformation("Deleted firmware version {Id}, count: {Count}", id, result.DeletedCount);
    }

    #endregion

    #region Explicit Interface Implementations

    // ICoordinatorRepository
    Task<Coordinator?> ICoordinatorRepository.GetByIdAsync(string id, CancellationToken ct)
        => GetCoordinatorByIdAsync(id, ct);
    
    Task<Coordinator?> ICoordinatorRepository.GetBySiteAndIdAsync(string siteId, string coordId, CancellationToken ct)
        => GetCoordinatorBySiteAndIdAsync(siteId, coordId, ct);
    
    Task<Coordinator?> ICoordinatorRepository.GetByFarmAndIdAsync(string farmId, string coordId, CancellationToken ct)
        => GetCoordinatorByFarmAndIdAsync(farmId, coordId, ct);
    
    Task<IReadOnlyList<Coordinator>> ICoordinatorRepository.GetByFarmAsync(string farmId, CancellationToken ct)
        => GetCoordinatorsByFarmAsync(farmId, ct);

    Task<IReadOnlyList<Coordinator>> ICoordinatorRepository.GetAllAsync(CancellationToken ct)
        => GetAllCoordinatorsAsync(ct);
    
    Task ICoordinatorRepository.UpsertAsync(Coordinator coordinator, CancellationToken ct)
        => UpsertCoordinatorAsync(coordinator, ct);
    
    Task<long> ICoordinatorRepository.CountOnlineAsync(TimeSpan threshold, CancellationToken ct)
        => CountOnlineCoordinatorsAsync(threshold, ct);

    Task<bool> ICoordinatorRepository.DeleteAsync(string coordId, CancellationToken ct)
        => DeleteCoordinatorAsync(coordId, ct);

    // ITowerRepository
    Task<Tower?> ITowerRepository.GetByIdAsync(string towerId, CancellationToken ct)
        => GetTowerByIdAsync(towerId, ct);
    
    Task<Tower?> ITowerRepository.GetByFarmCoordAndIdAsync(string farmId, string coordId, string towerId, CancellationToken ct)
        => GetTowerByFarmCoordAndIdAsync(farmId, coordId, towerId, ct);
    
    Task<IReadOnlyList<Tower>> ITowerRepository.GetByCoordinatorAsync(string farmId, string coordId, CancellationToken ct)
        => GetTowersByCoordinatorAsync(farmId, coordId, ct);
    
    Task<IReadOnlyList<Tower>> ITowerRepository.GetByFarmAsync(string farmId, CancellationToken ct)
        => GetTowersByFarmAsync(farmId, ct);

    Task<IReadOnlyList<Tower>> ITowerRepository.GetAllAsync(CancellationToken ct)
        => GetAllTowersAsync(ct);
    
    Task ITowerRepository.UpsertAsync(Tower tower, CancellationToken ct)
        => UpsertTowerAsync(tower, ct);
    
    Task ITowerRepository.DeleteAsync(string farmId, string coordId, string towerId, CancellationToken ct)
        => DeleteTowerAsync(farmId, coordId, towerId, ct);
    
    Task ITowerRepository.UpdateNameAsync(string farmId, string coordId, string towerId, string name, CancellationToken ct)
        => UpdateTowerNameAsync(farmId, coordId, towerId, name, ct);

    // IOtaJobRepository
    Task<OtaJob?> IOtaJobRepository.GetByIdAsync(string id, CancellationToken ct)
        => GetOtaJobByIdAsync(id, ct);
    
    Task<IReadOnlyList<OtaJob>> IOtaJobRepository.GetAllAsync(string? farmId, int limit, CancellationToken ct)
        => GetOtaJobsAsync(farmId, limit, ct);
    
    Task IOtaJobRepository.CreateAsync(OtaJob job, CancellationToken ct)
        => CreateOtaJobAsync(job, ct);
    
    Task IOtaJobRepository.UpdateAsync(OtaJob job, CancellationToken ct)
        => UpdateOtaJobAsync(job, ct);
    
    Task IOtaJobRepository.UpdateStatusAsync(string id, string status, CancellationToken ct)
        => UpdateOtaJobStatusAsync(id, status, ct);

    // ISettingsRepository
    Task<Settings?> ISettingsRepository.GetAsync(string siteId, CancellationToken ct)
        => GetSettingsAsync(siteId, ct);
    
    Task ISettingsRepository.SaveAsync(Settings settings, CancellationToken ct)
        => SaveSettingsAsync(settings, ct);

    // IZoneRepository
    Task IZoneRepository.CreateAsync(Zone zone, CancellationToken ct)
        => CreateZoneAsync(zone, ct);
    
    Task<Zone?> IZoneRepository.GetByIdAsync(string id, CancellationToken ct)
        => GetZoneByIdAsync(id, ct);

    Task<IReadOnlyList<Zone>> IZoneRepository.GetAllAsync(CancellationToken ct)
        => GetAllZonesAsync(ct);
    
    Task<IReadOnlyList<Zone>> IZoneRepository.GetBySiteAsync(string siteId, CancellationToken ct)
        => GetZonesBySiteAsync(siteId, ct);
    
    Task<Zone?> IZoneRepository.GetByCoordinatorAsync(string siteId, string coordinatorId, CancellationToken ct)
        => GetZoneByCoordinatorAsync(siteId, coordinatorId, ct);
    
    Task IZoneRepository.DeleteAsync(string zoneId, CancellationToken ct)
        => DeleteZoneAsync(zoneId, ct);
    
    Task IZoneRepository.UpdateAsync(Zone zone, CancellationToken ct)
        => UpdateZoneAsync(zone, ct);

    // IFirmwareRepository
    Task<FirmwareVersion?> IFirmwareRepository.GetByIdAsync(string id, CancellationToken ct)
        => GetFirmwareByIdAsync(id, ct);
    
    Task<FirmwareVersion?> IFirmwareRepository.GetByVersionAndTypeAsync(string version, string deviceType, CancellationToken ct)
        => GetFirmwareByVersionAndTypeAsync(version, deviceType, ct);
    
    Task<IReadOnlyList<FirmwareVersion>> IFirmwareRepository.GetAllAsync(string? deviceType, bool? stableOnly, int limit, CancellationToken ct)
        => GetAllFirmwareAsync(deviceType, stableOnly, limit, ct);
    
    Task<FirmwareVersion?> IFirmwareRepository.GetLatestAsync(string deviceType, bool stableOnly, CancellationToken ct)
        => GetLatestFirmwareAsync(deviceType, stableOnly, ct);
    
    Task IFirmwareRepository.CreateAsync(FirmwareVersion firmware, CancellationToken ct)
        => CreateFirmwareAsync(firmware, ct);
    
    Task IFirmwareRepository.UpdateAsync(FirmwareVersion firmware, CancellationToken ct)
        => UpdateFirmwareAsync(firmware, ct);
    
    Task IFirmwareRepository.DeleteAsync(string id, CancellationToken ct)
        => DeleteFirmwareAsync(id, ct);

    #endregion
}
