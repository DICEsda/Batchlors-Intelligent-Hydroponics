namespace IoT.Backend.Services;

/// <summary>
/// Background service that periodically processes pending sync operations
/// and checks for stale twins (devices that haven't reported in a while)
/// </summary>
public class TwinSyncBackgroundService : BackgroundService
{
    private readonly ITwinService _twinService;
    private readonly ILogger<TwinSyncBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    // Default intervals (can be overridden in appsettings.json)
    private readonly TimeSpan _syncInterval;
    private readonly TimeSpan _staleCheckInterval;
    private readonly TimeSpan _staleThreshold;

    public TwinSyncBackgroundService(
        ITwinService twinService,
        ILogger<TwinSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _twinService = twinService;
        _logger = logger;
        _configuration = configuration;

        // Read intervals from configuration or use defaults
        _syncInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("TwinSync:SyncIntervalSeconds", 5));
        _staleCheckInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("TwinSync:StaleCheckIntervalSeconds", 30));
        _staleThreshold = TimeSpan.FromSeconds(
            _configuration.GetValue("TwinSync:StaleThresholdSeconds", 120));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TwinSyncBackgroundService starting. SyncInterval={SyncInterval}s, StaleCheckInterval={StaleCheckInterval}s, StaleThreshold={StaleThreshold}s",
            _syncInterval.TotalSeconds,
            _staleCheckInterval.TotalSeconds,
            _staleThreshold.TotalSeconds);

        // Track when we last checked for stale twins
        var lastStaleCheck = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process pending sync operations (retry commands to devices)
                await ProcessPendingSyncsAsync(stoppingToken);

                // Check for stale twins periodically (less frequent than sync)
                if (DateTime.UtcNow - lastStaleCheck >= _staleCheckInterval)
                {
                    await CheckStaleTwinsAsync(stoppingToken);
                    lastStaleCheck = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown - don't log as error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TwinSyncBackgroundService loop");
            }

            // Wait before next iteration
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TwinSyncBackgroundService stopped");
    }

    private async Task ProcessPendingSyncsAsync(CancellationToken ct)
    {
        try
        {
            await _twinService.ProcessPendingSyncsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending syncs");
        }
    }

    private async Task CheckStaleTwinsAsync(CancellationToken ct)
    {
        try
        {
            await _twinService.CheckAndMarkStaleTwinsAsync(_staleThreshold, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for stale twins");
        }
    }
}
