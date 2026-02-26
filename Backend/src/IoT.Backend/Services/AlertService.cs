using IoT.Backend.Models;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Alert service implementation for monitoring telemetry and generating alerts.
/// Monitors all 10 alert types: battery, temperature (high/low), connectivity,
/// water level, pH, EC, pump failure, coordinator offline, tower offline.
/// </summary>
public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepo;
    private readonly IFarmRepository _farmRepo;
    private readonly IWsBroadcaster _wsBroadcaster;
    private readonly ILogger<AlertService> _logger;

    // Alert thresholds
    private const float LOW_BATTERY_THRESHOLD = 3.0f;        // Volts
    private const float HIGH_TEMP_THRESHOLD = 35.0f;         // °C
    private const float LOW_TEMP_THRESHOLD = 15.0f;          // °C
    private const int CONNECTIVITY_TIMEOUT_SECONDS = 300;    // 5 minutes
    private const float LOW_WATER_THRESHOLD = 20.0f;         // Percentage
    private const float PH_MIN = 5.5f;
    private const float PH_MAX = 7.5f;
    // EC thresholds are dynamic based on setpoints

    public AlertService(
        IAlertRepository alertRepo,
        IFarmRepository farmRepo,
        IWsBroadcaster wsBroadcaster,
        ILogger<AlertService> logger)
    {
        _alertRepo = alertRepo;
        _farmRepo = farmRepo;
        _wsBroadcaster = wsBroadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Checks coordinator telemetry for alert conditions.
    /// </summary>
    public async Task CheckCoordinatorAlertsAsync(Coordinator coordinator, CancellationToken ct = default)
    {
        var coordId = coordinator.CoordId;
        var farmId = coordinator.FarmId ?? "unknown";

        // 1. Check coordinator connectivity (offline if last_seen > 5 minutes ago)
        var timeSinceLastSeen = DateTime.UtcNow - coordinator.LastSeen;
        if (timeSinceLastSeen.TotalSeconds > CONNECTIVITY_TIMEOUT_SECONDS)
        {
            await CreateAlertAsync(
                farmId, coordId, null,
                "warning", "connectivity",
                $"Coordinator {coordId} has not reported data for {(int)timeSinceLastSeen.TotalMinutes} minutes",
                ct);
        }
        else
        {
            // Auto-resolve connectivity alert if back online
            await AutoResolveAlertAsync($"{farmId}:{coordId}:connectivity", ct);
        }

        // 2. Check temperature (if available)
        _logger.LogDebug("Checking coordinator {CoordId} temperature: {TempC}°C (threshold: {Threshold}°C)", 
            coordId, coordinator.TempC, HIGH_TEMP_THRESHOLD);
        
        if (coordinator.TempC > 0)
        {
            if (coordinator.TempC > HIGH_TEMP_THRESHOLD)
            {
                _logger.LogWarning("Temperature high detected: {TempC}°C > {Threshold}°C", 
                    coordinator.TempC, HIGH_TEMP_THRESHOLD);
                
                await CreateAlertAsync(
                    farmId, coordId, null,
                    "critical", "temperature_high",
                    $"Coordinator {coordId} temperature is critically high: {coordinator.TempC:F1}°C",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{coordId}:temperature_high", ct);
            }

            if (coordinator.TempC < LOW_TEMP_THRESHOLD)
            {
                await CreateAlertAsync(
                    farmId, coordId, null,
                    "warning", "temperature_low",
                    $"Coordinator {coordId} temperature is too low: {coordinator.TempC:F1}°C",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{coordId}:temperature_low", ct);
            }
        }

        // 3. Check water level (if available)
        if (coordinator.WaterLevelPct.HasValue)
        {
            if (coordinator.WaterLevelPct.Value < LOW_WATER_THRESHOLD)
            {
                await CreateAlertAsync(
                    farmId, coordId, null,
                    "critical", "water_level",
                    $"Coordinator {coordId} water level is critically low: {coordinator.WaterLevelPct.Value:F1}%",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{coordId}:water_level", ct);
            }
        }

        // 4. Check pH (if available)
        if (coordinator.Ph.HasValue)
        {
            if (coordinator.Ph.Value < PH_MIN || coordinator.Ph.Value > PH_MAX)
            {
                await CreateAlertAsync(
                    farmId, coordId, null,
                    "warning", "ph_out_of_range",
                    $"Coordinator {coordId} pH is out of range: {coordinator.Ph.Value:F2} (target: {PH_MIN}-{PH_MAX})",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{coordId}:ph_out_of_range", ct);
            }
        }

        // 5. Check EC (if available and setpoint exists)
        if (coordinator.EcMsCm.HasValue && coordinator.Setpoints?.EcTarget != null)
        {
            var deviation = Math.Abs(coordinator.EcMsCm.Value - coordinator.Setpoints.EcTarget);
            var tolerance = coordinator.Setpoints.EcTarget * 0.15f; // 15% tolerance

            if (deviation > tolerance)
            {
                await CreateAlertAsync(
                    farmId, coordId, null,
                    "warning", "ec_out_of_range",
                    $"Coordinator {coordId} EC is out of range: {coordinator.EcMsCm.Value:F2} (target: {coordinator.Setpoints.EcTarget:F2})",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{coordId}:ec_out_of_range", ct);
            }
        }

        // 6. Pump failure detection disabled — MainPumpOn == false is normal
        //    scheduled operation, not a hardware failure. True pump failure
        //    detection requires comparing desired vs reported state via the
        //    digital twin system. See GitHub issue #68.
        //    Auto-resolve any existing pump_failure alerts from the old logic.
        await AutoResolveAlertAsync($"{farmId}:{coordId}:pump_failure", ct);
    }

    /// <summary>
    /// Checks tower telemetry for alert conditions.
    /// </summary>
    public async Task CheckTowerAlertsAsync(Tower tower, CancellationToken ct = default)
    {
        var towerId = tower.TowerId;
        var farmId = tower.FarmId ?? "unknown";
        var coordId = tower.CoordId;

        // 1. Check tower connectivity (offline if last_seen > 5 minutes ago)
        var timeSinceLastSeen = DateTime.UtcNow - tower.LastSeen;
        if (timeSinceLastSeen.TotalSeconds > CONNECTIVITY_TIMEOUT_SECONDS)
        {
            await CreateAlertAsync(
                farmId, coordId, towerId,
                "warning", "connectivity",
                $"Tower {towerId} has not reported data for {(int)timeSinceLastSeen.TotalMinutes} minutes",
                ct);
        }
        else
        {
            // Auto-resolve connectivity alert if back online
            await AutoResolveAlertAsync($"{farmId}:{towerId}:connectivity", ct);
        }

        // 2. Check battery voltage (if available) - convert mV to V
        if (tower.VbatMv > 0)
        {
            var batteryVolts = tower.VbatMv / 1000.0f;
            if (batteryVolts < LOW_BATTERY_THRESHOLD)
            {
                await CreateAlertAsync(
                    farmId, coordId, towerId,
                    "critical", "battery_low",
                    $"Tower {towerId} battery is critically low: {batteryVolts:F2}V",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{towerId}:battery_low", ct);
            }
        }

        // 3. Check temperature (if available)
        if (tower.AirTempC > 0)
        {
            if (tower.AirTempC > HIGH_TEMP_THRESHOLD)
            {
                await CreateAlertAsync(
                    farmId, coordId, towerId,
                    "critical", "temperature_high",
                    $"Tower {towerId} temperature is critically high: {tower.AirTempC:F1}°C",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{towerId}:temperature_high", ct);
            }

            if (tower.AirTempC < LOW_TEMP_THRESHOLD)
            {
                await CreateAlertAsync(
                    farmId, coordId, towerId,
                    "warning", "temperature_low",
                    $"Tower {towerId} temperature is too low: {tower.AirTempC:F1}°C",
                    ct);
            }
            else
            {
                await AutoResolveAlertAsync($"{farmId}:{towerId}:temperature_low", ct);
            }
        }

        // 4. Check if tower is marked as offline
        if (tower.StatusMode == "offline" || tower.StatusMode == "error")
        {
            await CreateAlertAsync(
                farmId, coordId, towerId,
                "critical", "tower_offline",
                $"Tower {towerId} is offline",
                ct);
        }
        else
        {
            await AutoResolveAlertAsync($"{farmId}:{towerId}:tower_offline", ct);
        }
    }

    /// <summary>
    /// Creates a new alert if one doesn't already exist with the same alert_key.
    /// Broadcasts the alert via WebSocket to all connected clients.
    /// </summary>
    public async Task CreateAlertAsync(
        string farmId,
        string? coordId,
        string? towerId,
        string severity,
        string category,
        string message,
        CancellationToken ct = default)
    {
        // Build alert_key for deduplication
        var deviceId = towerId ?? coordId ?? "unknown";
        var alertKey = $"{farmId}:{deviceId}:{category}";

        // Check if alert already exists and is active
        var existingAlert = await _alertRepo.GetActiveAlertByKeyAsync(alertKey, ct);
        if (existingAlert != null)
        {
            // Alert already exists, don't create duplicate
            return;
        }

        // Create new alert
        var alert = new Alert
        {
            Id = null!, // Let MongoDB generate the _id
            FarmId = farmId,
            CoordId = coordId,
            TowerId = towerId,
            Severity = severity,
            Status = "active",
            Message = message,
            Category = category,
            AlertKey = alertKey,
            CreatedAt = DateTime.UtcNow
        };

        await _alertRepo.UpsertAsync(alert, ct);

        _logger.LogInformation(
            "Alert created: {Category} - {Severity} - Farm: {FarmId}, Coord: {CoordId}, Tower: {TowerId}",
            category, severity, farmId, coordId ?? "N/A", towerId ?? "N/A");

        // Broadcast via WebSocket
        var payload = new AlertPayload
        {
            AlertId = alert.Id,
            FarmId = alert.FarmId,
            CoordId = alert.CoordId,
            TowerId = alert.TowerId,
            Severity = alert.Severity,
            Status = alert.Status,
            Message = alert.Message,
            Category = alert.Category,
            Timestamp = ((DateTimeOffset)alert.CreatedAt).ToUnixTimeMilliseconds()
        };

        await _wsBroadcaster.BroadcastAlertCreatedAsync(payload, ct);

        // Increment farm's active alert count
        await _farmRepo.IncrementAlertCountAsync(farmId, ct);
        
        // Get updated farm for broadcasting
        var farm = await _farmRepo.GetByFarmIdAsync(farmId, ct);
        if (farm != null)
        {
            // Broadcast farm update
            await _wsBroadcaster.BroadcastFarmUpdateAsync(new FarmUpdatePayload
            {
                FarmId = farm.FarmId,
                Name = farm.Name,
                CoordinatorCount = farm.CoordinatorCount,
                TowerCount = farm.TowerCount,
                ActiveAlertCount = farm.ActiveAlertCount
            }, ct);
        }
    }

    /// <summary>
    /// Auto-resolves an alert if it exists and is active.
    /// Used when conditions return to normal.
    /// </summary>
    public async Task AutoResolveAlertAsync(string alertKey, CancellationToken ct = default)
    {
        var alert = await _alertRepo.GetActiveAlertByKeyAsync(alertKey, ct);
        if (alert == null)
        {
            // No active alert to resolve
            return;
        }

        // Resolve the alert using repository method (avoids MongoDB _id immutable error)
        await _alertRepo.ResolveAsync(alert.Id, ct);

        _logger.LogInformation("Alert auto-resolved: {AlertKey}", alertKey);

        // Broadcast via WebSocket
        var payload = new AlertPayload
        {
            AlertId = alert.Id,
            FarmId = alert.FarmId,
            CoordId = alert.CoordId,
            TowerId = alert.TowerId,
            Severity = alert.Severity,
            Status = "resolved", // Alert is now resolved
            Message = alert.Message,
            Category = alert.Category,
            Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds()
        };

        await _wsBroadcaster.BroadcastAlertUpdatedAsync(payload, ct);

        // Decrement farm's active alert count
        await _farmRepo.DecrementAlertCountAsync(alert.FarmId, ct);
        
        // Get updated farm for broadcasting
        var farm = await _farmRepo.GetByFarmIdAsync(alert.FarmId, ct);
        if (farm != null)
        {
            // Broadcast farm update
            await _wsBroadcaster.BroadcastFarmUpdateAsync(new FarmUpdatePayload
            {
                FarmId = farm.FarmId,
                Name = farm.Name,
                CoordinatorCount = farm.CoordinatorCount,
                TowerCount = farm.TowerCount,
                ActiveAlertCount = farm.ActiveAlertCount
            }, ct);
        }
    }
}
