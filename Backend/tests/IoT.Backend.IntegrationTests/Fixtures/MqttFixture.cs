using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Fixtures;

/// <summary>
/// Provides an MQTT (Mosquitto) test container.
/// Uses a simple in-memory configuration without authentication for testing.
/// </summary>
public class MqttFixture : IAsyncLifetime
{
    private IContainer? _container;
    private readonly ITestOutputHelper? _output;

    /// <summary>
    /// Gets the hostname to connect to the MQTT broker.
    /// Uses container hostname for Docker-in-Docker compatibility.
    /// </summary>
    public string Host { get; private set; } = "localhost";
    
    public int Port { get; private set; }
    public string ConnectionString => $"mqtt://{Host}:{Port}";

    public MqttFixture(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output?.WriteLine("Starting Mosquitto container...");

        _container = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2.0")
            .WithPortBinding(1883, true)
            .WithCommand("mosquitto", "-c", "/mosquitto-no-auth.conf")
            // Create a simple config that allows anonymous access for testing
            .WithResourceMapping(
                CreateMosquittoConfig(),
                "/mosquitto-no-auth.conf")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(1883))
            .WithLogger(new TestContainerLogger(_output))
            .Build();

        await _container.StartAsync();
        Port = _container.GetMappedPublicPort(1883);
        
        // Use the container's hostname which works for Docker-in-Docker
        // In most cases this returns the proper host to reach the container
        Host = _container.Hostname;

        _output?.WriteLine($"Mosquitto container started - Host: {Host}, Port: {Port}");
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            _output?.WriteLine("Stopping Mosquitto container...");
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    private static byte[] CreateMosquittoConfig()
    {
        var config = """
            listener 1883
            allow_anonymous true
            log_type all
            log_dest stdout
            """;
        return System.Text.Encoding.UTF8.GetBytes(config);
    }
}

/// <summary>
/// Logger adapter for Testcontainers to output to xUnit test output.
/// </summary>
internal class TestContainerLogger : ILogger
{
    private readonly ITestOutputHelper? _output;

    public TestContainerLogger(ITestOutputHelper? output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _output != null && logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            _output?.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
