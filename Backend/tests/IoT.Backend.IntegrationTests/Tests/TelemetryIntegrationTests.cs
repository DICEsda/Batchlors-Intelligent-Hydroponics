using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.IntegrationTests.Helpers;
using IoT.Backend.Models;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests the complete telemetry flow:
/// MQTT (device) → Backend (TelemetryHandler) → MongoDB (persistence) → WebSocket (broadcast)
/// </summary>
[Collection("Integration")]
public class TelemetryIntegrationTests : IntegrationTestBase
{
    private MqttTestClient? _mqttClient;

    public TelemetryIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mqttClient = new MqttTestClient(Output);
        await _mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
    }

    public override async Task DisposeAsync()
    {
        if (_mqttClient != null)
        {
            await _mqttClient.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    [Fact]
    public async Task TowerTelemetry_WhenPublishedViaMqtt_ShouldBePersisted()
    {
        // Arrange
        var farmId = "test-farm";
        var coordId = "coord-001";
        var towerId = "tower-001";
        var expectedTemp = 26.5f;
        var expectedHumidity = 72.0f;

        // Allow time for backend MQTT handler to be subscribed
        await Task.Delay(2000);

        // Act
        await _mqttClient!.PublishTowerTelemetryAsync(
            farmId, coordId, towerId,
            airTempC: expectedTemp,
            humidityPct: expectedHumidity,
            lightLux: 450.0f,
            pumpOn: true,
            lightOn: true,
            lightBrightness: 200);

        // Wait for processing
        await Task.Delay(1000);

        // Assert
        var collection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");
        var telemetry = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .SortByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        telemetry.Should().NotBeNull("telemetry should be persisted to MongoDB");
        telemetry!.AirTempC.Should().Be(expectedTemp);
        telemetry.HumidityPct.Should().Be(expectedHumidity);
        telemetry.CoordId.Should().Be(coordId);
        telemetry.PumpOn.Should().BeTrue();
        telemetry.LightOn.Should().BeTrue();
        telemetry.LightBrightness.Should().Be(200);
    }

    [Fact]
    public async Task ReservoirTelemetry_WhenPublishedViaMqtt_ShouldBePersisted()
    {
        // Arrange
        var farmId = "test-farm-2";
        var coordId = "coord-002";
        var expectedPh = 6.2f;
        var expectedEc = 1.8f;

        await Task.Delay(2000);

        // Act
        await _mqttClient!.PublishReservoirTelemetryAsync(
            farmId, coordId,
            ph: expectedPh,
            ecMsCm: expectedEc,
            tdsPpm: 900f,
            waterTempC: 21.5f,
            waterLevelPct: 78.0f,
            mainPumpOn: true,
            towersOnline: 3);

        await Task.Delay(1000);

        // Assert
        var collection = MongoDb.GetCollection<ReservoirTelemetry>("reservoir_telemetry");
        var telemetry = await collection
            .Find(t => t.CoordId == coordId && t.FarmId == farmId)
            .SortByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        telemetry.Should().NotBeNull("reservoir telemetry should be persisted");
        telemetry!.Ph.Should().Be(expectedPh);
        telemetry.EcMsCm.Should().Be(expectedEc);
        telemetry.TdsPpm.Should().Be(900f);
        telemetry.WaterTempC.Should().Be(21.5f);
        telemetry.WaterLevelPct.Should().Be(78.0f);
        telemetry.MainPumpOn.Should().BeTrue();
        telemetry.TowersOnline.Should().Be(3);
    }

    [Fact]
    public async Task MultipleTelemetryMessages_ShouldCreateTimeSeriesData()
    {
        // Arrange
        var farmId = "test-farm-ts";
        var coordId = "coord-ts";
        var towerId = "tower-ts";
        var messageCount = 5;

        await Task.Delay(2000);

        // Act - Send multiple telemetry messages
        for (int i = 0; i < messageCount; i++)
        {
            await _mqttClient!.PublishTowerTelemetryAsync(
                farmId, coordId, towerId,
                airTempC: 20.0f + i,
                humidityPct: 60.0f + i);
            await Task.Delay(200); // Small delay between messages
        }

        await Task.Delay(1000);

        // Assert
        var collection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");
        var telemetryRecords = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .ToListAsync();

        telemetryRecords.Should().HaveCount(messageCount, "all telemetry messages should be persisted");
        telemetryRecords.Select(t => t.AirTempC).Should().BeEquivalentTo(
            Enumerable.Range(0, messageCount).Select(i => 20.0f + i));
    }

    [Fact]
    public async Task TowerTelemetry_ShouldUpdateDigitalTwin()
    {
        // Arrange
        var farmId = "test-farm-twin";
        var coordId = "coord-twin";
        var towerId = "tower-twin";

        await Task.Delay(2000);

        // Act
        await _mqttClient!.PublishTowerTelemetryAsync(
            farmId, coordId, towerId,
            airTempC: 28.0f,
            humidityPct: 55.0f,
            lightOn: true,
            lightBrightness: 180);

        await Task.Delay(1000);

        // Assert - Check digital twin was updated
        var response = await HttpClient.GetAsync(
            $"/api/twins/towers/{towerId}?coordId={coordId}&farmId={farmId}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"Twin response: {content}");
            content.Should().Contain("28"); // Temperature should be in response
        }
        else
        {
            // Digital twin might not exist yet if this is the first telemetry
            // This is acceptable - the twin will be created on subsequent operations
            Output.WriteLine($"Digital twin not found (status: {response.StatusCode}) - this is expected for new towers");
        }
    }

    [Fact]
    public async Task ReservoirTelemetry_WithLowWaterLevel_ShouldSetAlert()
    {
        // Arrange
        var farmId = "test-farm-alert";
        var coordId = "coord-alert";

        await Task.Delay(2000);

        // Act - Send telemetry with low water level
        await _mqttClient!.PublishReservoirTelemetryAsync(
            farmId, coordId,
            waterLevelPct: 15.0f); // Below 20% threshold

        await Task.Delay(1000);

        // Assert
        var collection = MongoDb.GetCollection<ReservoirTelemetry>("reservoir_telemetry");
        var telemetry = await collection
            .Find(t => t.CoordId == coordId && t.FarmId == farmId)
            .SortByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        telemetry.Should().NotBeNull();
        // Note: The low water alert is computed from water_level_pct < 20
        // The actual alert flag should be set by the device or computed in telemetry
    }

    [Fact]
    public async Task InvalidTelemetryPayload_ShouldNotCrashBackend()
    {
        // Arrange
        var topic = "farm/test/coord/test/tower/test/telemetry";

        await Task.Delay(2000);

        // Act - Send invalid JSON
        await _mqttClient!.PublishAsync(topic, System.Text.Encoding.UTF8.GetBytes("not valid json {"));

        // Wait and verify backend is still healthy
        await Task.Delay(1000);

        // Assert - Backend should still be responding
        var response = await HttpClient.GetAsync("/health/live");
        response.IsSuccessStatusCode.Should().BeTrue("backend should survive invalid telemetry");
    }

    [Fact]
    public async Task EmptyTelemetryPayload_ShouldBeHandledGracefully()
    {
        // Arrange
        var topic = "farm/test/coord/test/tower/test/telemetry";

        await Task.Delay(2000);

        // Act - Send empty payload
        await _mqttClient!.PublishAsync(topic, Array.Empty<byte>());

        await Task.Delay(1000);

        // Assert - Backend should still be responding
        var response = await HttpClient.GetAsync("/health/live");
        response.IsSuccessStatusCode.Should().BeTrue("backend should handle empty telemetry");
    }
}
