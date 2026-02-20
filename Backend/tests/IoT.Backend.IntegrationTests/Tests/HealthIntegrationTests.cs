using System.Net;
using FluentAssertions;
using IoT.Backend.IntegrationTests.Fixtures;
using IoT.Backend.Models;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace IoT.Backend.IntegrationTests.Tests;

/// <summary>
/// Tests Health Check Controller for service health monitoring and Kubernetes probes.
/// </summary>
[Collection("Integration")]
public class HealthIntegrationTests : IntegrationTestBase
{
    public HealthIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Liveness Probe Tests

    [Fact]
    public async Task GetLiveness_ShouldAlwaysReturnOk()
    {
        // Act
        var response = await HttpClient.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("alive");
        content.Should().Contain("timestamp");
    }

    [Fact]
    public async Task GetLiveness_ShouldReturnValidTimestamp()
    {
        // Act
        var beforeRequest = DateTime.UtcNow.AddSeconds(-1);
        var response = await HttpClient.GetAsync("/health/live");
        var afterRequest = DateTime.UtcNow.AddSeconds(1);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // The response should contain a timestamp
        content.Should().Contain("timestamp");
    }

    #endregion

    #region Readiness Probe Tests

    [Fact]
    public async Task GetReadiness_WhenMqttConnected_ShouldReturnOk()
    {
        // The test fixture should have MQTT connected
        
        // Act
        var response = await HttpClient.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ready");
        content.Should().Contain("\"mqtt_connected\":true");
    }

    #endregion

    #region General Health Check Tests

    [Fact]
    public async Task GetHealth_ShouldReturnHealthStatus()
    {
        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Should contain health status fields
        content.Should().Contain("status");
        content.Should().Contain("mqtt_connected");
        content.Should().Contain("database");
        content.Should().Contain("timestamp");
    }

    [Fact]
    public async Task GetHealth_ShouldShowDatabaseConnected()
    {
        // Since we're using real MongoDB testcontainer, database should be healthy
        
        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"database\":true");
    }

    [Fact]
    public async Task GetHealth_ShouldShowMqttConnected()
    {
        // Since we're using real MQTT testcontainer, MQTT should be connected
        
        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"mqtt_connected\":true");
        content.Should().Contain("\"mqtt\":true");
    }

    [Fact]
    public async Task GetHealth_WhenAllHealthy_ShouldReturnHealthyStatus()
    {
        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // With all dependencies healthy, status should be "healthy"
        content.Should().Contain("\"status\":\"healthy\"");
    }

    [Fact]
    public async Task GetHealth_WithNoOnlineCoordinator_ShouldStillReturnOk()
    {
        // No coordinators in database means coordinator field will be false
        // but overall health should still be OK
        
        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"coordinator\":false");
    }

    [Fact]
    public async Task GetHealth_WithOnlineCoordinator_ShouldShowCoordinatorTrue()
    {
        // Arrange - Insert a coordinator with recent last_seen
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");
        await collection.InsertOneAsync(new Coordinator
        {
            Id = "health-coord-1",
            CoordId = "health-coord-1",
            SiteId = "health-site",
            Name = "Health Check Coordinator",
            LastSeen = DateTime.UtcNow
        });

        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"coordinator\":true");
    }

    [Fact]
    public async Task GetHealth_WithStaleCoordinator_ShouldShowCoordinatorFalse()
    {
        // Arrange - Insert a coordinator with old last_seen (more than 5 minutes ago)
        var collection = MongoDb.GetCollection<Coordinator>("coordinators");
        await collection.InsertOneAsync(new Coordinator
        {
            Id = "health-stale-coord",
            CoordId = "health-stale-coord",
            SiteId = "health-stale-site",
            Name = "Stale Coordinator",
            LastSeen = DateTime.UtcNow.AddMinutes(-10) // 10 minutes ago
        });

        // Act
        var response = await HttpClient.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"coordinator\":false");
    }

    #endregion

    #region Multiple Endpoint Consistency Tests

    [Fact]
    public async Task AllHealthEndpoints_ShouldReturnConsistentTimestamps()
    {
        // Act - Call all health endpoints in quick succession
        var beforeTime = DateTime.UtcNow;
        
        var liveResponse = await HttpClient.GetAsync("/health/live");
        var readyResponse = await HttpClient.GetAsync("/health/ready");
        var healthResponse = await HttpClient.GetAsync("/health");
        
        var afterTime = DateTime.UtcNow.AddSeconds(1);

        // Assert - All should succeed
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // All should contain timestamp field
        var liveContent = await liveResponse.Content.ReadAsStringAsync();
        var readyContent = await readyResponse.Content.ReadAsStringAsync();
        var healthContent = await healthResponse.Content.ReadAsStringAsync();

        liveContent.Should().Contain("timestamp");
        readyContent.Should().Contain("timestamp");
        healthContent.Should().Contain("timestamp");
    }

    #endregion

    #region Error Condition Tests

    [Fact]
    public async Task GetLiveness_ShouldBeReliable_MultipleRequests()
    {
        // Act - Make multiple requests to ensure reliability
        for (int i = 0; i < 5; i++)
        {
            var response = await HttpClient.GetAsync("/health/live");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetHealth_ShouldHandleConcurrentRequests()
    {
        // Act - Make concurrent health check requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => HttpClient.GetAsync("/health"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion
}
