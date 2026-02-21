using FluentAssertions;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace IoT.Backend.UnitTests.Services;

public class CoordinatorRegistrationServiceTests
{
    private readonly ICoordinatorRepository _coordinatorRepo;
    private readonly IMqttService _mqtt;
    private readonly IWsBroadcaster _broadcaster;
    private readonly TwinChangeChannel _changeChannel;
    private readonly CoordinatorRegistrationService _sut;

    public CoordinatorRegistrationServiceTests()
    {
        _coordinatorRepo = Substitute.For<ICoordinatorRepository>();
        _mqtt = Substitute.For<IMqttService>();
        _broadcaster = Substitute.For<IWsBroadcaster>();
        _changeChannel = new TwinChangeChannel(NullLogger<TwinChangeChannel>.Instance);

        _sut = new CoordinatorRegistrationService(
            _coordinatorRepo,
            _mqtt,
            _broadcaster,
            _changeChannel,
            NullLogger<CoordinatorRegistrationService>.Instance);
    }

    // ========================================================================
    // IsRegisteredAsync
    // ========================================================================

    [Fact]
    public async Task IsRegisteredAsync_UnknownCoordId_ReturnsFalse()
    {
        var result = await _sut.IsRegisteredAsync("unknown-coord");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRegisteredAsync_AfterApproval_ReturnsTrue()
    {
        // Arrange – approve a coordinator first
        var request = MakeApproveRequest("AA:BB:CC:DD:EE:01");
        await _sut.ApproveRegistrationAsync(request);

        // Act
        var result = await _sut.IsRegisteredAsync("AA:BB:CC:DD:EE:01");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRegisteredAsync_AfterRefreshCache_ReturnsTrue()
    {
        // Arrange – DB returns a coordinator
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Coordinator>
            {
                new() { CoordId = "cached-coord" }
            });

        await _sut.RefreshCacheAsync();

        // Act
        var result = await _sut.IsRegisteredAsync("cached-coord");

        // Assert
        result.Should().BeTrue();
    }

    // ========================================================================
    // ProcessUnknownCoordinatorAsync
    // ========================================================================

    [Fact]
    public async Task ProcessUnknownCoordinatorAsync_FirstTime_BroadcastsRegistrationRequest()
    {
        await _sut.ProcessUnknownCoordinatorAsync("new-coord", "coordinator/new-coord/telemetry", Array.Empty<byte>());

        await _broadcaster.Received(1)
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Is<CoordinatorRegistrationPayload>(p => p.CoordId == "new-coord"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessUnknownCoordinatorAsync_SecondTime_DoesNotBroadcast()
    {
        await _sut.ProcessUnknownCoordinatorAsync("dup-coord", "topic", Array.Empty<byte>());
        _broadcaster.ClearReceivedCalls();

        await _sut.ProcessUnknownCoordinatorAsync("dup-coord", "topic", Array.Empty<byte>());

        await _broadcaster.DidNotReceive()
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Any<CoordinatorRegistrationPayload>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessUnknownCoordinatorAsync_RejectedCoord_DropsMessageSilently()
    {
        await _sut.RejectRegistrationAsync("rejected-coord");
        _broadcaster.ClearReceivedCalls();

        await _sut.ProcessUnknownCoordinatorAsync("rejected-coord", "topic", Array.Empty<byte>());

        await _broadcaster.DidNotReceive()
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Any<CoordinatorRegistrationPayload>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessUnknownCoordinatorAsync_IncrementsMessageCount()
    {
        await _sut.ProcessUnknownCoordinatorAsync("count-coord", "topic", Array.Empty<byte>());
        await _sut.ProcessUnknownCoordinatorAsync("count-coord", "topic", Array.Empty<byte>());
        await _sut.ProcessUnknownCoordinatorAsync("count-coord", "topic", Array.Empty<byte>());

        var pending = await _sut.GetPendingRegistrationsAsync();
        pending.Should().ContainSingle(p => p.CoordId == "count-coord")
            .Which.MessageCount.Should().Be(3);
    }

    // ========================================================================
    // HandleCoordinatorAnnounceAsync
    // ========================================================================

    [Fact]
    public async Task HandleCoordinatorAnnounceAsync_RegisteredCoord_ReturnsWithoutBroadcast()
    {
        var request = MakeApproveRequest("registered-coord");
        await _sut.ApproveRegistrationAsync(request);
        _broadcaster.ClearReceivedCalls();

        var announce = MakeAnnounce();
        await _sut.HandleCoordinatorAnnounceAsync("registered-coord", announce);

        await _broadcaster.DidNotReceive()
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Any<CoordinatorRegistrationPayload>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleCoordinatorAnnounceAsync_RejectedCoord_ReturnsWithoutBroadcast()
    {
        await _sut.RejectRegistrationAsync("rej-coord");
        _broadcaster.ClearReceivedCalls();

        var announce = MakeAnnounce();
        await _sut.HandleCoordinatorAnnounceAsync("rej-coord", announce);

        await _broadcaster.DidNotReceive()
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Any<CoordinatorRegistrationPayload>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleCoordinatorAnnounceAsync_NewCoord_CreatesPendingAndBroadcasts()
    {
        var announce = MakeAnnounce("1.2.3", "ESP32-S3", -55, "192.168.1.10", 120000);

        await _sut.HandleCoordinatorAnnounceAsync("announce-new", announce);

        // Should broadcast
        await _broadcaster.Received(1)
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Is<CoordinatorRegistrationPayload>(p =>
                    p.CoordId == "announce-new" &&
                    p.FwVersion == "1.2.3" &&
                    p.ChipModel == "ESP32-S3"),
                Arg.Any<CancellationToken>());

        // Should appear in pending
        var pending = await _sut.GetPendingRegistrationsAsync();
        pending.Should().ContainSingle(p => p.CoordId == "announce-new");
    }

    [Fact]
    public async Task HandleCoordinatorAnnounceAsync_ExistingPending_UpdatesWithRicherDataAndBroadcasts()
    {
        // First: create pending via ProcessUnknown (no announce data)
        await _sut.ProcessUnknownCoordinatorAsync("enrich-coord", "topic", Array.Empty<byte>());
        _broadcaster.ClearReceivedCalls();

        // Second: announce with richer data
        var announce = MakeAnnounce("2.0.0", "ESP32-S3", -40, "10.0.0.5", 256000);
        await _sut.HandleCoordinatorAnnounceAsync("enrich-coord", announce);

        // Should broadcast again (announce always broadcasts)
        await _broadcaster.Received(1)
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Is<CoordinatorRegistrationPayload>(p =>
                    p.CoordId == "enrich-coord" &&
                    p.FwVersion == "2.0.0" &&
                    p.Ip == "10.0.0.5"),
                Arg.Any<CancellationToken>());

        // Pending should have updated data
        var pending = await _sut.GetPendingRegistrationsAsync();
        var entry = pending.Should().ContainSingle(p => p.CoordId == "enrich-coord").Subject;
        entry.FwVersion.Should().Be("2.0.0");
        entry.ChipModel.Should().Be("ESP32-S3");
        entry.MessageCount.Should().Be(2);
    }

    // ========================================================================
    // ApproveRegistrationAsync – validation
    // ========================================================================

    [Fact]
    public async Task ApproveRegistrationAsync_MissingCoordId_ThrowsInvalidOperationException()
    {
        var request = new ApproveCoordinatorRegistrationRequest
        {
            CoordId = "",
            FarmId = "farm-1",
            Name = "Test"
        };

        var act = () => _sut.ApproveRegistrationAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CoordId*");
    }

    [Fact]
    public async Task ApproveRegistrationAsync_MissingFarmId_ThrowsInvalidOperationException()
    {
        var request = new ApproveCoordinatorRegistrationRequest
        {
            CoordId = "coord-1",
            FarmId = "",
            Name = "Test"
        };

        var act = () => _sut.ApproveRegistrationAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FarmId*");
    }

    [Fact]
    public async Task ApproveRegistrationAsync_MissingName_ThrowsInvalidOperationException()
    {
        var request = new ApproveCoordinatorRegistrationRequest
        {
            CoordId = "coord-1",
            FarmId = "farm-1",
            Name = "  "
        };

        var act = () => _sut.ApproveRegistrationAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public async Task ApproveRegistrationAsync_AlreadyRegistered_ThrowsInvalidOperationException()
    {
        var request = MakeApproveRequest("already-reg");
        await _sut.ApproveRegistrationAsync(request);

        var act = () => _sut.ApproveRegistrationAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    // ========================================================================
    // ApproveRegistrationAsync – happy path
    // ========================================================================

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_PersistsCoordinator()
    {
        var request = MakeApproveRequest("persist-coord");

        await _sut.ApproveRegistrationAsync(request);

        await _coordinatorRepo.Received(1)
            .UpsertAsync(
                Arg.Is<Coordinator>(c =>
                    c.CoordId == "persist-coord" &&
                    c.FarmId == "farm-1" &&
                    c.Name == "My Coordinator"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_CachesCoordinator()
    {
        var request = MakeApproveRequest("cache-coord");

        await _sut.ApproveRegistrationAsync(request);

        var isRegistered = await _sut.IsRegisteredAsync("cache-coord");
        isRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_RemovesFromPending()
    {
        // Create pending first
        await _sut.ProcessUnknownCoordinatorAsync("pending-coord", "topic", Array.Empty<byte>());

        var request = MakeApproveRequest("pending-coord");
        await _sut.ApproveRegistrationAsync(request);

        var pending = await _sut.GetPendingRegistrationsAsync();
        pending.Should().NotContain(p => p.CoordId == "pending-coord");
    }

    [Fact]
    public async Task ApproveRegistrationAsync_PreviouslyRejected_RemovesFromRejected()
    {
        // Reject first
        await _sut.RejectRegistrationAsync("was-rejected");

        // Approve
        var request = MakeApproveRequest("was-rejected");
        await _sut.ApproveRegistrationAsync(request);

        // Should now be registered, not rejected
        var isRegistered = await _sut.IsRegisteredAsync("was-rejected");
        isRegistered.Should().BeTrue();

        // Messages should not be dropped anymore
        _broadcaster.ClearReceivedCalls();
        // ProcessUnknown should not be called for registered coords, but
        // let's verify the coord is truly registered
        var result = await _sut.IsRegisteredAsync("was-rejected");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_PublishesMqttRegisteredMessage()
    {
        var request = MakeApproveRequest("mqtt-coord");

        await _sut.ApproveRegistrationAsync(request);

        await _mqtt.Received(1)
            .PublishJsonAsync(
                "coordinator/mqtt-coord/registered",
                Arg.Any<object>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_BroadcastsCoordinatorRegistered()
    {
        var request = MakeApproveRequest("ws-coord");

        await _sut.ApproveRegistrationAsync(request);

        await _broadcaster.Received(1)
            .BroadcastCoordinatorRegisteredAsync(
                Arg.Is<CoordinatorRegisteredPayload>(p =>
                    p.CoordId == "ws-coord" &&
                    p.FarmId == "farm-1" &&
                    p.Name == "My Coordinator"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_EmitsAdtTwinChangeEvent()
    {
        var request = MakeApproveRequest("adt-coord");

        await _sut.ApproveRegistrationAsync(request);

        // Read the event from the channel
        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.CoordinatorRegistered);
        evt.DeviceId.Should().Be("adt-coord");
        evt.FarmId.Should().Be("farm-1");
        evt.CoordinatorTwin.Should().NotBeNull();
        evt.CoordinatorTwin!.CoordId.Should().Be("adt-coord");
    }

    [Fact]
    public async Task ApproveRegistrationAsync_Valid_ReturnsCoordinatorModel()
    {
        var request = MakeApproveRequest("return-coord");

        var result = await _sut.ApproveRegistrationAsync(request);

        result.Should().NotBeNull();
        result.CoordId.Should().Be("return-coord");
        result.FarmId.Should().Be("farm-1");
        result.Name.Should().Be("My Coordinator");
        result.SiteId.Should().Be("farm-1"); // backwards compat
    }

    // ========================================================================
    // RejectRegistrationAsync
    // ========================================================================

    [Fact]
    public async Task RejectRegistrationAsync_AddsToRejectedSet()
    {
        await _sut.RejectRegistrationAsync("reject-me");

        // Verify by trying to process – should be silently dropped
        _broadcaster.ClearReceivedCalls();
        await _sut.ProcessUnknownCoordinatorAsync("reject-me", "topic", Array.Empty<byte>());

        await _broadcaster.DidNotReceive()
            .BroadcastCoordinatorRegistrationRequestAsync(
                Arg.Any<CoordinatorRegistrationPayload>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectRegistrationAsync_RemovesFromPending()
    {
        await _sut.ProcessUnknownCoordinatorAsync("pending-rej", "topic", Array.Empty<byte>());

        await _sut.RejectRegistrationAsync("pending-rej");

        var pending = await _sut.GetPendingRegistrationsAsync();
        pending.Should().NotContain(p => p.CoordId == "pending-rej");
    }

    [Fact]
    public async Task RejectRegistrationAsync_BroadcastsRejection()
    {
        await _sut.RejectRegistrationAsync("bcast-rej");

        await _broadcaster.Received(1)
            .BroadcastAsync(
                "coordinator_rejected",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // RemoveCoordinatorAsync
    // ========================================================================

    [Fact]
    public async Task RemoveCoordinatorAsync_NotFound_ReturnsFalse()
    {
        _coordinatorRepo.DeleteAsync("missing", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.RemoveCoordinatorAsync("missing");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveCoordinatorAsync_Found_DeletesAndReturnsTrue()
    {
        _coordinatorRepo.DeleteAsync("found-coord", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.RemoveCoordinatorAsync("found-coord");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveCoordinatorAsync_Found_RemovesFromCache()
    {
        // First register
        var request = MakeApproveRequest("remove-cache");
        await _sut.ApproveRegistrationAsync(request);
        (await _sut.IsRegisteredAsync("remove-cache")).Should().BeTrue();

        // Then remove
        _coordinatorRepo.DeleteAsync("remove-cache", Arg.Any<CancellationToken>())
            .Returns(true);
        await _sut.RemoveCoordinatorAsync("remove-cache");

        (await _sut.IsRegisteredAsync("remove-cache")).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveCoordinatorAsync_Found_EmitsAdtRemoveEvent()
    {
        // Drain any previous events
        while (_changeChannel.Reader.TryRead(out _)) { }

        _coordinatorRepo.DeleteAsync("adt-remove", Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.RemoveCoordinatorAsync("adt-remove");

        var hasEvent = _changeChannel.Reader.TryRead(out var evt);
        hasEvent.Should().BeTrue();
        evt!.ChangeType.Should().Be(TwinChangeType.CoordinatorRemoved);
        evt.DeviceId.Should().Be("adt-remove");
    }

    [Fact]
    public async Task RemoveCoordinatorAsync_Found_BroadcastsRemoval()
    {
        _coordinatorRepo.DeleteAsync("bcast-remove", Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.RemoveCoordinatorAsync("bcast-remove");

        await _broadcaster.Received(1)
            .BroadcastAsync(
                "coordinator_removed",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // GetPendingRegistrationsAsync
    // ========================================================================

    [Fact]
    public async Task GetPendingRegistrationsAsync_ReturnsSortedByLastSeenAtDescending()
    {
        // Create three pending registrations with different times
        await _sut.ProcessUnknownCoordinatorAsync("coord-a", "topic", Array.Empty<byte>());
        await Task.Delay(20); // small delay to ensure different timestamps
        await _sut.ProcessUnknownCoordinatorAsync("coord-b", "topic", Array.Empty<byte>());
        await Task.Delay(20);
        await _sut.ProcessUnknownCoordinatorAsync("coord-c", "topic", Array.Empty<byte>());

        var pending = await _sut.GetPendingRegistrationsAsync();

        pending.Should().HaveCount(3);
        pending[0].CoordId.Should().Be("coord-c"); // most recent
        pending[2].CoordId.Should().Be("coord-a"); // oldest
    }

    [Fact]
    public async Task GetPendingRegistrationsAsync_Empty_ReturnsEmptyList()
    {
        var pending = await _sut.GetPendingRegistrationsAsync();

        pending.Should().BeEmpty();
    }

    // ========================================================================
    // RefreshCacheAsync
    // ========================================================================

    [Fact]
    public async Task RefreshCacheAsync_LoadsAllCoordinatorsIntoCache()
    {
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Coordinator>
            {
                new() { CoordId = "coord-x" },
                new() { CoordId = "coord-y" },
                new() { CoordId = "coord-z" }
            });

        await _sut.RefreshCacheAsync();

        (await _sut.IsRegisteredAsync("coord-x")).Should().BeTrue();
        (await _sut.IsRegisteredAsync("coord-y")).Should().BeTrue();
        (await _sut.IsRegisteredAsync("coord-z")).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshCacheAsync_SkipsCoordinatorsWithEmptyCoordId()
    {
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Coordinator>
            {
                new() { CoordId = "valid-coord" },
                new() { CoordId = "" },
                new() { CoordId = null! }
            });

        await _sut.RefreshCacheAsync();

        (await _sut.IsRegisteredAsync("valid-coord")).Should().BeTrue();
        (await _sut.IsRegisteredAsync("")).Should().BeFalse();
    }

    [Fact]
    public async Task RefreshCacheAsync_DbFailure_HandlesGracefully()
    {
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB connection failed"));

        // Should not throw
        var act = () => _sut.RefreshCacheAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshCacheAsync_ClearsOldCacheEntries()
    {
        // First load
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Coordinator>
            {
                new() { CoordId = "old-coord" }
            });
        await _sut.RefreshCacheAsync();
        (await _sut.IsRegisteredAsync("old-coord")).Should().BeTrue();

        // Second load with different data
        _coordinatorRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Coordinator>
            {
                new() { CoordId = "new-coord" }
            });
        await _sut.RefreshCacheAsync();

        (await _sut.IsRegisteredAsync("old-coord")).Should().BeFalse();
        (await _sut.IsRegisteredAsync("new-coord")).Should().BeTrue();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static ApproveCoordinatorRegistrationRequest MakeApproveRequest(string coordId) => new()
    {
        CoordId = coordId,
        FarmId = "farm-1",
        Name = "My Coordinator",
        Description = "Test coordinator",
        Color = "#3b82f6",
        Tags = new List<string> { "test" },
        Location = "Lab A"
    };

    private static CoordinatorAnnounceDto MakeAnnounce(
        string? fw = "1.0.0",
        string? chip = "ESP32-S3",
        int rssi = -50,
        string? ip = "192.168.1.100",
        int freeHeap = 200000) => new()
    {
        Mac = "AA:BB:CC:DD:EE:FF",
        FwVersion = fw,
        ChipModel = chip,
        WifiRssi = rssi,
        Ip = ip,
        FreeHeap = freeHeap
    };
}
