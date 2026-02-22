using FluentAssertions;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

public class TwinServiceTests
{
    // ── Shared fixtures ──────────────────────────────────────────────────────
    private readonly ITwinRepository _repo;
    private readonly IMqttService _mqtt;
    private readonly TwinChangeChannel _changeChannel;
    private readonly IWsBroadcaster _broadcaster;
    private readonly TwinService _sut;

    public TwinServiceTests()
    {
        _repo = Substitute.For<ITwinRepository>();
        _mqtt = Substitute.For<IMqttService>();
        _changeChannel = new TwinChangeChannel(NullLogger<TwinChangeChannel>.Instance);
        _broadcaster = Substitute.For<IWsBroadcaster>();

        _sut = new TwinService(
            _repo,
            _mqtt,
            _changeChannel,
            _broadcaster,
            NullLogger<TwinService>.Instance);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TowerTwin CreateTowerTwin(
        string towerId = "tower-1",
        string farmId = "farm-1",
        string coordId = "coord-1",
        SyncStatus syncStatus = SyncStatus.InSync,
        TowerReportedState? reported = null,
        TowerDesiredState? desired = null)
    {
        return new TowerTwin
        {
            TowerId = towerId,
            FarmId = farmId,
            CoordId = coordId,
            Metadata = new TwinMetadata { SyncStatus = syncStatus },
            Reported = reported ?? new TowerReportedState(),
            Desired = desired ?? new TowerDesiredState()
        };
    }

    private static CoordinatorTwin CreateCoordinatorTwin(
        string coordId = "coord-1",
        string farmId = "farm-1",
        SyncStatus syncStatus = SyncStatus.InSync,
        CoordinatorReportedState? reported = null,
        CoordinatorDesiredState? desired = null)
    {
        return new CoordinatorTwin
        {
            CoordId = coordId,
            FarmId = farmId,
            Metadata = new TwinMetadata { SyncStatus = syncStatus },
            Reported = reported ?? new CoordinatorReportedState(),
            Desired = desired ?? new CoordinatorDesiredState()
        };
    }

    // ========================================================================
    #region ProcessTowerTelemetryAsync
    // ========================================================================

    [Fact]
    public async Task ProcessTowerTelemetryAsync_RepoReturnsFalse_AutoCreatesTwinAndContinues()
    {
        // Arrange
        _repo.UpdateTowerReportedStateAsync(Arg.Any<string>(), Arg.Any<TowerReportedState>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var reported = new TowerReportedState { AirTempC = 25f };

        // Act
        await _sut.ProcessTowerTelemetryAsync("tower-1", "coord-1", "farm-1", reported);

        // Assert – twin was auto-created via upsert
        await _repo.Received(1)
            .UpsertTowerTwinAsync(Arg.Is<TowerTwin>(t =>
                t.TowerId == "tower-1" &&
                t.CoordId == "coord-1" &&
                t.FarmId == "farm-1" &&
                t.Reported == reported), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTowerTelemetryAsync_RepoReturnsTrue_WritesToChangeChannelAndBroadcasts()
    {
        // Arrange
        var reported = new TowerReportedState { AirTempC = 22f };
        _repo.UpdateTowerReportedStateAsync("tower-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns((TowerTwin?)null); // twin not found after update (edge case)

        // Act
        await _sut.ProcessTowerTelemetryAsync("tower-1", "coord-1", "farm-1", reported);

        // Assert – broadcaster was called
        await _broadcaster.Received(1)
            .BroadcastAsync("digital_twin_update", Arg.Any<object>(), Arg.Any<CancellationToken>());

        // Assert – change channel has an event
        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.TowerTelemetry);
        evt.DeviceId.Should().Be("tower-1");
        evt.TowerReported.Should().BeSameAs(reported);
    }

    [Fact]
    public async Task ProcessTowerTelemetryAsync_PendingSyncAndDeltaIsNull_UpdatesSyncStatusToInSync()
    {
        // Arrange – desired matches reported (no delta)
        var reported = new TowerReportedState { PumpOn = true, LightOn = false, LightBrightness = 128, StatusMode = "operational" };
        var desired = new TowerDesiredState { PumpOn = true, LightOn = false, LightBrightness = 128, StatusMode = "operational" };
        var twin = CreateTowerTwin(syncStatus: SyncStatus.Pending, reported: reported, desired: desired);

        _repo.UpdateTowerReportedStateAsync("tower-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.ProcessTowerTelemetryAsync("tower-1", "coord-1", "farm-1", reported);

        // Assert
        await _repo.Received(1)
            .UpdateTowerSyncStatusAsync("tower-1", SyncStatus.InSync, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTowerTelemetryAsync_PendingSyncAndDeltaExists_DoesNotUpdateSyncStatus()
    {
        // Arrange – desired differs from reported (delta exists)
        var reported = new TowerReportedState { PumpOn = false };
        var desired = new TowerDesiredState { PumpOn = true };
        var twin = CreateTowerTwin(syncStatus: SyncStatus.Pending, reported: reported, desired: desired);

        _repo.UpdateTowerReportedStateAsync("tower-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.ProcessTowerTelemetryAsync("tower-1", "coord-1", "farm-1", reported);

        // Assert
        await _repo.DidNotReceive()
            .UpdateTowerSyncStatusAsync(Arg.Any<string>(), Arg.Any<SyncStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTowerTelemetryAsync_TwinExistsButNotPending_DoesNotCheckDelta()
    {
        // Arrange – sync status is InSync, not Pending
        var reported = new TowerReportedState { PumpOn = false };
        var desired = new TowerDesiredState { PumpOn = true }; // would be a delta, but status isn't Pending
        var twin = CreateTowerTwin(syncStatus: SyncStatus.InSync, reported: reported, desired: desired);

        _repo.UpdateTowerReportedStateAsync("tower-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.ProcessTowerTelemetryAsync("tower-1", "coord-1", "farm-1", reported);

        // Assert – should NOT update sync status because it's not Pending
        await _repo.DidNotReceive()
            .UpdateTowerSyncStatusAsync(Arg.Any<string>(), Arg.Any<SyncStatus>(), Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region SetTowerDesiredStateAsync
    // ========================================================================

    [Fact]
    public async Task SetTowerDesiredStateAsync_RepoReturnsFalse_ReturnsEarlyWithoutBroadcasting()
    {
        // Arrange
        _repo.UpdateTowerDesiredStateAsync(Arg.Any<string>(), Arg.Any<TowerDesiredState>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.SetTowerDesiredStateAsync("tower-1", new TowerDesiredState { PumpOn = true });

        // Assert
        await _broadcaster.DidNotReceive()
            .BroadcastAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive()
            .GetTowerTwinByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTowerDesiredStateAsync_RepoReturnsTrue_EmitsChangeEventAndBroadcasts()
    {
        // Arrange
        var desired = new TowerDesiredState { LightOn = true };
        _repo.UpdateTowerDesiredStateAsync("tower-1", desired, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns((TowerTwin?)null);

        // Act
        await _sut.SetTowerDesiredStateAsync("tower-1", desired);

        // Assert – broadcaster was called
        await _broadcaster.Received(1)
            .BroadcastAsync("digital_twin_update", Arg.Any<object>(), Arg.Any<CancellationToken>());

        // Assert – change channel has the event
        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.TowerDesiredStateChanged);
        evt.DeviceId.Should().Be("tower-1");
    }

    [Fact]
    public async Task SetTowerDesiredStateAsync_TwinExistsWithDelta_PublishesMqttCommand()
    {
        // Arrange – desired differs from reported → delta exists → MQTT publish
        var desired = new TowerDesiredState { PumpOn = true };
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = false },
            desired: desired);

        _repo.UpdateTowerDesiredStateAsync("tower-1", desired, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.SetTowerDesiredStateAsync("tower-1", desired);

        // Assert
        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-1/coord-1/tower/tower-1/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTowerDesiredStateAsync_TwinExistsNoDelta_DoesNotPublishMqtt()
    {
        // Arrange – desired matches reported → no delta → no MQTT
        var desired = new TowerDesiredState { PumpOn = true };
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = true },
            desired: desired);

        _repo.UpdateTowerDesiredStateAsync("tower-1", desired, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.SetTowerDesiredStateAsync("tower-1", desired);

        // Assert
        await _mqtt.DidNotReceive()
            .PublishAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<byte>(), Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region GetTowerStateDeltaAsync
    // ========================================================================

    [Fact]
    public async Task GetTowerStateDeltaAsync_TwinIsNull_ReturnsNull()
    {
        // Arrange
        _repo.GetTowerTwinByIdAsync("tower-x", Arg.Any<CancellationToken>())
            .Returns((TowerTwin?)null);

        // Act
        var result = await _sut.GetTowerStateDeltaAsync("tower-x");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_TwinExistsWithDelta_ReturnsDelta()
    {
        // Arrange
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = false, LightBrightness = 100 },
            desired: new TowerDesiredState { PumpOn = true, LightBrightness = 200 });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        // Assert
        result.Should().NotBeNull();
        result!.PumpOn.Should().Be(true);
        result.LightBrightness.Should().Be(200);
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_TwinExistsNoDelta_ReturnsNull()
    {
        // Arrange – desired matches reported
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = true, LightOn = true, LightBrightness = 128, StatusMode = "operational" },
            desired: new TowerDesiredState { PumpOn = true, LightOn = true, LightBrightness = 128, StatusMode = "operational" });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    // ========================================================================
    #region CalculateTowerDelta (tested indirectly via GetTowerStateDeltaAsync)
    // ========================================================================

    [Fact]
    public async Task GetTowerStateDeltaAsync_PumpOnDiffers_IncludesPumpOnInDelta()
    {
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = false },
            desired: new TowerDesiredState { PumpOn = true });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().NotBeNull();
        result!.PumpOn.Should().Be(true);
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_LightOnDiffers_IncludesLightOnInDelta()
    {
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { LightOn = false },
            desired: new TowerDesiredState { LightOn = true });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().NotBeNull();
        result!.LightOn.Should().Be(true);
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_LightBrightnessDiffers_IncludesLightBrightnessInDelta()
    {
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { LightBrightness = 50 },
            desired: new TowerDesiredState { LightBrightness = 200 });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().NotBeNull();
        result!.LightBrightness.Should().Be(200);
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_StatusModeDiffers_IncludesStatusModeInDelta()
    {
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { StatusMode = "operational" },
            desired: new TowerDesiredState { StatusMode = "ota" });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().NotBeNull();
        result!.StatusMode.Should().Be("ota");
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_DesiredHasNullValues_ReturnsNullWhenAllNullOrMatch()
    {
        // Desired has all nulls → no delta
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { PumpOn = true, LightOn = false, LightBrightness = 100, StatusMode = "operational" },
            desired: new TowerDesiredState { PumpOn = null, LightOn = null, LightBrightness = null, StatusMode = null });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTowerStateDeltaAsync_DesiredStatusModeEmpty_DoesNotIncludeInDelta()
    {
        // Empty string StatusMode should not be included in delta
        var twin = CreateTowerTwin(
            reported: new TowerReportedState { StatusMode = "operational" },
            desired: new TowerDesiredState { StatusMode = "" });

        _repo.GetTowerTwinByIdAsync("tower-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetTowerStateDeltaAsync("tower-1");

        result.Should().BeNull();
    }

    #endregion

    // ========================================================================
    #region ProcessCoordinatorTelemetryAsync
    // ========================================================================

    [Fact]
    public async Task ProcessCoordinatorTelemetryAsync_RepoReturnsFalse_AutoCreatesTwinAndContinues()
    {
        // Arrange
        _repo.UpdateCoordinatorReportedStateAsync(Arg.Any<string>(), Arg.Any<CoordinatorReportedState>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var reported = new CoordinatorReportedState { StatusMode = "operational" };

        // Act
        await _sut.ProcessCoordinatorTelemetryAsync("coord-1", "farm-1", reported);

        // Assert – twin was auto-created via upsert
        await _repo.Received(1)
            .UpsertCoordinatorTwinAsync(Arg.Is<CoordinatorTwin>(t =>
                t.CoordId == "coord-1" &&
                t.FarmId == "farm-1" &&
                t.SiteId == "farm-1" &&
                t.Reported == reported), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCoordinatorTelemetryAsync_RepoReturnsTrue_WritesToChangeChannelAndBroadcasts()
    {
        // Arrange
        var reported = new CoordinatorReportedState { TempC = 24f };
        _repo.UpdateCoordinatorReportedStateAsync("coord-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>())
            .Returns((CoordinatorTwin?)null);

        // Drain any prior events
        while (_changeChannel.Reader.TryRead(out _)) { }

        // Act
        await _sut.ProcessCoordinatorTelemetryAsync("coord-1", "farm-1", reported);

        // Assert
        await _broadcaster.Received(1)
            .BroadcastAsync("digital_twin_update", Arg.Any<object>(), Arg.Any<CancellationToken>());

        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.CoordinatorTelemetry);
        evt.DeviceId.Should().Be("coord-1");
        evt.CoordinatorReported.Should().BeSameAs(reported);
    }

    [Fact]
    public async Task ProcessCoordinatorTelemetryAsync_PendingSyncAndDeltaIsNull_UpdatesSyncStatusToInSync()
    {
        // Arrange – desired matches reported
        var reported = new CoordinatorReportedState { MainPumpOn = true, StatusMode = "operational" };
        var desired = new CoordinatorDesiredState { MainPumpOn = true, StatusMode = "operational" };
        var twin = CreateCoordinatorTwin(syncStatus: SyncStatus.Pending, reported: reported, desired: desired);

        _repo.UpdateCoordinatorReportedStateAsync("coord-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.ProcessCoordinatorTelemetryAsync("coord-1", "farm-1", reported);

        // Assert
        await _repo.Received(1)
            .UpdateCoordinatorSyncStatusAsync("coord-1", SyncStatus.InSync, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCoordinatorTelemetryAsync_PendingSyncAndDeltaExists_DoesNotUpdateSyncStatus()
    {
        // Arrange – desired differs from reported
        var reported = new CoordinatorReportedState { MainPumpOn = false };
        var desired = new CoordinatorDesiredState { MainPumpOn = true };
        var twin = CreateCoordinatorTwin(syncStatus: SyncStatus.Pending, reported: reported, desired: desired);

        _repo.UpdateCoordinatorReportedStateAsync("coord-1", reported, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.ProcessCoordinatorTelemetryAsync("coord-1", "farm-1", reported);

        // Assert
        await _repo.DidNotReceive()
            .UpdateCoordinatorSyncStatusAsync(Arg.Any<string>(), Arg.Any<SyncStatus>(), Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region SetCoordinatorDesiredStateAsync
    // ========================================================================

    [Fact]
    public async Task SetCoordinatorDesiredStateAsync_RepoReturnsFalse_ReturnsEarlyWithoutBroadcasting()
    {
        // Arrange
        _repo.UpdateCoordinatorDesiredStateAsync(Arg.Any<string>(), Arg.Any<CoordinatorDesiredState>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.SetCoordinatorDesiredStateAsync("coord-1", new CoordinatorDesiredState { MainPumpOn = true });

        // Assert
        await _broadcaster.DidNotReceive()
            .BroadcastAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive()
            .GetCoordinatorTwinByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCoordinatorDesiredStateAsync_RepoReturnsTrue_EmitsChangeEventAndBroadcasts()
    {
        // Arrange
        var desired = new CoordinatorDesiredState { MainPumpOn = true };
        _repo.UpdateCoordinatorDesiredStateAsync("coord-1", desired, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>())
            .Returns((CoordinatorTwin?)null);

        // Drain prior events
        while (_changeChannel.Reader.TryRead(out _)) { }

        // Act
        await _sut.SetCoordinatorDesiredStateAsync("coord-1", desired);

        // Assert
        await _broadcaster.Received(1)
            .BroadcastAsync("digital_twin_update", Arg.Any<object>(), Arg.Any<CancellationToken>());

        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.CoordinatorDesiredStateChanged);
        evt.DeviceId.Should().Be("coord-1");
    }

    [Fact]
    public async Task SetCoordinatorDesiredStateAsync_TwinExistsWithDelta_PublishesMqttCommand()
    {
        // Arrange
        var desired = new CoordinatorDesiredState { MainPumpOn = true };
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { MainPumpOn = false },
            desired: desired);

        _repo.UpdateCoordinatorDesiredStateAsync("coord-1", desired, Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>())
            .Returns(twin);

        // Act
        await _sut.SetCoordinatorDesiredStateAsync("coord-1", desired);

        // Assert
        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-1/coord-1/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region GetCoordinatorStateDeltaAsync / CalculateCoordinatorDelta (indirect)
    // ========================================================================

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_TwinIsNull_ReturnsNull()
    {
        _repo.GetCoordinatorTwinByIdAsync("coord-x", Arg.Any<CancellationToken>())
            .Returns((CoordinatorTwin?)null);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-x");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_MainPumpOnDiffers_IncludesInDelta()
    {
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { MainPumpOn = false },
            desired: new CoordinatorDesiredState { MainPumpOn = true });

        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-1");

        result.Should().NotBeNull();
        result!.MainPumpOn.Should().Be(true);
    }

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_DosingPumpsDiffer_IncludesInDelta()
    {
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { DosingPumpPhOn = false, DosingPumpNutrientOn = false },
            desired: new CoordinatorDesiredState { DosingPumpPhOn = true, DosingPumpNutrientOn = true });

        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-1");

        result.Should().NotBeNull();
        result!.DosingPumpPhOn.Should().Be(true);
        result.DosingPumpNutrientOn.Should().Be(true);
    }

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_SetpointsPresent_AlwaysIncludedInDelta()
    {
        // Even if all actuator states match, setpoints should be included if non-null
        var setpoints = new ReservoirSetpoints { PhTarget = 6.5f, EcTarget = 2.0f };
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { MainPumpOn = true },
            desired: new CoordinatorDesiredState { MainPumpOn = true, Setpoints = setpoints });

        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-1");

        result.Should().NotBeNull();
        result!.Setpoints.Should().NotBeNull();
        result.Setpoints!.PhTarget.Should().Be(6.5f);
        result.Setpoints.EcTarget.Should().Be(2.0f);
    }

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_AllMatchNoSetpoints_ReturnsNull()
    {
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { MainPumpOn = true, DosingPumpPhOn = false, DosingPumpNutrientOn = false, StatusMode = "operational" },
            desired: new CoordinatorDesiredState { MainPumpOn = true, DosingPumpPhOn = false, DosingPumpNutrientOn = false, StatusMode = "operational", Setpoints = null });

        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCoordinatorStateDeltaAsync_StatusModeDiffers_IncludesInDelta()
    {
        var twin = CreateCoordinatorTwin(
            reported: new CoordinatorReportedState { StatusMode = "operational" },
            desired: new CoordinatorDesiredState { StatusMode = "maintenance" });

        _repo.GetCoordinatorTwinByIdAsync("coord-1", Arg.Any<CancellationToken>()).Returns(twin);

        var result = await _sut.GetCoordinatorStateDeltaAsync("coord-1");

        result.Should().NotBeNull();
        result!.StatusMode.Should().Be("maintenance");
    }

    #endregion

    // ========================================================================
    #region ProcessPendingSyncsAsync
    // ========================================================================

    [Fact]
    public async Task ProcessPendingSyncsAsync_IteratesPendingTowersAndCoordinators()
    {
        // Arrange – one tower with delta, one coordinator with delta
        var tower = CreateTowerTwin(
            towerId: "t1",
            syncStatus: SyncStatus.Pending,
            reported: new TowerReportedState { PumpOn = false },
            desired: new TowerDesiredState { PumpOn = true });

        var coord = CreateCoordinatorTwin(
            coordId: "c1",
            syncStatus: SyncStatus.Pending,
            reported: new CoordinatorReportedState { MainPumpOn = false },
            desired: new CoordinatorDesiredState { MainPumpOn = true });

        _repo.GetPendingSyncTwinsAsync(Arg.Any<CancellationToken>())
            .Returns((new List<TowerTwin> { tower } as IReadOnlyList<TowerTwin>,
                       new List<CoordinatorTwin> { coord } as IReadOnlyList<CoordinatorTwin>));

        // Act
        await _sut.ProcessPendingSyncsAsync();

        // Assert – MQTT published for both
        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-1/coord-1/tower/t1/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());

        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-1/c1/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingSyncsAsync_TowerWithNoDelta_SkipsMqttPublish()
    {
        // Arrange – tower desired matches reported → no delta
        var tower = CreateTowerTwin(
            towerId: "t1",
            syncStatus: SyncStatus.Pending,
            reported: new TowerReportedState { PumpOn = true },
            desired: new TowerDesiredState { PumpOn = true });

        _repo.GetPendingSyncTwinsAsync(Arg.Any<CancellationToken>())
            .Returns((new List<TowerTwin> { tower } as IReadOnlyList<TowerTwin>,
                       Array.Empty<CoordinatorTwin>() as IReadOnlyList<CoordinatorTwin>));

        // Act
        await _sut.ProcessPendingSyncsAsync();

        // Assert
        await _mqtt.DidNotReceive()
            .PublishAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<byte>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingSyncsAsync_OneTowerFails_ContinuesProcessingOthers()
    {
        // Arrange – two towers: first will cause MQTT to throw, second should still be processed
        var tower1 = CreateTowerTwin(
            towerId: "t-fail",
            syncStatus: SyncStatus.Pending,
            reported: new TowerReportedState { PumpOn = false },
            desired: new TowerDesiredState { PumpOn = true });

        var tower2 = CreateTowerTwin(
            towerId: "t-ok",
            farmId: "farm-2",
            coordId: "coord-2",
            syncStatus: SyncStatus.Pending,
            reported: new TowerReportedState { LightOn = false },
            desired: new TowerDesiredState { LightOn = true });

        _repo.GetPendingSyncTwinsAsync(Arg.Any<CancellationToken>())
            .Returns((new List<TowerTwin> { tower1, tower2 } as IReadOnlyList<TowerTwin>,
                       Array.Empty<CoordinatorTwin>() as IReadOnlyList<CoordinatorTwin>));

        // First tower publish throws
        _mqtt.PublishAsync(
                "hydro/farm-1/coord-1/tower/t-fail/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("MQTT down"));

        // Act – should not throw
        var act = () => _sut.ProcessPendingSyncsAsync();
        await act.Should().NotThrowAsync();

        // Assert – second tower was still published
        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-2/coord-2/tower/t-ok/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingSyncsAsync_CoordinatorWithNoDelta_SkipsMqttPublish()
    {
        // Arrange – coordinator desired matches reported
        var coord = CreateCoordinatorTwin(
            coordId: "c1",
            syncStatus: SyncStatus.Pending,
            reported: new CoordinatorReportedState { MainPumpOn = true, StatusMode = "operational" },
            desired: new CoordinatorDesiredState { MainPumpOn = true, StatusMode = "operational" });

        _repo.GetPendingSyncTwinsAsync(Arg.Any<CancellationToken>())
            .Returns((Array.Empty<TowerTwin>() as IReadOnlyList<TowerTwin>,
                       new List<CoordinatorTwin> { coord } as IReadOnlyList<CoordinatorTwin>));

        // Act
        await _sut.ProcessPendingSyncsAsync();

        // Assert
        await _mqtt.DidNotReceive()
            .PublishAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<byte>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingSyncsAsync_CoordinatorFails_ContinuesProcessing()
    {
        // Arrange – one coordinator that throws, followed by a tower that should still succeed
        var coord = CreateCoordinatorTwin(
            coordId: "c-fail",
            syncStatus: SyncStatus.Pending,
            reported: new CoordinatorReportedState { MainPumpOn = false },
            desired: new CoordinatorDesiredState { MainPumpOn = true });

        var tower = CreateTowerTwin(
            towerId: "t-ok",
            syncStatus: SyncStatus.Pending,
            reported: new TowerReportedState { PumpOn = false },
            desired: new TowerDesiredState { PumpOn = true });

        _repo.GetPendingSyncTwinsAsync(Arg.Any<CancellationToken>())
            .Returns((new List<TowerTwin> { tower } as IReadOnlyList<TowerTwin>,
                       new List<CoordinatorTwin> { coord } as IReadOnlyList<CoordinatorTwin>));

        _mqtt.PublishAsync(
                "hydro/farm-1/c-fail/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("MQTT down"));

        // Act
        var act = () => _sut.ProcessPendingSyncsAsync();
        await act.Should().NotThrowAsync();

        // Assert – tower was still published
        await _mqtt.Received(1)
            .PublishAsync(
                "hydro/farm-1/coord-1/tower/t-ok/cmd",
                Arg.Any<byte[]>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region MarkTowerSyncSuccessAsync / MarkCoordinatorSyncSuccessAsync
    // ========================================================================

    [Fact]
    public async Task MarkTowerSyncSuccessAsync_CallsRepoWithInSyncStatus()
    {
        // Act
        await _sut.MarkTowerSyncSuccessAsync("tower-1");

        // Assert
        await _repo.Received(1)
            .UpdateTowerSyncStatusAsync("tower-1", SyncStatus.InSync, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkCoordinatorSyncSuccessAsync_CallsRepoWithInSyncStatus()
    {
        // Act
        await _sut.MarkCoordinatorSyncSuccessAsync("coord-1");

        // Assert
        await _repo.Received(1)
            .UpdateCoordinatorSyncStatusAsync("coord-1", SyncStatus.InSync, Arg.Any<CancellationToken>());
    }

    #endregion

    // ========================================================================
    #region CheckAndMarkStaleTwinsAsync
    // ========================================================================

    [Fact]
    public async Task CheckAndMarkStaleTwinsAsync_DelegatesToRepoMarkStaleTwinsAsync()
    {
        // Arrange
        var threshold = TimeSpan.FromMinutes(5);
        _repo.MarkStaleTwinsAsync(threshold, Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        await _sut.CheckAndMarkStaleTwinsAsync(threshold);

        // Assert
        await _repo.Received(1)
            .MarkStaleTwinsAsync(threshold, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndMarkStaleTwinsAsync_ZeroStale_StillDelegatesToRepo()
    {
        // Arrange
        var threshold = TimeSpan.FromMinutes(10);
        _repo.MarkStaleTwinsAsync(threshold, Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        await _sut.CheckAndMarkStaleTwinsAsync(threshold);

        // Assert
        await _repo.Received(1)
            .MarkStaleTwinsAsync(threshold, Arg.Any<CancellationToken>());
    }

    #endregion
}
