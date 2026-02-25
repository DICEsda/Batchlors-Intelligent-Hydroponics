using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace IoT.Backend.IntegrationTests.Fixtures;

/// <summary>
/// Collection fixture that provides shared MongoDB and MQTT containers across all tests.
/// This avoids Testcontainers Ryuk conflicts when running multiple test classes.
/// </summary>
public class SharedContainerFixture : IAsyncLifetime
{
    private readonly IMessageSink _messageSink;
    public MongoDbFixture MongoDb { get; }
    public MqttFixture Mqtt { get; }

    public SharedContainerFixture(IMessageSink messageSink)
    {
        _messageSink = messageSink;
        MongoDb = new MongoDbFixture(null);
        Mqtt = new MqttFixture(null);
    }

    public async Task InitializeAsync()
    {
        _messageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage("Starting shared test containers..."));
        
        // Start containers in parallel
        await Task.WhenAll(
            MongoDb.InitializeAsync(),
            Mqtt.InitializeAsync());
        
        _messageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage($"Containers ready - MongoDB: {MongoDb.ConnectionString}, MQTT: {Mqtt.Host}:{Mqtt.Port}"));
    }

    public async Task DisposeAsync()
    {
        _messageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage("Stopping shared test containers..."));
        
        await Task.WhenAll(
            MongoDb.DisposeAsync(),
            Mqtt.DisposeAsync());
    }
}

/// <summary>
/// Define the test collection that shares containers.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<SharedContainerFixture>
{
    // This class has no code - it's just a marker for xUnit
}

/// <summary>
/// Base class for integration tests that provides access to shared fixtures
/// and creates a per-test WebApplicationFactory.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly SharedContainerFixture SharedFixture;
    protected readonly ITestOutputHelper Output;
    private WebApplicationFactory<Program>? _factory;
    
    public HttpClient HttpClient { get; private set; } = null!;
    public MongoDbFixture MongoDb => SharedFixture.MongoDb;
    public MqttFixture Mqtt => SharedFixture.Mqtt;
    public IServiceProvider Services => _factory?.Services ?? throw new InvalidOperationException("Factory not initialized");

    protected IntegrationTestBase(SharedContainerFixture fixture, ITestOutputHelper output)
    {
        SharedFixture = fixture;
        Output = output;
    }

    public virtual async Task InitializeAsync()
    {
        Output.WriteLine("Creating WebApplicationFactory...");
        
        // Create the test server with custom configuration pointing to shared containers
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Override configuration for tests
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MongoDB"] = MongoDb.ConnectionString,
                        ["MongoDB:Database"] = MongoDb.DatabaseName,
                        ["Mqtt:Host"] = Mqtt.Host,
                        ["Mqtt:Port"] = Mqtt.Port.ToString(),
                        ["Mqtt:Username"] = null,
                        ["Mqtt:Password"] = null,
                        ["Mqtt:ClientId"] = $"test-backend-{Guid.NewGuid():N}",
                        ["Cors:Origins:0"] = "http://localhost:4200"
                    });
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddXUnit(Output);
                    logging.SetMinimumLevel(LogLevel.Debug);
                });

                builder.UseEnvironment("Testing");
            });

        HttpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        // Wait for the backend to be ready
        await WaitForBackendReadyAsync();
        
        // Clean the database for test isolation
        await MongoDb.CleanDatabaseAsync();
        
        Output.WriteLine("Test fixture initialized");
    }

    public virtual async Task DisposeAsync()
    {
        Output.WriteLine("Disposing test fixture...");
        
        HttpClient?.Dispose();
        
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets a scoped service from the test server.
    /// </summary>
    public T GetService<T>() where T : notnull =>
        Services.GetRequiredService<T>();

    /// <summary>
    /// Creates a scoped service provider for isolated operations.
    /// </summary>
    public IServiceScope CreateScope() =>
        Services.CreateScope();

    private async Task WaitForBackendReadyAsync(int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await HttpClient.GetAsync("/health/live");
                if (response.IsSuccessStatusCode)
                {
                    Output.WriteLine("Backend is ready");
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Backend did not become ready in time");
    }
}

/// <summary>
/// xUnit logging provider for ASP.NET Core.
/// </summary>
public static class XUnitLoggerExtensions
{
    public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.AddProvider(new XUnitLoggerProvider(output));
        return builder;
    }
}

internal class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XUnitLoggerProvider(ITestOutputHelper output) => _output = output;

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(_output, categoryName);

    public void Dispose() { }
}

internal class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _category;

    public XUnitLogger(ITestOutputHelper output, string category)
    {
        _output = output;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        try
        {
            var shortCategory = _category.Split('.').LastOrDefault() ?? _category;
            _output.WriteLine($"[{logLevel}] {shortCategory}: {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }
        catch
        {
            // Ignore failures during test cleanup
        }
    }
}
