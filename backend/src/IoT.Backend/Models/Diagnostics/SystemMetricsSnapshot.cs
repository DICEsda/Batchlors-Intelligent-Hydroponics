namespace IoT.Backend.Models.Diagnostics;

/// <summary>
/// Point-in-time snapshot of backend performance metrics.
/// Produced every second by DiagnosticsService and exposed via REST + WebSocket.
/// </summary>
public class SystemMetricsSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ============================================================================
    // Throughput counters
    // ============================================================================

    public long TowerMessagesTotal { get; set; }
    public long ReservoirMessagesTotal { get; set; }
    public double TowerMessagesPerSecond { get; set; }
    public double ReservoirMessagesPerSecond { get; set; }

    // ============================================================================
    // Latency (milliseconds) – handler total
    // ============================================================================

    public double AvgHandlerMs { get; set; }
    public double P95HandlerMs { get; set; }
    public double P99HandlerMs { get; set; }
    public double MaxHandlerMs { get; set; }

    // ============================================================================
    // Latency breakdown
    // ============================================================================

    public double AvgMongoWriteMs { get; set; }
    public double MaxMongoWriteMs { get; set; }
    public double AvgTwinUpsertMs { get; set; }
    public double MaxTwinUpsertMs { get; set; }
    public double AvgWsBroadcastMs { get; set; }
    public double MaxWsBroadcastMs { get; set; }

    // ============================================================================
    // Error counts
    // ============================================================================

    public long MongoWriteErrors { get; set; }
    public long ProcessingErrors { get; set; }

    // ============================================================================
    // System
    // ============================================================================

    public int WebSocketClients { get; set; }
    public long UptimeSeconds { get; set; }
}
