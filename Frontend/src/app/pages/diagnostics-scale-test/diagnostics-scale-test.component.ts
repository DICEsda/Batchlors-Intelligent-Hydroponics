import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucidePlay,
  lucideSquare,
  lucideBarChart3,
  lucideAlertTriangle,
  lucideClock,
  lucideZap,
  lucideRefreshCw,
  lucideTrash2,
  lucideChevronRight,
} from '@ng-icons/lucide';
import { NgxEchartsDirective, provideEcharts } from 'ngx-echarts';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardContentDirective,
} from '../../components/ui/card';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmIconDirective } from '../../components/ui/icon';
import { DiagnosticsService } from '../../core/services/diagnostics.service';
import { ScaleTestResult, SystemMetricsSnapshot } from '../../core/models/diagnostics.model';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-diagnostics-scale-test',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgIcon,
    NgxEchartsDirective,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardContentDirective,
    HlmButtonDirective,
    HlmBadgeDirective,
    HlmIconDirective,
  ],
  providers: [
    provideIcons({
      lucidePlay,
      lucideSquare,
      lucideBarChart3,
      lucideAlertTriangle,
      lucideClock,
      lucideZap,
      lucideRefreshCw,
      lucideTrash2,
      lucideChevronRight,
    }),
    provideEcharts(),
  ],
  templateUrl: './diagnostics-scale-test.component.html',
  styleUrl: './diagnostics-scale-test.component.scss',
})
export class DiagnosticsScaleTestComponent {
  protected readonly diagnosticsService = inject(DiagnosticsService);

  /** Number of towers to record against (user-configurable). */
  readonly towerCount = signal<number>(250);

  /** Currently selected result for detail view. */
  readonly selectedResult = signal<ScaleTestResult | null>(null);

  // ---------------------------------------------------------------------------
  // Convenience accessors
  // ---------------------------------------------------------------------------
  readonly isRecording = this.diagnosticsService.isRecording;
  readonly scaleTestResults = this.diagnosticsService.scaleTestResults;

  // ---------------------------------------------------------------------------
  // Chart options (computed from selected result)
  // ---------------------------------------------------------------------------
  readonly throughputChartOptions = computed<EChartsOption | null>(() => {
    const result = this.selectedResult();
    if (!result) return null;
    return this.buildThroughputChart(result);
  });

  readonly latencyChartOptions = computed<EChartsOption | null>(() => {
    const result = this.selectedResult();
    if (!result) return null;
    return this.buildLatencyChart(result);
  });

  readonly errorChartOptions = computed<EChartsOption | null>(() => {
    const result = this.selectedResult();
    if (!result) return null;
    return this.buildErrorChart(result);
  });

  // ---------------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------------
  startRecording(): void {
    this.diagnosticsService.startRecording();
  }

  stopRecording(): void {
    const result = this.diagnosticsService.stopRecording(this.towerCount());
    if (result) {
      this.selectedResult.set(result);
    }
  }

  resetCounters(): void {
    this.diagnosticsService.resetCounters().subscribe();
  }

  selectResult(result: ScaleTestResult): void {
    this.selectedResult.set(result);
  }

  deleteResult(result: ScaleTestResult, event: MouseEvent): void {
    event.stopPropagation();
    this.diagnosticsService.scaleTestResults.update(results =>
      results.filter(r => r.startedAt !== result.startedAt)
    );
    if (this.selectedResult()?.startedAt === result.startedAt) {
      this.selectedResult.set(null);
    }
  }

  isSelected(result: ScaleTestResult): boolean {
    return this.selectedResult()?.startedAt === result.startedAt;
  }

  // ---------------------------------------------------------------------------
  // Template helpers
  // ---------------------------------------------------------------------------
  formatTimestamp(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  }

  formatDuration(seconds: number): string {
    if (seconds < 60) return `${seconds}s`;
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return s > 0 ? `${m}m ${s}s` : `${m}m`;
  }

  // ---------------------------------------------------------------------------
  // Chart builders (dark-theme friendly)
  // ---------------------------------------------------------------------------
  private timeLabels(snapshots: SystemMetricsSnapshot[]): string[] {
    const start = new Date(snapshots[0].timestamp).getTime();
    return snapshots.map(s => {
      const elapsed = Math.round((new Date(s.timestamp).getTime() - start) / 1000);
      return `${elapsed}s`;
    });
  }

  private baseChartOptions(): Partial<EChartsOption> {
    return {
      backgroundColor: 'transparent',
      textStyle: { color: 'hsl(0 0% 63.9%)' },
      grid: { left: 56, right: 16, top: 40, bottom: 32 },
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'hsl(240 10% 3.9%)',
        borderColor: 'hsl(240 3.7% 15.9%)',
        textStyle: { color: 'hsl(0 0% 98%)' },
      },
    };
  }

  private buildThroughputChart(result: ScaleTestResult): EChartsOption {
    const labels = this.timeLabels(result.snapshots);
    return {
      ...this.baseChartOptions(),
      legend: {
        data: ['Tower msg/s', 'Reservoir msg/s'],
        textStyle: { color: 'hsl(0 0% 63.9%)' },
        top: 4,
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      yAxis: {
        type: 'value',
        name: 'msg/s',
        nameTextStyle: { color: 'hsl(0 0% 63.9%)' },
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        splitLine: { lineStyle: { color: 'hsl(240 3.7% 15.9% / 0.4)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      series: [
        {
          name: 'Tower msg/s',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.towerMessagesPerSecond * 100) / 100),
          lineStyle: { width: 2 },
          itemStyle: { color: '#22d3ee' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(34,211,238,0.25)' },
                { offset: 1, color: 'rgba(34,211,238,0.02)' },
              ],
            },
          },
        },
        {
          name: 'Reservoir msg/s',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.reservoirMessagesPerSecond * 100) / 100),
          lineStyle: { width: 2 },
          itemStyle: { color: '#a78bfa' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(167,139,250,0.25)' },
                { offset: 1, color: 'rgba(167,139,250,0.02)' },
              ],
            },
          },
        },
      ],
    };
  }

  private buildLatencyChart(result: ScaleTestResult): EChartsOption {
    const labels = this.timeLabels(result.snapshots);
    return {
      ...this.baseChartOptions(),
      legend: {
        data: ['Handler', 'Mongo', 'Twin', 'WebSocket'],
        textStyle: { color: 'hsl(0 0% 63.9%)' },
        top: 4,
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      yAxis: {
        type: 'value',
        name: 'ms',
        nameTextStyle: { color: 'hsl(0 0% 63.9%)' },
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        splitLine: { lineStyle: { color: 'hsl(240 3.7% 15.9% / 0.4)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      series: [
        {
          name: 'Handler',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.avgHandlerMs * 1000) / 1000),
          lineStyle: { width: 2 },
          itemStyle: { color: '#f59e0b' },
        },
        {
          name: 'Mongo',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.avgMongoWriteMs * 1000) / 1000),
          lineStyle: { width: 2 },
          itemStyle: { color: '#10b981' },
        },
        {
          name: 'Twin',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.avgTwinUpsertMs * 1000) / 1000),
          lineStyle: { width: 2 },
          itemStyle: { color: '#3b82f6' },
        },
        {
          name: 'WebSocket',
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => Math.round(s.avgWsBroadcastMs * 1000) / 1000),
          lineStyle: { width: 2 },
          itemStyle: { color: '#ec4899' },
        },
      ],
    };
  }

  private buildErrorChart(result: ScaleTestResult): EChartsOption {
    const labels = this.timeLabels(result.snapshots);
    const firstSnap = result.snapshots[0];
    return {
      ...this.baseChartOptions(),
      legend: {
        data: ['Mongo Errors', 'Processing Errors'],
        textStyle: { color: 'hsl(0 0% 63.9%)' },
        top: 4,
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      yAxis: {
        type: 'value',
        name: 'cumulative',
        nameTextStyle: { color: 'hsl(0 0% 63.9%)' },
        axisLine: { lineStyle: { color: 'hsl(240 3.7% 15.9%)' } },
        splitLine: { lineStyle: { color: 'hsl(240 3.7% 15.9% / 0.4)' } },
        axisLabel: { color: 'hsl(0 0% 63.9%)' },
      },
      series: [
        {
          name: 'Mongo Errors',
          type: 'line',
          stack: 'errors',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => s.mongoWriteErrors - firstSnap.mongoWriteErrors),
          lineStyle: { width: 2 },
          itemStyle: { color: '#ef4444' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(239,68,68,0.35)' },
                { offset: 1, color: 'rgba(239,68,68,0.05)' },
              ],
            },
          },
        },
        {
          name: 'Processing Errors',
          type: 'line',
          stack: 'errors',
          smooth: true,
          symbol: 'none',
          data: result.snapshots.map(s => s.processingErrors - firstSnap.processingErrors),
          lineStyle: { width: 2 },
          itemStyle: { color: '#f97316' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(249,115,22,0.35)' },
                { offset: 1, color: 'rgba(249,115,22,0.05)' },
              ],
            },
          },
        },
      ],
    };
  }
}
