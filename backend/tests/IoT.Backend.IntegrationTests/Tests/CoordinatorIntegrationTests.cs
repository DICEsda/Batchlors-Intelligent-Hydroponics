using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.IntegrationTests.Helpers;
using IoT.Backend.Models;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests Coordinator Controller functionality including commands and reservoir operations.
/// </summary>
[Collection("Integration")]
public class CoordinatorIntegrationTests : IntegrationTestBase
{
    public CoordinatorIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Coordinator CRUD Tests

    [Fact]
    public async Task GetCoordinator_WhenExists_ShouldReturnCoordinator()
    {
        // Arrange
        var siteId = "test-site-coord";
        var coordId = "coord-get-001";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Living Room Coordinator",
            FwVersion = "1.0.0",
            TempC = 24.5f,
            WifiRssi = -55,
            TowersOnline = 3,
            LastSeen = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/coordinators/{siteId}/{coordId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Living Room Coordinator");
        content.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task GetCoordinator_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/coordinators/nonexistent-site/nonexistent-coord");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCoordinatorsByFarm_ShouldReturnAllInFarm()
    {
        // Arrange
        var farmId = "test-farm-coords";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertManyAsync(new[]
        {
            new Coordinator { Id = "coord-farm-1", CoordId = "coord-farm-1", FarmId = farmId, SiteId = farmId, Name = "Coord 1", LastSeen = DateTime.UtcNow },
            new Coordinator { Id = "coord-farm-2", CoordId = "coord-farm-2", FarmId = farmId, SiteId = farmId, Name = "Coord 2", LastSeen = DateTime.UtcNow },
            new Coordinator { Id = "coord-other", CoordId = "coord-other", FarmId = "other-farm", SiteId = "other-farm", Name = "Other", LastSeen = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/coordinators/farm/{farmId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Coord 1");
        content.Should().Contain("Coord 2");
        content.Should().NotContain("Other");
    }

    #endregion

    #region Reservoir Operations Tests

    [Fact]
    public async Task GetReservoirState_ShouldReturnCurrentState()
    {
        // Arrange
        var farmId = "test-farm-res";
        var coordId = "coord-reservoir";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            FarmId = farmId,
            SiteId = farmId,
            Name = "Reservoir Coordinator",
            Ph = 6.2f,
            EcMsCm = 1.5f,
            TdsPpm = 750f,
            WaterTempC = 22.0f,
            WaterLevelPct = 75f,
            MainPumpOn = true,
            DosingPumpPhOn = false,
            DosingPumpNutrientOn = false,
            LastSeen = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/coordinators/{farmId}/{coordId}/reservoir");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("6.2");
        content.Should().Contain("1.5");
        content.Should().Contain("75");
    }

    [Fact]
    public async Task SetReservoirTargets_ShouldPersistToDatabase()
    {
        // Arrange
        var farmId = "test-farm-targets";
        var coordId = "coord-targets";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            FarmId = farmId,
            SiteId = farmId,
            Name = "Target Coordinator",
            LastSeen = DateTime.UtcNow,
            Setpoints = new ReservoirSetpoints
            {
                PhTarget = 6.0f,
                PhTolerance = 0.3f,
                EcTarget = 1.5f,
                EcTolerance = 0.2f
            }
        });

        var targets = new
        {
            ph_min = 5.5f,
            ph_max = 6.5f,
            ec_min = 1.2f,
            ec_max = 1.8f
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/coordinators/{farmId}/{coordId}/reservoir/targets", targets);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ControlReservoirPump_ShouldPublishMqttCommand()
    {
        // Arrange
        var farmId = "test-farm-pump";
        var coordId = "coord-pump";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            FarmId = farmId,
            SiteId = farmId,
            Name = "Pump Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"farm/{farmId}/coord/{coordId}/reservoir/cmd";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var pumpRequest = new { on = true, duration_seconds = 300 };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/coordinators/{farmId}/{coordId}/reservoir/pump", pumpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Wait for MQTT message
        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("pump");
    }

    #endregion

    #region Command Tests

    [Fact]
    public async Task SendCommand_ShouldPublishToMqtt()
    {
        // Arrange
        var siteId = "test-site-cmd";
        var coordId = "coord-cmd";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Command Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var command = new { cmd = "identify", @params = new { duration = 5 } };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/coordinators/{siteId}/{coordId}/command", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.Topic.Should().Contain(coordId);
    }

    [Fact]
    public async Task RestartCoordinator_ShouldPublishRestartCommand()
    {
        // Arrange
        var siteId = "test-site-restart";
        var coordId = "coord-restart";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Restart Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        // Act
        var response = await HttpClient.PostAsync($"/api/coordinators/{siteId}/{coordId}/restart", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("restart");
    }

    [Fact]
    public async Task BroadcastToTowers_ShouldPublishBroadcastCommand()
    {
        // Arrange
        var siteId = "test-site-broadcast";
        var coordId = "coord-broadcast";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Broadcast Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"site/{siteId}/coord/{coordId}/broadcast";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var command = new { cmd = "led_test", @params = new { color = "green" } };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/coordinators/{siteId}/{coordId}/broadcast", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("led_test");
    }

    #endregion

    #region Pairing Mode Tests

    [Fact]
    public async Task EnterPairingMode_ShouldPublishPairingCommand()
    {
        // Arrange
        var siteId = "test-site-pair";
        var coordId = "coord-pair";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Pairing Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var pairRequest = new { duration_seconds = 120 };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/coordinators/{siteId}/{coordId}/pair", pairRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("pair");
    }

    [Fact]
    public async Task TriggerDiscovery_ShouldPublishDiscoveryCommand()
    {
        // Arrange
        var siteId = "test-site-discover";
        var coordId = "coord-discover";
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");

        await collection.InsertOneAsync(new Coordinator
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = siteId,
            Name = "Discovery Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        
        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        // Act
        var response = await HttpClient.PostAsync($"/api/coordinators/{siteId}/{coordId}/discover", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("discover");
    }

    #endregion
}
