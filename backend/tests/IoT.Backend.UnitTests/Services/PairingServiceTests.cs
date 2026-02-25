using FluentAssertions;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

public class PairingServiceTests
{
    private readonly ITowerRepository _towerRepo;
    private readonly IMqttService _mqtt;
    private readonly IWsBroadcaster _broadcaster;
    private readonly TwinChangeChannel _changeChannel;
    private readonly PairingService _sut;

    private const string FarmId = "farm-01";
    private const string CoordId = "coord-01";
    private const string TowerId = "tower-abc123";
    private const string MacAddress = "AA:BB:CC:DD:EE:FF";

    public PairingServiceTests()
    {
        _towerRepo = Substitute.For<ITowerRepository>();
        _mqtt = Substitute.For<IMqttService>();
        _broadcaster = Substitute.For<IWsBroadcaster>();
        _changeChannel = new TwinChangeChannel(NullLogger<TwinChangeChannel>.Instance);

        _sut = new PairingService(
            _towerRepo,
            _mqtt,
            _broadcaster,
            _changeChannel,
            NullLogger<PairingService>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an active session by calling StartPairingSessionAsync and returns it.
    /// </summary>
    private async Task<PairingSession> CreateActiveSessionAsync(int durationSeconds = 120)
    {
        return await _sut.StartPairingSessionAsync(FarmId, CoordId, durationSeconds);
    }

    /// <summary>
    /// Creates an active session and adds a pending pairing request for the given tower.
    /// </summary>
    private async Task<TowerPairingRequest> CreateSessionWithPendingRequestAsync(
        string towerId = TowerId,
        string macAddress = MacAddress)
    {
        await CreateActiveSessionAsync();

        var request = new TowerPairingRequest
        {
            TowerId = towerId,
            MacAddress = macAddress,
            Rssi = -55,
            FwVersion = "1.0.0",
            Capabilities = new TowerCapabilities
            {
                DhtSensor = true,
                LightSensor = true,
                PumpRelay = true,
                GrowLight = true,
                SlotCount = 6
            }
        };

        var result = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, request);
        return result!;
    }

    #region StartPairingSessionAsync

    [Fact]
    public async Task StartPairingSessionAsync_NewSession_CreatesAndReturnsActiveSession()
    {
        // Act
        var session = await _sut.StartPairingSessionAsync(FarmId, CoordId, 90);

        // Assert
        session.Should().NotBeNull();
        session.FarmId.Should().Be(FarmId);
        session.CoordId.Should().Be(CoordId);
        session.Status.Should().Be("active");
        session.DurationS.Should().Be(90);
        session.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        session.PendingRequests.Should().BeEmpty();
        session.ApprovedTowers.Should().BeEmpty();
        session.RejectedTowers.Should().BeEmpty();
    }

    [Fact]
    public async Task StartPairingSessionAsync_NewSession_PublishesMqttStartPairingCommand()
    {
        // Act
        await _sut.StartPairingSessionAsync(FarmId, CoordId, 60);

        // Assert
        var expectedTopic = $"farm/{FarmId}/coord/{CoordId}/cmd";
        await _mqtt.Received(1).PublishJsonAsync(
            expectedTopic,
            Arg.Is<object>(o => o != null),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPairingSessionAsync_NewSession_BroadcastsWsPairingStatus()
    {
        // Act
        await _sut.StartPairingSessionAsync(FarmId, CoordId, 60);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_status",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPairingSessionAsync_ActiveNonExpiredSessionExists_ReturnsExistingSession()
    {
        // Arrange — create an initial session with long duration
        var first = await _sut.StartPairingSessionAsync(FarmId, CoordId, 300);

        // Clear received calls so we can assert no new calls
        _mqtt.ClearReceivedCalls();
        _broadcaster.ClearReceivedCalls();

        // Act — try to start another session for the same farm/coord
        var second = await _sut.StartPairingSessionAsync(FarmId, CoordId, 60);

        // Assert — should return the same session, no new MQTT/WS calls
        second.Should().BeSameAs(first);
        await _mqtt.DidNotReceive().PublishJsonAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
        await _broadcaster.DidNotReceive().BroadcastAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPairingSessionAsync_SessionIdContainsFarmAndCoordId()
    {
        // Act
        var session = await _sut.StartPairingSessionAsync(FarmId, CoordId, 60);

        // Assert
        session.Id.Should().StartWith($"{FarmId}-{CoordId}-");
    }

    #endregion

    #region StopPairingSessionAsync

    [Fact]
    public async Task StopPairingSessionAsync_NoActiveSession_ReturnsNull()
    {
        // Act
        var result = await _sut.StopPairingSessionAsync(FarmId, CoordId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StopPairingSessionAsync_ActiveSession_SetsCancelledAndRemovesFromCache()
    {
        // Arrange
        await CreateActiveSessionAsync();

        // Act
        var result = await _sut.StopPairingSessionAsync(FarmId, CoordId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("cancelled");
        result.EndedAt.Should().NotBeNull();

        // Verify session is removed — GetActiveSession should return null
        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().BeNull();
    }

    [Fact]
    public async Task StopPairingSessionAsync_ActiveSession_PublishesMqttStopPairingCommand()
    {
        // Arrange
        await CreateActiveSessionAsync();
        _mqtt.ClearReceivedCalls();

        // Act
        await _sut.StopPairingSessionAsync(FarmId, CoordId);

        // Assert
        var expectedTopic = $"farm/{FarmId}/coord/{CoordId}/cmd";
        await _mqtt.Received(1).PublishJsonAsync(
            expectedTopic,
            Arg.Is<object>(o => o != null),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopPairingSessionAsync_ActiveSession_BroadcastsWsStoppedStatus()
    {
        // Arrange
        await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        // Act
        await _sut.StopPairingSessionAsync(FarmId, CoordId);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_status",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetActiveSessionAsync

    [Fact]
    public async Task GetActiveSessionAsync_NoSession_ReturnsNull()
    {
        // Act
        var result = await _sut.GetActiveSessionAsync(FarmId, CoordId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveSessionAsync_ActiveNonExpiredSession_ReturnsSession()
    {
        // Arrange
        var created = await CreateActiveSessionAsync(300);

        // Act
        var result = await _sut.GetActiveSessionAsync(FarmId, CoordId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(created);
    }

    [Fact]
    public async Task GetActiveSessionAsync_CancelledSession_ReturnsNull()
    {
        // Arrange — create then stop
        await CreateActiveSessionAsync();
        await _sut.StopPairingSessionAsync(FarmId, CoordId);

        // Act
        var result = await _sut.GetActiveSessionAsync(FarmId, CoordId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetPendingRequestsAsync

    [Fact]
    public async Task GetPendingRequestsAsync_NoSession_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetPendingRequestsAsync(FarmId, CoordId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRequestsAsync_SessionWithPendingRequests_ReturnsPendingOnly()
    {
        // Arrange — create session with one pending request
        await CreateSessionWithPendingRequestAsync();

        // Act
        var result = await _sut.GetPendingRequestsAsync(FarmId, CoordId);

        // Assert
        result.Should().HaveCount(1);
        result[0].TowerId.Should().Be(TowerId);
        result[0].Status.Should().Be("pending");
    }

    [Fact]
    public async Task GetPendingRequestsAsync_SessionWithApprovedRequest_ExcludesApproved()
    {
        // Arrange — create session, add request, then approve it
        await CreateSessionWithPendingRequestAsync();
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Act
        var result = await _sut.GetPendingRequestsAsync(FarmId, CoordId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ProcessPairingRequestAsync

    [Fact]
    public async Task ProcessPairingRequestAsync_NoActiveSession_ReturnsNull()
    {
        // Arrange
        var request = new TowerPairingRequest { TowerId = TowerId, MacAddress = MacAddress };

        // Act
        var result = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPairingRequestAsync_NewRequest_AddsToSessionAndReturnsWithGeneratedId()
    {
        // Arrange
        await CreateActiveSessionAsync();
        var request = new TowerPairingRequest
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Rssi = -60,
            FwVersion = "1.2.0"
        };

        // Act
        var result = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, request);

        // Assert
        result.Should().NotBeNull();
        result!.RequestId.Should().NotBeNullOrEmpty();
        result.RequestId.Should().HaveLength(8); // Guid "N" format truncated to 8 chars
        result.TowerId.Should().Be(TowerId);
        result.Status.Should().Be("pending");
        result.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessPairingRequestAsync_NewRequest_BroadcastsWsPairingRequest()
    {
        // Arrange
        await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        var request = new TowerPairingRequest { TowerId = TowerId, MacAddress = MacAddress };

        // Act
        await _sut.ProcessPairingRequestAsync(FarmId, CoordId, request);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_request",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPairingRequestAsync_DuplicateTowerId_UpdatesExistingRequest()
    {
        // Arrange
        await CreateActiveSessionAsync();

        var firstRequest = new TowerPairingRequest
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Rssi = -60,
            FwVersion = "1.0.0"
        };
        var firstResult = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, firstRequest);
        var originalRequestId = firstResult!.RequestId;

        _broadcaster.ClearReceivedCalls();

        var secondRequest = new TowerPairingRequest
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Rssi = -45,
            FwVersion = "1.1.0",
            Capabilities = new TowerCapabilities { DhtSensor = true, SlotCount = 8 }
        };

        // Act
        var result = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, secondRequest);

        // Assert — returns the same request object with updated fields
        result.Should().BeSameAs(firstResult);
        result!.RequestId.Should().Be(originalRequestId);
        result.Rssi.Should().Be(-45);
        result.FwVersion.Should().Be("1.1.0");
        result.Capabilities!.SlotCount.Should().Be(8);

        // Should NOT broadcast again for duplicate
        await _broadcaster.DidNotReceive().BroadcastAsync(
            "pairing_request",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPairingRequestAsync_ExpiredSession_ReturnsNull()
    {
        // Arrange — create a session with 1-second duration, then wait for it to expire
        await _sut.StartPairingSessionAsync(FarmId, CoordId, durationSeconds: 0);

        // The session ExpiresAt = UtcNow + 0 seconds, so it's already expired
        var request = new TowerPairingRequest { TowerId = TowerId, MacAddress = MacAddress };

        // Act
        var result = await _sut.ProcessPairingRequestAsync(FarmId, CoordId, request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ApprovePairingRequestAsync

    [Fact]
    public async Task ApprovePairingRequestAsync_NoSession_ReturnsNull()
    {
        // Act
        var result = await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_NoPendingRequestForTower_ReturnsNull()
    {
        // Arrange — session exists but no pending request for this tower
        await CreateActiveSessionAsync();

        // Act
        var result = await _sut.ApprovePairingRequestAsync(FarmId, CoordId, "nonexistent-tower");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_SetsApprovedAndCreatesTower()
    {
        // Arrange
        var pendingReq = await CreateSessionWithPendingRequestAsync();

        // Act
        var tower = await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        tower.Should().NotBeNull();
        tower!.TowerId.Should().Be(TowerId);
        tower.CoordId.Should().Be(CoordId);
        tower.FarmId.Should().Be(FarmId);
        tower.MacAddress.Should().Be(MacAddress);
        tower.StatusMode.Should().Be("pairing");
        tower.Id.Should().Be($"{FarmId}/{CoordId}/{TowerId}");

        // The request should be marked approved
        pendingReq.Status.Should().Be("approved");
        pendingReq.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_UpsertsToRepository()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();

        // Act
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        await _towerRepo.Received(1).UpsertAsync(
            Arg.Is<Tower>(t =>
                t.TowerId == TowerId &&
                t.FarmId == FarmId &&
                t.CoordId == CoordId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_PublishesMqttApprovePairingCommand()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        _mqtt.ClearReceivedCalls();

        // Act
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        var expectedTopic = $"farm/{FarmId}/coord/{CoordId}/cmd";
        await _mqtt.Received(1).PublishJsonAsync(
            expectedTopic,
            Arg.Is<object>(o => o != null),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_WritesTwinChangeEvent()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();

        // Act
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert — read the event from the channel
        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.TowerPaired);
        evt.DeviceId.Should().Be(TowerId);
        evt.FarmId.Should().Be(FarmId);
        evt.CoordId.Should().Be(CoordId);
        evt.TowerTwin.Should().NotBeNull();
        evt.TowerTwin!.TowerId.Should().Be(TowerId);
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_BroadcastsWsPairingApproved()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        _broadcaster.ClearReceivedCalls();

        // Act
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_approved",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovePairingRequestAsync_ValidRequest_AddsTowerIdToApprovedList()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        var session = await _sut.GetActiveSessionAsync(FarmId, CoordId);

        // Act
        await _sut.ApprovePairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        session!.ApprovedTowers.Should().Contain(TowerId);
    }

    #endregion

    #region RejectPairingRequestAsync

    [Fact]
    public async Task RejectPairingRequestAsync_NoSession_ReturnsFalse()
    {
        // Act
        var result = await _sut.RejectPairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RejectPairingRequestAsync_NoPendingRequest_ReturnsFalse()
    {
        // Arrange
        await CreateActiveSessionAsync();

        // Act
        var result = await _sut.RejectPairingRequestAsync(FarmId, CoordId, "nonexistent-tower");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RejectPairingRequestAsync_ValidRequest_SetsRejectedAndReturnsTrue()
    {
        // Arrange
        var pendingReq = await CreateSessionWithPendingRequestAsync();

        // Act
        var result = await _sut.RejectPairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        result.Should().BeTrue();
        pendingReq.Status.Should().Be("rejected");
        pendingReq.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectPairingRequestAsync_ValidRequest_PublishesMqttRejectPairingCommand()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        _mqtt.ClearReceivedCalls();

        // Act
        await _sut.RejectPairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        var expectedTopic = $"farm/{FarmId}/coord/{CoordId}/cmd";
        await _mqtt.Received(1).PublishJsonAsync(
            expectedTopic,
            Arg.Is<object>(o => o != null),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectPairingRequestAsync_ValidRequest_BroadcastsWsPairingRejected()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        _broadcaster.ClearReceivedCalls();

        // Act
        await _sut.RejectPairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_rejected",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectPairingRequestAsync_ValidRequest_AddsTowerIdToRejectedList()
    {
        // Arrange
        await CreateSessionWithPendingRequestAsync();
        var session = await _sut.GetActiveSessionAsync(FarmId, CoordId);

        // Act
        await _sut.RejectPairingRequestAsync(FarmId, CoordId, TowerId);

        // Assert
        session!.RejectedTowers.Should().Contain(TowerId);
    }

    #endregion

    #region ExpireTimedOutSessionsAsync

    [Fact]
    public async Task ExpireTimedOutSessionsAsync_ExpiredSession_SetsStatusExpiredAndRemoves()
    {
        // Arrange — create a session that expires immediately (0 seconds)
        await _sut.StartPairingSessionAsync(FarmId, CoordId, durationSeconds: 0);
        _broadcaster.ClearReceivedCalls();

        // Act
        await _sut.ExpireTimedOutSessionsAsync();

        // Assert — session should be removed from cache
        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().BeNull();

        // Should broadcast expiration
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_status",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpireTimedOutSessionsAsync_NonExpiredSession_LeavesUntouched()
    {
        // Arrange — create a session with long duration
        await _sut.StartPairingSessionAsync(FarmId, CoordId, durationSeconds: 600);
        _broadcaster.ClearReceivedCalls();

        // Act
        await _sut.ExpireTimedOutSessionsAsync();

        // Assert — session should still be active
        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().NotBeNull();
        active!.Status.Should().Be("active");

        // Should NOT broadcast anything
        await _broadcaster.DidNotReceive().BroadcastAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region HandlePairingStatusAsync

    [Fact]
    public async Task HandlePairingStatusAsync_StartedStatus_BroadcastsWithoutRemovingSession()
    {
        // Arrange
        await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        var status = new PairingStatusUpdate { Status = "started", RemainingSeconds = 60, PendingCount = 0 };

        // Act
        await _sut.HandlePairingStatusAsync(FarmId, CoordId, status);

        // Assert — session should still be active
        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().NotBeNull();

        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_status",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePairingStatusAsync_ActiveStatus_BroadcastsWithoutRemovingSession()
    {
        // Arrange
        await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        var status = new PairingStatusUpdate { Status = "active", RemainingSeconds = 30, PendingCount = 2 };

        // Act
        await _sut.HandlePairingStatusAsync(FarmId, CoordId, status);

        // Assert
        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().NotBeNull();
        active!.Status.Should().Be("active");
    }

    [Fact]
    public async Task HandlePairingStatusAsync_StoppedStatus_RemovesSessionAndSetsCompleted()
    {
        // Arrange
        var session = await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        var status = new PairingStatusUpdate { Status = "stopped" };

        // Act
        await _sut.HandlePairingStatusAsync(FarmId, CoordId, status);

        // Assert
        session.Status.Should().Be("completed");
        session.EndedAt.Should().NotBeNull();

        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().BeNull();
    }

    [Fact]
    public async Task HandlePairingStatusAsync_TimeoutStatus_RemovesSessionAndSetsExpired()
    {
        // Arrange
        var session = await CreateActiveSessionAsync();
        _broadcaster.ClearReceivedCalls();

        var status = new PairingStatusUpdate { Status = "timeout" };

        // Act
        await _sut.HandlePairingStatusAsync(FarmId, CoordId, status);

        // Assert
        session.Status.Should().Be("expired");
        session.EndedAt.Should().NotBeNull();

        var active = await _sut.GetActiveSessionAsync(FarmId, CoordId);
        active.Should().BeNull();
    }

    #endregion

    #region HandlePairingCompleteAsync

    [Fact]
    public async Task HandlePairingCompleteAsync_Success_UpdatesTowerToOperational()
    {
        // Arrange
        var existingTower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            StatusMode = "pairing"
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(existingTower);

        var completion = new PairingCompleteEvent
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Success = true
        };

        // Act
        await _sut.HandlePairingCompleteAsync(FarmId, CoordId, completion);

        // Assert
        existingTower.StatusMode.Should().Be("operational");
        await _towerRepo.Received(1).UpsertAsync(existingTower, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePairingCompleteAsync_SuccessWithCapabilitiesAndFwVersion_UpdatesThoseFields()
    {
        // Arrange
        var existingTower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            StatusMode = "pairing",
            FwVersion = "0.9.0"
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(existingTower);

        var newCapabilities = new TowerCapabilities
        {
            DhtSensor = true,
            LightSensor = false,
            PumpRelay = true,
            GrowLight = true,
            SlotCount = 12
        };

        var completion = new PairingCompleteEvent
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Success = true,
            Capabilities = newCapabilities,
            FwVersion = "2.0.0"
        };

        // Act
        await _sut.HandlePairingCompleteAsync(FarmId, CoordId, completion);

        // Assert
        existingTower.Capabilities.Should().BeSameAs(newCapabilities);
        existingTower.FwVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task HandlePairingCompleteAsync_Failure_BroadcastsFailureAndDoesNotUpdateTower()
    {
        // Arrange
        var completion = new PairingCompleteEvent
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Success = false,
            Error = "Handshake timeout"
        };

        // Act
        await _sut.HandlePairingCompleteAsync(FarmId, CoordId, completion);

        // Assert — should NOT call GetByFarmCoordAndIdAsync or UpsertAsync
        await _towerRepo.DidNotReceive().GetByFarmCoordAndIdAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _towerRepo.DidNotReceive().UpsertAsync(
            Arg.Any<Tower>(), Arg.Any<CancellationToken>());

        // Should broadcast failure
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_complete",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePairingCompleteAsync_SuccessTowerNotFound_DoesNotThrow()
    {
        // Arrange
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns((Tower?)null);

        var completion = new PairingCompleteEvent
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Success = true
        };

        // Act
        var act = () => _sut.HandlePairingCompleteAsync(FarmId, CoordId, completion);

        // Assert
        await act.Should().NotThrowAsync();

        // UpsertAsync should NOT be called since tower is null
        await _towerRepo.DidNotReceive().UpsertAsync(
            Arg.Any<Tower>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePairingCompleteAsync_Success_BroadcastsWsPairingComplete()
    {
        // Arrange
        var existingTower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            StatusMode = "pairing"
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(existingTower);

        var completion = new PairingCompleteEvent
        {
            TowerId = TowerId,
            MacAddress = MacAddress,
            Success = true
        };

        // Act
        await _sut.HandlePairingCompleteAsync(FarmId, CoordId, completion);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "pairing_complete",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region ForgetDeviceAsync

    [Fact]
    public async Task ForgetDeviceAsync_TowerNotFound_ReturnsFalse()
    {
        // Arrange
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns((Tower?)null);

        // Act
        var result = await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ForgetDeviceAsync_TowerFound_SendsMqttForgetDeviceCommand()
    {
        // Arrange
        var tower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            MacAddress = MacAddress
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(tower);

        // Act
        var result = await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        result.Should().BeTrue();

        var expectedTopic = $"farm/{FarmId}/coord/{CoordId}/cmd";
        await _mqtt.Received(1).PublishJsonAsync(
            expectedTopic,
            Arg.Is<object>(o => o != null),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgetDeviceAsync_TowerFound_DeletesFromRepository()
    {
        // Arrange
        var tower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            MacAddress = MacAddress
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(tower);

        // Act
        await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        await _towerRepo.Received(1).DeleteAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgetDeviceAsync_TowerFound_WritesTwinChangeEventTowerRemoved()
    {
        // Arrange
        var tower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            MacAddress = MacAddress
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(tower);

        // Act
        await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.TowerRemoved);
        evt.DeviceId.Should().Be(TowerId);
        evt.FarmId.Should().Be(FarmId);
        evt.CoordId.Should().Be(CoordId);
    }

    [Fact]
    public async Task ForgetDeviceAsync_TowerFound_BroadcastsWsDeviceForgotten()
    {
        // Arrange
        var tower = new Tower
        {
            Id = $"{FarmId}/{CoordId}/{TowerId}",
            TowerId = TowerId,
            CoordId = CoordId,
            FarmId = FarmId,
            MacAddress = MacAddress
        };
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns(tower);

        // Act
        await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        await _broadcaster.Received(1).BroadcastAsync(
            "device_forgotten",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgetDeviceAsync_TowerNotFound_DoesNotPublishMqttOrDelete()
    {
        // Arrange
        _towerRepo.GetByFarmCoordAndIdAsync(FarmId, CoordId, TowerId, Arg.Any<CancellationToken>())
            .Returns((Tower?)null);

        // Act
        await _sut.ForgetDeviceAsync(FarmId, CoordId, TowerId);

        // Assert
        await _mqtt.DidNotReceive().PublishJsonAsync(
            Arg.Any<string>(), Arg.Any<object>(), Arg.Any<byte>(), Arg.Any<CancellationToken>());
        await _towerRepo.DidNotReceive().DeleteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _broadcaster.DidNotReceive().BroadcastAsync(
            Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
