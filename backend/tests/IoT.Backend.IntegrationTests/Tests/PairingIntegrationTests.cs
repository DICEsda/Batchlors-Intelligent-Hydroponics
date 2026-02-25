using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.IntegrationTests.Helpers;
using IoT.Backend.Models;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests the complete tower pairing workflow:
/// API (start pairing) → MQTT (command to coordinator) → MQTT (tower request) → 
/// API (approve) → MQTT (approval command) → DB (tower created)
/// </summary>
[Collection("Integration")]
public class PairingIntegrationTests : IntegrationTestBase
{
    private MqttTestClient? _mqttClient;

    public PairingIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mqttClient = new MqttTestClient(Output);
        await _mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        
        // Subscribe to command topics to verify MQTT commands are sent
        await _mqttClient.SubscribeAsync("farm/+/coord/+/cmd");
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
    public async Task StartPairingSession_ShouldSendMqttCommand()
    {
        // Arrange
        var farmId = "test-farm";
        var coordId = "coord-001";
        var request = new { duration_s = 60 };

        await Task.Delay(2000); // Wait for MQTT to be ready

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 60 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify MQTT command was sent
        var mqttMessage = await _mqttClient!.WaitForMessageOnTopicAsync(
            $"farm/{farmId}/coord/{coordId}/cmd",
            TimeSpan.FromSeconds(5));

        mqttMessage.Should().NotBeNull("start_pairing command should be sent via MQTT");
        mqttMessage!.PayloadString.Should().Contain("start_pairing");
    }

    [Fact]
    public async Task StopPairingSession_ShouldSendStopCommand()
    {
        // Arrange
        var farmId = "test-farm-stop";
        var coordId = "coord-stop";

        await Task.Delay(2000);

        // Start a session first
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await _mqttClient!.ClearMessagesAsync();

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            "/api/pairing/stop",
            new { farm_id = farmId, coord_id = coordId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var mqttMessage = await _mqttClient.WaitForMessageOnTopicAsync(
            $"farm/{farmId}/coord/{coordId}/cmd",
            TimeSpan.FromSeconds(5));

        mqttMessage.Should().NotBeNull("stop_pairing command should be sent");
        mqttMessage!.PayloadString.Should().Contain("stop_pairing");
    }

    [Fact]
    public async Task GetActiveSession_WhenSessionExists_ShouldReturnSession()
    {
        // Arrange
        var farmId = "test-farm-get";
        var coordId = "coord-get";

        await Task.Delay(2000);

        // Start a session
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/pairing/session/{farmId}/{coordId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("active");
        content.Should().Contain(farmId);
        content.Should().Contain(coordId);
    }

    [Fact]
    public async Task GetActiveSession_WhenNoSession_ShouldReturnNotFound()
    {
        // Arrange
        var farmId = "nonexistent-farm";
        var coordId = "nonexistent-coord";

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/pairing/session/{farmId}/{coordId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PairingRequest_WhenSessionActive_ShouldBeRecorded()
    {
        // Arrange
        var farmId = "test-farm-req";
        var coordId = "coord-req";
        var towerId = "tower-new-001";
        var macAddress = "AA:BB:CC:DD:EE:01";

        await Task.Delay(2000);

        // Start pairing session
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        // Act - Simulate tower sending pairing request via MQTT
        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress);

        await Task.Delay(1000);

        // Assert - Check pending requests via API
        var response = await HttpClient.GetAsync(
            $"/api/pairing/requests/{farmId}/{coordId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(towerId);
        content.Should().Contain(macAddress);
    }

    [Fact]
    public async Task ApprovePairingRequest_ShouldCreateTowerAndSendCommand()
    {
        // Arrange
        var farmId = "test-farm-approve";
        var coordId = "coord-approve";
        var towerId = "tower-approved";
        var macAddress = "AA:BB:CC:DD:EE:02";

        await Task.Delay(2000);

        // Start session and submit request
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress);
        await Task.Delay(1000);

        await _mqttClient.ClearMessagesAsync();

        // Act - Approve the request
        var response = await HttpClient.PostAsJsonAsync(
            "/api/pairing/approve",
            new { farm_id = farmId, coord_id = coordId, tower_id = towerId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify approval command was sent via MQTT
        var mqttMessage = await _mqttClient.WaitForMessageOnTopicAsync(
            $"farm/{farmId}/coord/{coordId}/cmd",
            TimeSpan.FromSeconds(5));

        mqttMessage.Should().NotBeNull("approve_pairing command should be sent");
        mqttMessage!.PayloadString.Should().Contain("approve_pairing");
        mqttMessage.PayloadString.Should().Contain(towerId);

        // Verify tower was created in database
        var collection = MongoDb.GetCollection<Tower>("towers");
        var tower = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .FirstOrDefaultAsync();

        tower.Should().NotBeNull("tower should be created in database");
        tower!.CoordId.Should().Be(coordId);
        tower.MacAddress.Should().Be(macAddress);
        tower.StatusMode.Should().Be("pairing");
    }

    [Fact]
    public async Task RejectPairingRequest_ShouldSendRejectCommand()
    {
        // Arrange
        var farmId = "test-farm-reject";
        var coordId = "coord-reject";
        var towerId = "tower-rejected";
        var macAddress = "AA:BB:CC:DD:EE:03";

        await Task.Delay(2000);

        // Start session and submit request
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress);
        await Task.Delay(1000);

        await _mqttClient.ClearMessagesAsync();

        // Act - Reject the request
        var response = await HttpClient.PostAsJsonAsync(
            "/api/pairing/reject",
            new { farm_id = farmId, coord_id = coordId, tower_id = towerId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var mqttMessage = await _mqttClient.WaitForMessageOnTopicAsync(
            $"farm/{farmId}/coord/{coordId}/cmd",
            TimeSpan.FromSeconds(5));

        mqttMessage.Should().NotBeNull("reject_pairing command should be sent");
        mqttMessage!.PayloadString.Should().Contain("reject_pairing");

        // Verify tower was NOT created
        var collection = MongoDb.GetCollection<Tower>("towers");
        var tower = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .FirstOrDefaultAsync();

        tower.Should().BeNull("rejected tower should not be created");
    }

    [Fact]
    public async Task PairingComplete_WhenSuccessful_ShouldUpdateTowerStatus()
    {
        // Arrange
        var farmId = "test-farm-complete";
        var coordId = "coord-complete";
        var towerId = "tower-completed";
        var macAddress = "AA:BB:CC:DD:EE:04";

        await Task.Delay(2000);

        // Start session, submit and approve request
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress);
        await Task.Delay(1000);

        await HttpClient.PostAsJsonAsync(
            "/api/pairing/approve",
            new { farm_id = farmId, coord_id = coordId, tower_id = towerId });

        await Task.Delay(500);

        // Act - Simulate coordinator sending pairing complete
        await _mqttClient.PublishPairingCompleteAsync(farmId, coordId, towerId, success: true);

        await Task.Delay(1000);

        // Assert - Tower status should be updated to operational
        var collection = MongoDb.GetCollection<Tower>("towers");
        var tower = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .FirstOrDefaultAsync();

        tower.Should().NotBeNull();
        tower!.StatusMode.Should().Be("operational");
    }

    [Fact]
    public async Task ForgetDevice_ShouldDeleteTowerAndSendCommand()
    {
        // Arrange
        var farmId = "test-farm-forget";
        var coordId = "coord-forget";
        var towerId = "tower-forget";
        var macAddress = "AA:BB:CC:DD:EE:05";

        await Task.Delay(2000);

        // Create a tower through pairing flow
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress);
        await Task.Delay(1000);

        await HttpClient.PostAsJsonAsync(
            "/api/pairing/approve",
            new { farm_id = farmId, coord_id = coordId, tower_id = towerId });

        await Task.Delay(500);

        await _mqttClient.ClearMessagesAsync();

        // Act - Forget the device
        var response = await HttpClient.PostAsJsonAsync(
            "/api/pairing/forget",
            new { farm_id = farmId, coord_id = coordId, tower_id = towerId });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify forget command was sent
        var mqttMessage = await _mqttClient.WaitForMessageOnTopicAsync(
            $"farm/{farmId}/coord/{coordId}/cmd",
            TimeSpan.FromSeconds(5));

        mqttMessage.Should().NotBeNull("forget_device command should be sent");
        mqttMessage!.PayloadString.Should().Contain("forget_device");

        // Verify tower was deleted
        var collection = MongoDb.GetCollection<Tower>("towers");
        var tower = await collection
            .Find(t => t.TowerId == towerId && t.FarmId == farmId)
            .FirstOrDefaultAsync();

        tower.Should().BeNull("tower should be deleted from database");
    }

    [Fact]
    public async Task DuplicatePairingRequest_ShouldUpdateExisting()
    {
        // Arrange
        var farmId = "test-farm-dup";
        var coordId = "coord-dup";
        var towerId = "tower-dup";
        var macAddress = "AA:BB:CC:DD:EE:06";

        await Task.Delay(2000);

        // Start session
        await HttpClient.PostAsJsonAsync(
            "/api/pairing/start",
            new { farm_id = farmId, coord_id = coordId, duration_s = 120 });

        await Task.Delay(500);

        // Act - Send two requests from same tower
        await _mqttClient!.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress, rssi: -60);
        await Task.Delay(500);
        await _mqttClient.PublishPairingRequestAsync(farmId, coordId, towerId, macAddress, rssi: -55);
        await Task.Delay(1000);

        // Assert - Should only have one pending request
        var response = await HttpClient.GetAsync(
            $"/api/pairing/requests/{farmId}/{coordId}");

        var content = await response.Content.ReadAsStringAsync();
        
        // Count occurrences of tower_id
        var occurrences = content.Split(towerId).Length - 1;
        occurrences.Should().Be(1, "duplicate request should update existing");
    }
}
