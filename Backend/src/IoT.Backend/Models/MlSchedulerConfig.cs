namespace IoT.Backend.Models;

/// <summary>
/// Configuration options for the ML Scheduler background service.
/// Controls how often ML predictions are run and whether to sync to Azure Digital Twins.
/// </summary>
public class MlSchedulerConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string Section = "MlScheduler";

    /// <summary>
    /// Whether the ML scheduler is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to run ML predictions (in minutes).
    /// Default: 60 (every hour)
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// How many towers to process in each batch to avoid overloading.
    /// Default: 10
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Whether to sync ML predictions to Azure Digital Twins.
    /// Default: false (only sync to MongoDB)
    /// </summary>
    public bool SyncToAdt { get; set; } = false;

    /// <summary>
    /// How many hours of telemetry to average for ML predictions.
    /// Default: 24 hours
    /// </summary>
    public int TelemetryHoursToAverage { get; set; } = 24;

    /// <summary>
    /// Maximum number of consecutive failures before backing off.
    /// Default: 5
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Backoff multiplier when too many failures occur (applied to IntervalMinutes).
    /// Default: 2.0 (double the interval)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}
