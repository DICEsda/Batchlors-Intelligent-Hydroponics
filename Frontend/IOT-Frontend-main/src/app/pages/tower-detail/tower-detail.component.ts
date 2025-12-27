import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { HydroponicDataService, WebSocketService } from '../../core/services';
import { Tower, PlantSlot, TowerTelemetry, LedSchedule } from '../../core/models';
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
import { provideIcons, NgIcon } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideRefreshCw,
  lucideSettings,
  lucideFlower2,
  lucideSun,
  lucideDroplet,
  lucideThermometer,
  lucideWind,
  lucideAlertTriangle,
  lucideWifi,
  lucideWifiOff,
  lucideClock,
  lucideZap,
  lucideSprout,
  lucideCalendar,
  lucideTrendingUp,
  lucideActivity,
  lucidePower,
  lucideSunrise,
  lucideSunset,
  lucideGrid3x3,
  lucidePlug
} from '@ng-icons/lucide';

@Component({
  selector: 'app-tower-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmSkeletonComponent,
    NgIcon
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideRefreshCw,
      lucideSettings,
      lucideFlower2,
      lucideSun,
      lucideDroplet,
      lucideThermometer,
      lucideWind,
      lucideAlertTriangle,
      lucideWifi,
      lucideWifiOff,
      lucideClock,
      lucideZap,
      lucideSprout,
      lucideCalendar,
      lucideTrendingUp,
      lucideActivity,
      lucidePower,
      lucideSunrise,
      lucideSunset,
      lucideGrid3x3,
      lucidePlug
    })
  ],
  templateUrl: './tower-detail.component.html',
  styleUrl: './tower-detail.component.scss'
})
export class TowerDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly dataService = inject(HydroponicDataService);
  private readonly wsService = inject(WebSocketService);
  private subscriptions: Subscription[] = [];

  // State
  readonly towerId = signal<string>('');
  readonly tower = this.dataService.selectedTower;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly wsConnected = this.wsService.connected;

  // Live telemetry from WebSocket
  readonly liveTelemetry = signal<TowerTelemetry | null>(null);

  // Computed values for sensors
  readonly sensorData = computed(() => {
    const t = this.tower();
    const live = this.liveTelemetry();

    // Prefer live telemetry if available
    if (live) {
      return {
        ambientTemp: live.ambientTemp,
        humidity: live.humidity,
        lightLevel: live.lightLevel,
        lastUpdated: new Date(live.timestamp)
      };
    }

    if (t?.sensors) {
      return {
        ambientTemp: t.sensors.ambientTemp,
        humidity: t.sensors.humidity,
        lightLevel: t.sensors.lightLevel,
        lastUpdated: t.sensors.lastUpdated ? new Date(t.sensors.lastUpdated) : null
      };
    }

    return null;
  });

  // Temperature status
  readonly tempStatus = computed(() => {
    const data = this.sensorData();
    if (!data) return { status: 'unknown', color: 'neutral' };

    const temp = data.ambientTemp;
    if (temp >= 20 && temp <= 26) return { status: 'optimal', color: 'green' };
    if (temp >= 16 && temp <= 30) return { status: 'acceptable', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // Humidity status
  readonly humidityStatus = computed(() => {
    const data = this.sensorData();
    if (!data) return { status: 'unknown', color: 'neutral' };

    const humidity = data.humidity;
    if (humidity >= 50 && humidity <= 70) return { status: 'optimal', color: 'green' };
    if (humidity >= 40 && humidity <= 80) return { status: 'acceptable', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // Light level status
  readonly lightStatus = computed(() => {
    const data = this.sensorData();
    if (!data) return { status: 'unknown', color: 'neutral' };

    const lux = data.lightLevel;
    if (lux >= 10000 && lux <= 25000) return { status: 'optimal', color: 'green' };
    if (lux >= 5000 && lux <= 35000) return { status: 'acceptable', color: 'yellow' };
    return { status: 'low', color: 'red' };
  });

  // Plant slots
  readonly plantSlots = computed(() => {
    const t = this.tower();
    return t?.plants || [];
  });

  // Slot statistics
  readonly slotStats = computed(() => {
    const slots = this.plantSlots();
    const total = slots.length;
    const occupied = slots.filter(s => !s.isEmpty).length;
    const empty = total - occupied;
    const avgHealth = slots.filter(s => !s.isEmpty && s.metrics)
      .reduce((sum, s) => sum + (s.metrics?.healthScore || 0), 0) / (occupied || 1);

    return { total, occupied, empty, avgHealth };
  });

  // LED schedule
  readonly ledSchedule = computed(() => {
    const t = this.tower();
    return t?.ledSchedule || null;
  });

  // LED status
  readonly ledActive = computed(() => {
    const t = this.tower();
    return t?.activeLeds || false;
  });

  ngOnInit(): void {
    // Get tower ID from route
    const sub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.towerId.set(id);
        this.loadTowerData(id);
        this.subscribeToRealTimeUpdates(id);
      }
    });
    this.subscriptions.push(sub);

    // Connect WebSocket if not connected
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.dataService.selectedTower.set(null);

    // Unsubscribe from tower updates
    const id = this.towerId();
    if (id) {
      this.wsService.unsubscribe('tower', id);
    }
  }

  private async loadTowerData(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      await this.dataService.loadTower(id);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load tower');
    } finally {
      this.loading.set(false);
    }
  }

  private subscribeToRealTimeUpdates(id: string): void {
    // Subscribe to tower WebSocket updates
    this.wsService.subscribeToTower(id);

    // Listen for tower telemetry updates
    const telemetrySub = this.wsService.towerTelemetry$.subscribe(telemetry => {
      if (telemetry.towerId === id) {
        this.liveTelemetry.set(telemetry);
      }
    });
    this.subscriptions.push(telemetrySub);
  }

  refreshData(): void {
    const id = this.towerId();
    if (id) {
      this.loadTowerData(id);
    }
  }

  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'online': return 'default';
      case 'offline': return 'destructive';
      case 'warning': return 'secondary';
      default: return 'outline';
    }
  }

  getHealthColor(score: number): string {
    if (score >= 80) return 'green';
    if (score >= 50) return 'yellow';
    return 'red';
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

  formatDaysAgo(date: Date | string | null): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    const days = Math.floor((Date.now() - d.getTime()) / (1000 * 60 * 60 * 24));
    return `${days} days`;
  }

  trackBySlotIndex(_: number, slot: PlantSlot): number {
    return slot.slotIndex;
  }
}
