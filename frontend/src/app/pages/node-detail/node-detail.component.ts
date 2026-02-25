import { Component, OnInit, OnDestroy, inject, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription, filter } from 'rxjs';
import { IoTDataService, WebSocketService, ApiService } from '../../core/services';
import {
  Node,
  NodeTelemetry,
  Zone,
  Coordinator,
  LedColor,
  getBatteryPercent,
  getBatteryStatus,
  getSignalStrength,
  TimeRange,
  TowerTelemetry,
} from '../../core/models';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
  HlmCardContentDirective
} from '../../components/ui/card';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import { HlmLabelDirective } from '../../components/ui/label';
import { TelemetryChartComponent } from '../../components/ui/telemetry-chart/telemetry-chart.component';
import { provideIcons, NgIcon } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideRefreshCw,
  lucideSettings,
  lucideSun,
  lucideThermometer,
  lucideAlertTriangle,
  lucideWifi,
  lucideWifiOff,
  lucideClock,
  lucideZap,
  lucideBattery,
  lucideBatteryLow,
  lucideBatteryWarning,
  lucideLightbulb,
  lucideLightbulbOff,
  lucidePalette,
  lucideSliders,
  lucidePower,
  lucideServer,
  lucideMapPin,
  lucideCpu,
  lucideSignal,
  lucideActivity,
  lucideBarChart3,
} from '@ng-icons/lucide';

@Component({
  selector: 'app-node-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmSkeletonComponent,
    HlmLabelDirective,
    NgIcon,
    TelemetryChartComponent,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideRefreshCw,
      lucideSettings,
      lucideSun,
      lucideThermometer,
      lucideAlertTriangle,
      lucideWifi,
      lucideWifiOff,
      lucideClock,
      lucideZap,
      lucideBattery,
      lucideBatteryLow,
      lucideBatteryWarning,
      lucideLightbulb,
      lucideLightbulbOff,
      lucidePalette,
      lucideSliders,
      lucidePower,
      lucideServer,
      lucideMapPin,
      lucideCpu,
      lucideSignal,
      lucideActivity,
      lucideBarChart3,
    })
  ],
  templateUrl: './node-detail.component.html',
  styleUrl: './node-detail.component.scss'
})
export class NodeDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);
  private readonly api = inject(ApiService);
  private subscriptions: Subscription[] = [];

  // State
  readonly nodeId = signal<string>('');
  readonly node = this.dataService.selectedNode;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly wsConnected = this.wsService.connected;

  // Live telemetry
  readonly liveTelemetry = signal<NodeTelemetry | null>(null);

  // LED Control State
  readonly ledControlLoading = signal(false);
  readonly testColor = signal<LedColor>({ r: 255, g: 255, b: 255, w: 0 });
  readonly brightness = signal<number>(100);
  readonly ledOn = signal(true);

  // Color presets for quick testing
  readonly colorPresets: { name: string; color: LedColor }[] = [
    { name: 'White', color: { r: 255, g: 255, b: 255, w: 255 } },
    { name: 'Warm White', color: { r: 255, g: 200, b: 150, w: 200 } },
    { name: 'Cool White', color: { r: 200, g: 220, b: 255, w: 255 } },
    { name: 'Red', color: { r: 255, g: 0, b: 0, w: 0 } },
    { name: 'Green', color: { r: 0, g: 255, b: 0, w: 0 } },
    { name: 'Blue', color: { r: 0, g: 0, b: 255, w: 0 } },
    { name: 'Purple', color: { r: 128, g: 0, b: 255, w: 0 } },
    { name: 'Orange', color: { r: 255, g: 128, b: 0, w: 0 } },
  ];

  // Computed node status
  readonly nodeStatus = computed(() => {
    const n = this.node();
    if (!n) return 'offline';
    return n.status_mode;
  });

  // Battery computed values
  readonly batteryPercent = computed(() => {
    const n = this.node();
    const live = this.liveTelemetry();
    const vbat = live?.vbat_mv ?? n?.vbat_mv ?? 0;
    return getBatteryPercent(vbat);
  });

  readonly batteryStatus = computed(() => {
    const n = this.node();
    const live = this.liveTelemetry();
    const vbat = live?.vbat_mv ?? n?.vbat_mv ?? 0;
    return getBatteryStatus(vbat);
  });

  // Signal strength
  readonly signalStrength = computed(() => {
    const n = this.node();
    const live = this.liveTelemetry();
    const rssi = live?.avg_r ?? n?.avg_r ?? -100;
    return getSignalStrength(rssi);
  });

  // Temperature
  readonly temperature = computed(() => {
    const n = this.node();
    const live = this.liveTelemetry();
    return live?.temp_c ?? n?.temp_c ?? null;
  });

  // RSSI value
  readonly rssi = computed(() => {
    const n = this.node();
    const live = this.liveTelemetry();
    return live?.avg_r ?? n?.avg_r ?? null;
  });

  // Get parent coordinator
  readonly parentCoordinator = computed(() => {
    const n = this.node();
    if (!n) return null;
    const coords = this.dataService.coordinators();
    return coords.find(c => c.coord_id === n.coordinator_id || c._id === n.coordinator_id) || null;
  });

  // ============================================================================
  // Telemetry Charts
  // ============================================================================
  @ViewChild('tempChart') tempChart?: TelemetryChartComponent;
  @ViewChild('humidityChart') humidityChart?: TelemetryChartComponent;
  @ViewChild('lightChart') lightChart?: TelemetryChartComponent;
  @ViewChild('batteryChart') batteryChart?: TelemetryChartComponent;

  // Get assigned zone
  readonly assignedZone = computed(() => {
    const n = this.node();
    if (!n?.zone_id) return null;
    const zones = this.dataService.zones();
    return zones.find(z => z._id === n.zone_id) || null;
  });

  ngOnInit(): void {
    // Get node ID from route
    const sub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.nodeId.set(id);
        this.loadNodeData(id);
        this.subscribeToRealTimeUpdates(id);
      }
    });
    this.subscriptions.push(sub);

    // Connect WebSocket if not connected
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }

    // Load zones if not already loaded
    if (this.dataService.zones().length === 0) {
      this.dataService.loadZones();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.dataService.selectedNode.set(null);

    const id = this.nodeId();
    if (id) {
      this.wsService.unsubscribe('node', id);
    }
  }

  private async loadNodeData(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      await this.dataService.loadNode(id);
      // Also load coordinators for parent info
      await this.dataService.loadCoordinators();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load node');
    } finally {
      this.loading.set(false);
    }
  }

  private subscribeToRealTimeUpdates(id: string): void {
    // Subscribe to node/tower WebSocket updates (backend uses "tower" terminology)
    this.wsService.subscribeToTower(id);

    // Listen for tower telemetry updates and map to node telemetry
    const telemetrySub = this.wsService.towerTelemetry$.subscribe((telemetry: any) => {
      // Check if this telemetry is for our node (by towerId or node_id)
      if (telemetry.towerId === id || telemetry.node_id === id || telemetry.light_id === this.node()?.light_id) {
        // Map tower telemetry to node telemetry format
        this.liveTelemetry.set({
          node_id: telemetry.towerId || telemetry.node_id || id,
          light_id: this.node()?.light_id || '',
          timestamp: new Date(telemetry.timestamp || Date.now()),
          temp_c: telemetry.temp_c ?? telemetry.temperature ?? 0,
          vbat_mv: telemetry.vbat_mv ?? telemetry.batteryMv ?? 0,
          avg_r: telemetry.avg_r ?? telemetry.rssi ?? -50,
          status_mode: telemetry.status_mode ?? 'operational',
        });

        // Update telemetry charts
        const now = new Date(telemetry.timestamp || Date.now());
        const temp = telemetry.ambientTemp ?? telemetry.temp_c ?? telemetry.temperature;
        const humidity = telemetry.humidity;
        const light = telemetry.lightLevel;
        const battery = telemetry.batteryVoltage ?? telemetry.vbat_mv ?? telemetry.batteryMv;

        if (temp != null) this.tempChart?.appendPoint({ time: now, value: temp });
        if (humidity != null) this.humidityChart?.appendPoint({ time: now, value: humidity });
        if (light != null) this.lightChart?.appendPoint({ time: now, value: light });
        if (battery != null) this.batteryChart?.appendPoint({ time: now, value: battery });
      }
    });
    this.subscriptions.push(telemetrySub);
  }

  refreshData(): void {
    const id = this.nodeId();
    if (id) {
      this.loadNodeData(id);
    }
  }

  // ============================================================================
  // LED Control Methods
  // ============================================================================

  selectColorPreset(preset: { name: string; color: LedColor }): void {
    this.testColor.set({ ...preset.color });
  }

  async testLedColor(): Promise<void> {
    const n = this.node();
    if (!n) return;

    this.ledControlLoading.set(true);
    try {
      await this.api.testNodeColor({
        node_id: n.light_id,
        color: this.testColor(),
        duration_ms: 5000, // 5 second test
      }).toPromise();
      this.ledOn.set(true);
    } catch (err) {
      console.error('Failed to test LED color:', err);
      this.error.set('Failed to test LED color');
    } finally {
      this.ledControlLoading.set(false);
    }
  }

  async setLedBrightness(): Promise<void> {
    const n = this.node();
    if (!n) return;

    this.ledControlLoading.set(true);
    try {
      await this.api.setNodeBrightness({
        node_id: n.light_id,
        brightness: this.brightness(),
      }).toPromise();
    } catch (err) {
      console.error('Failed to set brightness:', err);
      this.error.set('Failed to set brightness');
    } finally {
      this.ledControlLoading.set(false);
    }
  }

  async turnOffLed(): Promise<void> {
    const n = this.node();
    if (!n) return;

    this.ledControlLoading.set(true);
    try {
      await this.api.turnOffNode(n.light_id).toPromise();
      this.ledOn.set(false);
    } catch (err) {
      console.error('Failed to turn off LED:', err);
      this.error.set('Failed to turn off LED');
    } finally {
      this.ledControlLoading.set(false);
    }
  }

  async turnOnLed(): Promise<void> {
    const n = this.node();
    if (!n) return;

    this.ledControlLoading.set(true);
    try {
      await this.api.controlNodeLight({
        node_id: n.light_id,
        color: this.testColor(),
        brightness: this.brightness(),
      }).toPromise();
      this.ledOn.set(true);
    } catch (err) {
      console.error('Failed to turn on LED:', err);
      this.error.set('Failed to turn on LED');
    } finally {
      this.ledControlLoading.set(false);
    }
  }

  // ============================================================================
  // UI Helper Methods
  // ============================================================================

  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'operational':
        return 'default';
      case 'pairing':
        return 'secondary';
      case 'error':
        return 'destructive';
      case 'ota':
        return 'outline';
      default:
        return 'outline';
    }
  }

  getBatteryIcon(): string {
    const status = this.batteryStatus();
    switch (status) {
      case 'good': return 'lucideBattery';
      case 'low': return 'lucideBatteryLow';
      case 'critical': return 'lucideBatteryWarning';
      default: return 'lucideBattery';
    }
  }

  getSignalIcon(): string {
    return this.signalStrength() === 'poor' ? 'lucideWifiOff' : 'lucideWifi';
  }

  formatTimestamp(date: Date | string | null): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleString();
  }

  formatTimeAgo(date: Date | string | null): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    const seconds = Math.floor((Date.now() - d.getTime()) / 1000);

    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    return d.toLocaleDateString();
  }

  getColorStyle(color: LedColor): string {
    return `rgb(${color.r}, ${color.g}, ${color.b})`;
  }

  updateColorR(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.testColor.update(c => ({ ...c, r: value }));
  }

  updateColorG(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.testColor.update(c => ({ ...c, g: value }));
  }

  updateColorB(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.testColor.update(c => ({ ...c, b: value }));
  }

  updateColorW(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.testColor.update(c => ({ ...c, w: value }));
  }

  updateBrightness(event: Event): void {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    this.brightness.set(value);
  }
}
