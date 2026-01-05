import { Component, OnInit, OnDestroy, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { IoTDataService, WebSocketService } from '../../core/services';
import { CoordinatorSummary, NodeSummary, Alert } from '../../core/models';

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
  lucideMapPin
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
      lucideServer,
      lucideSun,
      lucideBattery,
      lucideLayoutGrid,
      lucideMapPin
    })
  ],
  templateUrl: './farm-overview.component.html',
  styleUrl: './farm-overview.component.scss'
})
export class FarmOverviewComponent implements OnInit, OnDestroy {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);

  // Expose data from service
  readonly sites = this.dataService.sites;
  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;
  readonly zones = this.dataService.zones;
  readonly activeAlerts = this.dataService.activeAlerts;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly usingMockData = this.dataService.usingMockData;

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

  // Summary stats
  readonly systemStats = computed(() => ({
    sites: {
      total: this.totalSites()
    },
    coordinators: {
      online: this.onlineCoordinators(),
      total: this.totalCoordinators()
    },
    nodes: {
      online: this.onlineNodes(),
      total: this.totalNodes(),
      pairing: this.pairingNodes(),
      error: this.errorNodes(),
      lowBattery: this.lowBatteryNodes()
    },
    alerts: {
      critical: this.criticalAlerts(),
      total: this.activeAlerts().length
    }
  }));

  // Legacy alias for template backward compatibility
  readonly farmStats = this.systemStats;

  ngOnInit(): void {
    // Load initial data (with automatic mock fallback)
    this.dataService.loadDashboardData();

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
    this.dataService.loadDashboardData();
  }

  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'online':
      case 'operational':
        return 'default';
      case 'offline':
      case 'error':
        return 'destructive';
      case 'warning':
      case 'pairing':
      case 'ota':
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

  getNodeStatusDisplay(statusMode: string): string {
    switch (statusMode) {
      case 'operational':
        return 'Online';
      case 'pairing':
        return 'Pairing';
      case 'ota':
        return 'Updating';
      case 'error':
        return 'Error';
      default:
        return statusMode;
    }
  }

  getBatteryIcon(vbatMv: number | undefined): string {
    if (!vbatMv) return 'lucideBattery';
    if (vbatMv < 3200) return 'lucideBattery'; // Low - would use lucideBatteryLow if available
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

  trackByCoordinatorId(_: number, coord: CoordinatorSummary): string {
    return coord._id;
  }

  trackByNodeId(_: number, node: NodeSummary): string {
    return node._id;
  }

  trackByAlertId(_: number, alert: Alert): string {
    return alert._id;
  }
}
