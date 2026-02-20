using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.Models;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests REST API CRUD operations for Towers, Coordinators, and Zones.
/// </summary>
[Collection("Integration")]
public class ApiCrudTests : IntegrationTestBase
{
    public ApiCrudTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Tower API Tests

    [Fact]
    public async Task CreateTower_ShouldPersistToDatabase()
    {
        // Arrange
        var farmId = "test-farm";
        var coordId = "coord-001";
        var towerId = "tower-create-001";

        var tower = new
        {
            tower_id = towerId,
            coord_id = coordId,
            farm_id = farmId,
            name = "Test Tower 1",
            air_temp_c = 25.0f,
            humidity_pct = 65.0f,
            status_mode = "operational"
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync(
            $"/api/towers/{farmId}/{coordId}/{towerId}",
            tower);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = MongoDb.GetCollection<Tower>("towers");
        var created = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Test Tower 1");
        created.FarmId.Should().Be(farmId);
    }

    [Fact]
    public async Task GetTower_WhenExists_ShouldReturnTower()
    {
        // Arrange
        var farmId = "test-farm-get";
        var coordId = "coord-get";
        var towerId = "tower-get-001";

        // Insert directly to DB
        var collection = MongoDb.GetCollection<Tower>("towers");
        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}/{coordId}/{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Get Test Tower",
            AirTempC = 26.5f,
            HumidityPct = 70.0f,
            StatusMode = "operational",
            CreatedAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/towers/{farmId}/{coordId}/{towerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Get Test Tower");
        content.Should().Contain("26.5");
    }

    [Fact]
    public async Task GetTower_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync(
            "/api/towers/nonexistent/nonexistent/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTowersByFarm_ShouldReturnAllTowersInFarm()
    {
        // Arrange
        var farmId = "test-farm-list";
        var coordId = "coord-list";
        var collection = MongoDb.GetCollection<Tower>("towers");

        await collection.InsertManyAsync(new[]
        {
            new Tower { Id = $"{farmId}/{coordId}/t1", TowerId = "t1", CoordId = coordId, FarmId = farmId, Name = "Tower 1", CreatedAt = DateTime.UtcNow },
            new Tower { Id = $"{farmId}/{coordId}/t2", TowerId = "t2", CoordId = coordId, FarmId = farmId, Name = "Tower 2", CreatedAt = DateTime.UtcNow },
            new Tower { Id = "other/other/t3", TowerId = "t3", CoordId = "other", FarmId = "other", Name = "Other Tower", CreatedAt = DateTime.UtcNow }
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
    public async Task UpdateTowerName_ShouldPersist()
    {
        // Arrange
        var farmId = "test-farm-rename";
        var coordId = "coord-rename";
        var towerId = "tower-rename";

        var collection = MongoDb.GetCollection<Tower>("towers");
        await collection.InsertOneAsync(new Tower
        {
            Id = $"{farmId}/{coordId}/{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "Old Name",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/towers/{farmId}/{coordId}/{towerId}/name",
            new { name = "New Name" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        updated!.Name.Should().Be("New Name");
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
            Id = $"{farmId}/{coordId}/{towerId}",
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            Name = "To Delete",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.DeleteAsync(
            $"/api/towers/{farmId}/{coordId}/{towerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleted = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        deleted.Should().BeNull();
    }

    #endregion

    #region Zone API Tests

    [Fact]
    public async Task CreateZone_ShouldPersist()
    {
        // Arrange
        var siteId = "test-site-create";
        var coordId = "coord-for-zone";

        // First, insert a coordinator that the zone will reference
        // Repository queries by _id = coordId AND site_id = siteId
        var coordCollection = MongoDb.GetCollection<Coordinator>("coordinators");
        await coordCollection.InsertOneAsync(new Coordinator
        {
            Id = coordId,  // _id must match the coordId, not a composite key
            CoordId = coordId,
            SiteId = siteId,
            Name = "Test Coordinator",
            LastSeen = DateTime.UtcNow
        });

        var zone = new
        {
            name = "Living Room",
            site_id = siteId,
            coordinator_id = coordId,
            description = "Main living area",
            color = "#3B82F6"
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/api/zones", zone);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var collection = MongoDb.GetCollection<Zone>("zones");
        var created = await collection.Find(z => z.Name == "Living Room").FirstOrDefaultAsync();

        created.Should().NotBeNull();
        created!.SiteId.Should().Be(siteId);
        created.Description.Should().Be("Main living area");
        created.CoordinatorId.Should().Be(coordId);
    }

    [Fact]
    public async Task GetZonesBySite_ShouldReturnZones()
    {
        // Arrange
        var siteId = "test-site-zones";
        var collection = MongoDb.GetCollection<Zone>("zones");

        await collection.InsertManyAsync(new[]
        {
            new Zone { Id = "z1", Name = "Kitchen", SiteId = siteId, CreatedAt = DateTime.UtcNow },
            new Zone { Id = "z2", Name = "Bedroom", SiteId = siteId, CreatedAt = DateTime.UtcNow },
            new Zone { Id = "z3", Name = "Other", SiteId = "other-site", CreatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/zones/site/{siteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Kitchen");
        content.Should().Contain("Bedroom");
        content.Should().NotContain("Other");
    }

    [Fact]
    public async Task UpdateZone_ShouldPersist()
    {
        // Arrange
        var zoneId = "zone-update-test";
        var collection = MongoDb.GetCollection<Zone>("zones");

        await collection.InsertOneAsync(new Zone
        {
            Id = zoneId,
            Name = "Old Zone Name",
            SiteId = "test-site",
            CreatedAt = DateTime.UtcNow
        });

        var update = new
        {
            name = "Updated Zone Name",
            site_id = "test-site",
            description = "New description",
            color = "#EF4444"
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/zones/{zoneId}", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await collection.Find(z => z.Id == zoneId).FirstOrDefaultAsync();
        updated!.Name.Should().Be("Updated Zone Name");
        updated.Description.Should().Be("New description");
    }

    [Fact]
    public async Task DeleteZone_ShouldRemove()
    {
        // Arrange
        var zoneId = "zone-delete-test";
        var collection = MongoDb.GetCollection<Zone>("zones");

        await collection.InsertOneAsync(new Zone
        {
            Id = zoneId,
            Name = "To Delete",
            SiteId = "test-site",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.DeleteAsync($"/api/zones/{zoneId}");

        // Assert - API returns Ok with success message, not NoContent
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await collection.Find(z => z.Id == zoneId).FirstOrDefaultAsync();
        deleted.Should().BeNull();
    }

    #endregion

    #region Telemetry API Tests

    [Fact]
    public async Task GetTowerTelemetry_ShouldReturnHistoricalData()
    {
        // Arrange
        var farmId = "test-farm-tel";
        var coordId = "coord-tel";
        var towerId = "tower-tel";

        var collection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync(new[]
        {
            new TowerTelemetry { FarmId = farmId, CoordId = coordId, TowerId = towerId, Timestamp = now.AddHours(-2), AirTempC = 24.0f },
            new TowerTelemetry { FarmId = farmId, CoordId = coordId, TowerId = towerId, Timestamp = now.AddHours(-1), AirTempC = 25.0f },
            new TowerTelemetry { FarmId = farmId, CoordId = coordId, TowerId = towerId, Timestamp = now, AirTempC = 26.0f }
        });

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/telemetry/tower/{farmId}/{coordId}/{towerId}?hours=24");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("24");
        content.Should().Contain("25");
        content.Should().Contain("26");
    }

    [Fact]
    public async Task GetLatestTowerTelemetry_ShouldReturnMostRecent()
    {
        // Arrange
        var farmId = "test-farm-latest";
        var coordId = "coord-latest";
        var towerId = "tower-latest";

        var collection = MongoDb.GetCollection<TowerTelemetry>("tower_telemetry");
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync(new[]
        {
            new TowerTelemetry { FarmId = farmId, CoordId = coordId, TowerId = towerId, Timestamp = now.AddHours(-1), AirTempC = 20.0f },
            new TowerTelemetry { FarmId = farmId, CoordId = coordId, TowerId = towerId, Timestamp = now, AirTempC = 28.5f }
        });

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/telemetry/tower/{farmId}/{coordId}/{towerId}/latest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("28.5");
    }

    [Fact]
    public async Task GetReservoirTelemetry_ShouldReturnHistoricalData()
    {
        // Arrange
        var farmId = "test-farm-res";
        var coordId = "coord-res";

        var collection = MongoDb.GetCollection<ReservoirTelemetry>("reservoir_telemetry");
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync(new[]
        {
            new ReservoirTelemetry { FarmId = farmId, CoordId = coordId, Timestamp = now.AddHours(-1), Ph = 6.0f, EcMsCm = 1.5f },
            new ReservoirTelemetry { FarmId = farmId, CoordId = coordId, Timestamp = now, Ph = 6.2f, EcMsCm = 1.6f }
        });

        // Act
        var response = await HttpClient.GetAsync(
            $"/api/telemetry/reservoir/{farmId}/{coordId}?hours=24");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        // Use regex or partial match - JSON may serialize 6.0f as "6" or "6.0"
        content.Should().Contain("\"ph\":");
        content.Should().Contain("6.2");
    }

    #endregion

    #region Health API Tests

    [Fact]
    public async Task HealthLive_ShouldReturnHealthy()
    {
        // Act
        var response = await HttpClient.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_ShouldReturnHealthy()
    {
        // Act
        var response = await HttpClient.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
