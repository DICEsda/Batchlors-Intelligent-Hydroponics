import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { NgxEchartsDirective, provideEcharts } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';

/**
 * A single data point for telemetry charts.
 */
export interface TelemetryPoint {
  time: Date;
  value: number;
}

/**
 * Reusable Telemetry Chart Component
 *
 * Wraps ngx-echarts to display a real-time line chart with:
 * - Smooth area-fill line
 * - Optional threshold lines (high/low)
 * - Rolling 1-hour data window with auto-pruning
 * - Dark theme compatible styling
 *
 * Usage:
 * ```html
 * <app-telemetry-chart
 *   title="pH Level"
 *   unit="pH"
 *   color="#8b5cf6"
 *   [minY]="0" [maxY]="14"
 *   [thresholdLow]="5.5" [thresholdHigh]="7.5"
 *   [data]="phData"
 *   height="200px"
 * />
 * ```
 */
@Component({
  selector: 'app-telemetry-chart',
  standalone: true,
  imports: [NgxEchartsDirective],
  providers: [provideEcharts()],
  templateUrl: './telemetry-chart.component.html',
  styleUrl: './telemetry-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TelemetryChartComponent {
  // ============================================================================
  // Inputs
  // ============================================================================

  @Input() title = '';
  @Input() unit = '';
  @Input() color = '#22c55e';
  @Input() minY?: number;
  @Input() maxY?: number;
  @Input() thresholdLow?: number;
  @Input() thresholdHigh?: number;
  @Input() height = '200px';

  // ============================================================================
  // Internal data store
  // ============================================================================

  private _points: TelemetryPoint[] = [];

  /** The full ECharts option (rebuilt on each data change). */
  chartOptions: EChartsOption = {};

  /** Merge object for incremental updates. */
  chartMerge: EChartsOption = {};

  // ============================================================================
  // Data setter — replaces all data
  // ============================================================================

  @Input()
  set data(points: TelemetryPoint[]) {
    if (!points) {
      this._points = [];
    } else {
      this._points = points.map(p => ({
        time: p.time instanceof Date ? p.time : new Date(p.time),
        value: p.value,
      }));
    }
    this.pruneOldPoints();
    this.rebuildChart();
  }

  // ============================================================================
  // Public API — append a single point (used with @ViewChild)
  // ============================================================================

  appendPoint(point: TelemetryPoint): void {
    this._points.push({
      time: point.time instanceof Date ? point.time : new Date(point.time),
      value: point.value,
    });
    this.pruneOldPoints();
    this.updateChartMerge();
  }

  // ============================================================================
  // Data management
  // ============================================================================

  /**
   * Remove points older than 1 hour from the rolling window.
   */
  private pruneOldPoints(): void {
    const cutoff = Date.now() - 60 * 60 * 1000; // 1 hour
    this._points = this._points.filter(p => p.time.getTime() >= cutoff);
  }

  // ============================================================================
  // Chart builders
  // ============================================================================

  /**
   * Full chart rebuild — used when initial data is set.
   */
  private rebuildChart(): void {
    this.chartOptions = this.buildOptions();
    // Clear merge so the full options take effect
    this.chartMerge = {};
  }

  /**
   * Incremental merge — used when appending a single point for efficiency.
   */
  private updateChartMerge(): void {
    const seriesData = this._points.map(p => [p.time.getTime(), p.value]);

    this.chartMerge = {
      series: [
        {
          data: seriesData,
        },
      ],
    };
  }

  /**
   * Builds the complete ECharts option.
   */
  private buildOptions(): EChartsOption {
    const seriesData = this._points.map(p => [p.time.getTime(), p.value]);

    // Build markLine data for thresholds
    const markLineData: any[] = [];
    if (this.thresholdHigh != null) {
      markLineData.push({
        yAxis: this.thresholdHigh,
        label: {
          formatter: `High: ${this.thresholdHigh}`,
          position: 'insideEndTop',
          color: 'rgba(239, 68, 68, 0.7)',
          fontSize: 10,
        },
        lineStyle: {
          color: 'rgba(239, 68, 68, 0.5)',
          type: 'dashed' as const,
          width: 1,
        },
      });
    }
    if (this.thresholdLow != null) {
      markLineData.push({
        yAxis: this.thresholdLow,
        label: {
          formatter: `Low: ${this.thresholdLow}`,
          position: 'insideEndBottom',
          color: 'rgba(59, 130, 246, 0.7)',
          fontSize: 10,
        },
        lineStyle: {
          color: 'rgba(59, 130, 246, 0.5)',
          type: 'dashed' as const,
          width: 1,
        },
      });
    }

    const color = this.color;
    const unit = this.unit;
    const title = this.title;

    return {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(17, 17, 27, 0.92)',
        borderColor: 'rgba(255,255,255,0.08)',
        textStyle: { color: '#e2e8f0', fontSize: 12 },
        formatter(params: any): string {
          const p = Array.isArray(params) ? params[0] : params;
          if (!p || p.value == null) return '';
          const val = p.value[1];
          const ts = new Date(p.value[0]);
          const hh = ts.getHours().toString().padStart(2, '0');
          const mm = ts.getMinutes().toString().padStart(2, '0');
          const ss = ts.getSeconds().toString().padStart(2, '0');
          return `<strong>${hh}:${mm}:${ss}</strong><br/>${title}: <b>${val} ${unit}</b>`;
        },
      },
      grid: {
        top: 12,
        right: 12,
        bottom: 24,
        left: 44,
        containLabel: false,
      },
      xAxis: {
        type: 'time',
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          hideOverlap: true,
          formatter: '{HH}:{mm}',
        },
        axisLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.15)' } },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        min: this.minY,
        max: this.maxY,
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          formatter: `{value} ${this.unit}`,
        },
        splitLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.06)' } },
      },
      legend: { show: false },
      series: [
        {
          name: this.title,
          type: 'line',
          data: seriesData,
          smooth: true,
          symbol: 'none',
          lineStyle: { color, width: 2 },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: `${color}66` }, // 40% opacity
                { offset: 1, color: 'rgba(0,0,0,0)' },
              ],
            } as any,
          },
          markLine:
            markLineData.length > 0
              ? {
                  silent: true,
                  symbol: 'none',
                  data: markLineData,
                }
              : undefined,
        },
      ],
      animation: true,
      animationDuration: 300,
    };
  }
}
