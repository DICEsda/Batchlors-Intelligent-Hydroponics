using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.Models.DigitalTwin;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests Digital Twin Controller functionality for tower and coordinator state management.
/// </summary>
[Collection("Integration")]
public class TwinIntegrationTests : IntegrationTestBase
{
    public TwinIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Tower Twin Tests

    [Fact]
    public async Task GetTowerTwin_WhenExists_ShouldReturnTwin()
    {
        // Arrange
        var towerId = "tower-twin-get";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertOneAsync(new TowerTwin
        {
            Id = towerId,
            TowerId = towerId,
            CoordId = "coord-001",
            FarmId = "farm-001",
            Name = "Test Tower Twin",
            Reported = new TowerReportedState
            {
                AirTempC = 25.5f,
                HumidityPct = 65.0f,
                LightOn = true,
                LightBrightness = 200,
                PumpOn = false,
                StatusMode = "operational",
                FwVersion = "1.2.0"
            },
            Desired = new TowerDesiredState
            {
                LightOn = true,
                LightBrightness = 200
            },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                Version = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/towers/{towerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Tower Twin");
        content.Should().Contain("1.2.0");
        content.Should().Contain("operational");
    }

    [Fact]
    public async Task GetTowerTwin_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/towers/nonexistent-tower");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTowerTwins_ShouldReturnAllForCoordinator()
    {
        // Arrange
        var farmId = "farm-twins-list";
        var coordId = "coord-twins-list";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertManyAsync(new[]
        {
            new TowerTwin { Id = "twin-list-1", TowerId = "twin-list-1", CoordId = coordId, FarmId = farmId, Name = "Twin 1", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } },
            new TowerTwin { Id = "twin-list-2", TowerId = "twin-list-2", CoordId = coordId, FarmId = farmId, Name = "Twin 2", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } },
            new TowerTwin { Id = "twin-other", TowerId = "twin-other", CoordId = "other-coord", FarmId = farmId, Name = "Other Twin", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/towers?farmId={farmId}&coordId={coordId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Twin 1");
        content.Should().Contain("Twin 2");
        content.Should().NotContain("Other Twin");
    }

    [Fact]
    public async Task GetTowerTwins_WithoutRequiredParams_ShouldReturnBadRequest()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/towers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetTowerDesiredState_ShouldPersistAndReturnAccepted()
    {
        // Arrange
        var towerId = "tower-desired";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertOneAsync(new TowerTwin
        {
            Id = towerId,
            TowerId = towerId,
            CoordId = "coord-desired",
            FarmId = "farm-desired",
            Reported = new TowerReportedState
            {
                LightOn = false,
                LightBrightness = 0,
                PumpOn = false
            },
            Desired = new TowerDesiredState(),
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        var desiredState = new
        {
            light_on = true,
            light_brightness = 180,
            pump_on = true
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/twins/towers/{towerId}/desired", desiredState);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("pending");

        // Verify database was updated
        var twin = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        twin.Should().NotBeNull();
        twin!.Desired.LightOn.Should().Be(true);
        twin.Desired.LightBrightness.Should().Be(180);
        twin.Desired.PumpOn.Should().Be(true);
    }

    [Fact]
    public async Task SetTowerDesiredState_WithNullBody_ShouldReturnBadRequest()
    {
        // Act
        var response = await HttpClient.PutAsJsonAsync<object?>("/api/twins/towers/some-tower/desired", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTowerStateDelta_WhenInSync_ShouldReturnIsInSyncTrue()
    {
        // Arrange
        var towerId = "tower-delta-sync";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertOneAsync(new TowerTwin
        {
            Id = towerId,
            TowerId = towerId,
            CoordId = "coord-delta",
            FarmId = "farm-delta",
            Reported = new TowerReportedState
            {
                LightOn = true,
                LightBrightness = 200,
                PumpOn = false
            },
            Desired = new TowerDesiredState
            {
                LightOn = true,
                LightBrightness = 200,
                PumpOn = false
            },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/towers/{towerId}/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"is_in_sync\":true");
    }

    [Fact]
    public async Task GetTowerStateDelta_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/towers/nonexistent/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkTowerSyncSuccess_ShouldUpdateSyncStatus()
    {
        // Arrange
        var towerId = "tower-sync-success";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertOneAsync(new TowerTwin
        {
            Id = towerId,
            TowerId = towerId,
            CoordId = "coord-sync",
            FarmId = "farm-sync",
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.Pending,
                SyncRetryCount = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.PostAsync($"/api/twins/towers/{towerId}/sync/success", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");

        // Verify database was updated
        var twin = await collection.Find(t => t.TowerId == towerId).FirstOrDefaultAsync();
        twin.Should().NotBeNull();
        twin!.Metadata.SyncStatus.Should().Be(SyncStatus.InSync);
        twin.Metadata.SyncRetryCount.Should().Be(0);
    }

    #endregion

    #region Coordinator Twin Tests

    [Fact]
    public async Task GetCoordinatorTwin_WhenExists_ShouldReturnTwin()
    {
        // Arrange
        var coordId = "coord-twin-get";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = "site-001",
            FarmId = "farm-001",
            Name = "Main Reservoir",
            Reported = new CoordinatorReportedState
            {
                Ph = 6.2f,
                EcMsCm = 1.5f,
                WaterTempC = 22.0f,
                WaterLevelPct = 75.0f,
                MainPumpOn = true,
                FwVersion = "2.0.0",
                TowersOnline = 4,
                StatusMode = "operational"
            },
            Desired = new CoordinatorDesiredState
            {
                MainPumpOn = true
            },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/coordinators/{coordId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Main Reservoir");
        content.Should().Contain("2.0.0");
        content.Should().Contain("6.2");
    }

    [Fact]
    public async Task GetCoordinatorTwin_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/coordinators/nonexistent-coord");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCoordinatorTwins_ShouldReturnAllForFarm()
    {
        // Arrange
        var farmId = "farm-coord-twins";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertManyAsync(new[]
        {
            new CoordinatorTwin { Id = "coord-farm-1", CoordId = "coord-farm-1", SiteId = farmId, FarmId = farmId, Name = "Coordinator 1", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } },
            new CoordinatorTwin { Id = "coord-farm-2", CoordId = "coord-farm-2", SiteId = farmId, FarmId = farmId, Name = "Coordinator 2", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } },
            new CoordinatorTwin { Id = "coord-other", CoordId = "coord-other", SiteId = "other-farm", FarmId = "other-farm", Name = "Other Coordinator", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/coordinators?farmId={farmId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Coordinator 1");
        content.Should().Contain("Coordinator 2");
        content.Should().NotContain("Other Coordinator");
    }

    [Fact]
    public async Task GetCoordinatorTwins_WithoutFarmId_ShouldReturnBadRequest()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/coordinators");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetCoordinatorDesiredState_ShouldPersistAndReturnAccepted()
    {
        // Arrange
        var coordId = "coord-desired-set";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = "site-desired",
            FarmId = "farm-desired",
            Reported = new CoordinatorReportedState
            {
                MainPumpOn = false,
                DosingPumpPhOn = false,
                DosingPumpNutrientOn = false
            },
            Desired = new CoordinatorDesiredState(),
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        var desiredState = new
        {
            main_pump_on = true,
            dosing_pump_ph_on = false,
            dosing_pump_nutrient_on = true,
            setpoints = new
            {
                ph_target = 6.5f,
                ph_tolerance = 0.2f,
                ec_target = 1.8f,
                ec_tolerance = 0.1f
            }
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/twins/coordinators/{coordId}/desired", desiredState);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("pending");

        // Verify database was updated
        var twin = await collection.Find(c => c.CoordId == coordId).FirstOrDefaultAsync();
        twin.Should().NotBeNull();
        twin!.Desired.MainPumpOn.Should().Be(true);
        twin.Desired.DosingPumpNutrientOn.Should().Be(true);
    }

    [Fact]
    public async Task GetCoordinatorStateDelta_WhenInSync_ShouldReturnIsInSyncTrue()
    {
        // Arrange
        var coordId = "coord-delta-sync";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = "site-delta",
            FarmId = "farm-delta",
            Reported = new CoordinatorReportedState
            {
                MainPumpOn = true,
                DosingPumpPhOn = false
            },
            Desired = new CoordinatorDesiredState
            {
                MainPumpOn = true,
                DosingPumpPhOn = false
            },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.InSync,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/coordinators/{coordId}/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"is_in_sync\":true");
    }

    [Fact]
    public async Task GetCoordinatorStateDelta_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/coordinators/nonexistent/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkCoordinatorSyncSuccess_ShouldUpdateSyncStatus()
    {
        // Arrange
        var coordId = "coord-sync-success";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = "site-sync",
            FarmId = "farm-sync",
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.Pending,
                SyncRetryCount = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.PostAsync($"/api/twins/coordinators/{coordId}/sync/success", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");

        // Verify database was updated
        var twin = await collection.Find(c => c.CoordId == coordId).FirstOrDefaultAsync();
        twin.Should().NotBeNull();
        twin!.Metadata.SyncStatus.Should().Be(SyncStatus.InSync);
    }

    #endregion

    #region Farm Overview Tests

    [Fact]
    public async Task GetFarmTwins_ShouldReturnAllCoordinatorsAndTowers()
    {
        // Arrange
        var farmId = "farm-overview";
        var coordId = "coord-overview";
        var coordCollection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");
        var towerCollection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await coordCollection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = farmId,
            FarmId = farmId,
            Name = "Main Coordinator",
            Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });

        await towerCollection.InsertManyAsync(new[]
        {
            new TowerTwin { Id = "tower-ov-1", TowerId = "tower-ov-1", CoordId = coordId, FarmId = farmId, Name = "Tower One", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } },
            new TowerTwin { Id = "tower-ov-2", TowerId = "tower-ov-2", CoordId = coordId, FarmId = farmId, Name = "Tower Two", Metadata = new TwinMetadata { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow } }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/farms/{farmId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Main Coordinator");
        content.Should().Contain("Tower One");
        content.Should().Contain("Tower Two");
        content.Should().Contain(farmId);
    }

    [Fact]
    public async Task GetFarmTwins_WhenEmpty_ShouldReturnEmptyLists()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/twins/farms/empty-farm");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("empty-farm");
        content.Should().Contain("\"coordinators\":[]");
        content.Should().Contain("\"towers\":[]");
    }

    #endregion

    #region Sync Status Tests

    [Fact]
    public async Task TowerTwin_WithPendingStatus_ShouldReflectInDelta()
    {
        // Arrange
        var towerId = "tower-pending";
        var collection = MongoDb.GetCollection<TowerTwin>("tower_twins");

        await collection.InsertOneAsync(new TowerTwin
        {
            Id = towerId,
            TowerId = towerId,
            CoordId = "coord-pending",
            FarmId = "farm-pending",
            Reported = new TowerReportedState { LightOn = false },
            Desired = new TowerDesiredState { LightOn = true },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.Pending,
                LastSyncAttempt = DateTime.UtcNow.AddMinutes(-5),
                SyncRetryCount = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/towers/{towerId}/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("pending");
        content.Should().Contain("\"is_in_sync\":false");
    }

    [Fact]
    public async Task CoordinatorTwin_WithConflictStatus_ShouldReflectInDelta()
    {
        // Arrange
        var coordId = "coord-conflict";
        var collection = MongoDb.GetCollection<CoordinatorTwin>("coordinator_twins");

        await collection.InsertOneAsync(new CoordinatorTwin
        {
            Id = coordId,
            CoordId = coordId,
            SiteId = "site-conflict",
            FarmId = "farm-conflict",
            Reported = new CoordinatorReportedState { MainPumpOn = false },
            Desired = new CoordinatorDesiredState { MainPumpOn = true },
            Metadata = new TwinMetadata
            {
                SyncStatus = SyncStatus.Conflict,
                SyncRetryCount = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/twins/coordinators/{coordId}/delta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("conflict");
        content.Should().Contain("\"is_in_sync\":false");
    }

    #endregion
}
