using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Fixtures;

/// <summary>
/// Provides a MongoDB test container with automatic cleanup.
/// Each test class gets a fresh database.
/// </summary>
public class MongoDbFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;
    private readonly ITestOutputHelper? _output;

    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; } = "iot_test";
    public IMongoClient? Client { get; private set; }
    public IMongoDatabase? Database { get; private set; }

    public MongoDbFixture(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output?.WriteLine("Starting MongoDB container...");

        _container = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .WithPortBinding(27017, true)
            .Build();

        await _container.StartAsync();
        
        ConnectionString = _container.GetConnectionString();
        Client = new MongoClient(ConnectionString);
        Database = Client.GetDatabase(DatabaseName);

        _output?.WriteLine($"MongoDB container started: {ConnectionString}");
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            _output?.WriteLine("Stopping MongoDB container...");
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Cleans all collections in the test database.
    /// Call this between tests for isolation.
    /// </summary>
    public async Task CleanDatabaseAsync()
    {
        if (Database == null) return;

        var collections = await Database.ListCollectionNamesAsync();
        await collections.ForEachAsync(async name =>
        {
            await Database.DropCollectionAsync(name);
        });

        _output?.WriteLine("Database cleaned");
    }

    /// <summary>
    /// Gets a typed collection from the test database.
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string name) =>
        Database?.GetCollection<T>(name) ?? throw new InvalidOperationException("Database not initialized");
}
