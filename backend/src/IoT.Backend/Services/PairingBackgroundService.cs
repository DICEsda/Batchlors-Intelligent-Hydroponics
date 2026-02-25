namespace IoT.Backend.Services;

/// <summary>
/// Background service that periodically checks for and expires timed-out pairing sessions.
/// Runs every 5 seconds to ensure sessions don't linger past their expiration time.
/// </summary>
public class PairingBackgroundService : BackgroundService
{
    private readonly IPairingService _pairingService;
    private readonly ILogger<PairingBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    public PairingBackgroundService(
        IPairingService pairingService,
        ILogger<PairingBackgroundService> logger)
    {
        _pairingService = pairingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PairingBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _pairingService.ExpireTimedOutSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for expired pairing sessions");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("PairingBackgroundService stopped");
    }
}
