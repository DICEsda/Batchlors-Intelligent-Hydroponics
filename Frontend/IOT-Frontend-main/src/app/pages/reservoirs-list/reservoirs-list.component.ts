import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideServer,
  lucideDroplet,
  lucideThermometer,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideChevronRight,
  lucidePlus,
  lucideRefreshCw
} from '@ng-icons/lucide';
import { IoTDataService, WebSocketService } from '../../core/services';
import { CoordinatorSummary } from '../../core/models';
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
  selector: 'app-reservoirs-list',
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
      lucideServer,
      lucideDroplet,
      lucideThermometer,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideChevronRight,
      lucidePlus,
      lucideRefreshCw
    })
  ],
  templateUrl: './reservoirs-list.component.html',
  styleUrl: './reservoirs-list.component.scss',
})
export class ReservoirsListComponent {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);

  readonly coordinators = this.dataService.coordinators;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly usingMockData = this.dataService.usingMockData;
  readonly wsConnected = this.wsService.connected;
  
  readonly onlineCount = this.dataService.onlineCoordinatorCount;
  readonly totalCount = computed(() => this.coordinators().length);

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
        return 'secondary';
      default:
        return 'outline';
    }
  }

  trackByCoordinatorId(_: number, coord: CoordinatorSummary): string {
    return coord._id;
  }
}
