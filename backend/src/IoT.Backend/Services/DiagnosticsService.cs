using System.Collections.Concurrent;
using IoT.Backend.Models.Diagnostics;

namespace IoT.Backend.Services;

/// <summary>
/// Thread-safe singleton that collects backend performance metrics.
/// A 1-second timer computes percentiles and throughput rates from raw samples
/// and stores snapshots in a rolling 1-hour history buffer.
/// </summary>
public sealed class DiagnosticsService : IDiagnosticsService, IDisposable
{
    // ============================================================================
    // Constants
    // ============================================================================

    private const int MaxLatencySamples = 2000;
    private const int MaxHistorySnapshots = 3600; // 1 hour at 1 snapshot/sec

    // ============================================================================
    // Throughput counters (updated via Interlocked)
    // ============================================================================

    private long _towerMessagesTotal;
    private long _reservoirMessagesTotal;
    private long _mongoWriteErrors;
    private long _processingErrors;

    // Previous-tick counters for rate calculation
    private long _prevTowerTotal;
    private long _prevReservoirTotal;

    // ============================================================================
    // Latency sample queues (ConcurrentQueue, capped at MaxLatencySamples)
    // ============================================================================

    private readonly ConcurrentQueue<double> _handlerTotalMs = new();
    private readonly ConcurrentQueue<double> _mongoWriteMs = new();
    private readonly ConcurrentQueue<double> _twinUpsertMs = new();
    private readonly ConcurrentQueue<double> _wsBroadcastMs = new();

    // Track queue lengths to avoid calling Count (O(n) on ConcurrentQueue)
    private int _handlerTotalCount;
    private int _mongoWriteCount;
    private int _twinUpsertCount;
    private int _wsBroadcastCount;

    // ============================================================================
    // History
    // ============================================================================

    private readonly ConcurrentQueue<SystemMetricsSnapshot> _history = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly Timer _timer;

    // ============================================================================
    // Constructor
    // ============================================================================

    public DiagnosticsService()
    {
        // Fire every 1 second to compute a snapshot
        _timer = new Timer(ComputeSnapshot, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    // ============================================================================
    // IDiagnosticsService – Recording
    // ============================================================================

    public void RecordTowerMessage(TimeSpan total, TimeSpan mongoWrite, TimeSpan twinUpsert, TimeSpan wsBroadcast)
    {
        Interlocked.Increment(ref _towerMessagesTotal);
        EnqueueSample(_handlerTotalMs, total.TotalMilliseconds, ref _handlerTotalCount);
        EnqueueSample(_mongoWriteMs, mongoWrite.TotalMilliseconds, ref _mongoWriteCount);
        EnqueueSample(_twinUpsertMs, twinUpsert.TotalMilliseconds, ref _twinUpsertCount);
        EnqueueSample(_wsBroadcastMs, wsBroadcast.TotalMilliseconds, ref _wsBroadcastCount);
    }

    public void RecordReservoirMessage(TimeSpan total, TimeSpan mongoWrite, TimeSpan twinUpsert, TimeSpan wsBroadcast)
    {
        Interlocked.Increment(ref _reservoirMessagesTotal);
        EnqueueSample(_handlerTotalMs, total.TotalMilliseconds, ref _handlerTotalCount);
        EnqueueSample(_mongoWriteMs, mongoWrite.TotalMilliseconds, ref _mongoWriteCount);
        EnqueueSample(_twinUpsertMs, twinUpsert.TotalMilliseconds, ref _twinUpsertCount);
        EnqueueSample(_wsBroadcastMs, wsBroadcast.TotalMilliseconds, ref _wsBroadcastCount);
    }

    public void RecordError(string component)
    {
        switch (component)
        {
            case "mongo_write":
                Interlocked.Increment(ref _mongoWriteErrors);
                break;
            case "processing":
                Interlocked.Increment(ref _processingErrors);
                break;
            // twin_upsert and ws_broadcast errors are counted under processing
            case "twin_upsert":
            case "ws_broadcast":
                Interlocked.Increment(ref _processingErrors);
                break;
        }
    }

    // ============================================================================
    // IDiagnosticsService – Queries
    // ============================================================================

    public SystemMetricsSnapshot GetCurrentSnapshot(int wsClientCount)
    {
        // Return the latest snapshot from history, enriched with live WS count
        if (_history.TryPeek(out _))
        {
            // Get the most recent snapshot
            SystemMetricsSnapshot? latest = null;
            foreach (var s in _history)
            {
                latest = s;
            }

            if (latest != null)
            {
                // Return a copy with the live WS client count
                return new SystemMetricsSnapshot
                {
                    Timestamp = latest.Timestamp,
                    TowerMessagesTotal = latest.TowerMessagesTotal,
                    ReservoirMessagesTotal = latest.ReservoirMessagesTotal,
                    TowerMessagesPerSecond = latest.TowerMessagesPerSecond,
                    ReservoirMessagesPerSecond = latest.ReservoirMessagesPerSecond,
                    AvgHandlerMs = latest.AvgHandlerMs,
                    P95HandlerMs = latest.P95HandlerMs,
                    P99HandlerMs = latest.P99HandlerMs,
                    MaxHandlerMs = latest.MaxHandlerMs,
                    AvgMongoWriteMs = latest.AvgMongoWriteMs,
                    MaxMongoWriteMs = latest.MaxMongoWriteMs,
                    AvgTwinUpsertMs = latest.AvgTwinUpsertMs,
                    MaxTwinUpsertMs = latest.MaxTwinUpsertMs,
                    AvgWsBroadcastMs = latest.AvgWsBroadcastMs,
                    MaxWsBroadcastMs = latest.MaxWsBroadcastMs,
                    MongoWriteErrors = latest.MongoWriteErrors,
                    ProcessingErrors = latest.ProcessingErrors,
                    WebSocketClients = wsClientCount,
                    UptimeSeconds = latest.UptimeSeconds
                };
            }
        }

        // No history yet – compute a fresh snapshot
        return BuildSnapshot(wsClientCount);
    }

    public IReadOnlyList<SystemMetricsSnapshot> GetHistory(int minutes = 30)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        var result = new List<SystemMetricsSnapshot>();

        foreach (var snapshot in _history)
        {
            if (snapshot.Timestamp >= cutoff)
            {
                result.Add(snapshot);
            }
        }

        return result;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _towerMessagesTotal, 0);
        Interlocked.Exchange(ref _reservoirMessagesTotal, 0);
        Interlocked.Exchange(ref _mongoWriteErrors, 0);
        Interlocked.Exchange(ref _processingErrors, 0);
        Interlocked.Exchange(ref _prevTowerTotal, 0);
        Interlocked.Exchange(ref _prevReservoirTotal, 0);

        DrainQueue(_handlerTotalMs, ref _handlerTotalCount);
        DrainQueue(_mongoWriteMs, ref _mongoWriteCount);
        DrainQueue(_twinUpsertMs, ref _twinUpsertCount);
        DrainQueue(_wsBroadcastMs, ref _wsBroadcastCount);

        while (_history.TryDequeue(out _)) { }
    }

    // ============================================================================
    // Timer callback – runs every 1 second
    // ============================================================================

    private void ComputeSnapshot(object? state)
    {
        try
        {
            var snapshot = BuildSnapshot(wsClientCount: 0);

            _history.Enqueue(snapshot);

            // Cap history at MaxHistorySnapshots
            while (_history.Count > MaxHistorySnapshots)
            {
                _history.TryDequeue(out _);
            }

            // Update previous counters for next rate calculation
            Interlocked.Exchange(ref _prevTowerTotal, snapshot.TowerMessagesTotal);
            Interlocked.Exchange(ref _prevReservoirTotal, snapshot.ReservoirMessagesTotal);
        }
        catch
        {
            // Timer callbacks must not throw
        }
    }

    private SystemMetricsSnapshot BuildSnapshot(int wsClientCount)
    {
        var now = DateTime.UtcNow;
        var towerTotal = Interlocked.Read(ref _towerMessagesTotal);
        var reservoirTotal = Interlocked.Read(ref _reservoirMessagesTotal);
        var prevTower = Interlocked.Read(ref _prevTowerTotal);
        var prevReservoir = Interlocked.Read(ref _prevReservoirTotal);

        // Snapshot latency queues into arrays for percentile calculation
        var handlerSamples = _handlerTotalMs.ToArray();
        var mongoSamples = _mongoWriteMs.ToArray();
        var twinSamples = _twinUpsertMs.ToArray();
        var wsSamples = _wsBroadcastMs.ToArray();

        // Sort for percentile calculation
        Array.Sort(handlerSamples);
        Array.Sort(mongoSamples);
        Array.Sort(twinSamples);
        Array.Sort(wsSamples);

        return new SystemMetricsSnapshot
        {
            Timestamp = now,

            // Throughput
            TowerMessagesTotal = towerTotal,
            ReservoirMessagesTotal = reservoirTotal,
            TowerMessagesPerSecond = Math.Max(0, towerTotal - prevTower),
            ReservoirMessagesPerSecond = Math.Max(0, reservoirTotal - prevReservoir),

            // Handler total latency
            AvgHandlerMs = ComputeAverage(handlerSamples),
            P95HandlerMs = ComputePercentile(handlerSamples, 0.95),
            P99HandlerMs = ComputePercentile(handlerSamples, 0.99),
            MaxHandlerMs = handlerSamples.Length > 0 ? handlerSamples[^1] : 0,

            // Mongo write latency
            AvgMongoWriteMs = ComputeAverage(mongoSamples),
            MaxMongoWriteMs = mongoSamples.Length > 0 ? mongoSamples[^1] : 0,

            // Twin upsert latency
            AvgTwinUpsertMs = ComputeAverage(twinSamples),
            MaxTwinUpsertMs = twinSamples.Length > 0 ? twinSamples[^1] : 0,

            // WebSocket broadcast latency
            AvgWsBroadcastMs = ComputeAverage(wsSamples),
            MaxWsBroadcastMs = wsSamples.Length > 0 ? wsSamples[^1] : 0,

            // Errors
            MongoWriteErrors = Interlocked.Read(ref _mongoWriteErrors),
            ProcessingErrors = Interlocked.Read(ref _processingErrors),

            // System
            WebSocketClients = wsClientCount,
            UptimeSeconds = (long)(now - _startedAt).TotalSeconds
        };
    }

    // ============================================================================
    // Helpers
    // ============================================================================

    private static void EnqueueSample(ConcurrentQueue<double> queue, double value, ref int count)
    {
        queue.Enqueue(value);
        var currentCount = Interlocked.Increment(ref count);

        // Cap at MaxLatencySamples – dequeue oldest when full
        while (currentCount > MaxLatencySamples && queue.TryDequeue(out _))
        {
            currentCount = Interlocked.Decrement(ref count);
        }
    }

    private static void DrainQueue(ConcurrentQueue<double> queue, ref int count)
    {
        while (queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref count);
        }
    }

    private static double ComputeAverage(double[] sorted)
    {
        if (sorted.Length == 0) return 0;

        double sum = 0;
        for (int i = 0; i < sorted.Length; i++)
        {
            sum += sorted[i];
        }
        return Math.Round(sum / sorted.Length, 3);
    }

    private static double ComputePercentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;

        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return Math.Round(sorted[index], 3);
    }

    // ============================================================================
    // IDisposable
    // ============================================================================

    public void Dispose()
    {
        _timer.Dispose();
    }
}
