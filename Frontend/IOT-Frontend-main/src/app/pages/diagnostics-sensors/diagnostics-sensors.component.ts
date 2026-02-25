import { Component, inject, signal, effect, computed, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideThermometer,
  lucideDroplets,
  lucideBeaker,
  lucideSun,
  lucideWind,
  lucideFilter,
  lucideRefreshCw,
  lucideActivity,
  lucideGauge,
  lucideWaves,
  lucideBarChart3,
  lucideAlertTriangle,
  lucideLoader2,
} from '@ng-icons/lucide';
import { NgxEchartsDirective, provideEcharts } from 'ngx-echarts';
import { HlmCardDirective } from '../../components/ui/card';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { TelemetryHistoryService } from '../../core/services/telemetry-history.service';
import { ApiService } from '../../core/services/api.service';
import { IoTDataService } from '../../core/services/iot-data.service';
import { ReservoirTelemetry, TowerTelemetry } from '../../core/models/telemetry.model';
import type { EChartsOption } from 'echarts';

/**
 * Sensor Diagnostics Page
 *
 * Displays historical sensor telemetry from MongoDB for reservoirs and towers.
 * Reservoir charts: pH, EC, Water Level, Water Temperature
 * Tower charts: Ambient Temperature, Humidity, Light Level
 */
@Component({
  selector: 'app-diagnostics-sensors',
  standalone: true,
  imports: [
    FormsModule,
    NgIcon,
    NgxEchartsDirective,
    HlmCardDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmBadgeDirective,
  ],
  providers: [
    provideIcons({
      lucideThermometer,
      lucideDroplets,
      lucideBeaker,
      lucideSun,
      lucideWind,
      lucideFilter,
      lucideRefreshCw,
      lucideActivity,
      lucideGauge,
      lucideWaves,
      lucideBarChart3,
      lucideAlertTriangle,
      lucideLoader2,
    }),
    provideEcharts(),
  ],
  templateUrl: './diagnostics-sensors.component.html',
  styleUrl: './diagnostics-sensors.component.scss',
})
export class DiagnosticsSensorsComponent implements OnInit, OnDestroy {
  private readonly telemetryService = inject(TelemetryHistoryService);
  private readonly apiService = inject(ApiService);
  private readonly dataService = inject(IoTDataService);

  // ============================================================================
  // Device lists from backend
  // ============================================================================
  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;

  /** Unique farm IDs extracted from loaded coordinators */
  readonly farmOptions = computed(() => {
    const coords = this.coordinators();
    const farmIds = [...new Set(coords.map(c => c.site_id).filter(Boolean))];
    return farmIds.length > 0 ? farmIds : ['farm-001'];
  });

  /** Towers filtered to the currently selected coordinator */
  readonly filteredTowers = computed(() => {
    const coordId = this.selectedCoordId();
    if (!coordId) return this.nodes();
    return this.nodes().filter(n => n.coordinator_id === coordId);
  });

  // ============================================================================
  // Filter state
  // ============================================================================
  readonly selectedFarmId = signal<string>('');
  readonly selectedCoordId = signal<string>('');
  readonly selectedTowerId = signal<string>('');
  readonly timeRange = signal<number>(60);

  readonly timeRangeOptions: { label: string; value: number }[] = [
    { label: '15 min', value: 15 },
    { label: '30 min', value: 30 },
    { label: '1 hour', value: 60 },
    { label: '6 hours', value: 360 },
    { label: '24 hours', value: 1440 },
  ];

  // ============================================================================
  // Data state
  // ============================================================================
  readonly reservoirData = signal<ReservoirTelemetry[]>([]);
  readonly towerData = signal<TowerTelemetry[]>([]);
  readonly reservoirLoading = signal<boolean>(false);
  readonly towerLoading = signal<boolean>(false);
  readonly reservoirError = signal<string | null>(null);
  readonly towerError = signal<string | null>(null);
  readonly lastFetchTime = signal<Date | null>(null);

  // ============================================================================
  // Computed: data availability
  // ============================================================================
  readonly hasReservoirData = computed(() => this.reservoirData().length > 0);
  readonly hasTowerData = computed(() => this.towerData().length > 0);
  readonly reservoirDataPoints = computed(() => this.reservoirData().length);
  readonly towerDataPoints = computed(() => this.towerData().length);

  // ============================================================================
  // Computed: ECharts options for Reservoir data
  // ============================================================================
  readonly phChartOptions = computed(() =>
    this.buildReservoirChart(
      'pH Level',
      this.reservoirData(),
      (d) => d.ph,
      '#22c55e',
      'pH',
      { min: 0, max: 14 },
      [{ from: 5.5, to: 6.5, label: 'Optimal', color: 'rgba(34, 197, 94, 0.08)' }]
    )
  );

  readonly ecChartOptions = computed(() =>
    this.buildReservoirChart(
      'EC (mS/cm)',
      this.reservoirData(),
      (d) => d.ec,
      '#f59e0b',
      'mS/cm',
      { min: 0, max: 5 },
      [{ from: 1.0, to: 2.5, label: 'Optimal', color: 'rgba(245, 158, 11, 0.08)' }]
    )
  );

  readonly waterLevelChartOptions = computed(() =>
    this.buildReservoirChart(
      'Water Level',
      this.reservoirData(),
      (d) => d.waterLevel,
      '#3b82f6',
      '%',
      { min: 0, max: 100 },
      []
    )
  );

  readonly waterTempChartOptions = computed(() =>
    this.buildReservoirChart(
      'Water Temperature',
      this.reservoirData(),
      (d) => d.temperature,
      '#ef4444',
      '\u00B0C',
      undefined,
      []
    )
  );

  // ============================================================================
  // Computed: ECharts options for Tower data
  // ============================================================================
  readonly ambientTempChartOptions = computed(() =>
    this.buildTowerChart(
      'Ambient Temperature',
      this.towerData(),
      (d) => d.ambientTemp,
      '#ef4444',
      '\u00B0C'
    )
  );

  readonly humidityChartOptions = computed(() =>
    this.buildTowerChart(
      'Humidity',
      this.towerData(),
      (d) => d.humidity,
      '#06b6d4',
      '%',
      { min: 0, max: 100 }
    )
  );

  readonly lightLevelChartOptions = computed(() =>
    this.buildTowerChart(
      'Light Level',
      this.towerData(),
      (d) => d.lightLevel,
      '#eab308',
      'lux'
    )
  );

  // ============================================================================
  // Auto-reload effect (reservoir)
  // ============================================================================
  private readonly reservoirEffect = effect(() => {
    const coordId = this.selectedCoordId();
    const farmId = this.selectedFarmId();
    const minutes = this.timeRange();

    if (!coordId) {
      this.reservoirData.set([]);
      return;
    }

    this.fetchReservoirData(coordId, farmId, minutes);
  });

  // ============================================================================
  // Auto-reload effect (tower)
  // ============================================================================
  private readonly towerEffect = effect(() => {
    const towerId = this.selectedTowerId();
    const farmId = this.selectedFarmId();
    const coordId = this.selectedCoordId();
    const minutes = this.timeRange();

    if (!towerId || !coordId) {
      this.towerData.set([]);
      return;
    }

    this.fetchTowerData(towerId, farmId, coordId, minutes);
  });

  // ============================================================================
  // Subscriptions tracking
  // ============================================================================
  private reservoirSub?: { unsubscribe(): void };
  private towerSub?: { unsubscribe(): void };

  ngOnInit(): void {
    // Load coordinators and nodes from backend, then auto-select first options
    this.dataService.loadCoordinators().then(() => {
      const coords = this.coordinators();
      if (coords.length > 0) {
        // Auto-select farm
        const farmId = coords[0].site_id || 'farm-001';
        this.selectedFarmId.set(farmId);
        // Auto-select first coordinator
        this.selectedCoordId.set(coords[0].coord_id);
      }
    });
    this.dataService.loadNodes();
  }

  ngOnDestroy(): void {
    this.reservoirSub?.unsubscribe();
    this.towerSub?.unsubscribe();
  }

  // ============================================================================
  // Data fetching
  // ============================================================================
  loadData(): void {
    const coordId = this.selectedCoordId();
    const farmId = this.selectedFarmId();
    const towerId = this.selectedTowerId();
    const minutes = this.timeRange();

    if (coordId) {
      this.fetchReservoirData(coordId, farmId, minutes);
    }
    if (towerId && coordId) {
      this.fetchTowerData(towerId, farmId, coordId, minutes);
    }
  }

  private fetchReservoirData(coordId: string, farmId: string, minutes: number): void {
    this.reservoirLoading.set(true);
    this.reservoirError.set(null);

    this.reservoirSub?.unsubscribe();
    this.reservoirSub = this.telemetryService
      .getReservoirHistory(coordId, farmId, minutes)
      .subscribe({
        next: (data) => {
          this.reservoirData.set(data);
          this.reservoirLoading.set(false);
          this.lastFetchTime.set(new Date());
        },
        error: (err) => {
          this.reservoirError.set(err.message || 'Failed to load reservoir data');
          this.reservoirLoading.set(false);
          this.reservoirData.set([]);
        },
      });
  }

  private fetchTowerData(towerId: string, farmId: string, coordId: string, minutes: number): void {
    this.towerLoading.set(true);
    this.towerError.set(null);

    this.towerSub?.unsubscribe();
    this.towerSub = this.telemetryService
      .getTowerHistory(towerId, farmId, coordId, minutes)
      .subscribe({
        next: (data) => {
          this.towerData.set(data);
          this.towerLoading.set(false);
          this.lastFetchTime.set(new Date());
        },
        error: (err) => {
          this.towerError.set(err.message || 'Failed to load tower data');
          this.towerLoading.set(false);
          this.towerData.set([]);
        },
      });
  }

  // ============================================================================
  // Chart builders
  // ============================================================================

  /**
   * Builds an ECharts option for a reservoir telemetry metric.
   */
  private buildReservoirChart(
    title: string,
    data: ReservoirTelemetry[],
    accessor: (d: ReservoirTelemetry) => number,
    color: string,
    unit: string,
    yAxisRange?: { min: number; max: number },
    bands?: { from: number; to: number; label: string; color: string }[]
  ): EChartsOption {
    const timestamps = data.map((d) => this.formatTimestamp(d.timestamp));
    const values = data.map((d) => accessor(d));

    const markAreaData: any[] = (bands ?? []).map((band) => [
      {
        yAxis: band.from,
        itemStyle: { color: band.color },
      },
      {
        yAxis: band.to,
        label: {
          show: true,
          position: 'insideTopLeft',
          formatter: band.label,
          color: 'rgba(255,255,255,0.5)',
          fontSize: 10,
        },
      },
    ]);

    return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(17, 17, 27, 0.92)',
        borderColor: 'rgba(255,255,255,0.08)',
        textStyle: { color: '#e2e8f0', fontSize: 12 },
        formatter: (params: any) => {
          const p = Array.isArray(params) ? params[0] : params;
          return `<strong>${p.axisValueLabel}</strong><br/>${title}: <b>${p.value} ${unit}</b>`;
        },
      },
      grid: {
        top: 28,
        right: 16,
        bottom: 28,
        left: 48,
        containLabel: false,
      },
      xAxis: {
        type: 'category',
        data: timestamps,
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          rotate: 0,
          hideOverlap: true,
        },
        axisLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.15)' } },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        min: yAxisRange?.min,
        max: yAxisRange?.max,
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          formatter: `{value} ${unit}`,
        },
        splitLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.06)' } },
      },
      series: [
        {
          name: title,
          type: 'line',
          data: values,
          smooth: true,
          symbol: 'none',
          lineStyle: { color, width: 2 },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: color.replace(')', ', 0.25)').replace('rgb', 'rgba') || `${color}40` },
                { offset: 1, color: 'rgba(0,0,0,0)' },
              ],
            } as any,
          },
          markArea:
            markAreaData.length > 0
              ? {
                  silent: true,
                  data: markAreaData,
                }
              : undefined,
        },
      ],
      animation: true,
      animationDuration: 600,
    };
  }

  /**
   * Builds an ECharts option for a tower telemetry metric.
   */
  private buildTowerChart(
    title: string,
    data: TowerTelemetry[],
    accessor: (d: TowerTelemetry) => number,
    color: string,
    unit: string,
    yAxisRange?: { min: number; max: number }
  ): EChartsOption {
    const timestamps = data.map((d) => this.formatTimestamp(d.timestamp));
    const values = data.map((d) => accessor(d));

    return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(17, 17, 27, 0.92)',
        borderColor: 'rgba(255,255,255,0.08)',
        textStyle: { color: '#e2e8f0', fontSize: 12 },
        formatter: (params: any) => {
          const p = Array.isArray(params) ? params[0] : params;
          return `<strong>${p.axisValueLabel}</strong><br/>${title}: <b>${p.value} ${unit}</b>`;
        },
      },
      grid: {
        top: 28,
        right: 16,
        bottom: 28,
        left: 48,
        containLabel: false,
      },
      xAxis: {
        type: 'category',
        data: timestamps,
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          rotate: 0,
          hideOverlap: true,
        },
        axisLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.15)' } },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        min: yAxisRange?.min,
        max: yAxisRange?.max,
        axisLabel: {
          color: 'rgba(148, 163, 184, 0.7)',
          fontSize: 10,
          formatter: `{value} ${unit}`,
        },
        splitLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.06)' } },
      },
      series: [
        {
          name: title,
          type: 'line',
          data: values,
          smooth: true,
          symbol: 'none',
          lineStyle: { color, width: 2 },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: `${color}40` },
                { offset: 1, color: 'rgba(0,0,0,0)' },
              ],
            } as any,
          },
        },
      ],
      animation: true,
      animationDuration: 600,
    };
  }

  // ============================================================================
  // Helpers
  // ============================================================================

  /**
   * Formats a Date or ISO string into a short time label for chart axes.
   */
  private formatTimestamp(ts: Date | string): string {
    const date = typeof ts === 'string' ? new Date(ts) : ts;
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    return `${hours}:${minutes}`;
  }

  /**
   * Template helper: format the last fetch time for display.
   */
  formatLastFetchTime(): string {
    const t = this.lastFetchTime();
    if (!t) return 'Never';
    return t.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  }

  /**
   * Template helper: get the label for the current time range.
   */
  getTimeRangeLabel(): string {
    const val = this.timeRange();
    return this.timeRangeOptions.find((o) => o.value === val)?.label ?? `${val} min`;
  }

  /**
   * Called when the time range <select> changes.
   */
  onTimeRangeChange(value: string): void {
    this.timeRange.set(Number(value));
  }

  /**
   * Called when a dropdown selection changes.
   */
  onFarmIdChange(value: string): void {
    this.selectedFarmId.set(value);
  }

  onCoordIdChange(value: string): void {
    this.selectedCoordId.set(value);
    // Reset tower when coordinator changes
    this.selectedTowerId.set('');
    // Auto-set farm from the selected coordinator
    const coord = this.coordinators().find(c => c.coord_id === value);
    if (coord?.site_id) {
      this.selectedFarmId.set(coord.site_id);
    }
  }

  onTowerIdChange(value: string): void {
    this.selectedTowerId.set(value);
  }
}
