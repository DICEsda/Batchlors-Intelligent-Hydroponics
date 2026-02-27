import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, Observable } from 'rxjs';
import { WebSocketService } from './websocket.service';
import { EnvironmentService } from './environment.service';
import { SystemMetricsSnapshot, ScaleTestResult } from '../models/diagnostics.model';

/**
 * Diagnostics Service
 * Subscribes to WebSocket `diagnostics_update` messages and provides
 * signals + observables for the diagnostics dashboard pages.
 * Also exposes REST methods for fetching history and resetting counters.
 */
@Injectable({
  providedIn: 'root'
})
export class DiagnosticsService {
  private readonly ws = inject(WebSocketService);
  private readonly http = inject(HttpClient);
  private readonly env = inject(EnvironmentService);

  // Current snapshot (updated every 2s via WebSocket)
  readonly currentSnapshot = signal<SystemMetricsSnapshot | null>(null);

  // Rolling history for time-series charts (keep last 300 snapshots = 10 min at 2s interval)
  private readonly maxHistory = 300;
  readonly history = signal<SystemMetricsSnapshot[]>([]);

  // Scale test recording
  readonly isRecording = signal<boolean>(false);
  readonly scaleTestResults = signal<ScaleTestResult[]>([]);
  private recordingStart: string | null = null;
  private recordingSnapshots: SystemMetricsSnapshot[] = [];

  // Computed convenience signals
  readonly totalMessagesPerSecond = computed(() => {
    const snap = this.currentSnapshot();
    return snap ? snap.towerMessagesPerSecond + snap.reservoirMessagesPerSecond : 0;
  });

  readonly totalMessages = computed(() => {
    const snap = this.currentSnapshot();
    return snap ? snap.towerMessagesTotal + snap.reservoirMessagesTotal : 0;
  });

  readonly totalErrors = computed(() => {
    const snap = this.currentSnapshot();
    return snap ? snap.mongoWriteErrors + snap.processingErrors : 0;
  });

  // Subject for external subscribers
  private readonly snapshotSubject = new Subject<SystemMetricsSnapshot>();
  readonly snapshot$ = this.snapshotSubject.asObservable();

  constructor() {
    // Subscribe to the general message stream from WebSocket
    this.ws.messages$.subscribe({
      next: (msg) => {
        if (msg.type === 'diagnostics_update') {
          const snapshot = msg.payload as SystemMetricsSnapshot;
          this.onSnapshot(snapshot);
        }
      },
      error: (err) => console.error('DiagnosticsService: WebSocket stream error', err),
    });
  }

  /**
   * Handle incoming diagnostics snapshot from WebSocket
   */
  private onSnapshot(snapshot: SystemMetricsSnapshot): void {
    this.currentSnapshot.set(snapshot);
    this.snapshotSubject.next(snapshot);

    // Append to rolling history
    this.history.update(h => {
      const updated = [...h, snapshot];
      if (updated.length > this.maxHistory) {
        return updated.slice(updated.length - this.maxHistory);
      }
      return updated;
    });

    // If recording a scale test, capture the snapshot
    if (this.isRecording()) {
      this.recordingSnapshots.push(snapshot);
    }
  }

  // ===========================================================================
  // REST API Methods
  // ===========================================================================

  /**
   * Fetch current snapshot from REST (fallback if WS not connected)
   */
  fetchCurrent(): Observable<SystemMetricsSnapshot> {
    return this.http.get<SystemMetricsSnapshot>(`${this.env.apiUrl}/api/diagnostics`);
  }

  /**
   * Fetch historical snapshots from the backend
   */
  fetchHistory(minutes: number = 30): Observable<SystemMetricsSnapshot[]> {
    return this.http.get<SystemMetricsSnapshot[]>(
      `${this.env.apiUrl}/api/diagnostics/history`,
      { params: { minutes: minutes.toString() } }
    );
  }

  /**
   * Reset all diagnostics counters on the backend
   */
  resetCounters(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.env.apiUrl}/api/diagnostics/reset`, {});
  }

  // ===========================================================================
  // Scale Test Recording
  // ===========================================================================

  /**
   * Start recording snapshots for a scale test
   */
  startRecording(): void {
    this.recordingStart = new Date().toISOString();
    this.recordingSnapshots = [];
    this.isRecording.set(true);
  }

  /**
   * Stop recording and compute scale test results
   */
  stopRecording(towerCount: number): ScaleTestResult | null {
    if (!this.isRecording() || this.recordingSnapshots.length === 0) {
      this.isRecording.set(false);
      return null;
    }

    const snapshots = [...this.recordingSnapshots];
    const endedAt = new Date().toISOString();

    const peakThroughput = Math.max(
      ...snapshots.map(s => s.towerMessagesPerSecond + s.reservoirMessagesPerSecond)
    );
    const avgThroughput = snapshots.reduce(
      (sum, s) => sum + s.towerMessagesPerSecond + s.reservoirMessagesPerSecond, 0
    ) / snapshots.length;

    const lastSnap = snapshots[snapshots.length - 1];
    const firstSnap = snapshots[0];

    const result: ScaleTestResult = {
      startedAt: this.recordingStart!,
      endedAt,
      towerCount,
      durationSeconds: Math.round(
        (new Date(endedAt).getTime() - new Date(this.recordingStart!).getTime()) / 1000
      ),
      peakThroughput: Math.round(peakThroughput * 100) / 100,
      avgThroughput: Math.round(avgThroughput * 100) / 100,
      avgHandlerMs: Math.round(
        snapshots.reduce((s, x) => s + x.avgHandlerMs, 0) / snapshots.length * 1000
      ) / 1000,
      p95HandlerMs: Math.round(
        snapshots.reduce((s, x) => s + x.p95HandlerMs, 0) / snapshots.length * 1000
      ) / 1000,
      p99HandlerMs: Math.round(
        snapshots.reduce((s, x) => s + x.p99HandlerMs, 0) / snapshots.length * 1000
      ) / 1000,
      avgMongoWriteMs: Math.round(
        snapshots.reduce((s, x) => s + x.avgMongoWriteMs, 0) / snapshots.length * 1000
      ) / 1000,
      avgTwinUpsertMs: Math.round(
        snapshots.reduce((s, x) => s + x.avgTwinUpsertMs, 0) / snapshots.length * 1000
      ) / 1000,
      avgWsBroadcastMs: Math.round(
        snapshots.reduce((s, x) => s + x.avgWsBroadcastMs, 0) / snapshots.length * 1000
      ) / 1000,
      totalMessages: lastSnap.towerMessagesTotal + lastSnap.reservoirMessagesTotal
        - firstSnap.towerMessagesTotal - firstSnap.reservoirMessagesTotal,
      totalErrors: lastSnap.mongoWriteErrors + lastSnap.processingErrors
        - firstSnap.mongoWriteErrors - firstSnap.processingErrors,
      snapshots
    };

    this.scaleTestResults.update(results => [...results, result]);
    this.isRecording.set(false);
    this.recordingSnapshots = [];
    this.recordingStart = null;

    return result;
  }
}
