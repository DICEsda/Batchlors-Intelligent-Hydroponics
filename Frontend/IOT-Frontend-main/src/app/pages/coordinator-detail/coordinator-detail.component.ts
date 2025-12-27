import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { HydroponicDataService, WebSocketService } from '../../core/services';
import { Coordinator, TowerSummary, ReservoirTelemetry } from '../../core/models';
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
  lucideDroplet,
  lucideThermometer,
  lucideActivity,
  lucideGauge,
  lucideArrowLeft,
  lucideRefreshCw,
  lucideSettings,
  lucideFlower2,
  lucideChevronRight,
  lucideAlertTriangle,
  lucideWifi,
  lucideWifiOff,
  lucideClock,
  lucideZap
} from '@ng-icons/lucide';

@Component({
  selector: 'app-coordinator-detail',
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
      lucideDroplet,
      lucideThermometer,
      lucideActivity,
      lucideGauge,
      lucideArrowLeft,
      lucideRefreshCw,
      lucideSettings,
      lucideFlower2,
      lucideChevronRight,
      lucideAlertTriangle,
      lucideWifi,
      lucideWifiOff,
      lucideClock,
      lucideZap
    })
  ],
  templateUrl: './coordinator-detail.component.html',
  styleUrl: './coordinator-detail.component.scss'
})
export class CoordinatorDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly dataService = inject(HydroponicDataService);
  private readonly wsService = inject(WebSocketService);
  private subscriptions: Subscription[] = [];

  // State
  readonly coordinatorId = signal<string>('');
  readonly coordinator = this.dataService.selectedCoordinator;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly wsConnected = this.wsService.connected;

  // Live telemetry from WebSocket
  readonly liveTelemetry = signal<ReservoirTelemetry | null>(null);

  // Computed values for reservoir metrics
  readonly reservoirMetrics = computed(() => {
    const coord = this.coordinator();
    const live = this.liveTelemetry();
    
    // Prefer live telemetry if available, fall back to stored data
    if (live) {
      return {
        ph: live.ph,
        ec: live.ec,
        temperature: live.temperature,
        waterLevel: live.waterLevel,
        lastUpdate: new Date(live.timestamp)
      };
    }
    
    if (coord?.reservoir) {
      return {
        ph: coord.reservoir.ph,
        ec: coord.reservoir.ec,
        temperature: coord.reservoir.temperature,
        waterLevel: coord.reservoir.waterLevel,
        lastUpdate: coord.reservoir.lastUpdated ? new Date(coord.reservoir.lastUpdated) : null
      };
    }
    
    return null;
  });

  // pH status and color
  readonly phStatus = computed(() => {
    const metrics = this.reservoirMetrics();
    if (!metrics) return { status: 'unknown', color: 'neutral' };
    
    const ph = metrics.ph;
    if (ph >= 5.5 && ph <= 6.5) return { status: 'optimal', color: 'green' };
    if (ph >= 5.0 && ph <= 7.0) return { status: 'acceptable', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // EC status and color
  readonly ecStatus = computed(() => {
    const metrics = this.reservoirMetrics();
    if (!metrics) return { status: 'unknown', color: 'neutral' };
    
    const ec = metrics.ec;
    if (ec >= 1.2 && ec <= 2.0) return { status: 'optimal', color: 'green' };
    if (ec >= 0.8 && ec <= 2.5) return { status: 'acceptable', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // Temperature status
  readonly tempStatus = computed(() => {
    const metrics = this.reservoirMetrics();
    if (!metrics) return { status: 'unknown', color: 'neutral' };
    
    const temp = metrics.temperature;
    if (temp >= 18 && temp <= 24) return { status: 'optimal', color: 'green' };
    if (temp >= 15 && temp <= 28) return { status: 'acceptable', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // Water level status
  readonly levelStatus = computed(() => {
    const metrics = this.reservoirMetrics();
    if (!metrics) return { status: 'unknown', color: 'neutral' };
    
    const level = metrics.waterLevel;
    if (level >= 50) return { status: 'good', color: 'green' };
    if (level >= 25) return { status: 'low', color: 'yellow' };
    return { status: 'critical', color: 'red' };
  });

  // Connected towers - filtered from towers signal by coordId
  readonly connectedTowers = computed(() => {
    const coord = this.coordinator();
    if (!coord) return [];
    const allTowers = this.dataService.towers();
    return allTowers.filter(t => t.coordId === coord.coordId);
  });

  ngOnInit(): void {
    // Get coordinator ID from route
    const sub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.coordinatorId.set(id);
        this.loadCoordinatorData(id);
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
    this.dataService.clearSelectedCoordinator();
    
    // Unsubscribe from coordinator updates
    const id = this.coordinatorId();
    if (id) {
      this.wsService.unsubscribe('coordinator', id);
    }
  }

  private loadCoordinatorData(id: string): void {
    this.dataService.loadCoordinatorById(id);
  }

  private subscribeToRealTimeUpdates(id: string): void {
    // Subscribe to coordinator WebSocket updates
    this.wsService.subscribeToCoordinator(id);
    
    // Listen for reservoir telemetry updates
    const telemetrySub = this.wsService.reservoirTelemetry$.subscribe(telemetry => {
      if (telemetry.coordId === id) {
        this.liveTelemetry.set(telemetry);
      }
    });
    this.subscriptions.push(telemetrySub);
  }

  refreshData(): void {
    const id = this.coordinatorId();
    if (id) {
      this.loadCoordinatorData(id);
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

  trackByTowerId(_: number, tower: TowerSummary): string {
    return tower._id;
  }
}
