using FluentAssertions;
using IoT.Backend.Models;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="AlertService"/>.
/// Covers CheckCoordinatorAlertsAsync, CheckTowerAlertsAsync,
/// CreateAlertAsync, and AutoResolveAlertAsync.
/// </summary>
public class AlertServiceTests
{
    private readonly IAlertRepository _alertRepo;
    private readonly IFarmRepository _farmRepo;
    private readonly IWsBroadcaster _wsBroadcaster;
    private readonly ILogger<AlertService> _logger;
    private readonly AlertService _sut;

    // Mirror the constants from AlertService for readability
    private const float LOW_BATTERY_THRESHOLD = 3.0f;
    private const float HIGH_TEMP_THRESHOLD = 35.0f;
    private const float LOW_TEMP_THRESHOLD = 15.0f;
    private const int CONNECTIVITY_TIMEOUT_SECONDS = 300;
    private const float LOW_WATER_THRESHOLD = 20.0f;
    private const float PH_MIN = 5.5f;
    private const float PH_MAX = 7.5f;

    public AlertServiceTests()
    {
        _alertRepo = Substitute.For<IAlertRepository>();
        _farmRepo = Substitute.For<IFarmRepository>();
        _wsBroadcaster = Substitute.For<IWsBroadcaster>();
        _logger = NullLogger<AlertService>.Instance;

        _sut = new AlertService(_alertRepo, _farmRepo, _wsBroadcaster, _logger);

        // Default: no existing active alert (so creates are allowed)
        _alertRepo.GetActiveAlertByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Default: farm exists for broadcast
        _farmRepo.GetByFarmIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Farm
            {
                FarmId = "farm-1",
                Name = "Test Farm",
                CoordinatorCount = 1,
                TowerCount = 2,
                ActiveAlertCount = 0
            });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Coordinator MakeCoordinator(
        string coordId = "coord-1",
        string farmId = "farm-1",
        float tempC = 25f,
        float? waterLevelPct = 50f,
        float? ph = 6.5f,
        float? ecMsCm = 1.5f,
        bool? mainPumpOn = true,
        DateTime? lastSeen = null,
        ReservoirSetpoints? setpoints = null)
    {
        return new Coordinator
        {
            CoordId = coordId,
            FarmId = farmId,
            TempC = tempC,
            WaterLevelPct = waterLevelPct,
            Ph = ph,
            EcMsCm = ecMsCm,
            MainPumpOn = mainPumpOn,
            LastSeen = lastSeen ?? DateTime.UtcNow,
            Setpoints = setpoints ?? new ReservoirSetpoints { EcTarget = 1.5f }
        };
    }

    private static Tower MakeTower(
        string towerId = "tower-1",
        string coordId = "coord-1",
        string farmId = "farm-1",
        float airTempC = 25f,
        int vbatMv = 3500,
        string statusMode = "operational",
        DateTime? lastSeen = null)
    {
        return new Tower
        {
            TowerId = towerId,
            CoordId = coordId,
            FarmId = farmId,
            AirTempC = airTempC,
            VbatMv = vbatMv,
            StatusMode = statusMode,
            LastSeen = lastSeen ?? DateTime.UtcNow
        };
    }

    private static Alert MakeActiveAlert(string alertKey, string farmId = "farm-1")
    {
        return new Alert
        {
            Id = "alert-123",
            FarmId = farmId,
            AlertKey = alertKey,
            Status = "active",
            Severity = "warning",
            Category = alertKey.Split(':').Last(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    #region CheckCoordinatorAlertsAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_LastSeenExceedsTimeout_CreatesConnectivityAlert()
    {
        // Arrange
        var coord = MakeCoordinator(
            lastSeen: DateTime.UtcNow.AddSeconds(-(CONNECTIVITY_TIMEOUT_SECONDS + 60)));

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – a connectivity alert was upserted
        await _alertRepo.Received(1).UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "connectivity" &&
                a.Severity == "warning" &&
                a.FarmId == "farm-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_LastSeenRecent_AutoResolvesConnectivityAlert()
    {
        // Arrange
        var coord = MakeCoordinator(lastSeen: DateTime.UtcNow);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:connectivity");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:connectivity", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_HighTemperature_CreatesCriticalTempHighAlert()
    {
        // Arrange
        var coord = MakeCoordinator(tempC: 40f);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "temperature_high" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NormalTemperature_AutoResolvesTempHighAlert()
    {
        // Arrange – temp is normal (25°C), but there's an existing high-temp alert
        var coord = MakeCoordinator(tempC: 25f);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:temperature_high");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:temperature_high", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_LowTemperature_CreatesWarningTempLowAlert()
    {
        // Arrange – temp is 10°C (> 0 and < 15)
        var coord = MakeCoordinator(tempC: 10f);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "temperature_low" &&
                a.Severity == "warning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NormalTemperature_AutoResolvesTempLowAlert()
    {
        // Arrange
        var coord = MakeCoordinator(tempC: 25f);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:temperature_low");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:temperature_low", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_LowWaterLevel_CreatesCriticalWaterLevelAlert()
    {
        // Arrange
        var coord = MakeCoordinator(waterLevelPct: 10f);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "water_level" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NormalWaterLevel_AutoResolvesWaterLevelAlert()
    {
        // Arrange
        var coord = MakeCoordinator(waterLevelPct: 80f);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:water_level");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:water_level", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_PhBelowMin_CreatesPhOutOfRangeAlert()
    {
        // Arrange – pH 4.0 is below PH_MIN (5.5)
        var coord = MakeCoordinator(ph: 4.0f);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "ph_out_of_range" &&
                a.Severity == "warning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_PhAboveMax_CreatesPhOutOfRangeAlert()
    {
        // Arrange – pH 8.5 is above PH_MAX (7.5)
        var coord = MakeCoordinator(ph: 8.5f);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "ph_out_of_range" &&
                a.Severity == "warning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_PhInRange_AutoResolvesPhAlert()
    {
        // Arrange
        var coord = MakeCoordinator(ph: 6.5f);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:ph_out_of_range");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:ph_out_of_range", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_EcOutOfRange_CreatesEcOutOfRangeAlert()
    {
        // Arrange – target 2.0, tolerance 15% = 0.3, so 2.5 is 0.5 deviation > 0.3
        var coord = MakeCoordinator(
            ecMsCm: 2.5f,
            setpoints: new ReservoirSetpoints { EcTarget = 2.0f });

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "ec_out_of_range" &&
                a.Severity == "warning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_EcInRange_AutoResolvesEcAlert()
    {
        // Arrange – target 2.0, tolerance 15% = 0.3, EC 2.1 → deviation 0.1 < 0.3
        var coord = MakeCoordinator(
            ecMsCm: 2.1f,
            setpoints: new ReservoirSetpoints { EcTarget = 2.0f });
        var existingAlert = MakeActiveAlert("farm-1:coord-1:ec_out_of_range");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:ec_out_of_range", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_PumpOff_DoesNotCreatePumpFailureAlert()
    {
        // Arrange — pump is off (normal scheduled operation)
        var coord = MakeCoordinator(mainPumpOn: false);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert — pump_failure alert creation was disabled (issue #68)
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "pump_failure"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_AlwaysAutoResolvesPumpFailureAlert()
    {
        // Arrange — existing pump_failure alert from old logic
        var coord = MakeCoordinator(mainPumpOn: false);
        var existingAlert = MakeActiveAlert("farm-1:coord-1:pump_failure");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:pump_failure", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert — stale pump_failure alerts are always auto-resolved
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region CheckTowerAlertsAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckTowerAlertsAsync_LastSeenExceedsTimeout_CreatesConnectivityAlert()
    {
        // Arrange
        var tower = MakeTower(
            lastSeen: DateTime.UtcNow.AddSeconds(-(CONNECTIVITY_TIMEOUT_SECONDS + 60)));

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "connectivity" &&
                a.Severity == "warning" &&
                a.TowerId == "tower-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_LastSeenRecent_AutoResolvesConnectivityAlert()
    {
        // Arrange
        var tower = MakeTower(lastSeen: DateTime.UtcNow);
        var existingAlert = MakeActiveAlert("farm-1:tower-1:connectivity");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:tower-1:connectivity", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_LowBattery_CreatesCriticalBatteryLowAlert()
    {
        // Arrange – 2500 mV = 2.5V < 3.0V threshold
        var tower = MakeTower(vbatMv: 2500);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "battery_low" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_GoodBattery_AutoResolvesBatteryLowAlert()
    {
        // Arrange – 3500 mV = 3.5V > 3.0V threshold
        var tower = MakeTower(vbatMv: 3500);
        var existingAlert = MakeActiveAlert("farm-1:tower-1:battery_low");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:tower-1:battery_low", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_HighTemperature_CreatesCriticalTempHighAlert()
    {
        // Arrange
        var tower = MakeTower(airTempC: 40f);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "temperature_high" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_LowTemperature_CreatesWarningTempLowAlert()
    {
        // Arrange – 10°C is > 0 and < 15
        var tower = MakeTower(airTempC: 10f);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "temperature_low" &&
                a.Severity == "warning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_StatusModeOffline_CreatesCriticalTowerOfflineAlert()
    {
        // Arrange
        var tower = MakeTower(statusMode: "offline");

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "tower_offline" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_StatusModeError_CreatesCriticalTowerOfflineAlert()
    {
        // Arrange
        var tower = MakeTower(statusMode: "error");

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a =>
                a.Category == "tower_offline" &&
                a.Severity == "critical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_StatusModeOperational_AutoResolvesTowerOfflineAlert()
    {
        // Arrange
        var tower = MakeTower(statusMode: "operational");
        var existingAlert = MakeActiveAlert("farm-1:tower-1:tower_offline");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:tower-1:tower_offline", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert
        await _alertRepo.Received().ResolveAsync("alert-123", Arg.Any<CancellationToken>());
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region CreateAlertAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAlertAsync_ExistingActiveAlertWithSameKey_DoesNotCreateDuplicate()
    {
        // Arrange – an active alert already exists for this key
        var existingAlert = MakeActiveAlert("farm-1:coord-1:temperature_high");
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:temperature_high", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.CreateAlertAsync("farm-1", "coord-1", null, "critical", "temperature_high",
            "Temperature is high", CancellationToken.None);

        // Assert – UpsertAsync should NOT have been called
        await _alertRepo.DidNotReceive().UpsertAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>());
        await _wsBroadcaster.DidNotReceive()
            .BroadcastAlertCreatedAsync(Arg.Any<AlertPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAlertAsync_NoExistingAlert_CreatesAlertAndBroadcastsAndIncrementsCount()
    {
        // Arrange – no existing alert (default mock returns null)

        // Act
        await _sut.CreateAlertAsync("farm-1", "coord-1", null, "critical", "temperature_high",
            "Temperature is high", CancellationToken.None);

        // Assert – alert was upserted
        await _alertRepo.Received(1).UpsertAsync(
            Arg.Is<Alert>(a =>
                a.FarmId == "farm-1" &&
                a.CoordId == "coord-1" &&
                a.TowerId == null &&
                a.Severity == "critical" &&
                a.Category == "temperature_high" &&
                a.Status == "active" &&
                a.AlertKey == "farm-1:coord-1:temperature_high"),
            Arg.Any<CancellationToken>());

        // Assert – broadcast was sent
        await _wsBroadcaster.Received(1).BroadcastAlertCreatedAsync(
            Arg.Is<AlertPayload>(p =>
                p.FarmId == "farm-1" &&
                p.Severity == "critical" &&
                p.Category == "temperature_high" &&
                p.Status == "active"),
            Arg.Any<CancellationToken>());

        // Assert – farm alert count was incremented
        await _farmRepo.Received(1).IncrementAlertCountAsync("farm-1", Arg.Any<CancellationToken>());

        // Assert – farm update was broadcast
        await _wsBroadcaster.Received(1).BroadcastFarmUpdateAsync(
            Arg.Is<FarmUpdatePayload>(p => p.FarmId == "farm-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAlertAsync_FarmNotFoundAfterCreation_DoesNotThrowAndSkipsFarmBroadcast()
    {
        // Arrange – farm lookup returns null
        _farmRepo.GetByFarmIdAsync("farm-1", Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act – should not throw
        var act = () => _sut.CreateAlertAsync("farm-1", "coord-1", null, "warning", "connectivity",
            "Offline", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        // Alert was still created
        await _alertRepo.Received(1).UpsertAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>());

        // Alert broadcast was still sent
        await _wsBroadcaster.Received(1).BroadcastAlertCreatedAsync(
            Arg.Any<AlertPayload>(), Arg.Any<CancellationToken>());

        // Farm update broadcast was NOT sent (farm is null)
        await _wsBroadcaster.DidNotReceive().BroadcastFarmUpdateAsync(
            Arg.Any<FarmUpdatePayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAlertAsync_WithTowerId_UsesTowerIdAsDeviceIdInAlertKey()
    {
        // Arrange – creating a tower-level alert

        // Act
        await _sut.CreateAlertAsync("farm-1", "coord-1", "tower-1", "critical", "battery_low",
            "Battery low", CancellationToken.None);

        // Assert – alert key should use towerId (not coordId)
        await _alertRepo.Received(1).UpsertAsync(
            Arg.Is<Alert>(a => a.AlertKey == "farm-1:tower-1:battery_low"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region AutoResolveAlertAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AutoResolveAlertAsync_NoActiveAlert_DoesNothing()
    {
        // Arrange – no active alert for this key (default mock returns null)

        // Act
        await _sut.AutoResolveAlertAsync("farm-1:coord-1:connectivity");

        // Assert – nothing was resolved or broadcast
        await _alertRepo.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _wsBroadcaster.DidNotReceive()
            .BroadcastAlertUpdatedAsync(Arg.Any<AlertPayload>(), Arg.Any<CancellationToken>());
        await _farmRepo.DidNotReceive()
            .DecrementAlertCountAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoResolveAlertAsync_ActiveAlertExists_ResolvesAndBroadcastsAndDecrementsCount()
    {
        // Arrange
        var existingAlert = MakeActiveAlert("farm-1:coord-1:connectivity");
        existingAlert.CoordId = "coord-1";
        existingAlert.FarmId = "farm-1";
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:connectivity", Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        // Act
        await _sut.AutoResolveAlertAsync("farm-1:coord-1:connectivity");

        // Assert – alert was resolved
        await _alertRepo.Received(1).ResolveAsync("alert-123", Arg.Any<CancellationToken>());

        // Assert – update broadcast was sent with status "resolved"
        await _wsBroadcaster.Received(1).BroadcastAlertUpdatedAsync(
            Arg.Is<AlertPayload>(p =>
                p.AlertId == "alert-123" &&
                p.Status == "resolved" &&
                p.FarmId == "farm-1"),
            Arg.Any<CancellationToken>());

        // Assert – farm alert count was decremented
        await _farmRepo.Received(1).DecrementAlertCountAsync("farm-1", Arg.Any<CancellationToken>());

        // Assert – farm update was broadcast
        await _wsBroadcaster.Received(1).BroadcastFarmUpdateAsync(
            Arg.Is<FarmUpdatePayload>(p => p.FarmId == "farm-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoResolveAlertAsync_FarmNotFound_DoesNotThrowAndSkipsFarmBroadcast()
    {
        // Arrange
        var existingAlert = MakeActiveAlert("farm-1:coord-1:connectivity");
        existingAlert.FarmId = "farm-1";
        _alertRepo.GetActiveAlertByKeyAsync("farm-1:coord-1:connectivity", Arg.Any<CancellationToken>())
            .Returns(existingAlert);
        _farmRepo.GetByFarmIdAsync("farm-1", Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = () => _sut.AutoResolveAlertAsync("farm-1:coord-1:connectivity");

        // Assert
        await act.Should().NotThrowAsync();

        // Alert was still resolved
        await _alertRepo.Received(1).ResolveAsync("alert-123", Arg.Any<CancellationToken>());

        // Alert update broadcast was still sent
        await _wsBroadcaster.Received(1).BroadcastAlertUpdatedAsync(
            Arg.Any<AlertPayload>(), Arg.Any<CancellationToken>());

        // Farm update broadcast was NOT sent
        await _wsBroadcaster.DidNotReceive().BroadcastFarmUpdateAsync(
            Arg.Any<FarmUpdatePayload>(), Arg.Any<CancellationToken>());
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════════
    #region Edge Cases & Additional Coverage
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NullFarmId_DefaultsToUnknown()
    {
        // Arrange – FarmId is null
        var coord = MakeCoordinator(tempC: 40f);
        coord.FarmId = null;

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – alert should use "unknown" as farmId
        await _alertRepo.Received().UpsertAsync(
            Arg.Is<Alert>(a => a.FarmId == "unknown"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_TempCZero_SkipsTemperatureChecks()
    {
        // Arrange – TempC == 0 means sensor not available; should skip temp checks
        var coord = MakeCoordinator(tempC: 0f, waterLevelPct: null, ph: null, ecMsCm: null, mainPumpOn: null);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – no temperature alerts created or resolved
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "temperature_high" || a.Category == "temperature_low"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NullWaterLevel_SkipsWaterLevelCheck()
    {
        // Arrange
        var coord = MakeCoordinator(waterLevelPct: null);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – no water_level alert created
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "water_level"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NullPh_SkipsPhCheck()
    {
        // Arrange
        var coord = MakeCoordinator(ph: null);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – no ph_out_of_range alert created
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "ph_out_of_range"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_NullSetpoints_SkipsEcCheck()
    {
        // Arrange – EC value present but no setpoints
        var coord = MakeCoordinator(ecMsCm: 3.0f);
        coord.Setpoints = null;

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – no ec_out_of_range alert created
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "ec_out_of_range"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_VbatMvZero_SkipsBatteryCheck()
    {
        // Arrange – VbatMv == 0 means sensor not available
        var tower = MakeTower(vbatMv: 0);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert – no battery_low alert created
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "battery_low"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckTowerAlertsAsync_AirTempCZero_SkipsTemperatureChecks()
    {
        // Arrange – AirTempC == 0 means sensor not available
        var tower = MakeTower(airTempC: 0f);

        // Act
        await _sut.CheckTowerAlertsAsync(tower);

        // Assert – no temperature alerts created
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "temperature_high" || a.Category == "temperature_low"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCoordinatorAlertsAsync_MainPumpNull_SkipsPumpCheck()
    {
        // Arrange – MainPumpOn is null (sensor not available)
        var coord = MakeCoordinator(mainPumpOn: null);

        // Act
        await _sut.CheckCoordinatorAlertsAsync(coord);

        // Assert – no pump_failure alert created or resolved
        await _alertRepo.DidNotReceive().UpsertAsync(
            Arg.Is<Alert>(a => a.Category == "pump_failure"),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
