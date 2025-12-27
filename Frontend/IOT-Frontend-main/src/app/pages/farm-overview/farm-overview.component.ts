import { Component, OnInit, OnDestroy, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HydroponicDataService, WebSocketService } from '../../core/services';
import { CoordinatorSummary, TowerSummary, Alert } from '../../core/models';

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
  lucideServer
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
    NgIcon
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
      lucideServer
    })
  ],
  templateUrl: './farm-overview.component.html',
  styleUrl: './farm-overview.component.scss'
})
export class FarmOverviewComponent implements OnInit, OnDestroy {
  private readonly dataService = inject(HydroponicDataService);
  private readonly wsService = inject(WebSocketService);

  // Expose data from service
  readonly coordinators = this.dataService.coordinators;
  readonly towers = this.dataService.towers;
  readonly activeAlerts = this.dataService.activeAlerts;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;

  // Computed metrics
  readonly onlineCoordinators = this.dataService.onlineCoordinatorCount;
  readonly totalCoordinators = computed(() => this.coordinators().length);
  readonly onlineTowers = this.dataService.onlineTowerCount;
  readonly totalTowers = computed(() => this.towers().length);
  readonly criticalAlerts = computed(() =>
    this.activeAlerts().filter(a => a.severity === 'critical').length
  );
  readonly wsConnected = this.wsService.connected;

  // Summary stats
  readonly farmStats = computed(() => ({
    coordinators: {
      online: this.onlineCoordinators(),
      total: this.totalCoordinators()
    },
    towers: {
      online: this.onlineTowers(),
      total: this.totalTowers()
    },
    alerts: {
      critical: this.criticalAlerts(),
      total: this.activeAlerts().length
    }
  }));

  ngOnInit(): void {
    // Load initial data
    this.dataService.loadCoordinators();
    this.dataService.loadTowers();
    this.dataService.loadActiveAlerts();

    // Connect WebSocket for real-time updates
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }

    // Start auto-refresh
    this.dataService.startAutoRefresh();
  }

  ngOnDestroy(): void {
    this.dataService.stopAutoRefresh();
  }

  refreshData(): void {
    this.dataService.loadCoordinators();
    this.dataService.loadTowers();
    this.dataService.loadActiveAlerts();
  }

  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'online':
        return 'default';
      case 'offline':
        return 'destructive';
      case 'warning':
        return 'secondary';
      default:
        return 'outline';
    }
  }

  getAlertBadgeVariant(severity: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (severity) {
      case 'critical':
        return 'destructive';
      case 'warning':
        return 'secondary';
      case 'info':
        return 'outline';
      default:
        return 'default';
    }
  }

  formatTimestamp(timestamp: Date | string): string {
    const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
    return date.toLocaleString();
  }

  trackByCoordinatorId(_: number, coord: CoordinatorSummary): string {
    return coord._id;
  }

  trackByTowerId(_: number, tower: TowerSummary): string {
    return tower._id;
  }

  trackByAlertId(_: number, alert: Alert): string {
    return alert._id;
  }
}
