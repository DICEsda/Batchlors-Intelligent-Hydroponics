import { Component, inject, computed, signal, OnInit, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideLeaf,
  lucideWaves,
  lucideDroplet,
  lucideThermometer,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideChevronRight,
  lucideChevronDown,
  lucidePlus,
  lucideRefreshCw,
  lucidePencil,
  lucideTrash2,
  lucideBattery,
  lucideSun,
  lucideAlertTriangle,
  lucideZap,
  lucideX,
  lucideCheck,
  lucideRadioTower
} from '@ng-icons/lucide';
import { IoTDataService, WebSocketService } from '../../core/services';
import { NodeSummary, CoordinatorSummary } from '../../core/models';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import {
  HlmDialogComponent,
  HlmDialogOverlayDirective,
  HlmDialogContentDirective,
  HlmDialogHeaderDirective,
  HlmDialogTitleDirective,
  HlmDialogDescriptionDirective,
  HlmDialogFooterDirective,
  HlmDialogCloseDirective
} from '../../components/ui/dialog';
import { HlmInputDirective } from '../../components/ui/input';
import { HlmLabelDirective } from '../../components/ui/label';

@Component({
  selector: 'app-towers-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    NgIcon,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmDialogComponent,
    HlmDialogOverlayDirective,
    HlmDialogContentDirective,
    HlmDialogHeaderDirective,
    HlmDialogTitleDirective,
    HlmDialogDescriptionDirective,
    HlmDialogFooterDirective,
    HlmDialogCloseDirective,
    HlmInputDirective,
    HlmLabelDirective
  ],
  providers: [
    provideIcons({
      lucideLeaf,
      lucideWaves,
      lucideDroplet,
      lucideThermometer,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideChevronRight,
      lucideChevronDown,
      lucidePlus,
      lucideRefreshCw,
      lucidePencil,
      lucideTrash2,
      lucideBattery,
      lucideSun,
      lucideAlertTriangle,
      lucideZap,
      lucideX,
      lucideCheck,
      lucideRadioTower
    })
  ],
  templateUrl: './towers-list.component.html',
  styleUrl: './towers-list.component.scss',
})
export class TowersListComponent implements OnInit {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly nodes = this.dataService.nodes;
  readonly coordinators = this.dataService.coordinators;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly usingMockData = this.dataService.usingMockData;
  readonly wsConnected = this.wsService.connected;
  
  readonly onlineCount = this.dataService.onlineNodeCount;
  readonly totalCount = computed(() => this.nodes().length);
  readonly pairingCount = this.dataService.pairingNodeCount;
  readonly lowBatteryCount = this.dataService.lowBatteryNodeCount;

  // Group towers by their connected reservoir
  readonly towersByReservoir = computed(() => {
    const nodes = this.nodes();
    const coords = this.coordinators();
    
    const grouped: Map<string, { reservoir: CoordinatorSummary | null, towers: NodeSummary[] }> = new Map();
    
    // Group by coordinator_id
    for (const node of nodes) {
      const coordId = node.coordinator_id || 'unassigned';
      if (!grouped.has(coordId)) {
        const reservoir = coords.find(c => c._id === coordId || c.coord_id === coordId) || null;
        grouped.set(coordId, { reservoir, towers: [] });
      }
      grouped.get(coordId)!.towers.push(node);
    }
    
    return Array.from(grouped.entries()).map(([id, data]) => ({
      id,
      reservoir: data.reservoir,
      towers: data.towers,
      onlineCount: data.towers.filter(t => t.status_mode === 'operational').length
    }));
  });

  // Track expanded groups
  readonly expandedGroups = signal<Set<string>>(new Set(['all']));

  // Pairing dialog state
  readonly showPairingDialog = signal<boolean>(false);
  readonly pairingTowerId = signal<string>('T-3A7B');
  readonly pairingTowerName = signal<string>('');
  readonly pairingCoordinatorId = signal<string>('');

  ngOnInit(): void {
    // Check for query parameters to auto-open pairing dialog
    this.route.queryParams.subscribe(params => {
      if (params['action'] === 'pair' && params['towerId']) {
        this.pairingTowerId.set(params['towerId']);
        this.showPairingDialog.set(true);
      }
    });
  }

  refreshData(): void {
    this.dataService.loadDashboardData();
  }

  openPairingDialog(): void {
    this.showPairingDialog.set(true);
  }

  closePairingDialog(): void {
    this.showPairingDialog.set(false);
    // Remove query parameters
    this.router.navigate([], {
      queryParams: {},
      queryParamsHandling: 'merge'
    });
  }

  approvePairing(): void {
    const towerId = this.pairingTowerId();
    const towerName = this.pairingTowerName();
    const coordinatorId = this.pairingCoordinatorId();
    
    console.log('Approving pairing:', { towerId, towerName, coordinatorId });
    
    // TODO: Call API to approve pairing
    // For now, just close the dialog
    this.closePairingDialog();
    
    // Show success toast (if toast service is available)
    console.log(`Tower ${towerId} paired successfully!`);
  }

  toggleGroup(groupId: string): void {
    this.expandedGroups.update(set => {
      const newSet = new Set(set);
      if (newSet.has(groupId)) {
        newSet.delete(groupId);
      } else {
        newSet.add(groupId);
      }
      return newSet;
    });
  }

  isGroupExpanded(groupId: string): boolean {
    return this.expandedGroups().has(groupId);
  }

  editTower(towerId: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    console.log('Edit tower:', towerId);
    // TODO: Implement edit dialog
  }

  deleteTower(towerId: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    if (confirm('Are you sure you want to delete this tower?')) {
      console.log('Delete tower:', towerId);
      // TODO: Implement actual delete
    }
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

  getBatteryStatus(vbatMv: number | undefined): 'good' | 'warning' | 'critical' {
    if (!vbatMv) return 'warning';
    if (vbatMv > 3500) return 'good';
    if (vbatMv > 3200) return 'warning';
    return 'critical';
  }

  trackByNodeId(_: number, node: NodeSummary): string {
    return node._id;
  }

  trackByGroupId(_: number, group: { id: string }): string {
    return group.id;
  }
}
