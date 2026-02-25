import { Component, inject, computed, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideActivity,
  lucideDatabase,
  lucideWifi,
  lucideAlertTriangle,
  lucideClock,
  lucideRefreshCw,
  lucideServer,
  lucideZap,
} from '@ng-icons/lucide';
import { NgxEchartsDirective, provideEcharts } from 'ngx-echarts';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
  HlmCardContentDirective,
} from '../../components/ui/card';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmIconDirective } from '../../components/ui/icon';
import { DiagnosticsService } from '../../core/services/diagnostics.service';
import { WebSocketService } from '../../core/services/websocket.service';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-diagnostics-system',
  standalone: true,
  imports: [
    CommonModule,
    NgIcon,
    NgxEchartsDirective,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
    HlmButtonDirective,
    HlmBadgeDirective,
    HlmIconDirective,
  ],
  providers: [
    provideIcons({
      lucideActivity,
      lucideDatabase,
      lucideWifi,
      lucideAlertTriangle,
      lucideClock,
      lucideRefreshCw,
      lucideServer,
      lucideZap,
    }),
    provideEcharts(),
  ],
  templateUrl: './diagnostics-system.component.html',
  styleUrl: './diagnostics-system.component.scss',
})
export class DiagnosticsSystemComponent implements OnInit, OnDestroy {
  readonly diagnosticsService = inject(DiagnosticsService);
  private readonly wsService = inject(WebSocketService);

  // ============================================================================
  // Convenience signals from the service
  // ============================================================================
  readonly snapshot = this.diagnosticsService.currentSnapshot;
  readonly history = this.diagnosticsService.history;
  readonly totalMsgPerSec = this.diagnosticsService.totalMessagesPerSecond;
  readonly totalMessages = this.diagnosticsService.totalMessages;
  readonly totalErrors = this.diagnosticsService.totalErrors;
  readonly wsConnected = this.wsService.connected;
  readonly wsConnecting = this.wsService.connecting;

  readonly hasData = computed(() => this.snapshot() !== null);
  readonly hasHistory = computed(() => this.history().length > 1);

  // ============================================================================
  // Formatted stat values
  // ============================================================================
  readonly messagesPerSec = computed(() => {
    const snap = this.snapshot();
    if (!snap) return '0.0';
    const total = snap.towerMessagesPerSecond + snap.reservoirMessagesPerSecond;
    return total.toFixed(1);
  });

  readonly avgLatency = computed(() => {
    const snap = this.snapshot();
    if (!snap) return '0.0';
    return snap.avgHandlerMs.toFixed(1);
  });

  readonly p95Latency = computed(() => {
    const snap = this.snapshot();
    if (!snap) return '0.0';
    return snap.p95HandlerMs.toFixed(1);
  });

  readonly wsClients = computed(() => {
    const snap = this.snapshot();
    return snap ? snap.webSocketClients : 0;
  });

  readonly uptime = computed(() => {
    const snap = this.snapshot();
    if (!snap) return '--';
    const secs = snap.uptimeSeconds;
    const h = Math.floor(secs / 3600);
    const m = Math.floor((secs % 3600) / 60);
    if (h >= 24) {
      const d = Math.floor(h / 24);
      return `${d}d ${h % 24}h`;
    }
    return `${h}h ${m}m`;
  });

  // ============================================================================
  // Pipeline breakdown table data
  // ============================================================================
  readonly pipelineBreakdown = computed(() => {
    const snap = this.snapshot();
    if (!snap) return [];
    return [
      {
        component: 'Handler (total)',
        avg: snap.avgHandlerMs.toFixed(2),
        max: snap.maxHandlerMs.toFixed(2),
        p95: snap.p95HandlerMs.toFixed(2),
        p99: snap.p99HandlerMs.toFixed(2),
      },
      {
        component: 'MongoDB Write',
        avg: snap.avgMongoWriteMs.toFixed(2),
        max: snap.maxMongoWriteMs.toFixed(2),
        p95: '--',
        p99: '--',
      },
      {
        component: 'Twin Upsert',
        avg: snap.avgTwinUpsertMs.toFixed(2),
        max: snap.maxTwinUpsertMs.toFixed(2),
        p95: '--',
        p99: '--',
      },
      {
        component: 'WS Broadcast',
        avg: snap.avgWsBroadcastMs.toFixed(2),
        max: snap.maxWsBroadcastMs.toFixed(2),
        p95: '--',
        p99: '--',
      },
    ];
  });

  // ============================================================================
  // ECharts — Throughput Chart Options (computed from history signal)
  // ============================================================================
  readonly throughputOptions = computed<EChartsOption>(() => {
    const snapshots = this.history();
    if (snapshots.length === 0) return this.emptyChartOption('Throughput (msg/s)');

    const timestamps = snapshots.map(s => this.formatTime(s.timestamp));
    const towerSeries = snapshots.map(s => s.towerMessagesPerSecond);
    const reservoirSeries = snapshots.map(s => s.reservoirMessagesPerSecond);
    const totalSeries = snapshots.map(
      s => s.towerMessagesPerSecond + s.reservoirMessagesPerSecond
    );

    return {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'hsl(222.2 47.4% 11.2%)',
        borderColor: 'hsl(217.2 32.6% 17.5%)',
        textStyle: { color: '#e2e8f0', fontSize: 12 },
      },
      legend: {
        data: ['Total', 'Tower', 'Reservoir'],
        textStyle: { color: '#94a3b8' },
        bottom: 0,
      },
      grid: {
        top: 40,
        right: 20,
        bottom: 40,
        left: 50,
        containLabel: false,
      },
      xAxis: {
        type: 'category',
        data: timestamps,
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#94a3b8', fontSize: 10, rotate: 0 },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        name: 'msg/s',
        nameTextStyle: { color: '#94a3b8', fontSize: 11 },
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#94a3b8', fontSize: 10 },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
      },
      series: [
        {
          name: 'Total',
          type: 'line',
          data: totalSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 2, color: '#38bdf8' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(56, 189, 248, 0.25)' },
                { offset: 1, color: 'rgba(56, 189, 248, 0.02)' },
              ],
            },
          },
        },
        {
          name: 'Tower',
          type: 'line',
          data: towerSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#a78bfa' },
        },
        {
          name: 'Reservoir',
          type: 'line',
          data: reservoirSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#34d399' },
        },
      ],
    };
  });

  // ============================================================================
  // ECharts — Latency Breakdown Chart Options (computed from history signal)
  // ============================================================================
  readonly latencyOptions = computed<EChartsOption>(() => {
    const snapshots = this.history();
    if (snapshots.length === 0) return this.emptyChartOption('Latency Breakdown (ms)');

    const timestamps = snapshots.map(s => this.formatTime(s.timestamp));
    const handlerSeries = snapshots.map(s => s.avgHandlerMs);
    const mongoSeries = snapshots.map(s => s.avgMongoWriteMs);
    const twinSeries = snapshots.map(s => s.avgTwinUpsertMs);
    const wsSeries = snapshots.map(s => s.avgWsBroadcastMs);

    return {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'hsl(222.2 47.4% 11.2%)',
        borderColor: 'hsl(217.2 32.6% 17.5%)',
        textStyle: { color: '#e2e8f0', fontSize: 12 },
        valueFormatter: (value: unknown) => {
          const num = Number(value);
          return isNaN(num) ? String(value) : `${num.toFixed(2)} ms`;
        },
      },
      legend: {
        data: ['Handler', 'MongoDB', 'Twin Upsert', 'WS Broadcast'],
        textStyle: { color: '#94a3b8' },
        bottom: 0,
      },
      grid: {
        top: 40,
        right: 20,
        bottom: 40,
        left: 50,
        containLabel: false,
      },
      xAxis: {
        type: 'category',
        data: timestamps,
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#94a3b8', fontSize: 10, rotate: 0 },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        name: 'ms',
        nameTextStyle: { color: '#94a3b8', fontSize: 11 },
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#94a3b8', fontSize: 10 },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
      },
      series: [
        {
          name: 'Handler',
          type: 'line',
          data: handlerSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 2, color: '#f97316' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(249, 115, 22, 0.2)' },
                { offset: 1, color: 'rgba(249, 115, 22, 0.02)' },
              ],
            },
          },
        },
        {
          name: 'MongoDB',
          type: 'line',
          data: mongoSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#22d3ee' },
        },
        {
          name: 'Twin Upsert',
          type: 'line',
          data: twinSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#a78bfa' },
        },
        {
          name: 'WS Broadcast',
          type: 'line',
          data: wsSeries,
          smooth: true,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#fb7185' },
        },
      ],
    };
  });

  // ============================================================================
  // Lifecycle
  // ============================================================================
  ngOnInit(): void {
    // Ensure WebSocket is connected so diagnostics_update messages arrive
    if (!this.wsService.connected() && !this.wsService.connecting()) {
      this.wsService.connect();
    }
  }

  ngOnDestroy(): void {
    // No manual cleanup needed — service is root-scoped and signals are auto-tracked
  }

  // ============================================================================
  // Actions
  // ============================================================================
  resetCounters(): void {
    this.diagnosticsService.resetCounters().subscribe({
      next: () => {
        // History and snapshot will update naturally via the next WS message
      },
      error: (err) => {
        console.error('[DiagnosticsSystem] Failed to reset counters:', err);
      },
    });
  }

  // ============================================================================
  // Helpers
  // ============================================================================
  private formatTime(timestamp: string): string {
    const d = new Date(timestamp);
    const h = d.getHours().toString().padStart(2, '0');
    const m = d.getMinutes().toString().padStart(2, '0');
    const s = d.getSeconds().toString().padStart(2, '0');
    return `${h}:${m}:${s}`;
  }

  private emptyChartOption(title: string): EChartsOption {
    return {
      backgroundColor: 'transparent',
      title: {
        text: title,
        left: 'center',
        top: 'center',
        textStyle: { color: '#475569', fontSize: 14, fontWeight: 'normal' },
      },
      xAxis: { type: 'category', data: [] },
      yAxis: { type: 'value' },
      series: [],
    };
  }
}
