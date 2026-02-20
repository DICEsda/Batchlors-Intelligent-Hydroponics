using System.Text.Json;
using Azure;
using Azure.DigitalTwins.Core;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;

namespace IoT.Backend.Services;

/// <summary>
/// Maps local Twin models to Azure Digital Twins BasicDigitalTwin format.
/// Handles conversion between C# models and DTDL-compliant twin structures.
/// </summary>
public class AdtTwinMapper
{
    /// <summary>
    /// DTDL model ID for hydroponic tower
    /// </summary>
    public const string TowerModelId = "dtmi:iot:hydroponics:Tower;1";

    /// <summary>
    /// DTDL model ID for hydroponic coordinator
    /// </summary>
    public const string CoordinatorModelId = "dtmi:iot:hydroponics:Coordinator;1";

    /// <summary>
    /// DTDL model ID for hydroponic reservoir
    /// </summary>
    public const string ReservoirModelId = "dtmi:iot:hydroponics:Reservoir;1";

    /// <summary>
    /// DTDL model ID for hydroponic farm
    /// </summary>
    public const string FarmModelId = "dtmi:iot:hydroponics:Farm;1";

    private readonly ILogger<AdtTwinMapper> _logger;

    public AdtTwinMapper(ILogger<AdtTwinMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps a TowerTwin to a BasicDigitalTwin for Azure Digital Twins.
    /// </summary>
    /// <param name="tower">The source tower twin from MongoDB</param>
    /// <returns>A BasicDigitalTwin ready for ADT upsert</returns>
    public BasicDigitalTwin MapTowerTwin(TowerTwin tower)
    {
        var twin = new BasicDigitalTwin
        {
            Id = SanitizeTwinId($"tower-{tower.TowerId}"),
            Metadata = { ModelId = TowerModelId },
            Contents = new Dictionary<string, object>
            {
                ["mac_address"] = tower.TowerId,
                ["name"] = tower.Name ?? $"Tower {tower.TowerId}",
                ["firmware_version"] = tower.Reported.FwVersion,
                ["status"] = MapStatusMode(tower.Reported.StatusMode),
                ["signal_quality"] = tower.Reported.SignalQuality ?? 0,
                ["battery_mv"] = tower.Reported.VbatMv,
                ["last_seen"] = tower.Metadata.LastReportedAt ?? DateTime.UtcNow
            }
        };

        // Add capabilities if present
        if (tower.Capabilities != null)
        {
            twin.Contents["slot_count"] = tower.Capabilities.SlotCount;
            twin.Contents["capabilities"] = new Dictionary<string, object>
            {
                ["dht_sensor"] = tower.Capabilities.DhtSensor,
                ["light_sensor"] = tower.Capabilities.LightSensor,
                ["pump_relay"] = tower.Capabilities.PumpRelay,
                ["grow_light"] = tower.Capabilities.GrowLight
            };
        }

        // Add crop information if available
        if (tower.CropType != CropType.Unknown)
        {
            twin.Contents["crop_type"] = tower.CropType.ToString();
        }

        if (tower.PlantingDate.HasValue)
        {
            twin.Contents["planted_date"] = tower.PlantingDate.Value.ToString("yyyy-MM-dd");
        }

        // Add reported state
        twin.Contents["reported_state"] = new Dictionary<string, object>
        {
            ["air_temp_c"] = tower.Reported.AirTempC,
            ["humidity_pct"] = tower.Reported.HumidityPct,
            ["light_lux"] = tower.Reported.LightLux,
            ["pump_on"] = tower.Reported.PumpOn,
            ["light_on"] = tower.Reported.LightOn,
            ["light_brightness"] = tower.Reported.LightBrightness
        };

        // Add desired state
        var desiredState = new Dictionary<string, object>();
        if (tower.Desired.PumpOn.HasValue) desiredState["pump_on"] = tower.Desired.PumpOn.Value;
        if (tower.Desired.LightOn.HasValue) desiredState["light_on"] = tower.Desired.LightOn.Value;
        if (tower.Desired.LightBrightness.HasValue) desiredState["light_brightness"] = tower.Desired.LightBrightness.Value;

        if (desiredState.Count > 0)
        {
            twin.Contents["desired_state"] = desiredState;
        }

        // Add growth tracking
        var growthTracking = new Dictionary<string, object>();
        if (tower.LastHeightCm.HasValue) growthTracking["last_height_cm"] = tower.LastHeightCm.Value;
        if (tower.LastHeightAt.HasValue) growthTracking["last_measured_at"] = tower.LastHeightAt.Value;
        if (tower.PlantingDate.HasValue)
        {
            growthTracking["days_since_planting"] = (int)(DateTime.UtcNow - tower.PlantingDate.Value).TotalDays;
        }

        if (growthTracking.Count > 0)
        {
            twin.Contents["growth_tracking"] = growthTracking;
        }

        // Add ML predictions if available
        if (tower.PredictedHeightCm.HasValue || tower.ExpectedHarvestDate.HasValue)
        {
            var mlPredictions = BuildMlPredictions(tower);
            if (mlPredictions.Count > 0)
            {
                twin.Contents["ml_predictions"] = mlPredictions;
            }
        }

        return twin;
    }

    /// <summary>
    /// Maps a CoordinatorTwin to a BasicDigitalTwin for Azure Digital Twins.
    /// </summary>
    /// <param name="coordinator">The source coordinator twin from MongoDB</param>
    /// <returns>A BasicDigitalTwin ready for ADT upsert</returns>
    public BasicDigitalTwin MapCoordinatorTwin(CoordinatorTwin coordinator)
    {
        var twin = new BasicDigitalTwin
        {
            Id = SanitizeTwinId($"coordinator-{coordinator.CoordId}"),
            Metadata = { ModelId = CoordinatorModelId },
            Contents = new Dictionary<string, object>
            {
                ["mac_address"] = coordinator.CoordId,
                ["firmware_version"] = coordinator.Reported.FwVersion,
                ["status"] = MapCoordinatorStatus(coordinator.Reported.StatusMode),
                ["wifi_rssi"] = coordinator.Reported.WifiRssi,
                ["uptime_seconds"] = coordinator.Reported.UptimeS,
                ["towers_online"] = coordinator.Reported.TowersOnline,
                ["last_seen"] = coordinator.Metadata.LastReportedAt ?? DateTime.UtcNow
            }
        };

        // Add name if present
        if (!string.IsNullOrEmpty(coordinator.Name))
        {
            twin.Contents["name"] = coordinator.Name;
        }

        // Add reported state
        var reportedState = new Dictionary<string, object>();
        if (coordinator.Reported.LightLux.HasValue) reportedState["ambient_light_lux"] = coordinator.Reported.LightLux.Value;
        reportedState["ambient_temp_c"] = coordinator.Reported.TempC;

        if (reportedState.Count > 0)
        {
            twin.Contents["reported_state"] = reportedState;
        }

        // Add desired state
        var desiredState = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(coordinator.Desired.StatusMode))
        {
            desiredState["pairing_enabled"] = coordinator.Desired.StatusMode == "pairing";
        }

        if (desiredState.Count > 0)
        {
            twin.Contents["desired_state"] = desiredState;
        }

        return twin;
    }

    /// <summary>
    /// Creates a JSON Patch document for updating tower telemetry properties.
    /// </summary>
    /// <param name="reported">The reported state to update</param>
    /// <returns>A JsonPatchDocument with property updates</returns>
    public JsonPatchDocument CreateTowerTelemetryPatch(TowerReportedState reported)
    {
        var patch = new JsonPatchDocument();

        // Update reported state object
        var reportedState = new Dictionary<string, object>
        {
            ["air_temp_c"] = reported.AirTempC,
            ["humidity_pct"] = reported.HumidityPct,
            ["light_lux"] = reported.LightLux,
            ["pump_on"] = reported.PumpOn,
            ["light_on"] = reported.LightOn,
            ["light_brightness"] = reported.LightBrightness
        };

        patch.AppendReplace("/reported_state", reportedState);
        patch.AppendReplace("/last_seen", DateTime.UtcNow);
        patch.AppendReplace("/status", MapStatusMode(reported.StatusMode));
        patch.AppendReplace("/firmware_version", reported.FwVersion);
        patch.AppendReplace("/battery_mv", reported.VbatMv);

        if (reported.SignalQuality.HasValue)
        {
            patch.AppendReplace("/signal_quality", reported.SignalQuality.Value);
        }

        return patch;
    }

    /// <summary>
    /// Creates a JSON Patch document for updating coordinator telemetry properties.
    /// </summary>
    /// <param name="reported">The reported state to update</param>
    /// <returns>A JsonPatchDocument with property updates</returns>
    public JsonPatchDocument CreateCoordinatorTelemetryPatch(CoordinatorReportedState reported)
    {
        var patch = new JsonPatchDocument();

        // Update reported state object
        var reportedState = new Dictionary<string, object>
        {
            ["ambient_temp_c"] = reported.TempC
        };

        if (reported.LightLux.HasValue)
        {
            reportedState["ambient_light_lux"] = reported.LightLux.Value;
        }

        patch.AppendReplace("/reported_state", reportedState);
        patch.AppendReplace("/last_seen", DateTime.UtcNow);
        patch.AppendReplace("/status", MapCoordinatorStatus(reported.StatusMode));
        patch.AppendReplace("/wifi_rssi", reported.WifiRssi);
        patch.AppendReplace("/uptime_seconds", reported.UptimeS);
        patch.AppendReplace("/towers_online", reported.TowersOnline);
        patch.AppendReplace("/firmware_version", reported.FwVersion);

        return patch;
    }

    /// <summary>
    /// Creates a JSON Patch document for updating ML predictions on a tower.
    /// </summary>
    /// <param name="predictions">The ML predictions to apply</param>
    /// <returns>A JsonPatchDocument with ML prediction updates</returns>
    public JsonPatchDocument CreateMlPredictionsPatch(TowerMlPredictions predictions)
    {
        var patch = new JsonPatchDocument();

        var mlPredictions = new Dictionary<string, object>
        {
            ["model_name"] = predictions.ModelName,
            ["model_version"] = predictions.ModelVersion,
            ["generated_at"] = predictions.GeneratedAt
        };

        if (predictions.PredictedHeightCm.HasValue)
            mlPredictions["predicted_height_cm"] = predictions.PredictedHeightCm.Value;

        if (predictions.PredictedHarvestDate.HasValue)
            mlPredictions["predicted_harvest_date"] = predictions.PredictedHarvestDate.Value.ToString("yyyy-MM-dd");

        if (predictions.DaysToHarvest.HasValue)
            mlPredictions["days_to_harvest"] = predictions.DaysToHarvest.Value;

        if (predictions.GrowthRateCmPerDay.HasValue)
            mlPredictions["growth_rate_cm_per_day"] = predictions.GrowthRateCmPerDay.Value;

        if (predictions.HealthScore.HasValue)
            mlPredictions["health_score"] = predictions.HealthScore.Value;

        if (predictions.AnomalyScore.HasValue)
            mlPredictions["anomaly_score"] = predictions.AnomalyScore.Value;

        if (predictions.RecommendedPh.HasValue)
            mlPredictions["recommended_ph"] = predictions.RecommendedPh.Value;

        if (predictions.RecommendedEc.HasValue)
            mlPredictions["recommended_ec"] = predictions.RecommendedEc.Value;

        if (predictions.RecommendedLightHours.HasValue)
            mlPredictions["recommended_light_hours"] = predictions.RecommendedLightHours.Value;

        patch.AppendReplace("/ml_predictions", mlPredictions);

        return patch;
    }

    /// <summary>
    /// Creates tower environmental telemetry for ADT.
    /// </summary>
    public object CreateTowerEnvironmentalTelemetry(TowerReportedState reported)
    {
        return new
        {
            air_temp_c = reported.AirTempC,
            humidity_pct = reported.HumidityPct,
            light_lux = reported.LightLux
        };
    }

    /// <summary>
    /// Creates coordinator ambient telemetry for ADT.
    /// </summary>
    public object CreateCoordinatorAmbientTelemetry(CoordinatorReportedState reported)
    {
        return new
        {
            ambient_light_lux = reported.LightLux ?? 0,
            ambient_temp_c = reported.TempC
        };
    }

    /// <summary>
    /// Generates the ADT twin ID for a tower.
    /// </summary>
    public string GetTowerTwinId(string towerId)
    {
        return SanitizeTwinId($"tower-{towerId}");
    }

    /// <summary>
    /// Generates the ADT twin ID for a coordinator.
    /// </summary>
    public string GetCoordinatorTwinId(string coordId)
    {
        return SanitizeTwinId($"coordinator-{coordId}");
    }

    /// <summary>
    /// Generates the relationship ID for a coordinator-tower relationship.
    /// </summary>
    public string GetHasTowerRelationshipId(string coordId, string towerId)
    {
        return $"{GetCoordinatorTwinId(coordId)}-hasTower-{GetTowerTwinId(towerId)}";
    }

    private Dictionary<string, object> BuildMlPredictions(TowerTwin tower)
    {
        var predictions = new Dictionary<string, object>
        {
            ["generated_at"] = DateTime.UtcNow
        };

        if (tower.PredictedHeightCm.HasValue)
        {
            predictions["predicted_height_cm"] = tower.PredictedHeightCm.Value;
        }

        if (tower.ExpectedHarvestDate.HasValue)
        {
            predictions["predicted_harvest_date"] = tower.ExpectedHarvestDate.Value.ToString("yyyy-MM-dd");
            predictions["days_to_harvest"] = Math.Max(0, (int)(tower.ExpectedHarvestDate.Value - DateTime.UtcNow).TotalDays);
        }

        // Calculate growth rate if we have height history
        if (tower.LastHeightCm.HasValue && tower.PlantingDate.HasValue && tower.LastHeightAt.HasValue)
        {
            var daysSincePlanting = (tower.LastHeightAt.Value - tower.PlantingDate.Value).TotalDays;
            if (daysSincePlanting > 0)
            {
                predictions["growth_rate_cm_per_day"] = Math.Round(tower.LastHeightCm.Value / daysSincePlanting, 2);
            }
        }

        return predictions;
    }

    /// <summary>
    /// Sanitizes a string to be a valid ADT twin ID.
    /// ADT twin IDs must be alphanumeric with hyphens and underscores only.
    /// </summary>
    private string SanitizeTwinId(string id)
    {
        // Replace colons (from MAC addresses) with hyphens
        var sanitized = id.Replace(":", "-").Replace(" ", "-");
        
        // Remove any other invalid characters
        return string.Concat(sanitized.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
    }

    private string MapStatusMode(string statusMode)
    {
        return statusMode?.ToLowerInvariant() switch
        {
            "operational" => "operational",
            "pairing" => "pairing",
            "ota" => "ota",
            "error" => "error",
            "idle" => "offline",
            _ => "offline"
        };
    }

    private string MapCoordinatorStatus(string statusMode)
    {
        return statusMode?.ToLowerInvariant() switch
        {
            "operational" => "online",
            "pairing" => "pairing",
            "maintenance" => "maintenance",
            "error" => "error",
            _ => "offline"
        };
    }
}

/// <summary>
/// ML predictions data structure for tower twins.
/// Used for updating ml_predictions property in ADT.
/// </summary>
public class TowerMlPredictions
{
    public string ModelName { get; set; } = "growth-predictor";
    public string ModelVersion { get; set; } = "1.0.0";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public double? PredictedHeightCm { get; set; }
    public DateTime? PredictedHarvestDate { get; set; }
    public int? DaysToHarvest { get; set; }
    public double? GrowthRateCmPerDay { get; set; }
    public double? HealthScore { get; set; }
    public double? AnomalyScore { get; set; }
    public double? RecommendedPh { get; set; }
    public double? RecommendedEc { get; set; }
    public int? RecommendedLightHours { get; set; }
}
