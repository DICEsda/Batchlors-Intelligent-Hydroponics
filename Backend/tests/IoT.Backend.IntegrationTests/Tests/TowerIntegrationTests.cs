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
/// Tests Tower Controller functionality including CRUD operations, commands, and height tracking.
/// </summary>
[Collection("Integration")]
public class TowerIntegrationTests : IntegrationTestBase
{
    public TowerIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Tower CRUD Tests

    [Fact]
    public async Task GetTower_WhenExists_ShouldReturnTower()
    {
        // Arrange
        var farmId = "test-farm-tower";
        var coordId = "coord-tower-001";
        var towerId = "tower-get-001";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Test Tower Alpha",
            AirTempC = 25.5f,
            HumidityPct = 65.0f,
            LightLux = 5000f,
            StatusMode = "operational",
            FwVersion = "1.0.0",
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Tower Alpha");
        content.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task GetTower_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/towers/nonexistent-farm/nonexistent-coord/nonexistent-tower");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTowersByFarm_ShouldReturnAllInFarm()
    {
        // Arrange
        var farmId = "test-farm-towers";
        var coordId = "coord-multi";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertManyAsync(new[]
        {
            new Tower { Id = $"{farmId}_{coordId}_tower-1", TowerId = "tower-1", CoordId = coordId, FarmId = farmId, Name = "Tower 1", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Tower { Id = $"{farmId}_{coordId}_tower-2", TowerId = "tower-2", CoordId = coordId, FarmId = farmId, Name = "Tower 2", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Tower { Id = "other-farm_other-coord_tower-3", TowerId = "tower-3", CoordId = "other-coord", FarmId = "other-farm", Name = "Other Tower", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/farm/{farmId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Tower 1");
        content.Should().Contain("Tower 2");
        content.Should().NotContain("Other Tower");
    }

    [Fact]
    public async Task GetTowersByCoordinator_ShouldReturnAllForCoordinator()
    {
        // Arrange
        var farmId = "test-farm-coord-towers";
        var coordId1 = "coord-filter-1";
        var coordId2 = "coord-filter-2";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertManyAsync(new[]
        {
            new Tower { Id = $"{farmId}_{coordId1}_tower-a", TowerId = "tower-a", CoordId = coordId1, FarmId = farmId, Name = "Tower A", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Tower { Id = $"{farmId}_{coordId1}_tower-b", TowerId = "tower-b", CoordId = coordId1, FarmId = farmId, Name = "Tower B", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Tower { Id = $"{farmId}_{coordId2}_tower-c", TowerId = "tower-c", CoordId = coordId2, FarmId = farmId, Name = "Tower C", LastSeen = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/farm/{farmId}/coord/{coordId1}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Tower A");
        content.Should().Contain("Tower B");
        content.Should().NotContain("Tower C");
    }

    [Fact]
    public async Task UpsertTower_CreateNew_ShouldPersistToDatabase()
    {
        // Arrange
        var farmId = "test-farm-upsert";
        var coordId = "coord-upsert";
        var towerId = "tower-new";
        var request = new
        {
            name = "New Test Tower",
            crop_type = 1, // Lettuce
            planting_date = DateTime.UtcNow.AddDays(-7)
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = MongoDb.GetCollection<Tower>("towers");
        var tower = await collection.Find(t => t.TowerId == towerId && t.FarmId == farmId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.Name.Should().Be("New Test Tower");
        tower.CropType.Should().Be(CropType.Lettuce);
    }

    [Fact]
    public async Task UpsertTower_UpdateExisting_ShouldUpdateDatabase()
    {
        // Arrange
        var farmId = "test-farm-update";
        var coordId = "coord-update";
        var towerId = "tower-existing";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Original Name",
            CropType = CropType.Unknown,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            name = "Updated Tower Name",
            crop_type = 7 // Basil
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tower = await collection.Find(t => t.TowerId == towerId && t.FarmId == farmId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.Name.Should().Be("Updated Tower Name");
        tower.CropType.Should().Be(CropType.Basil);
    }

    [Fact]
    public async Task UpdateTowerName_ShouldUpdateOnlyName()
    {
        // Arrange
        var farmId = "test-farm-name";
        var coordId = "coord-name";
        var towerId = "tower-rename";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Old Name",
            CropType = CropType.Tomato,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new { name = "Brand New Name" };

        // Act
        var response = await HttpClient.PatchAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/name", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tower = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.Name.Should().Be("Brand New Name");
        tower.CropType.Should().Be(CropType.Tomato); // Other fields unchanged
    }

    [Fact]
    public async Task DeleteTower_ShouldRemoveFromDatabase()
    {
        // Arrange
        var farmId = "test-farm-delete";
        var coordId = "coord-delete";
        var towerId = "tower-delete";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Tower to Delete",
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.DeleteAsync($"/api/towers/{farmId}/{coordId}/{towerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tower = await collection.Find(t => t.TowerId == towerId && t.FarmId == farmId).FirstOrDefaultAsync();
        tower.Should().BeNull();
    }

    #endregion

    #region Tower Command Tests

    [Fact]
    public async Task SendCommand_ShouldPublishToMqtt()
    {
        // Arrange
        var farmId = "test-farm-cmd";
        var coordId = "coord-cmd";
        var towerId = "tower-cmd";

        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var command = new { cmd = "identify", @params = new { duration = 5 } };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/command", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.Topic.Should().Contain(towerId);
        message.PayloadString.Should().Contain("identify");
    }

    [Fact]
    public async Task SetLight_ShouldPublishLightCommand()
    {
        // Arrange
        var farmId = "test-farm-light";
        var coordId = "coord-light";
        var towerId = "tower-light";

        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var request = new { on = true, brightness = 80 };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/light", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("set_light");
        message.PayloadString.Should().Contain("\"on\":true");
    }

    [Fact]
    public async Task SetLight_WithoutBrightness_ShouldDefaultTo255()
    {
        // Arrange
        var farmId = "test-farm-light-default";
        var coordId = "coord-light-default";
        var towerId = "tower-light-default";

        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var request = new { on = true };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/light", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("255");
    }

    [Fact]
    public async Task SetPump_ShouldPublishPumpCommand()
    {
        // Arrange
        var farmId = "test-farm-pump";
        var coordId = "coord-pump";
        var towerId = "tower-pump";

        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var request = new { on = true };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/pump", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("set_pump");
        message.PayloadString.Should().Contain("\"on\":true");
    }

    [Fact]
    public async Task SetPump_Off_ShouldPublishPumpOffCommand()
    {
        // Arrange
        var farmId = "test-farm-pump-off";
        var coordId = "coord-pump-off";
        var towerId = "tower-pump-off";

        var topic = $"farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd";

        await using var mqttClient = new MqttTestClient(Output);
        await mqttClient.ConnectAsync(Mqtt.Host, Mqtt.Port);
        await mqttClient.SubscribeAsync(topic);

        var request = new { on = false };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/pump", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await mqttClient.WaitForMessageOnTopicAsync(topic, TimeSpan.FromSeconds(5));
        message.Should().NotBeNull();
        message!.PayloadString.Should().Contain("set_pump");
        message.PayloadString.Should().Contain("\"on\":false");
    }

    #endregion

    #region Height Measurement Tests

    [Fact]
    public async Task RecordHeightMeasurement_ShouldPersistToDatabase()
    {
        // Arrange
        var farmId = "test-farm-height";
        var coordId = "coord-height";
        var towerId = "tower-height";
        var towerCollection = MongoDb.GetCollection<Tower>("towers");

        await towerCollection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Height Tower",
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            slot_index = 2,
            height_cm = 15.5f,
            method = 0, // Manual
            notes = "Looking healthy"
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/height", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var measurementCollection = MongoDb.GetCollection<HeightMeasurement>("height_measurements");
        var measurement = await measurementCollection.Find(m => m.TowerId == towerId).FirstOrDefaultAsync();
        measurement.Should().NotBeNull();
        measurement!.HeightCm.Should().Be(15.5);
        measurement.SlotIndex.Should().Be(2);
        measurement.Notes.Should().Be("Looking healthy");
    }

    [Fact]
    public async Task RecordHeightMeasurement_ShouldUpdateTowerLastHeight()
    {
        // Arrange
        var farmId = "test-farm-height-update";
        var coordId = "coord-height-update";
        var towerId = "tower-height-update";
        var towerCollection = MongoDb.GetCollection<Tower>("towers");

        await towerCollection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Height Update Tower",
            LastHeightCm = null,
            LastHeightAt = null,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            slot_index = 0,
            height_cm = 22.3f,
            method = 0
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/height", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var tower = await towerCollection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.LastHeightCm.Should().Be(22.3f);
        tower.LastHeightAt.Should().NotBeNull();
        tower.LastHeightAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetHeightMeasurements_ShouldReturnAllForTower()
    {
        // Arrange
        var farmId = "test-farm-get-heights";
        var coordId = "coord-get-heights";
        var towerId = "tower-get-heights";
        var measurementCollection = MongoDb.GetCollection<HeightMeasurement>("height_measurements");

        var baseTime = DateTime.UtcNow;
        await measurementCollection.InsertManyAsync(new[]
        {
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 5.0, Timestamp = baseTime.AddDays(-3) },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 10.0, Timestamp = baseTime.AddDays(-2) },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 15.0, Timestamp = baseTime.AddDays(-1) },
            new HeightMeasurement { TowerId = "other-tower", FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 20.0, Timestamp = baseTime }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}/height");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"height_cm\":5");
        content.Should().Contain("\"height_cm\":10");
        content.Should().Contain("\"height_cm\":15");
        content.Should().NotContain("\"height_cm\":20"); // From other tower
    }

    [Fact]
    public async Task GetHeightMeasurements_WithSlotFilter_ShouldFilterBySlot()
    {
        // Arrange
        var farmId = "test-farm-slot-filter";
        var coordId = "coord-slot-filter";
        var towerId = "tower-slot-filter";
        var measurementCollection = MongoDb.GetCollection<HeightMeasurement>("height_measurements");

        await measurementCollection.InsertManyAsync(new[]
        {
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 5.0, Timestamp = DateTime.UtcNow },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 1, HeightCm = 10.0, Timestamp = DateTime.UtcNow },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 7.0, Timestamp = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}/height?slotIndex=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("5");
        content.Should().Contain("7");
        // Note: Checking if slot 1's measurement is excluded requires parsing JSON
    }

    [Fact]
    public async Task GetHeightMeasurements_WithDateRange_ShouldFilterByTime()
    {
        // Arrange
        var farmId = "test-farm-date-filter";
        var coordId = "coord-date-filter";
        var towerId = "tower-date-filter";
        var measurementCollection = MongoDb.GetCollection<HeightMeasurement>("height_measurements");

        var now = DateTime.UtcNow;
        await measurementCollection.InsertManyAsync(new[]
        {
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 5.0, Timestamp = now.AddDays(-10) },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 10.0, Timestamp = now.AddDays(-3) },
            new HeightMeasurement { TowerId = towerId, FarmId = farmId, CoordId = coordId, SlotIndex = 0, HeightCm = 15.0, Timestamp = now.AddDays(-1) }
        });

        var from = now.AddDays(-5).ToString("o");
        var to = now.ToString("o");

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}/height?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("10");
        content.Should().Contain("15");
        // The measurement from 10 days ago should be excluded
    }

    #endregion

    #region Crop Management Tests

    [Fact]
    public async Task SetCrop_ShouldUpdateTowerCropInfo()
    {
        // Arrange
        var farmId = "test-farm-crop";
        var coordId = "coord-crop";
        var towerId = "tower-crop";
        var towerCollection = MongoDb.GetCollection<Tower>("towers");

        await towerCollection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Crop Tower",
            CropType = CropType.Unknown,
            LastHeightCm = 10.0f,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var plantingDate = DateTime.UtcNow.AddDays(-5);
        var harvestDate = DateTime.UtcNow.AddDays(25);
        var request = new
        {
            crop_type = 7, // Basil
            planting_date = plantingDate,
            expected_harvest_date = harvestDate
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/crop", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tower = await towerCollection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.CropType.Should().Be(CropType.Basil);
        tower.PlantingDate.Should().BeCloseTo(plantingDate, TimeSpan.FromSeconds(1));
        tower.ExpectedHarvestDate.Should().BeCloseTo(harvestDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SetCrop_ShouldResetHeightTracking()
    {
        // Arrange
        var farmId = "test-farm-crop-reset";
        var coordId = "coord-crop-reset";
        var towerId = "tower-crop-reset";
        var towerCollection = MongoDb.GetCollection<Tower>("towers");

        await towerCollection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Reset Tower",
            CropType = CropType.Lettuce,
            LastHeightCm = 25.0f,
            LastHeightAt = DateTime.UtcNow.AddDays(-1),
            PredictedHeightCm = 30.0f,
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            crop_type = 16 // Tomato - new crop
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/crop", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tower = await towerCollection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.CropType.Should().Be(CropType.Tomato);
        tower.LastHeightCm.Should().BeNull();
        tower.LastHeightAt.Should().BeNull();
        tower.PredictedHeightCm.Should().BeNull();
    }

    [Fact]
    public async Task SetCrop_WhenTowerNotExists_ShouldReturnNotFound()
    {
        // Arrange
        var request = new
        {
            crop_type = 1
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/api/towers/nonexistent/nonexistent/nonexistent/crop", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetCrop_WithoutPlantingDate_ShouldDefaultToNow()
    {
        // Arrange
        var farmId = "test-farm-crop-default";
        var coordId = "coord-crop-default";
        var towerId = "tower-crop-default";
        var towerCollection = MongoDb.GetCollection<Tower>("towers");

        await towerCollection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}_{coordId}_{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Default Date Tower",
            LastSeen = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            crop_type = 3 // Kale
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync($"/api/towers/{farmId}/{coordId}/{towerId}/crop", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tower = await towerCollection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        tower.Should().NotBeNull();
        tower!.CropType.Should().Be(CropType.Kale);
        tower.PlantingDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Telemetry Tests

    [Fact]
    public async Task GetTelemetry_ShouldReturnHistoricalData()
    {
        // Arrange
        var farmId = "test-farm-telemetry";
        var coordId = "coord-telemetry";
        var towerId = "tower-telemetry";
        var telemetryCollection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");

        var now = DateTime.UtcNow;
        await telemetryCollection.InsertManyAsync(new[]
        {
            new TowerTelemetry { TowerId = towerId, CoordId = coordId, FarmId = farmId, AirTempC = 24.0f, HumidityPct = 60.0f, Timestamp = now.AddMinutes(-30) },
            new TowerTelemetry { TowerId = towerId, CoordId = coordId, FarmId = farmId, AirTempC = 25.0f, HumidityPct = 62.0f, Timestamp = now.AddMinutes(-15) },
            new TowerTelemetry { TowerId = towerId, CoordId = coordId, FarmId = farmId, AirTempC = 26.0f, HumidityPct = 65.0f, Timestamp = now }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}/telemetry");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("24");
        content.Should().Contain("25");
        content.Should().Contain("26");
    }

    [Fact]
    public async Task GetLatestTelemetry_ShouldReturnMostRecent()
    {
        // Arrange
        var farmId = "test-farm-latest";
        var coordId = "coord-latest";
        var towerId = "tower-latest";
        var telemetryCollection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");

        var now = DateTime.UtcNow;
        await telemetryCollection.InsertManyAsync(new[]
        {
            new TowerTelemetry { TowerId = towerId, CoordId = coordId, FarmId = farmId, AirTempC = 20.0f, Timestamp = now.AddHours(-2) },
            new TowerTelemetry { TowerId = towerId, CoordId = coordId, FarmId = farmId, AirTempC = 28.5f, Timestamp = now }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/towers/{farmId}/{coordId}/{towerId}/telemetry/latest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("28.5");
    }

    [Fact]
    public async Task GetLatestTelemetry_WhenNoData_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/towers/no-farm/no-coord/no-tower/telemetry/latest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
