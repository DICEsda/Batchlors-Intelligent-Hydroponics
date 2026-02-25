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
/// Tests OTA (Over-The-Air) update functionality including jobs and firmware management.
/// </summary>
[Collection("Integration")]
public class OtaIntegrationTests : IntegrationTestBase
{
    public OtaIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Firmware Version Tests

    [Fact]
    public async Task CreateFirmware_ShouldPersistToDatabase()
    {
        // Arrange
        var firmware = new
        {
            version = "1.0.0",
            device_type = "coordinator",
            changelog = "Initial release",
            download_url = "https://example.com/firmware/1.0.0/coordinator.bin",
            checksum = "abc123def456",
            file_size_bytes = 1024000,
            is_stable = true
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/api/ota/firmware", firmware);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        var created = await collection.Find(f => f.Version == "1.0.0").FirstOrDefaultAsync();

        created.Should().NotBeNull();
        created!.DeviceType.Should().Be("coordinator");
        created.Changelog.Should().Be("Initial release");
        created.IsStable.Should().BeTrue();
    }

    [Fact]
    public async Task GetFirmware_WhenExists_ShouldReturnFirmware()
    {
        // Arrange
        var firmwareId = "fw-get-test";
        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await collection.InsertOneAsync(new FirmwareVersion
        {
            Id = firmwareId,
            Version = "2.0.0",
            DeviceType = "tower",
            Changelog = "Major update",
            DownloadUrl = "https://example.com/firmware/2.0.0/tower.bin",
            Checksum = "xyz789",
            FileSizeBytes = 2048000,
            IsStable = true,
            ReleaseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/ota/firmware/{firmwareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("2.0.0");
        content.Should().Contain("tower");
    }

    [Fact]
    public async Task GetFirmware_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/ota/firmware/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLatestFirmware_ShouldReturnMostRecent()
    {
        // Arrange
        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await collection.InsertManyAsync(new[]
        {
            new FirmwareVersion
            {
                Id = "fw-old",
                Version = "1.0.0",
                DeviceType = "coordinator",
                DownloadUrl = "https://example.com/1.0.0.bin",
                Checksum = "old",
                IsStable = true,
                ReleaseDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new FirmwareVersion
            {
                Id = "fw-new",
                Version = "1.1.0",
                DeviceType = "coordinator",
                DownloadUrl = "https://example.com/1.1.0.bin",
                Checksum = "new",
                IsStable = true,
                ReleaseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        });

        // Act
        var response = await HttpClient.GetAsync("/api/ota/firmware/latest/coordinator");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("1.1.0");
    }

    [Fact]
    public async Task ListFirmware_ShouldReturnAllVersions()
    {
        // Arrange
        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await collection.InsertManyAsync(new[]
        {
            new FirmwareVersion { Id = "fw-list-1", Version = "1.0.0", DeviceType = "coordinator", DownloadUrl = "url1", Checksum = "c1", ReleaseDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new FirmwareVersion { Id = "fw-list-2", Version = "1.1.0", DeviceType = "coordinator", DownloadUrl = "url2", Checksum = "c2", ReleaseDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new FirmwareVersion { Id = "fw-list-3", Version = "1.0.0", DeviceType = "tower", DownloadUrl = "url3", Checksum = "c3", ReleaseDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync("/api/ota/firmware");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("fw-list-1");
        content.Should().Contain("fw-list-2");
        content.Should().Contain("fw-list-3");
    }

    [Fact]
    public async Task ListFirmware_WithDeviceTypeFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await collection.InsertManyAsync(new[]
        {
            new FirmwareVersion { Id = "fw-filter-coord", Version = "1.0.0", DeviceType = "coordinator", DownloadUrl = "url1", Checksum = "c1", ReleaseDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new FirmwareVersion { Id = "fw-filter-tower", Version = "1.0.0", DeviceType = "tower", DownloadUrl = "url2", Checksum = "c2", ReleaseDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync("/api/ota/firmware?device_type=tower");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("fw-filter-tower");
        content.Should().NotContain("fw-filter-coord");
    }

    [Fact]
    public async Task DeleteFirmware_ShouldRemoveFromDatabase()
    {
        // Arrange
        var firmwareId = "fw-delete-test";
        var collection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await collection.InsertOneAsync(new FirmwareVersion
        {
            Id = firmwareId,
            Version = "1.0.0-delete",
            DeviceType = "coordinator",
            DownloadUrl = "url",
            Checksum = "c",
            ReleaseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.DeleteAsync($"/api/ota/firmware/{firmwareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await collection.Find(f => f.Id == firmwareId).FirstOrDefaultAsync();
        deleted.Should().BeNull();
    }

    #endregion

    #region OTA Job Tests

    [Fact]
    public async Task StartOtaJob_ShouldCreateJobInDatabase()
    {
        // Arrange
        var farmId = "test-farm-ota";
        var coordId = "coord-ota";

        // Insert firmware first
        var fwCollection = MongoDb.GetCollection<FirmwareVersion>("firmware_versions");
        await fwCollection.InsertOneAsync(new FirmwareVersion
        {
            Id = "fw-for-job",
            Version = "2.0.0",
            DeviceType = "tower",
            DownloadUrl = "https://example.com/fw.bin",
            Checksum = "sha256hash",
            IsStable = true,
            ReleaseDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var request = new
        {
            farm_id = farmId,
            coord_id = coordId,
            target_type = "tower",
            target_version = "2.0.0",
            firmware_url = "https://example.com/fw.bin",
            rollout_strategy = "immediate"
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/api/ota/start", request);

        // Assert - CreatedAtAction returns 201 Created
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var jobCollection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        var job = await jobCollection.Find(j => j.FarmId == farmId).FirstOrDefaultAsync();

        job.Should().NotBeNull();
        job!.Status.Should().Be("pending");
        job.TargetVersion.Should().Be("2.0.0");
        job.RolloutStrategy.Should().Be("immediate");
    }

    [Fact]
    public async Task GetOtaJob_WhenExists_ShouldReturnJob()
    {
        // Arrange
        var jobId = "job-get-test";
        var collection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        await collection.InsertOneAsync(new OtaJob
        {
            Id = jobId,
            FarmId = "test-farm",
            CoordId = "coord-1",
            TargetType = "tower",
            TargetVersion = "1.5.0",
            Status = "in_progress",
            Progress = 50,
            DevicesTotal = 10,
            DevicesUpdated = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/ota/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("in_progress");
        content.Should().Contain("1.5.0");
    }

    [Fact]
    public async Task ListOtaJobs_ShouldReturnAllJobs()
    {
        // Arrange
        var collection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        await collection.InsertManyAsync(new[]
        {
            new OtaJob { Id = "job-list-1", FarmId = "farm-a", TargetVersion = "1.0.0", Status = "completed", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new OtaJob { Id = "job-list-2", FarmId = "farm-a", TargetVersion = "1.1.0", Status = "pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new OtaJob { Id = "job-list-3", FarmId = "farm-b", TargetVersion = "1.0.0", Status = "failed", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync("/api/ota/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("job-list-1");
        content.Should().Contain("job-list-2");
        content.Should().Contain("job-list-3");
    }

    [Fact]
    public async Task ListOtaJobs_WithFarmFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var collection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        await collection.InsertManyAsync(new[]
        {
            new OtaJob { Id = "job-filter-a", FarmId = "farm-filter-a", TargetVersion = "1.0.0", Status = "completed", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new OtaJob { Id = "job-filter-b", FarmId = "farm-filter-b", TargetVersion = "1.0.0", Status = "completed", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });

        // Act
        var response = await HttpClient.GetAsync("/api/ota/jobs?farm_id=farm-filter-a");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("job-filter-a");
        content.Should().NotContain("job-filter-b");
    }

    [Fact]
    public async Task CancelOtaJob_ShouldUpdateStatus()
    {
        // Arrange
        var jobId = "job-cancel-test";
        var collection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        await collection.InsertOneAsync(new OtaJob
        {
            Id = jobId,
            FarmId = "test-farm",
            TargetVersion = "1.0.0",
            Status = "in_progress",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.PostAsync($"/api/ota/jobs/{jobId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await collection.Find(j => j.Id == jobId).FirstOrDefaultAsync();
        updated!.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task UpdateOtaJobProgress_ShouldPersistProgress()
    {
        // Arrange
        var jobId = "job-progress-test";
        var collection = MongoDb.GetCollection<OtaJob>("ota_jobs");
        await collection.InsertOneAsync(new OtaJob
        {
            Id = jobId,
            FarmId = "test-farm",
            TargetVersion = "1.0.0",
            Status = "in_progress",
            Progress = 0,
            DevicesTotal = 10,
            DevicesUpdated = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var progressUpdate = new
        {
            progress = 75,
            devices_updated = 7,
            devices_failed = 1
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/api/ota/jobs/{jobId}/progress", progressUpdate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await collection.Find(j => j.Id == jobId).FirstOrDefaultAsync();
        updated!.Progress.Should().Be(75);
        updated.DevicesUpdated.Should().Be(7);
        updated.DevicesFailed.Should().Be(1);
    }

    #endregion
}
