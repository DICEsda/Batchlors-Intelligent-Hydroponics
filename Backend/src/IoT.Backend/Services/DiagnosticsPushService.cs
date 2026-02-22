using IoT.Backend.Models.Diagnostics;

namespace IoT.Backend.Services;

/// <summary>
/// Background service that periodically pushes the latest diagnostics snapshot
/// to all connected WebSocket clients via IWsBroadcaster.
/// Runs every 2 seconds so the frontend diagnostics dashboard updates in near-real-time.
/// </summary>
public sealed class DiagnosticsPushService : BackgroundService
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ILogger<DiagnosticsPushService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    public DiagnosticsPushService(
        IDiagnosticsService diagnostics,
        IWsBroadcaster broadcaster,
        ILogger<DiagnosticsPushService> logger)
    {
        _diagnostics = diagnostics;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DiagnosticsPushService started – broadcasting every {Interval}s", Interval.TotalSeconds);

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (_broadcaster.ConnectedClientCount == 0)
                    continue;

                var snapshot = _diagnostics.GetCurrentSnapshot(_broadcaster.ConnectedClientCount);
                await _broadcaster.BroadcastDiagnosticsAsync(snapshot, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DiagnosticsPushService: failed to broadcast snapshot");
            }
        }

        _logger.LogInformation("DiagnosticsPushService stopped");
    }
}
