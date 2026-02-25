import { Component, OnInit, OnDestroy, inject, computed, signal, ChangeDetectorRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { IoTDataService, WebSocketService, TwinService } from '../../core/services';
import { CoordinatorSummary, NodeSummary, Alert, CoordinatorTwin, TowerTwin, ReservoirTelemetry } from '../../core/models';
import { TelemetryChartComponent, TelemetryPoint } from '../../components/ui/telemetry-chart/telemetry-chart.component';

import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import { provideIcons, NgIcon } from '@ng-icons/core';
import {
  lucideDroplet,
  lucideThermometer,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideAlertTriangle,
  lucideRefreshCw,
  lucidePlus,
  lucideChevronRight,
  lucideFlower2,
  lucideGauge,
  lucideServer,
  lucideSun,
  lucideBattery,
  lucideLayoutGrid,
  lucideMapPin,
  lucideInfo,
  lucideDatabase,
  lucideRadio,
  lucideGitBranch,
  lucideBox,
  lucideCircleDot,
  lucideCloudCog,
  lucideLeaf,
  lucideZap,
  lucideSignal,
  lucideClock,
  lucideArrowUpDown,
  lucideBeaker,
  lucideX,
  lucideBarChart3,
} from '@ng-icons/lucide';

@Component({
  selector: 'app-farm-overview',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmSkeletonComponent,
    NgIcon,
    TelemetryChartComponent,
  ],
  providers: [
    provideIcons({
      lucideDroplet,
      lucideThermometer,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideAlertTriangle,
      lucideRefreshCw,
      lucidePlus,
      lucideChevronRight,
      lucideFlower2,
      lucideGauge,
      lucideServer,
      lucideSun,
      lucideBattery,
      lucideLayoutGrid,
      lucideMapPin,
      lucideInfo,
      lucideDatabase,
      lucideRadio,
      lucideGitBranch,
      lucideBox,
      lucideCircleDot,
      lucideCloudCog,
      lucideLeaf,
      lucideZap,
      lucideSignal,
      lucideClock,
      lucideArrowUpDown,
      lucideBeaker,
      lucideX,
      lucideBarChart3,
    })
  ],
  templateUrl: './farm-overview.component.html',
  styleUrl: './farm-overview.component.scss'
})
export class FarmOverviewComponent implements OnInit, OnDestroy {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);
  private readonly twinService = inject(TwinService);
  private readonly cdr = inject(ChangeDetectorRef);

  // Data from IoT service
  readonly sites = this.dataService.sites;
  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;
  readonly zones = this.dataService.zones;
  readonly activeAlerts = this.dataService.activeAlerts;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;

  // Digital Twin data
  readonly coordTwins = this.twinService.coordTwins;
  readonly towerTwins = this.twinService.towerTwins;
  readonly twinLoading = this.twinService.isLoading;

  // Computed metrics
  readonly totalSites = computed(() => this.sites().length);
  readonly onlineCoordinators = this.dataService.onlineCoordinatorCount;
  readonly totalCoordinators = computed(() => this.coordinators().length);
  readonly onlineNodes = this.dataService.onlineNodeCount;
  readonly totalNodes = computed(() => this.nodes().length);
  readonly pairingNodes = this.dataService.pairingNodeCount;
  readonly errorNodes = this.dataService.errorNodeCount;
  readonly lowBatteryNodes = this.dataService.lowBatteryNodeCount;
  readonly criticalAlerts = computed(() =>
    this.activeAlerts().filter(a => a.severity === 'critical').length
  );
  readonly wsConnected = this.wsService.connected;

  readonly systemStats = computed(() => ({
    sites: { total: this.totalSites() },
    coordinators: { online: this.onlineCoordinators(), total: this.totalCoordinators() },
    nodes: { online: this.onlineNodes(), total: this.totalNodes(), pairing: this.pairingNodes(), error: this.errorNodes(), lowBattery: this.lowBatteryNodes() },
    alerts: { critical: this.criticalAlerts(), total: this.activeAlerts().length }
  }));
  readonly farmStats = this.systemStats;

  // ---- Selection State ----
  readonly selectedType = signal<'coordinator' | 'tower' | null>(null);
  readonly selectedId = signal<string | null>(null);

  /** Grouped view: coordinators → their towers */
  readonly twinsByCoordinator = computed(() => {
    const coords = this.coordTwins();
    const towers = this.towerTwins();
    return coords.map(c => ({
      coordinator: c,
      towers: towers.filter(t => t.coordId === c.coordId),
    }));
  });

  /** The selected coordinator twin (if type === 'coordinator') */
  readonly selectedCoordTwin = computed<CoordinatorTwin | null>(() => {
    if (this.selectedType() !== 'coordinator') return null;
    return this.coordTwins().find(c => c.coordId === this.selectedId()) ?? null;
  });

  /** The selected tower twin (if type === 'tower') */
  readonly selectedTowerTwin = computed<TowerTwin | null>(() => {
    if (this.selectedType() !== 'tower') return null;
    return this.towerTwins().find(t => t.towerId === this.selectedId()) ?? null;
  });

  /** Towers belonging to selected coordinator */
  readonly selectedCoordTowers = computed<TowerTwin[]>(() => {
    const coord = this.selectedCoordTwin();
    if (!coord) return [];
    return this.towerTwins().filter(t => t.coordId === coord.coordId);
  });

  /** Does the detail panel have something to show? */
  readonly hasSelection = computed(() => this.selectedType() !== null && this.selectedId() !== null);

  // ---- System Health Sparkline Charts ----
  @ViewChild('avgPhChart') avgPhChart?: TelemetryChartComponent;
  @ViewChild('avgEcChart') avgEcChart?: TelemetryChartComponent;
  @ViewChild('avgWaterLevelChart') avgWaterLevelChart?: TelemetryChartComponent;

  private telemetrySubs: Subscription[] = [];

  // Running averages for aggregation
  private phAccum: { sum: number; count: number } = { sum: 0, count: 0 };
  private ecAccum: { sum: number; count: number } = { sum: 0, count: 0 };
  private waterLevelAccum: { sum: number; count: number } = { sum: 0, count: 0 };
  private lastAggTimestamp = 0;
  private readonly AGG_INTERVAL_MS = 5000; // aggregate every 5s

  // ---- Crop Type Mapping ----
  private readonly cropMap: Record<number | string, string> = {
    1: 'Lettuce', 2: 'Spinach', 3: 'Kale', 4: 'Arugula',
    5: 'Chard', 6: 'Mint', 7: 'Basil', 8: 'Cilantro',
  };

  getCropName(cropType: string | number | null | undefined): string {
    if (cropType == null) return 'Unknown';
    const mapped = this.cropMap[cropType];
    if (mapped) return mapped;
    // If it's a string name, capitalize first letter
    if (typeof cropType === 'string') {
      return cropType.charAt(0).toUpperCase() + cropType.slice(1);
    }
    return String(cropType);
  }

  // ---- Selection Actions ----
  selectNode(type: 'coordinator' | 'tower', id: string): void {
    // Toggle off if already selected
    if (this.selectedType() === type && this.selectedId() === id) {
      this.clearSelection();
      return;
    }
    this.selectedType.set(type);
    this.selectedId.set(id);
    this.cdr.detectChanges();
  }

  clearSelection(): void {
    this.selectedType.set(null);
    this.selectedId.set(null);
    this.cdr.detectChanges();
  }

  isNodeSelected(type: 'coordinator' | 'tower', id: string): boolean {
    return this.selectedType() === type && this.selectedId() === id;
  }

  // ---- Lifecycle ----
  ngOnInit(): void {
    this.dataService.loadDashboardData();

    if (!this.wsService.connected()) {
      this.wsService.connect();
    }

    this.dataService.startAutoRefresh();

    // Load digital twin data
    this.twinService.loadFarmTwins('farm-alpha');

    // Subscribe to reservoir telemetry for system health sparklines
    this.subscribeToReservoirTelemetry();
  }

  ngOnDestroy(): void {
    this.dataService.stopAutoRefresh();
    this.telemetrySubs.forEach(s => s.unsubscribe());
  }

  /**
   * Subscribe to reservoir telemetry for aggregated sparkline charts.
   * Accumulates readings across all coordinators and emits running averages.
   */
  private subscribeToReservoirTelemetry(): void {
    const sub = this.wsService.reservoirTelemetry$.subscribe((t: ReservoirTelemetry) => {
      // Accumulate values
      if (t.ph != null) { this.phAccum.sum += t.ph; this.phAccum.count++; }
      if (t.ec != null) { this.ecAccum.sum += t.ec; this.ecAccum.count++; }
      if (t.waterLevel != null) { this.waterLevelAccum.sum += t.waterLevel; this.waterLevelAccum.count++; }

      // Emit aggregated point every AGG_INTERVAL_MS
      const now = Date.now();
      if (now - this.lastAggTimestamp >= this.AGG_INTERVAL_MS) {
        this.lastAggTimestamp = now;
        const time = new Date(now);

        if (this.phAccum.count > 0) {
          this.avgPhChart?.appendPoint({ time, value: +(this.phAccum.sum / this.phAccum.count).toFixed(2) });
          this.phAccum = { sum: 0, count: 0 };
        }
        if (this.ecAccum.count > 0) {
          this.avgEcChart?.appendPoint({ time, value: +(this.ecAccum.sum / this.ecAccum.count).toFixed(2) });
          this.ecAccum = { sum: 0, count: 0 };
        }
        if (this.waterLevelAccum.count > 0) {
          this.avgWaterLevelChart?.appendPoint({ time, value: +(this.waterLevelAccum.sum / this.waterLevelAccum.count).toFixed(1) });
          this.waterLevelAccum = { sum: 0, count: 0 };
        }
      }
    });
    this.telemetrySubs.push(sub);
  }

  refreshData(): void {
    this.dataService.loadDashboardData();
    this.twinService.loadFarmTwins('farm-alpha');
  }

  // ---- Template helpers ----
  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'online': case 'operational': return 'default';
      case 'offline': case 'error': return 'destructive';
      case 'warning': case 'pairing': case 'ota': return 'secondary';
      default: return 'outline';
    }
  }

  getAlertBadgeVariant(severity: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (severity) {
      case 'critical': return 'destructive';
      case 'warning': return 'secondary';
      case 'info': return 'outline';
      default: return 'default';
    }
  }

  getNodeStatusDisplay(statusMode: string): string {
    switch (statusMode) {
      case 'operational': return 'Online';
      case 'pairing': return 'Pairing';
      case 'ota': return 'Updating';
      case 'error': return 'Error';
      default: return statusMode;
    }
  }

  getSyncStatusClass(status: string | undefined): string {
    if (!status) return '';
    switch (status) {
      case 'in_sync': case 'insync': return 'sync-ok';
      case 'pending': return 'sync-pending';
      case 'stale': case 'conflict': return 'sync-warn';
      case 'offline': return 'sync-off';
      default: return '';
    }
  }

  getSyncStatusLabel(status: string | undefined): string {
    if (!status) return 'Unknown';
    switch (status) {
      case 'in_sync': case 'insync': return 'In Sync';
      case 'pending': return 'Pending';
      case 'stale': return 'Stale';
      case 'conflict': return 'Conflict';
      case 'offline': return 'Offline';
      default: return status;
    }
  }

  formatHealthScore(score: number | null | undefined): string {
    if (score == null) return '--';
    // If value is between 0-1, treat as fraction and multiply by 100
    if (score > 0 && score <= 1) return Math.round(score * 100).toString();
    return Math.round(score).toString();
  }

  getBatteryIcon(vbatMv: number | undefined): string {
    if (!vbatMv) return 'lucideBattery';
    return 'lucideBattery';
  }

  formatTimestamp(timestamp: Date | string): string {
    const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
    return date.toLocaleString();
  }

  formatLastSeen(lastSeen: Date | string | undefined): string {
    if (!lastSeen) return 'Never';
    const date = typeof lastSeen === 'string' ? new Date(lastSeen) : lastSeen;
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);
    if (diffSecs < 60) return `${diffSecs}s ago`;
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    return date.toLocaleDateString();
  }

  formatUptime(seconds: number | null | undefined): string {
    if (!seconds) return '--';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (h >= 24) return `${Math.floor(h / 24)}d ${h % 24}h`;
    return `${h}h ${m}m`;
  }

  trackByCoordinatorId(_: number, coord: CoordinatorSummary): string { return coord._id; }
  trackByNodeId(_: number, node: NodeSummary): string { return node._id; }
  trackByAlertId(_: number, alert: Alert): string { return alert._id; }
}
