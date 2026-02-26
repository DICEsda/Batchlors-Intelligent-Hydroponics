using MongoDB.Bson;
using MongoDB.Driver;

namespace IoT.Backend.Repositories;

/// <summary>
/// Generic MongoDB repository base class.
/// Provides common CRUD operations for any entity type.
/// </summary>
public abstract class MongoRepository<T> where T : class
{
    protected readonly IMongoCollection<T> Collection;
    protected readonly ILogger Logger;

    protected MongoRepository(IMongoDatabase database, string collectionName, ILogger logger)
    {
        Collection = database.GetCollection<T>(collectionName);
        Logger = logger;
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(_ => true).ToListAsync(ct);
    }

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public virtual async Task<T> InsertAsync(T entity, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    public virtual async Task<T> UpsertAsync(T entity, CancellationToken ct = default)
    {
        // Get _id field using reflection
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException($"Type {typeof(T).Name} must have an Id property");

        var id = idProperty.GetValue(entity) as string;
        if (string.IsNullOrEmpty(id))
        {
            // If no Id, treat as insert
            return await InsertAsync(entity, ct);
        }

        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var options = new ReplaceOptions { IsUpsert = true };
        
        await Collection.ReplaceOneAsync(filter, entity, options, ct);
        return entity;
    }

    public virtual async Task<T> UpdateAsync(string id, UpdateDefinition<T> update, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var options = new FindOneAndUpdateOptions<T>
        {
            ReturnDocument = ReturnDocument.After
        };
        
        return await Collection.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    public virtual async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        await Collection.DeleteOneAsync(filter, ct);
    }
}
