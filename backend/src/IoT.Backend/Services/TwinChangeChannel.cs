using System.Threading.Channels;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;

namespace IoT.Backend.Services;

/// <summary>
/// Types of twin change events that trigger ADT sync.
/// </summary>
public enum TwinChangeType
{
    /// <summary>Tower reported state updated (telemetry)</summary>
    TowerTelemetry,

    /// <summary>Tower desired state changed (user command)</summary>
    TowerDesiredStateChanged,

    /// <summary>Coordinator reported state updated (telemetry)</summary>
    CoordinatorTelemetry,

    /// <summary>Coordinator desired state changed (user command)</summary>
    CoordinatorDesiredStateChanged,

    /// <summary>New tower paired to a coordinator</summary>
    TowerPaired,

    /// <summary>Tower removed/forgotten from coordinator</summary>
    TowerRemoved,

    /// <summary>Full tower twin upsert (create or replace)</summary>
    TowerUpsert,

    /// <summary>Full coordinator twin upsert (create or replace)</summary>
    CoordinatorUpsert,

    /// <summary>Coordinator registered — create ADT twin + Farm→Coordinator relationship</summary>
    CoordinatorRegistered,

    /// <summary>Coordinator removed — delete ADT twin + relationships</summary>
    CoordinatorRemoved,

    /// <summary>Farm upsert — create or replace Farm ADT twin</summary>
    FarmUpsert
}

/// <summary>
/// Event record pushed into the channel when a local twin changes.
/// The <see cref="AdtSyncService"/> reads these events and syncs to Azure Digital Twins.
/// </summary>
public record TwinChangeEvent
{
    /// <summary>Type of change that occurred</summary>
    public required TwinChangeType ChangeType { get; init; }

    /// <summary>Tower ID (for tower events) or Coordinator ID (for coordinator events)</summary>
    public required string DeviceId { get; init; }

    /// <summary>Farm ID for relationship context</summary>
    public string? FarmId { get; init; }

    /// <summary>Coordinator ID (for tower events, identifies the parent coordinator)</summary>
    public string? CoordId { get; init; }

    /// <summary>Optional tower reported state snapshot (for telemetry events)</summary>
    public TowerReportedState? TowerReported { get; init; }

    /// <summary>Optional coordinator reported state snapshot (for telemetry events)</summary>
    public CoordinatorReportedState? CoordinatorReported { get; init; }

    /// <summary>Optional full tower twin snapshot (for upsert events)</summary>
    public TowerTwin? TowerTwin { get; init; }

    /// <summary>Optional full coordinator twin snapshot (for upsert events)</summary>
    public CoordinatorTwin? CoordinatorTwin { get; init; }

    /// <summary>Optional Farm model (for FarmUpsert events)</summary>
    public Farm? Farm { get; init; }

    /// <summary>When the event was created</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// In-memory bounded channel for twin change events.
/// Producers (TwinService, TelemetryHandler, PairingService) write events;
/// the <see cref="AdtSyncService"/> background service reads and syncs to ADT.
/// 
/// DI Registration in Program.cs:
/// <code>
/// builder.Services.AddSingleton&lt;TwinChangeChannel&gt;();
/// </code>
/// </summary>
public class TwinChangeChannel
{
    private readonly Channel<TwinChangeEvent> _channel;
    private readonly ILogger<TwinChangeChannel> _logger;

    /// <summary>
    /// Channel capacity. Events beyond this are dropped (BoundedChannelFullMode.DropOldest)
    /// to prevent backpressure from blocking the hot telemetry path.
    /// </summary>
    private const int Capacity = 1000;

    public TwinChangeChannel(ILogger<TwinChangeChannel> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<TwinChangeEvent>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,   // Only AdtSyncService reads
            SingleWriter = false   // Multiple producers write
        });
    }

    /// <summary>
    /// Writer for producers to push events into the channel.
    /// </summary>
    public ChannelWriter<TwinChangeEvent> Writer => _channel.Writer;

    /// <summary>
    /// Reader for the AdtSyncService consumer.
    /// </summary>
    public ChannelReader<TwinChangeEvent> Reader => _channel.Reader;

    /// <summary>
    /// Convenience method for producers. Tries to write the event without blocking.
    /// If the channel is full, the oldest event is dropped (DropOldest policy).
    /// </summary>
    public bool TryWrite(TwinChangeEvent evt)
    {
        var written = _channel.Writer.TryWrite(evt);
        if (!written)
        {
            _logger.LogWarning(
                "TwinChangeChannel: failed to write {ChangeType} for {DeviceId} (channel full or completed)",
                evt.ChangeType, evt.DeviceId);
        }
        return written;
    }
}
