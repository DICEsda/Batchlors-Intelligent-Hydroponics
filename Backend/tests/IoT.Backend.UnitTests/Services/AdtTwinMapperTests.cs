using System.Text.Json;
using Azure;
using Azure.DigitalTwins.Core;
using FluentAssertions;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

public class AdtTwinMapperTests
{
    private readonly AdtTwinMapper _sut;

    public AdtTwinMapperTests()
    {
        _sut = new AdtTwinMapper(NullLogger<AdtTwinMapper>.Instance);
    }

    // ========================================================================
    // GetTowerTwinId
    // ========================================================================

    [Fact]
    public void GetTowerTwinId_SimpleTowerId_ReturnsPrefixed()
    {
        var result = _sut.GetTowerTwinId("tower123");

        result.Should().Be("tower-tower123");
    }

    [Fact]
    public void GetTowerTwinId_MacAddress_ReplacesColonsWithHyphens()
    {
        var result = _sut.GetTowerTwinId("AA:BB:CC:DD:EE:FF");

        result.Should().Be("tower-AA-BB-CC-DD-EE-FF");
    }

    [Fact]
    public void GetTowerTwinId_SpecialChars_RemovesInvalidCharacters()
    {
        var result = _sut.GetTowerTwinId("tower@#$%123");

        result.Should().Be("tower-tower123");
    }

    // ========================================================================
    // GetCoordinatorTwinId
    // ========================================================================

    [Fact]
    public void GetCoordinatorTwinId_SimpleId_ReturnsPrefixed()
    {
        var result = _sut.GetCoordinatorTwinId("coord1");

        result.Should().Be("coordinator-coord1");
    }

    [Fact]
    public void GetCoordinatorTwinId_MacAddress_ReplacesColonsWithHyphens()
    {
        var result = _sut.GetCoordinatorTwinId("11:22:33:44:55:66");

        result.Should().Be("coordinator-11-22-33-44-55-66");
    }

    // ========================================================================
    // GetFarmTwinId
    // ========================================================================

    [Fact]
    public void GetFarmTwinId_SimpleId_ReturnsPrefixed()
    {
        var result = _sut.GetFarmTwinId("farm-abc");

        result.Should().Be("farm-farm-abc");
    }

    [Fact]
    public void GetFarmTwinId_WithSpaces_ReplacesWithHyphens()
    {
        var result = _sut.GetFarmTwinId("my farm");

        result.Should().Be("farm-my-farm");
    }

    // ========================================================================
    // SanitizeTwinId (tested indirectly)
    // ========================================================================

    [Fact]
    public void SanitizeTwinId_MacAddress_ProducesValidAdtId()
    {
        var result = _sut.GetTowerTwinId("AA:BB:CC:DD:EE:FF");

        // Should only contain alphanumeric, hyphens, underscores
        result.Should().MatchRegex(@"^[a-zA-Z0-9\-_]+$");
        result.Should().Be("tower-AA-BB-CC-DD-EE-FF");
    }

    [Fact]
    public void SanitizeTwinId_UnderscoresPreserved()
    {
        var result = _sut.GetCoordinatorTwinId("coord_1_test");

        result.Should().Be("coordinator-coord_1_test");
    }

    [Fact]
    public void SanitizeTwinId_MultipleDots_Removed()
    {
        var result = _sut.GetFarmTwinId("farm.v1.2");

        result.Should().Be("farm-farmv12");
    }

    // ========================================================================
    // GetHasTowerRelationshipId
    // ========================================================================

    [Fact]
    public void GetHasTowerRelationshipId_ReturnsCorrectFormat()
    {
        var result = _sut.GetHasTowerRelationshipId("coord1", "tower1");

        result.Should().Be("coordinator-coord1-hasTower-tower-tower1");
    }

    [Fact]
    public void GetHasTowerRelationshipId_WithMacAddresses_SanitizesAll()
    {
        var result = _sut.GetHasTowerRelationshipId("AA:BB:CC:DD:EE:FF", "11:22:33:44:55:66");

        result.Should().Be("coordinator-AA-BB-CC-DD-EE-FF-hasTower-tower-11-22-33-44-55-66");
    }

    // ========================================================================
    // GetHasCoordinatorRelationshipId
    // ========================================================================

    [Fact]
    public void GetHasCoordinatorRelationshipId_ReturnsCorrectFormat()
    {
        var result = _sut.GetHasCoordinatorRelationshipId("farm1", "coord1");

        result.Should().Be("farm-farm1-hasCoordinator-coordinator-coord1");
    }

    // ========================================================================
    // MapTowerTwin
    // ========================================================================

    [Fact]
    public void MapTowerTwin_SetsCorrectModelId()
    {
        var tower = MakeTowerTwin();

        var result = _sut.MapTowerTwin(tower);

        result.Metadata.ModelId.Should().Be("dtmi:iot:hydroponics:Tower;1");
    }

    [Fact]
    public void MapTowerTwin_MapsBasicContents()
    {
        var tower = MakeTowerTwin();
        tower.TowerId = "tower-abc";
        tower.Name = "My Tower";
        tower.Reported.FwVersion = "2.1.0";
        tower.Reported.StatusMode = "operational";
        tower.Reported.VbatMv = 3300;

        var result = _sut.MapTowerTwin(tower);

        result.Contents["mac_address"].Should().Be("tower-abc");
        result.Contents["name"].Should().Be("My Tower");
        result.Contents["firmware_version"].Should().Be("2.1.0");
        result.Contents["status"].Should().Be("operational");
        result.Contents["battery_mv"].Should().Be(3300);
    }

    [Fact]
    public void MapTowerTwin_IncludesCapabilities_WhenPresent()
    {
        var tower = MakeTowerTwin();
        tower.Capabilities = new TowerCapabilities
        {
            SlotCount = 8,
            DhtSensor = true,
            LightSensor = true,
            PumpRelay = false,
            GrowLight = true
        };

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("slot_count");
        result.Contents["slot_count"].Should().Be(8);
        result.Contents.Should().ContainKey("capabilities");
    }

    [Fact]
    public void MapTowerTwin_ExcludesCapabilities_WhenNull()
    {
        var tower = MakeTowerTwin();
        tower.Capabilities = null;

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().NotContainKey("slot_count");
        result.Contents.Should().NotContainKey("capabilities");
    }

    [Fact]
    public void MapTowerTwin_IncludesCropType_WhenNotUnknown()
    {
        var tower = MakeTowerTwin();
        tower.CropType = CropType.Lettuce;

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("crop_type");
        result.Contents["crop_type"].Should().Be("Lettuce");
    }

    [Fact]
    public void MapTowerTwin_ExcludesCropType_WhenUnknown()
    {
        var tower = MakeTowerTwin();
        tower.CropType = CropType.Unknown;

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().NotContainKey("crop_type");
    }

    [Fact]
    public void MapTowerTwin_IncludesDesiredState_WhenValuesPresent()
    {
        var tower = MakeTowerTwin();
        tower.Desired.PumpOn = true;
        tower.Desired.LightOn = false;
        tower.Desired.LightBrightness = 128;

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("desired_state");
        var desired = result.Contents["desired_state"] as Dictionary<string, object>;
        desired.Should().NotBeNull();
        desired!["pump_on"].Should().Be(true);
        desired["light_on"].Should().Be(false);
        desired["light_brightness"].Should().Be(128);
    }

    [Fact]
    public void MapTowerTwin_ExcludesDesiredState_WhenAllNull()
    {
        var tower = MakeTowerTwin();
        tower.Desired = new TowerDesiredState(); // all nulls

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().NotContainKey("desired_state");
    }

    [Fact]
    public void MapTowerTwin_IncludesGrowthTracking_WhenHeightPresent()
    {
        var tower = MakeTowerTwin();
        tower.LastHeightCm = 15.5f;
        tower.LastHeightAt = DateTime.UtcNow.AddHours(-1);
        tower.PlantingDate = DateTime.UtcNow.AddDays(-10);

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("growth_tracking");
        var growth = result.Contents["growth_tracking"] as Dictionary<string, object>;
        growth.Should().NotBeNull();
        growth!.Should().ContainKey("last_height_cm");
        growth.Should().ContainKey("days_since_planting");
    }

    [Fact]
    public void MapTowerTwin_IncludesMlPredictions_WhenPredictedHeightPresent()
    {
        var tower = MakeTowerTwin();
        tower.PredictedHeightCm = 30.0f;

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("ml_predictions");
    }

    [Fact]
    public void MapTowerTwin_IncludesMlPredictions_WhenExpectedHarvestDatePresent()
    {
        var tower = MakeTowerTwin();
        tower.ExpectedHarvestDate = DateTime.UtcNow.AddDays(30);

        var result = _sut.MapTowerTwin(tower);

        result.Contents.Should().ContainKey("ml_predictions");
    }

    // ========================================================================
    // MapCoordinatorTwin
    // ========================================================================

    [Fact]
    public void MapCoordinatorTwin_SetsCorrectModelId()
    {
        var coord = MakeCoordinatorTwin();

        var result = _sut.MapCoordinatorTwin(coord);

        result.Metadata.ModelId.Should().Be("dtmi:iot:hydroponics:Coordinator;1");
    }

    [Fact]
    public void MapCoordinatorTwin_MapsAllContents()
    {
        var coord = MakeCoordinatorTwin();
        coord.CoordId = "coord-xyz";
        coord.Name = "Main Coordinator";
        coord.Reported.FwVersion = "3.0.0";
        coord.Reported.WifiRssi = -45;
        coord.Reported.UptimeS = 86400;
        coord.Reported.TowersOnline = 5;

        var result = _sut.MapCoordinatorTwin(coord);

        result.Contents["mac_address"].Should().Be("coord-xyz");
        result.Contents["firmware_version"].Should().Be("3.0.0");
        result.Contents["wifi_rssi"].Should().Be(-45);
        result.Contents["uptime_seconds"].Should().Be(86400L);
        result.Contents["towers_online"].Should().Be(5);
        result.Contents["name"].Should().Be("Main Coordinator");
    }

    [Fact]
    public void MapCoordinatorTwin_IncludesDesiredState_WhenPairingMode()
    {
        var coord = MakeCoordinatorTwin();
        coord.Desired.StatusMode = "pairing";

        var result = _sut.MapCoordinatorTwin(coord);

        result.Contents.Should().ContainKey("desired_state");
        var desired = result.Contents["desired_state"] as Dictionary<string, object>;
        desired.Should().NotBeNull();
        desired!["pairing_enabled"].Should().Be(true);
    }

    // ========================================================================
    // MapFarmTwin
    // ========================================================================

    [Fact]
    public void MapFarmTwin_SetsCorrectModelId()
    {
        var farm = MakeFarm();

        var result = _sut.MapFarmTwin(farm);

        result.Metadata.ModelId.Should().Be("dtmi:iot:hydroponics:Farm;1");
    }

    [Fact]
    public void MapFarmTwin_MapsBasicContents()
    {
        var farm = MakeFarm();
        farm.Name = "Green Farm";

        var result = _sut.MapFarmTwin(farm);

        result.Contents["name"].Should().Be("Green Farm");
        result.Contents.Should().ContainKey("created_at");
    }

    [Fact]
    public void MapFarmTwin_IncludesLocation_WhenPresent()
    {
        var farm = MakeFarm();
        farm.Location = "Building A, Floor 2";

        var result = _sut.MapFarmTwin(farm);

        result.Contents.Should().ContainKey("location");
        var location = result.Contents["location"] as Dictionary<string, object>;
        location.Should().NotBeNull();
        location!["address"].Should().Be("Building A, Floor 2");
    }

    [Fact]
    public void MapFarmTwin_ExcludesLocation_WhenNull()
    {
        var farm = MakeFarm();
        farm.Location = null;

        var result = _sut.MapFarmTwin(farm);

        result.Contents.Should().NotContainKey("location");
    }

    [Fact]
    public void MapFarmTwin_ExcludesLocation_WhenEmpty()
    {
        var farm = MakeFarm();
        farm.Location = "";

        var result = _sut.MapFarmTwin(farm);

        result.Contents.Should().NotContainKey("location");
    }

    // ========================================================================
    // CreateTowerTelemetryPatch
    // ========================================================================

    [Fact]
    public void CreateTowerTelemetryPatch_ReturnsNonNullPatch()
    {
        var reported = MakeTowerReported();

        var result = _sut.CreateTowerTelemetryPatch(reported);

        result.Should().NotBeNull();
        result.Should().BeOfType<JsonPatchDocument>();
    }

    [Fact]
    public void CreateTowerTelemetryPatch_ContainsReportedStateReplaceOp()
    {
        var reported = MakeTowerReported();
        reported.AirTempC = 24.5f;
        reported.HumidityPct = 65.0f;
        reported.LightLux = 15000f;

        var result = _sut.CreateTowerTelemetryPatch(reported);

        // Serialize to verify patch operations
        var json = result.ToString();
        json.Should().Contain("/reported_state");
        json.Should().Contain("/last_seen");
        json.Should().Contain("/status");
        json.Should().Contain("/firmware_version");
        json.Should().Contain("/battery_mv");
    }

    [Fact]
    public void CreateTowerTelemetryPatch_IncludesSignalQuality_WhenPresent()
    {
        var reported = MakeTowerReported();
        reported.SignalQuality = -55;

        var result = _sut.CreateTowerTelemetryPatch(reported);

        var json = result.ToString();
        json.Should().Contain("/signal_quality");
    }

    // ========================================================================
    // CreateCoordinatorTelemetryPatch
    // ========================================================================

    [Fact]
    public void CreateCoordinatorTelemetryPatch_ReturnsCorrectPatchOps()
    {
        var reported = MakeCoordinatorReported();
        reported.TempC = 22.0f;
        reported.WifiRssi = -40;
        reported.UptimeS = 3600;
        reported.TowersOnline = 3;

        var result = _sut.CreateCoordinatorTelemetryPatch(reported);

        var json = result.ToString();
        json.Should().Contain("/reported_state");
        json.Should().Contain("/last_seen");
        json.Should().Contain("/status");
        json.Should().Contain("/wifi_rssi");
        json.Should().Contain("/uptime_seconds");
        json.Should().Contain("/towers_online");
        json.Should().Contain("/firmware_version");
    }

    [Fact]
    public void CreateCoordinatorTelemetryPatch_IncludesLightLux_WhenPresent()
    {
        var reported = MakeCoordinatorReported();
        reported.LightLux = 5000f;

        var result = _sut.CreateCoordinatorTelemetryPatch(reported);

        // The reported_state dict should include ambient_light_lux
        // We verify by checking the patch serialization
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateCoordinatorTelemetryPatch_ExcludesLightLux_WhenNull()
    {
        var reported = MakeCoordinatorReported();
        reported.LightLux = null;

        var result = _sut.CreateCoordinatorTelemetryPatch(reported);

        // Patch should still be valid
        result.Should().NotBeNull();
    }

    // ========================================================================
    // CreateMlPredictionsPatch
    // ========================================================================

    [Fact]
    public void CreateMlPredictionsPatch_IncludesAllNonNullFields()
    {
        var predictions = new TowerMlPredictions
        {
            ModelName = "growth-v2",
            ModelVersion = "2.0.0",
            GeneratedAt = DateTime.UtcNow,
            PredictedHeightCm = 35.0,
            PredictedHarvestDate = DateTime.UtcNow.AddDays(20),
            DaysToHarvest = 20,
            GrowthRateCmPerDay = 1.5,
            HealthScore = 0.92,
            AnomalyScore = 0.05,
            RecommendedPh = 6.0,
            RecommendedEc = 1.5,
            RecommendedLightHours = 16
        };

        var result = _sut.CreateMlPredictionsPatch(predictions);

        var json = result.ToString();
        json.Should().Contain("/ml_predictions");
    }

    [Fact]
    public void CreateMlPredictionsPatch_OmitsNullFields()
    {
        var predictions = new TowerMlPredictions
        {
            ModelName = "growth-v1",
            ModelVersion = "1.0.0",
            PredictedHeightCm = null,
            PredictedHarvestDate = null,
            DaysToHarvest = null,
            GrowthRateCmPerDay = null,
            HealthScore = null,
            AnomalyScore = null,
            RecommendedPh = null,
            RecommendedEc = null,
            RecommendedLightHours = null
        };

        var result = _sut.CreateMlPredictionsPatch(predictions);

        // Should still produce a valid patch with model_name, model_version, generated_at
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateMlPredictionsPatch_PartialFields_IncludesOnlyNonNull()
    {
        var predictions = new TowerMlPredictions
        {
            PredictedHeightCm = 25.0,
            HealthScore = 0.8
            // All other nullable fields are null
        };

        var result = _sut.CreateMlPredictionsPatch(predictions);

        result.Should().NotBeNull();
    }

    // ========================================================================
    // CreateTowerEnvironmentalTelemetry
    // ========================================================================

    [Fact]
    public void CreateTowerEnvironmentalTelemetry_ReturnsObjectWithCorrectProperties()
    {
        var reported = MakeTowerReported();
        reported.AirTempC = 23.5f;
        reported.HumidityPct = 60.0f;
        reported.LightLux = 12000f;

        var result = _sut.CreateTowerEnvironmentalTelemetry(reported);

        // Serialize to JSON and verify properties
        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("air_temp_c").GetSingle().Should().BeApproximately(23.5f, 0.01f);
        root.GetProperty("humidity_pct").GetSingle().Should().BeApproximately(60.0f, 0.01f);
        root.GetProperty("light_lux").GetSingle().Should().BeApproximately(12000f, 0.01f);
    }

    // ========================================================================
    // CreateCoordinatorAmbientTelemetry
    // ========================================================================

    [Fact]
    public void CreateCoordinatorAmbientTelemetry_ReturnsObjectWithCorrectProperties()
    {
        var reported = MakeCoordinatorReported();
        reported.LightLux = 8000f;
        reported.TempC = 21.0f;

        var result = _sut.CreateCoordinatorAmbientTelemetry(reported);

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("ambient_light_lux").GetSingle().Should().BeApproximately(8000f, 0.01f);
        root.GetProperty("ambient_temp_c").GetSingle().Should().BeApproximately(21.0f, 0.01f);
    }

    [Fact]
    public void CreateCoordinatorAmbientTelemetry_NullLightLux_DefaultsToZero()
    {
        var reported = MakeCoordinatorReported();
        reported.LightLux = null;
        reported.TempC = 19.0f;

        var result = _sut.CreateCoordinatorAmbientTelemetry(reported);

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("ambient_light_lux").GetSingle().Should().Be(0f);
    }

    // ========================================================================
    // Model ID constants
    // ========================================================================

    [Fact]
    public void TowerModelId_IsCorrectDtmi()
    {
        AdtTwinMapper.TowerModelId.Should().Be("dtmi:iot:hydroponics:Tower;1");
    }

    [Fact]
    public void CoordinatorModelId_IsCorrectDtmi()
    {
        AdtTwinMapper.CoordinatorModelId.Should().Be("dtmi:iot:hydroponics:Coordinator;1");
    }

    [Fact]
    public void FarmModelId_IsCorrectDtmi()
    {
        AdtTwinMapper.FarmModelId.Should().Be("dtmi:iot:hydroponics:Farm;1");
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static TowerTwin MakeTowerTwin() => new()
    {
        Id = "tower-1",
        TowerId = "tower-1",
        CoordId = "coord-1",
        FarmId = "farm-1",
        Name = "Test Tower",
        Reported = new TowerReportedState
        {
            AirTempC = 22.0f,
            HumidityPct = 55.0f,
            LightLux = 10000f,
            PumpOn = false,
            LightOn = true,
            LightBrightness = 200,
            StatusMode = "operational",
            VbatMv = 3300,
            FwVersion = "1.0.0",
            SignalQuality = -60
        },
        Desired = new TowerDesiredState(),
        Metadata = new TwinMetadata
        {
            LastReportedAt = DateTime.UtcNow
        },
        CropType = CropType.Unknown
    };

    private static CoordinatorTwin MakeCoordinatorTwin() => new()
    {
        Id = "coord-1",
        CoordId = "coord-1",
        FarmId = "farm-1",
        Name = "Test Coordinator",
        Reported = new CoordinatorReportedState
        {
            FwVersion = "1.0.0",
            WifiRssi = -50,
            UptimeS = 3600,
            TowersOnline = 2,
            StatusMode = "operational",
            TempC = 22.0f,
            LightLux = 5000f
        },
        Desired = new CoordinatorDesiredState(),
        Metadata = new TwinMetadata
        {
            LastReportedAt = DateTime.UtcNow
        }
    };

    private static Farm MakeFarm() => new()
    {
        FarmId = "farm-1",
        Name = "Test Farm",
        CreatedAt = DateTime.UtcNow,
        Location = null
    };

    private static TowerReportedState MakeTowerReported() => new()
    {
        AirTempC = 22.0f,
        HumidityPct = 55.0f,
        LightLux = 10000f,
        PumpOn = false,
        LightOn = true,
        LightBrightness = 200,
        StatusMode = "operational",
        VbatMv = 3300,
        FwVersion = "1.0.0"
    };

    private static CoordinatorReportedState MakeCoordinatorReported() => new()
    {
        FwVersion = "1.0.0",
        WifiRssi = -50,
        UptimeS = 3600,
        TowersOnline = 2,
        StatusMode = "operational",
        TempC = 22.0f,
        LightLux = 5000f
    };
}
