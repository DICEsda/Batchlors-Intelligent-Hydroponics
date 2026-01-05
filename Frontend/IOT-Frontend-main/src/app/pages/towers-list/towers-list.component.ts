import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideFlower2,
  lucideDroplet,
  lucideThermometer,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideChevronRight,
  lucidePlus,
  lucideRefreshCw,
  lucideBattery,
  lucideSun
} from '@ng-icons/lucide';
import { IoTDataService, WebSocketService } from '../../core/services';
import { NodeSummary } from '../../core/models';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
  HlmCardContentDirective,
} from '../../components/ui/card';

@Component({
  selector: 'app-towers-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgIcon,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmSkeletonComponent,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideFlower2,
      lucideDroplet,
      lucideThermometer,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideChevronRight,
      lucidePlus,
      lucideRefreshCw,
      lucideBattery,
      lucideSun
    })
  ],
  templateUrl: './towers-list.component.html',
  styleUrl: './towers-list.component.scss',
})
export class TowersListComponent {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);

  readonly nodes = this.dataService.nodes;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly usingMockData = this.dataService.usingMockData;
  readonly wsConnected = this.wsService.connected;
  
  readonly onlineCount = this.dataService.onlineNodeCount;
  readonly totalCount = computed(() => this.nodes().length);
  readonly pairingCount = this.dataService.pairingNodeCount;
  readonly lowBatteryCount = this.dataService.lowBatteryNodeCount;

  refreshData(): void {
    this.dataService.loadDashboardData();
  }

  getStatusBadgeVariant(statusMode: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (statusMode) {
      case 'operational':
        return 'default';
      case 'offline':
      case 'error':
        return 'destructive';
      case 'pairing':
      case 'ota':
        return 'secondary';
      default:
        return 'outline';
    }
  }

  getStatusDisplay(statusMode: string): string {
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

  trackByNodeId(_: number, node: NodeSummary): string {
    return node._id;
  }
}
