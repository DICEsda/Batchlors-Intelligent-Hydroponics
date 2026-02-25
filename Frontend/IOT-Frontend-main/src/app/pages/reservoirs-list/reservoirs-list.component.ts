import { Component, inject, computed, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideWaves,
  lucideLeaf,
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
  lucideMapPin,
  lucideAlertTriangle,
  lucidePlay,
  lucideSquare,
  lucideRotateCcw,
  lucideSettings,
  lucideInfo,
  lucideSliders,
  lucideClock,
  lucideRadio,
  lucideZap,
  lucideCpu,
  lucideHash,
  lucideServer,
  lucideSave,
  lucideAlertCircle
} from '@ng-icons/lucide';
import { IoTDataService, WebSocketService, ApiService } from '../../core/services';
import { ToastService } from '../../core/services/toast.service';
import { CoordinatorSummary, NodeSummary } from '../../core/models';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { CoordinatorDialogComponent, CoordinatorDialogData } from '../../components/coordinator-dialog/coordinator-dialog.component';
import { firstValueFrom } from 'rxjs';

export type CoordinatorTab = 'towers' | 'configuration' | 'info' | 'actions';

export interface CoordinatorConfig {
  node_listening: boolean;
  log_publish_frequency: number;
  status_report_interval: number;
  telemetry_interval: number;
}

@Component({
  selector: 'app-reservoirs-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgIcon,
    HlmBadgeDirective,
    HlmButtonDirective,
    CoordinatorDialogComponent
  ],
  providers: [
    provideIcons({
      lucideWaves,
      lucideLeaf,
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
      lucideMapPin,
      lucideAlertTriangle,
      lucidePlay,
      lucideSquare,
      lucideRotateCcw,
      lucideSettings,
      lucideInfo,
      lucideSliders,
      lucideClock,
      lucideRadio,
      lucideZap,
      lucideCpu,
      lucideHash,
      lucideServer,
      lucideSave,
      lucideAlertCircle
    })
  ],
  templateUrl: './reservoirs-list.component.html',
  styleUrl: './reservoirs-list.component.scss',
})
export class ReservoirsListComponent implements OnInit {
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);
  private readonly toastService = inject(ToastService);
  private readonly apiService = inject(ApiService);

  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly wsConnected = this.wsService.connected;
  
  readonly onlineCount = this.dataService.onlineCoordinatorCount;
  readonly totalCount = computed(() => this.coordinators().length);
  readonly totalTowers = computed(() => this.nodes().length);
  readonly onlineTowers = this.dataService.onlineNodeCount;

  ngOnInit(): void {
    this.dataService.loadDashboardData();
  }

  // Track expanded reservoirs
  readonly expandedReservoirs = signal<Set<string>>(new Set());

  // Track active tab per coordinator
  readonly activeTabs = signal<Map<string, CoordinatorTab>>(new Map());

  // Track config state per coordinator
  readonly configMap = signal<Map<string, CoordinatorConfig>>(new Map());

  // Track saving state per coordinator
  readonly savingConfig = signal<Set<string>>(new Set());

  // Dialog state
  readonly isDialogOpen = signal<boolean>(false);
  readonly editingCoordinator = signal<CoordinatorDialogData | null>(null);

  refreshData(): void {
    this.dataService.loadDashboardData();
  }

  toggleExpand(reservoirId: string): void {
    this.expandedReservoirs.update(set => {
      const newSet = new Set(set);
      if (newSet.has(reservoirId)) {
        newSet.delete(reservoirId);
      } else {
        newSet.add(reservoirId);
      }
      return newSet;
    });

    // Initialize tab and config if expanding for first time
    if (!this.activeTabs().has(reservoirId)) {
      this.activeTabs.update(m => {
        const newMap = new Map(m);
        newMap.set(reservoirId, 'towers');
        return newMap;
      });
    }
    if (!this.configMap().has(reservoirId)) {
      this.initConfigForCoordinator(reservoirId);
    }
  }

  isExpanded(reservoirId: string): boolean {
    return this.expandedReservoirs().has(reservoirId);
  }

  getActiveTab(reservoirId: string): CoordinatorTab {
    return this.activeTabs().get(reservoirId) || 'towers';
  }

  setActiveTab(reservoirId: string, tab: CoordinatorTab): void {
    this.activeTabs.update(m => {
      const newMap = new Map(m);
      newMap.set(reservoirId, tab);
      return newMap;
    });
  }

  getConfig(reservoirId: string): CoordinatorConfig {
    return this.configMap().get(reservoirId) || {
      node_listening: false,
      log_publish_frequency: 30,
      status_report_interval: 60,
      telemetry_interval: 30
    };
  }

  updateConfig(reservoirId: string, field: keyof CoordinatorConfig, value: boolean | number): void {
    this.configMap.update(m => {
      const newMap = new Map(m);
      const current = newMap.get(reservoirId) || {
        node_listening: false,
        log_publish_frequency: 30,
        status_report_interval: 60,
        telemetry_interval: 30
      };
      newMap.set(reservoirId, { ...current, [field]: value });
      return newMap;
    });
  }

  private initConfigForCoordinator(reservoirId: string): void {
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    const anyCoord = coordinator as any;
    this.configMap.update(m => {
      const newMap = new Map(m);
      newMap.set(reservoirId, {
        node_listening: anyCoord?.node_listening ?? false,
        log_publish_frequency: anyCoord?.log_publish_frequency ?? 30,
        status_report_interval: anyCoord?.status_report_interval ?? 60,
        telemetry_interval: anyCoord?.telemetry_interval ?? 30
      });
      return newMap;
    });
  }

  isSavingConfig(reservoirId: string): boolean {
    return this.savingConfig().has(reservoirId);
  }

  getCoordinatorColor(coordinator: CoordinatorSummary): string {
    return (coordinator as any).color || '#3b82f6';
  }

  getCoordinatorInitial(coordinator: CoordinatorSummary): string {
    const name = coordinator.name || coordinator.coord_id || '?';
    return name.charAt(0).toUpperCase();
  }

  getTowersForReservoir(reservoir: CoordinatorSummary): NodeSummary[] {
    return this.nodes().filter(n => 
      n.coordinator_id === reservoir._id || n.coordinator_id === reservoir.coord_id
    );
  }

  getOnlineTowersCount(reservoir: CoordinatorSummary): number {
    return this.getTowersForReservoir(reservoir).filter(t => t.status_mode === 'operational').length;
  }

  editReservoir(reservoirId: string, event: Event): void {
    event.stopPropagation();
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    if (coordinator) {
      this.editingCoordinator.set({
        _id: coordinator._id,
        coord_id: coordinator.coord_id,
        name: coordinator.name || '',
        description: (coordinator as any).description || '',
        location: (coordinator as any).location || '',
        tags: (coordinator as any).tags || [],
        color: (coordinator as any).color || '#3b82f6'
      });
      this.isDialogOpen.set(true);
    }
  }

  closeDialog(): void {
    this.isDialogOpen.set(false);
    this.editingCoordinator.set(null);
  }

  async saveCoordinator(data: CoordinatorDialogData): Promise<void> {
    try {
      await this.dataService.updateCoordinator(data._id, {
        name: data.name,
        description: data.description,
        location: data.location,
        tags: data.tags,
        color: data.color
      });

      this.toastService.success(
        'Coordinator Updated',
        `Successfully updated ${data.name || data.coord_id}`
      );

      this.closeDialog();
      this.refreshData();
    } catch (error) {
      console.error('Failed to update coordinator:', error);
      this.toastService.error(
        'Update Failed',
        'Failed to update coordinator. Please try again.'
      );
    }
  }

  // ============================================================================
  // Configuration Tab
  // ============================================================================

  async saveConfiguration(reservoirId: string): Promise<void> {
    const config = this.getConfig(reservoirId);
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    if (!coordinator) return;

    this.savingConfig.update(s => {
      const newSet = new Set(s);
      newSet.add(reservoirId);
      return newSet;
    });

    try {
      await firstValueFrom(
        this.apiService.updateCoordinatorConfig(coordinator.coord_id, {
          node_listening_enabled: config.node_listening,
          log_publish_frequency_seconds: config.log_publish_frequency,
          status_report_interval_seconds: config.status_report_interval,
          telemetry_interval_seconds: config.telemetry_interval
        })
      );

      this.toastService.success(
        'Configuration Saved',
        `Configuration updated for ${coordinator.name || coordinator.coord_id}`
      );
    } catch (error) {
      console.error('Failed to save configuration:', error);
      this.toastService.error(
        'Save Failed',
        'Failed to save coordinator configuration. Please try again.'
      );
    } finally {
      this.savingConfig.update(s => {
        const newSet = new Set(s);
        newSet.delete(reservoirId);
        return newSet;
      });
    }
  }

  // ============================================================================
  // Actions Tab
  // ============================================================================

  async restartCoordinator(reservoirId: string): Promise<void> {
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    if (!coordinator) return;

    if (!confirm(`Are you sure you want to restart coordinator "${coordinator.name || coordinator.coord_id}"? This will briefly disconnect all connected towers.`)) {
      return;
    }

    try {
      await firstValueFrom(
        this.apiService.restartCoordinator({ coord_id: coordinator.coord_id })
      );

      this.toastService.success(
        'Restart Sent',
        `Restart command sent to ${coordinator.name || coordinator.coord_id}`
      );
    } catch (error) {
      console.error('Failed to restart coordinator:', error);
      this.toastService.error(
        'Restart Failed',
        'Failed to send restart command. Please try again.'
      );
    }
  }

  async startPairingMode(reservoirId: string): Promise<void> {
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    if (!coordinator) return;

    try {
      const farmId = (coordinator as any).farm_id || coordinator.site_id || '';
      await firstValueFrom(
        this.apiService.startPairing(farmId, coordinator.coord_id, 60)
      );

      this.toastService.success(
        'Pairing Mode Active',
        `${coordinator.name || coordinator.coord_id} is now listening for new towers (60s)`
      );
    } catch (error) {
      console.error('Failed to start pairing:', error);
      this.toastService.error(
        'Pairing Failed',
        'Failed to start pairing mode. Please try again.'
      );
    }
  }

  async removeCoordinator(reservoirId: string): Promise<void> {
    const coordinator = this.coordinators().find(c => c._id === reservoirId);
    if (!coordinator) return;

    if (!confirm(`Are you sure you want to remove coordinator "${coordinator.name || coordinator.coord_id}"? This action cannot be undone and will disconnect all associated towers.`)) {
      return;
    }

    try {
      await firstValueFrom(
        this.apiService.removeCoordinator(coordinator.coord_id)
      );

      this.toastService.success(
        'Coordinator Removed',
        `${coordinator.name || coordinator.coord_id} has been removed`
      );

      // Collapse the card and refresh
      this.expandedReservoirs.update(set => {
        const newSet = new Set(set);
        newSet.delete(reservoirId);
        return newSet;
      });
      this.refreshData();
    } catch (error) {
      console.error('Failed to remove coordinator:', error);
      this.toastService.error(
        'Remove Failed',
        'Failed to remove coordinator. Please try again.'
      );
    }
  }

  deleteReservoir(reservoirId: string, event: Event): void {
    event.stopPropagation();
    this.removeCoordinator(reservoirId);
  }

  // ============================================================================
  // Info Tab Helpers
  // ============================================================================

  getCoordinatorField(coordinator: CoordinatorSummary, field: string): any {
    return (coordinator as any)[field];
  }

  formatUptime(seconds: number | undefined): string {
    if (!seconds) return '--';
    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    if (days > 0) return `${days}d ${hours}h ${mins}m`;
    if (hours > 0) return `${hours}h ${mins}m`;
    return `${mins}m`;
  }

  formatBytes(bytes: number | undefined): string {
    if (bytes === undefined || bytes === null) return '--';
    if (bytes > 1048576) return `${(bytes / 1048576).toFixed(1)} MB`;
    if (bytes > 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${bytes} B`;
  }

  // ============================================================================
  // Display Helpers
  // ============================================================================

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
        return 'secondary';
      default:
        return 'outline';
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

  formatTimestamp(value: Date | string | undefined): string {
    if (!value) return '--';
    const date = typeof value === 'string' ? new Date(value) : value;
    return date.toLocaleString();
  }

  trackByCoordinatorId(_: number, coord: CoordinatorSummary): string {
    return coord._id;
  }

  trackByNodeId(_: number, node: NodeSummary): string {
    return node._id;
  }
}
