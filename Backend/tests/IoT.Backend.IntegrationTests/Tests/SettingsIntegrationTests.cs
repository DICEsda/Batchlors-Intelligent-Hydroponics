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
/// Tests Settings Controller functionality for site configuration.
/// </summary>
[Collection("Integration")]
public class SettingsIntegrationTests : IntegrationTestBase
{
    public SettingsIntegrationTests(SharedContainerFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }

    #region Get Settings Tests

    [Fact]
    public async Task GetSettings_WhenExists_ShouldReturnSettings()
    {
        // Arrange
        var siteId = "test-site-settings";
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertOneAsync(new Settings
        {
            SiteId = siteId,
            AutoMode = false,
            MotionSensitivity = 75,
            LightIntensity = 90,
            AutoOffDelay = 60,
            Zones = new List<string> { "Zone A", "Zone B", "Zone C" }
        });

        // Act
        var response = await HttpClient.GetAsync($"/api/settings?site_id={siteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Zone A");
        content.Should().Contain("Zone B");
        content.Should().Contain("Zone C");
        content.Should().Contain("\"auto_mode\":false");
        content.Should().Contain("75");
        content.Should().Contain("90");
    }

    [Fact]
    public async Task GetSettings_WhenNotExists_ShouldReturnDefaults()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/settings?site_id=nonexistent-site");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Should return default values
        content.Should().Contain("\"auto_mode\":true");
        content.Should().Contain("\"motion_sensitivity\":50");
        content.Should().Contain("\"light_intensity\":80");
        content.Should().Contain("\"auto_off_delay\":30");
        
        // Should contain default zones
        content.Should().Contain("Living Room");
        content.Should().Contain("Bedroom");
    }

    [Fact]
    public async Task GetSettings_WithoutSiteId_ShouldUseDefaultSiteId()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("site001"); // Default site ID
    }

    [Fact]
    public async Task GetSettings_ShouldIncludeMqttBrokerConfig()
    {
        // Act
        var response = await HttpClient.GetAsync("/api/settings?site_id=any-site");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Should have MQTT broker configured (from test environment or default)
        content.Should().Contain("mqtt_broker");
    }

    #endregion

    #region Save Settings Tests

    [Fact]
    public async Task SaveSettings_ShouldPersistToDatabase()
    {
        // Arrange
        var siteId = "test-site-save";
        var settings = new
        {
            site_id = siteId,
            auto_mode = false,
            motion_sensitivity = 80,
            light_intensity = 95,
            auto_off_delay = 45,
            zones = new[] { "Greenhouse", "Nursery", "Main Hall" }
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync("/api/settings", settings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");

        // Verify in database
        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.AutoMode.Should().BeFalse();
        saved.MotionSensitivity.Should().Be(80);
        saved.LightIntensity.Should().Be(95);
        saved.AutoOffDelay.Should().Be(45);
        saved.Zones.Should().Contain("Greenhouse");
    }

    [Fact]
    public async Task SaveSettings_WithoutSiteId_ShouldUseDefault()
    {
        // Arrange
        var settings = new
        {
            auto_mode = true,
            motion_sensitivity = 60,
            light_intensity = 70
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync("/api/settings", settings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify default site_id was used
        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == "site001").FirstOrDefaultAsync();
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveSettings_ShouldNotStoreSensitiveCredentials()
    {
        // Arrange
        var siteId = "test-site-no-creds";
        var settings = new
        {
            site_id = siteId,
            auto_mode = true,
            mqtt_broker = "tcp://evil.server:1883",
            mqtt_username = "hacker",
            mqtt_password = "password123",
            wifi_ssid = "MyWifi",
            wifi_password = "wifipassword"
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync("/api/settings", settings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify credentials were NOT stored in database
        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.MqttBroker.Should().BeNull();
        saved.MqttUsername.Should().BeNull();
        saved.MqttPassword.Should().BeNull();
        saved.WifiSsid.Should().BeNull();
        saved.WifiPassword.Should().BeNull();
    }

    [Fact]
    public async Task SaveSettings_ShouldUpdateExisting()
    {
        // Arrange
        var siteId = "test-site-update";
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertOneAsync(new Settings
        {
            SiteId = siteId,
            AutoMode = true,
            MotionSensitivity = 50,
            LightIntensity = 50
        });

        var updatedSettings = new
        {
            site_id = siteId,
            auto_mode = false,
            motion_sensitivity = 100,
            light_intensity = 100,
            auto_off_delay = 120
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync("/api/settings", updatedSettings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.AutoMode.Should().BeFalse();
        saved.MotionSensitivity.Should().Be(100);
        saved.LightIntensity.Should().Be(100);
        saved.AutoOffDelay.Should().Be(120);
    }

    #endregion

    #region Patch Settings Tests

    [Fact]
    public async Task PatchSettings_ShouldUpdateOnlySpecifiedFields()
    {
        // Arrange
        var siteId = "test-site-patch";
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertOneAsync(new Settings
        {
            SiteId = siteId,
            AutoMode = true,
            MotionSensitivity = 50,
            LightIntensity = 80,
            AutoOffDelay = 30,
            Zones = new List<string> { "Original Zone" }
        });

        var patch = new
        {
            motion_sensitivity = 75
        };

        // Act
        var response = await HttpClient.PatchAsJsonAsync($"/api/settings?site_id={siteId}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.MotionSensitivity.Should().Be(75); // Updated
        saved.AutoMode.Should().BeTrue(); // Unchanged
        saved.LightIntensity.Should().Be(80); // Unchanged
        saved.AutoOffDelay.Should().Be(30); // Unchanged
        saved.Zones.Should().Contain("Original Zone"); // Unchanged
    }

    [Fact]
    public async Task PatchSettings_WhenNotExists_ShouldCreateWithPatchedValues()
    {
        // Arrange
        var siteId = "test-site-patch-new";
        var patch = new
        {
            auto_mode = false,
            light_intensity = 50
        };

        // Act
        var response = await HttpClient.PatchAsJsonAsync($"/api/settings?site_id={siteId}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.AutoMode.Should().BeFalse();
        saved.LightIntensity.Should().Be(50);
    }

    [Fact]
    public async Task PatchSettings_ShouldUpdateZones()
    {
        // Arrange
        var siteId = "test-site-patch-zones";
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertOneAsync(new Settings
        {
            SiteId = siteId,
            Zones = new List<string> { "Old Zone 1", "Old Zone 2" }
        });

        var patch = new
        {
            zones = new[] { "New Zone A", "New Zone B", "New Zone C" }
        };

        // Act
        var response = await HttpClient.PatchAsJsonAsync($"/api/settings?site_id={siteId}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Zones.Should().HaveCount(3);
        saved.Zones.Should().Contain("New Zone A");
        saved.Zones.Should().Contain("New Zone B");
        saved.Zones.Should().Contain("New Zone C");
        saved.Zones.Should().NotContain("Old Zone 1");
    }

    [Fact]
    public async Task PatchSettings_WithMultipleFields_ShouldUpdateAll()
    {
        // Arrange
        var siteId = "test-site-patch-multi";
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertOneAsync(new Settings
        {
            SiteId = siteId,
            AutoMode = true,
            MotionSensitivity = 50,
            LightIntensity = 80,
            AutoOffDelay = 30
        });

        var patch = new
        {
            auto_mode = false,
            motion_sensitivity = 90,
            light_intensity = 100,
            auto_off_delay = 60
        };

        // Act
        var response = await HttpClient.PatchAsJsonAsync($"/api/settings?site_id={siteId}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.AutoMode.Should().BeFalse();
        saved.MotionSensitivity.Should().Be(90);
        saved.LightIntensity.Should().Be(100);
        saved.AutoOffDelay.Should().Be(60);
    }

    [Fact]
    public async Task PatchSettings_WithoutSiteId_ShouldUseDefault()
    {
        // Arrange
        var patch = new
        {
            light_intensity = 65
        };

        // Act
        var response = await HttpClient.PatchAsJsonAsync("/api/settings", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify default site_id was used
        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == "site001").FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.LightIntensity.Should().Be(65);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SaveSettings_WithEmptyZones_ShouldPersistEmptyList()
    {
        // Arrange
        var siteId = "test-site-empty-zones";
        var settings = new
        {
            site_id = siteId,
            zones = Array.Empty<string>()
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync("/api/settings", settings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = MongoDb.GetCollection<Settings>("settings");
        var saved = await collection.Find(s => s.SiteId == siteId).FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Zones.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSettings_MultipleSites_ShouldReturnCorrectSettings()
    {
        // Arrange
        var collection = MongoDb.GetCollection<Settings>("settings");

        await collection.InsertManyAsync(new[]
        {
            new Settings { SiteId = "site-multi-1", LightIntensity = 10 },
            new Settings { SiteId = "site-multi-2", LightIntensity = 20 },
            new Settings { SiteId = "site-multi-3", LightIntensity = 30 }
        });

        // Act
        var response1 = await HttpClient.GetAsync("/api/settings?site_id=site-multi-1");
        var response2 = await HttpClient.GetAsync("/api/settings?site_id=site-multi-2");
        var response3 = await HttpClient.GetAsync("/api/settings?site_id=site-multi-3");

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        content1.Should().Contain("\"light_intensity\":10");
        content2.Should().Contain("\"light_intensity\":20");
        content3.Should().Contain("\"light_intensity\":30");
    }

    #endregion
}
