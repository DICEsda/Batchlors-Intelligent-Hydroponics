/**
 * Diagnostics Models
 * Backend performance metrics for the diagnostics dashboard
 */

/**
 * Point-in-time snapshot of backend performance metrics.
 * Matches Backend SystemMetricsSnapshot exactly.
 * Received via WebSocket `diagnostics_update` messages and REST GET /api/diagnostics.
 */
export interface SystemMetricsSnapshot {
  timestamp: string;

  // Throughput counters
  towerMessagesTotal: number;
  reservoirMessagesTotal: number;
  towerMessagesPerSecond: number;
  reservoirMessagesPerSecond: number;

  // Latency (ms) - handler total
  avgHandlerMs: number;
  p95HandlerMs: number;
  p99HandlerMs: number;
  maxHandlerMs: number;

  // Latency breakdown
  avgMongoWriteMs: number;
  maxMongoWriteMs: number;
  avgTwinUpsertMs: number;
  maxTwinUpsertMs: number;
  avgWsBroadcastMs: number;
  maxWsBroadcastMs: number;

  // Error counts
  mongoWriteErrors: number;
  processingErrors: number;

  // System
  webSocketClients: number;
  uptimeSeconds: number;
}

/**
 * Scale test results captured from a simulator run.
 * Aggregated from diagnostics history during a test window.
 */
export interface ScaleTestResult {
  startedAt: string;
  endedAt: string;
  towerCount: number;
  durationSeconds: number;
  peakThroughput: number;       // max msg/s observed
  avgThroughput: number;
  avgHandlerMs: number;
  p95HandlerMs: number;
  p99HandlerMs: number;
  avgMongoWriteMs: number;
  avgTwinUpsertMs: number;
  avgWsBroadcastMs: number;
  totalMessages: number;
  totalErrors: number;
  snapshots: SystemMetricsSnapshot[];
}
