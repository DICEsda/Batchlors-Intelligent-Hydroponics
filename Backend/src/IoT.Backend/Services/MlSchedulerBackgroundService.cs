using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Models.Ml;
using IoT.Backend.Repositories;
using Microsoft.Extensions.Options;

namespace IoT.Backend.Services;

/// <summary>
/// Background service that periodically runs ML inference on all towers with active crops
/// and syncs predictions to MongoDB (and optionally Azure Digital Twins).
/// 
/// DI Registration in Program.cs:
/// <code>
/// // ML Scheduler
/// builder.Services.Configure&lt;MlSchedulerConfig&gt;(
///     builder.Configuration.GetSection(MlSchedulerConfig.Section));
/// builder.Services.AddHostedService&lt;MlSchedulerBackgroundService&gt;();
/// </code>
/// 
/// Configuration in appsettings.json:
/// <code>
/// {
///   "MlScheduler": {
///     "Enabled": true,
///     "IntervalMinutes": 60,
///     "BatchSize": 10,
///     "SyncToAdt": false,
///     "TelemetryHoursToAverage": 24,
///     "MaxConsecutiveFailures": 5,
///     "BackoffMultiplier": 2.0
///   }
/// }
/// </code>
/// </summary>
public class MlSchedulerBackgroundService : BackgroundService
{
    private readonly ITwinRepository _twinRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IMlService _mlService;
    private readonly IAzureDigitalTwinsService _adtService;
    private readonly AdtTwinMapper _adtMapper;
    private readonly ILogger<MlSchedulerBackgroundService> _logger;
    private readonly MlSchedulerConfig _config;

    private int _consecutiveFailures = 0;
    private TimeSpan _currentInterval;

    public MlSchedulerBackgroundService(
        ITwinRepository twinRepository,
        ITelemetryRepository telemetryRepository,
        IMlService mlService,
        IAzureDigitalTwinsService adtService,
        AdtTwinMapper adtMapper,
        IOptions<MlSchedulerConfig> config,
        ILogger<MlSchedulerBackgroundService> logger)
    {
        _twinRepository = twinRepository;
        _telemetryRepository = telemetryRepository;
        _mlService = mlService;
        _adtService = adtService;
        _adtMapper = adtMapper;
        _config = config.Value;
        _logger = logger;
        _currentInterval = TimeSpan.FromMinutes(_config.IntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("MlSchedulerBackgroundService is disabled");
            return;
        }

        _logger.LogInformation(
            "MlSchedulerBackgroundService starting. Interval={Interval}min, BatchSize={BatchSize}, SyncToAdt={SyncToAdt}",
            _config.IntervalMinutes,
            _config.BatchSize,
            _config.SyncToAdt);

        // Initial delay to let other services start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMlInferenceCycleAsync(stoppingToken);
                
                // Reset failures on success
                _consecutiveFailures = 0;
                _currentInterval = TimeSpan.FromMinutes(_config.IntervalMinutes);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Error in MlSchedulerBackgroundService cycle (failure #{FailureCount})", 
                    _consecutiveFailures);

                // Apply backoff if too many failures
                if (_consecutiveFailures >= _config.MaxConsecutiveFailures)
                {
                    _currentInterval = TimeSpan.FromMinutes(_config.IntervalMinutes * _config.BackoffMultiplier);
                    _logger.LogWarning(
                        "Too many consecutive failures ({Count}), backing off to {Interval}min interval",
                        _consecutiveFailures, _currentInterval.TotalMinutes);
                }
            }

            try
            {
                await Task.Delay(_currentInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("MlSchedulerBackgroundService stopped");
    }

    /// <summary>
    /// Runs a single ML inference cycle for all towers with active crops.
    /// </summary>
    private async Task RunMlInferenceCycleAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting ML inference cycle");

        // Get all towers with active crops
        var towers = await _twinRepository.GetTowerTwinsWithActiveCropAsync(ct);
        
        if (towers.Count == 0)
        {
            _logger.LogDebug("No towers with active crops found, skipping ML inference");
            return;
        }

        _logger.LogInformation("Found {TowerCount} towers with active crops", towers.Count);

        var stats = new MlInferenceCycleStats();

        // Process towers in batches
        var batches = towers
            .Select((tower, index) => new { tower, index })
            .GroupBy(x => x.index / _config.BatchSize)
            .Select(g => g.Select(x => x.tower).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();
            
            var batchTasks = batch.Select(tower => ProcessTowerAsync(tower, stats, ct));
            await Task.WhenAll(batchTasks);

            // Small delay between batches to avoid overloading
            if (batches.IndexOf(batch) < batches.Count - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "ML inference cycle completed in {Duration}ms. Processed={Processed}, Success={Success}, Failed={Failed}, Skipped={Skipped}, AdtSynced={AdtSynced}",
            duration.TotalMilliseconds,
            stats.TowersProcessed,
            stats.SuccessCount,
            stats.FailureCount,
            stats.SkippedCount,
            stats.AdtSyncCount);
    }

    /// <summary>
    /// Processes a single tower: gets telemetry, runs ML prediction, and updates twin.
    /// </summary>
    private async Task ProcessTowerAsync(TowerTwin tower, MlInferenceCycleStats stats, CancellationToken ct)
    {
        Interlocked.Increment(ref stats.TowersProcessed);

        try
        {
            // Validate tower has required data
            if (tower.CropType == CropType.Unknown || tower.PlantingDate == null)
            {
                _logger.LogDebug("Tower {TowerId} missing crop info, skipping", tower.TowerId);
                Interlocked.Increment(ref stats.SkippedCount);
                return;
            }

            // Calculate days since planting
            var daysSincePlanting = (int)(DateTime.UtcNow - tower.PlantingDate.Value).TotalDays;
            if (daysSincePlanting < 0)
            {
                _logger.LogDebug("Tower {TowerId} has future planting date, skipping", tower.TowerId);
                Interlocked.Increment(ref stats.SkippedCount);
                return;
            }

            // Get average telemetry for the configured time window
            var telemetryAverage = await GetAverageTelemetryAsync(tower, ct);

            // Build ML request
            var request = new GrowthPredictionRequest
            {
                TowerId = tower.TowerId,
                CropType = tower.CropType.ToString(),
                CurrentHeightCm = tower.LastHeightCm ?? 0,
                DaysSincePlanting = daysSincePlanting,
                AvgTempC = telemetryAverage?.AvgTempC,
                AvgHumidityPct = telemetryAverage?.AvgHumidityPct,
                AvgLightLux = telemetryAverage?.AvgLightLux,
                AvgPh = telemetryAverage?.AvgPh,
                AvgEcMsCm = telemetryAverage?.AvgEc
            };

            // Call ML service
            var prediction = await _mlService.PredictGrowthAsync(request, ct);
            
            if (prediction == null)
            {
                _logger.LogWarning("ML service returned null for tower {TowerId}", tower.TowerId);
                Interlocked.Increment(ref stats.FailureCount);
                return;
            }

            // Create ML predictions model
            var mlPredictions = new MlPredictions
            {
                PredictedHeightCm = prediction.PredictedHeightCm,
                ExpectedHarvestDate = DateTime.TryParse(prediction.PredictedHarvestDate, out var harvestDate) 
                    ? harvestDate 
                    : null,
                DaysToHarvest = prediction.DaysToHarvest,
                GrowthRateCmPerDay = prediction.GrowthRateCmPerDay,
                HealthScore = prediction.HealthScore,
                Confidence = prediction.Confidence,
                ModelName = prediction.ModelName,
                ModelVersion = prediction.ModelVersion,
                LastUpdatedAt = DateTime.UtcNow,
                InputAvgTempC = telemetryAverage?.AvgTempC,
                InputAvgHumidityPct = telemetryAverage?.AvgHumidityPct,
                InputAvgLightLux = telemetryAverage?.AvgLightLux
            };

            // Update tower twin in MongoDB
            var updated = await _twinRepository.UpdateTowerMlPredictionsAsync(tower.TowerId, mlPredictions, ct);
            
            if (!updated)
            {
                _logger.LogWarning("Failed to update ML predictions for tower {TowerId}", tower.TowerId);
                Interlocked.Increment(ref stats.FailureCount);
                return;
            }

            _logger.LogDebug(
                "Updated ML predictions for tower {TowerId}: height={Height}cm, harvest={HarvestDate}, health={Health}",
                tower.TowerId,
                prediction.PredictedHeightCm,
                prediction.PredictedHarvestDate,
                prediction.HealthScore);

            // Optionally sync to Azure Digital Twins
            if (_config.SyncToAdt)
            {
                await SyncToAzureDigitalTwinsAsync(tower.TowerId, mlPredictions, ct);
                Interlocked.Increment(ref stats.AdtSyncCount);
            }

            Interlocked.Increment(ref stats.SuccessCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tower {TowerId}", tower.TowerId);
            Interlocked.Increment(ref stats.FailureCount);
        }
    }

    /// <summary>
    /// Gets average telemetry values for a tower over the configured time window.
    /// </summary>
    private async Task<TelemetryAverage?> GetAverageTelemetryAsync(TowerTwin tower, CancellationToken ct)
    {
        try
        {
            var from = DateTime.UtcNow.AddHours(-_config.TelemetryHoursToAverage);
            var to = DateTime.UtcNow;

            // Get tower telemetry
            var towerTelemetry = await _telemetryRepository.GetTowerTelemetryAsync(
                tower.FarmId, tower.CoordId, tower.TowerId, from, to, limit: 1000, ct);

            // Get reservoir telemetry (for pH and EC)
            var reservoirTelemetry = await _telemetryRepository.GetReservoirTelemetryAsync(
                tower.FarmId, tower.CoordId, from, to, limit: 1000, ct);

            if (towerTelemetry.Count == 0)
            {
                _logger.LogDebug("No telemetry found for tower {TowerId} in last {Hours}h", 
                    tower.TowerId, _config.TelemetryHoursToAverage);
                return null;
            }

            // Calculate averages
            var avgTempC = towerTelemetry.Average(t => t.AirTempC);
            var avgHumidity = towerTelemetry.Average(t => t.HumidityPct);
            var avgLight = towerTelemetry.Average(t => t.LightLux);

            double? avgPh = null;
            double? avgEc = null;

            if (reservoirTelemetry.Count > 0)
            {
                var phReadings = reservoirTelemetry.Where(t => t.Ph.HasValue).Select(t => t.Ph!.Value).ToList();
                var ecReadings = reservoirTelemetry.Where(t => t.EcMsCm.HasValue).Select(t => t.EcMsCm!.Value).ToList();
                
                if (phReadings.Count > 0) avgPh = phReadings.Average();
                if (ecReadings.Count > 0) avgEc = ecReadings.Average();
            }

            return new TelemetryAverage
            {
                AvgTempC = avgTempC,
                AvgHumidityPct = avgHumidity,
                AvgLightLux = avgLight,
                AvgPh = avgPh,
                AvgEc = avgEc
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting average telemetry for tower {TowerId}", tower.TowerId);
            return null;
        }
    }

    /// <summary>
    /// Syncs ML predictions to Azure Digital Twins using the AdtTwinMapper.
    /// Patches the nested /ml_predictions object to match the DTDL Tower model schema.
    /// </summary>
    private async Task SyncToAzureDigitalTwinsAsync(string towerId, MlPredictions predictions, CancellationToken ct)
    {
        try
        {
            // Check if ADT is configured
            if (!await _adtService.IsConfiguredAsync())
            {
                _logger.LogDebug("Azure Digital Twins is not configured, skipping sync for tower {TowerId}", towerId);
                return;
            }

            // Use mapper for twin ID and patch generation (ensures DTDL-compliant paths)
            var adtTwinId = _adtMapper.GetTowerTwinId(towerId);

            var mlPredictions = new TowerMlPredictions
            {
                PredictedHeightCm = predictions.PredictedHeightCm,
                PredictedHarvestDate = predictions.ExpectedHarvestDate,
                DaysToHarvest = predictions.DaysToHarvest,
                GrowthRateCmPerDay = predictions.GrowthRateCmPerDay,
                HealthScore = predictions.HealthScore,
                ModelName = predictions.ModelName ?? "growth-predictor",
                ModelVersion = predictions.ModelVersion ?? "1.0.0",
                GeneratedAt = predictions.LastUpdatedAt ?? DateTime.UtcNow
            };

            var patch = _adtMapper.CreateMlPredictionsPatch(mlPredictions);
            await _adtService.UpdateTwinPropertyAsync(adtTwinId, patch, ct);
            
            _logger.LogDebug("Synced ML predictions to ADT for tower {TowerId}", towerId);
        }
        catch (Exception ex)
        {
            // Don't fail the whole operation if ADT sync fails
            _logger.LogWarning(ex, "Failed to sync ML predictions to ADT for tower {TowerId}", towerId);
        }
    }

    /// <summary>
    /// Helper class to store average telemetry values.
    /// </summary>
    private class TelemetryAverage
    {
        public double AvgTempC { get; set; }
        public double AvgHumidityPct { get; set; }
        public double AvgLightLux { get; set; }
        public double? AvgPh { get; set; }
        public double? AvgEc { get; set; }
    }

    /// <summary>
    /// Helper class to track ML inference cycle statistics.
    /// </summary>
    private class MlInferenceCycleStats
    {
        public int TowersProcessed;
        public int SuccessCount;
        public int FailureCount;
        public int SkippedCount;
        public int AdtSyncCount;
    }
}
