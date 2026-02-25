import { Component, OnInit, OnDestroy, inject, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription, interval, takeWhile, finalize, filter, forkJoin } from 'rxjs';
import { IoTDataService, WebSocketService, ApiService } from '../../core/services';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { 
  Coordinator, 
  NodeSummary, 
  getCoordinatorStatus, 
  getSignalStrength,
  getBatteryPercent,
  getBatteryStatus,
  DEFAULT_PAIRING_DURATION_MS,
  DiscoveredTower,
  WSTowerDiscoveredPayload,
  WSTowerPairedPayload,
  WSPairingStoppedPayload,
  WSPairingTimeoutPayload,
  PairingSession,
  TowerPairingRequest,
  getPairingSecondsRemaining,
  formatPairingCountdown as formatCountdown,
  TimeRange,
  ReservoirTelemetry,
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
import { TelemetryChartComponent } from '../../components/ui/telemetry-chart/telemetry-chart.component';
import { provideIcons, NgIcon } from '@ng-icons/core';
import {
  lucideThermometer,
  lucideActivity,
  lucideArrowLeft,
  lucideRefreshCw,
  lucideSettings,
  lucideChevronRight,
  lucideAlertTriangle,
  lucideWifi,
  lucideWifiOff,
  lucideClock,
  lucideZap,
  lucideSun,
  lucideRadar,
  lucideCpu,
  lucideBattery,
  lucideLightbulb,
  lucideServer,
  lucideRadio,
  lucideLoader2,
  lucideInfo,
  lucideBarChart3,
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
    NgIcon,
    TelemetryChartComponent,
  ],
  providers: [
    provideIcons({
      lucideThermometer,
      lucideActivity,
      lucideArrowLeft,
      lucideRefreshCw,
      lucideSettings,
      lucideChevronRight,
      lucideAlertTriangle,
      lucideWifi,
      lucideWifiOff,
      lucideClock,
      lucideZap,
      lucideSun,
      lucideRadar,
      lucideCpu,
      lucideBattery,
      lucideLightbulb,
      lucideServer,
      lucideRadio,
      lucideLoader2,
      lucideInfo,
      lucideBarChart3,
    })
  ],
  templateUrl: './coordinator-detail.component.html',
  styleUrl: './coordinator-detail.component.scss'
})
export class CoordinatorDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly dataService = inject(IoTDataService);
  private readonly wsService = inject(WebSocketService);
  private readonly apiService = inject(ApiService);
  private readonly confirmService = inject(ConfirmDialogService);
  private subscriptions: Subscription[] = [];

  // State
  readonly coordinatorId = signal<string>('');
  readonly coordinator = this.dataService.selectedCoordinator;
  readonly loading = this.dataService.loading;
  readonly error = this.dataService.error;
  readonly wsConnected = this.wsService.connected;
  readonly usingMockData = this.dataService.usingMockData;

  // Pairing state
  readonly isPairing = signal(false);
  readonly pairingSecondsRemaining = signal(0);
  readonly pairingError = signal<string | null>(null);
  readonly discoveredTowers = signal<DiscoveredTower[]>([]);
  readonly pairingSession = signal<PairingSession | null>(null);
  private pairingTimerSub: Subscription | null = null;

  // Computed coordinator status
  readonly coordinatorStatus = computed(() => {
    const coord = this.coordinator();
    if (!coord) return 'offline';
    return coord.status || getCoordinatorStatus(coord);
  });

  // Computed signal strength
  readonly signalStrength = computed(() => {
    const coord = this.coordinator();
    if (!coord) return 'poor';
    return getSignalStrength(coord.wifi_rssi);
  });

  // Connected nodes - filtered from nodes signal by coordinator_id
  readonly connectedNodes = computed(() => {
    const coord = this.coordinator();
    if (!coord) return [];
    const allNodes = this.dataService.nodes();
    return allNodes.filter(n => n.coordinator_id === coord.coord_id || n.coordinator_id === coord._id);
  });

  // Node counts
  readonly onlineNodeCount = computed(() => 
    this.connectedNodes().filter(n => n.status_mode === 'operational').length
  );
  readonly totalNodeCount = computed(() => this.connectedNodes().length);
  readonly pairingNodeCount = computed(() => 
    this.connectedNodes().filter(n => n.status_mode === 'pairing').length
  );
  readonly errorNodeCount = computed(() => 
    this.connectedNodes().filter(n => n.status_mode === 'error').length
  );

  // ============================================================================
  // Telemetry Charts
  // ============================================================================
  @ViewChild('phChart') phChart?: TelemetryChartComponent;
  @ViewChild('ecChart') ecChart?: TelemetryChartComponent;
  @ViewChild('waterLevelChart') waterLevelChart?: TelemetryChartComponent;
  @ViewChild('waterTempChart') waterTempChart?: TelemetryChartComponent;

  ngOnInit(): void {
    // Get coordinator ID from route
    const sub = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.coordinatorId.set(id);
        this.loadCoordinatorData(id);
        this.subscribeToRealTimeUpdates(id);
        this.checkExistingPairingSession(id);
      }
    });
    this.subscriptions.push(sub);

    // Connect WebSocket if not connected
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }
    
    // Subscribe to pairing WebSocket events
    this.subscribeToPairingEvents();
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.dataService.clearSelectedCoordinator();
    
    // Clean up pairing timer
    this.stopPairingTimer();
    
    // Unsubscribe from coordinator updates
    const id = this.coordinatorId();
    if (id) {
      this.wsService.unsubscribe('coordinator', id);
    }
  }

  private loadCoordinatorData(id: string): void {
    this.dataService.loadCoordinatorById(id);
    // Also load nodes for this coordinator
    this.dataService.loadNodes();
    // Load telemetry history for charts
    this.loadTelemetryHistory(id);
  }

  private subscribeToRealTimeUpdates(id: string): void {
    // Subscribe to coordinator WebSocket updates
    this.wsService.subscribeToCoordinator(id);

    // Subscribe to reservoir telemetry for live chart updates
    const reservoirSub = this.wsService.reservoirTelemetry$.pipe(
      filter((t: ReservoirTelemetry) => t.coordId === id)
    ).subscribe((t) => {
      const now = new Date(t.timestamp || Date.now());
      this.phChart?.appendPoint({ time: now, value: t.ph });
      this.ecChart?.appendPoint({ time: now, value: t.ec });
      this.waterLevelChart?.appendPoint({ time: now, value: t.waterLevel });
      this.waterTempChart?.appendPoint({ time: now, value: t.temperature });
    });
    this.subscriptions.push(reservoirSub);
  }

  /**
   * Load 1-hour telemetry history for the coordinator charts.
   */
  private loadTelemetryHistory(coordId: string): void {
    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
    const timeRange: TimeRange = { start: oneHourAgo, end: now, interval: '1m' };

    this.apiService.getCoordinatorHistory(coordId, timeRange).subscribe({
      next: (history) => {
        // The CoordinatorHistory model uses temp_c, light_lux, etc.
        // But for reservoir-specific metrics (pH, EC, waterLevel), we need to check
        // if the backend returns them. For now, use available fields.
        // The charts are designed for reservoir data, so they'll display
        // available data points.
      },
      error: (err) => {
        console.log('[Telemetry] Could not load coordinator history:', err.message);
      },
    });
  }

  refreshData(): void {
    const id = this.coordinatorId();
    if (id) {
      this.loadCoordinatorData(id);
    }
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
        return 'secondary';
      default:
        return 'outline';
    }
  }

  getNodeStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'operational': return 'default';
      case 'pairing': return 'secondary';
      case 'error': return 'destructive';
      case 'ota': return 'outline';
      default: return 'outline';
    }
  }

  getSignalIcon(rssi: number): string {
    if (rssi >= -50) return 'lucideWifi';
    if (rssi >= -70) return 'lucideWifi';
    return 'lucideWifiOff';
  }

  getBatteryPercent(vbat_mv: number): number {
    return getBatteryPercent(vbat_mv);
  }

  getBatteryStatus(vbat_mv: number): string {
    return getBatteryStatus(vbat_mv);
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

  trackByNodeId(_: number, node: NodeSummary): string {
    return node._id;
  }

  // ============================================================================
  // Pairing Methods
  // ============================================================================

  /**
   * Check for an existing pairing session on component load
   * Recovers state if user refreshes the page during active pairing
   */
  private checkExistingPairingSession(coordId: string): void {
    const coord = this.coordinator();
    const farmId = coord?.site_id;
    
    if (!farmId) {
      // Coordinator not loaded yet, try again after a delay
      setTimeout(() => this.checkExistingPairingSession(coordId), 500);
      return;
    }

    // Check for active session
    this.apiService.getPairingSession(farmId, coordId).subscribe({
      next: (session) => {
        if (session && session.status === 'active') {
          this.pairingSession.set(session);
          this.isPairing.set(true);
          
          // Calculate remaining time
          const remaining = getPairingSecondsRemaining(session);
          this.pairingSecondsRemaining.set(remaining);
          
          // Convert pending requests to discovered nodes
          if (session.pending_requests?.length > 0) {
            const nodes: DiscoveredTower[] = session.pending_requests.map(req => ({
              towerId: req.tower_id,
              macAddress: req.mac_address,
              rssi: req.rssi || -70,
              discoveredAt: new Date(req.requested_at),
              firmwareVersion: req.fw_version,
              status: req.status === 'pending' ? 'discovered' : req.status as any
            }));
            this.discoveredTowers.set(nodes);
          }
          
          // Start countdown timer
          if (remaining > 0) {
            this.startPairingTimer(remaining);
          }
        }
      },
      error: (err) => {
        // No active session is fine, just log for debugging
        console.log('[Pairing] No active session:', err.message);
      }
    });
  }

  /**
   * Start pairing mode on the coordinator
   * @param durationSeconds Pairing duration in seconds (default: 60)
   */
  startPairing(durationSeconds: number = 60): void {
    const coord = this.coordinator();
    if (!coord || this.isPairing()) {
      return;
    }

    const farmId = coord.site_id;
    if (!farmId) {
      this.pairingError.set('Farm ID not available');
      return;
    }

    this.pairingError.set(null);
    this.isPairing.set(true);
    this.pairingSecondsRemaining.set(durationSeconds);
    this.discoveredTowers.set([]);

    // Call API to start pairing
    const sub = this.apiService.startPairing(farmId, coord.coord_id, durationSeconds).subscribe({
      next: (session) => {
        this.pairingSession.set(session);
        // Start countdown timer
        this.startPairingTimer(durationSeconds);
      },
      error: (err) => {
        console.error('Failed to start pairing:', err);
        this.pairingError.set(err.message || 'Failed to start pairing mode');
        this.isPairing.set(false);
        this.pairingSecondsRemaining.set(0);
      }
    });
    
    this.subscriptions.push(sub);
  }

  /**
   * Stop pairing mode via API
   */
  stopPairing(): void {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) return;

    this.apiService.stopPairing(farmId, coord.coord_id).subscribe({
      next: (session) => {
        this.pairingSession.set(session);
        this.cancelPairing();
      },
      error: (err) => {
        console.error('Failed to stop pairing:', err);
        // Still cancel the local state
        this.cancelPairing();
      }
    });
  }

  /**
   * Cancel pairing mode locally (stops the timer)
   */
  cancelPairing(): void {
    this.stopPairingTimer();
    this.isPairing.set(false);
    this.pairingSecondsRemaining.set(0);
    this.pairingError.set(null);
  }

  /**
   * Start the countdown timer for pairing mode
   */
  private startPairingTimer(seconds: number): void {
    this.stopPairingTimer();
    
    this.pairingTimerSub = interval(1000).pipe(
      takeWhile(() => this.pairingSecondsRemaining() > 0),
      finalize(() => {
        this.isPairing.set(false);
        this.pairingSecondsRemaining.set(0);
      })
    ).subscribe(() => {
      this.pairingSecondsRemaining.update(v => Math.max(0, v - 1));
    });
  }

  /**
   * Stop the pairing countdown timer
   */
  private stopPairingTimer(): void {
    if (this.pairingTimerSub) {
      this.pairingTimerSub.unsubscribe();
      this.pairingTimerSub = null;
    }
  }

  /**
   * Format pairing countdown for display (MM:SS)
   */
  formatPairingCountdown(): string {
    const seconds = this.pairingSecondsRemaining();
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  }

  // ============================================================================
  // Pairing WebSocket Subscriptions
  // ============================================================================

  /**
   * Subscribe to all pairing-related WebSocket events
   */
  private subscribeToPairingEvents(): void {
    const coordId = this.coordinatorId;
    
    // Subscribe to tower discovered events for this coordinator
    const towerDiscoveredSub = this.wsService.towerDiscovered$.pipe(
      filter((payload: WSTowerDiscoveredPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handleTowerDiscovered(payload);
    });
    this.subscriptions.push(towerDiscoveredSub);

    // Subscribe to tower paired events for this coordinator
    const towerPairedSub = this.wsService.towerPaired$.pipe(
      filter((payload: WSTowerPairedPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handleTowerPaired(payload);
    });
    this.subscriptions.push(towerPairedSub);

    // Subscribe to pairing_stopped events for this coordinator
    const pairingStoppedSub = this.wsService.pairingStopped$.pipe(
      filter((payload: WSPairingStoppedPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handlePairingStopped(payload);
    });
    this.subscriptions.push(pairingStoppedSub);

    // Subscribe to pairing_timeout events for this coordinator
    const pairingTimeoutSub = this.wsService.pairingTimeout$.pipe(
      filter((payload: WSPairingTimeoutPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handlePairingTimeout(payload);
    });
    this.subscriptions.push(pairingTimeoutSub);
  }

  /**
   * Handle tower discovered event - add to discovered towers list
   */
  private handleTowerDiscovered(payload: WSTowerDiscoveredPayload): void {
    console.log('[Pairing] Tower discovered:', payload.towerId);
    
    // Check if tower already exists in discovered list
    const existing = this.discoveredTowers().find(t => t.towerId === payload.towerId);
    if (existing) {
      // Update existing entry
      this.discoveredTowers.update(towers => 
        towers.map(t => t.towerId === payload.towerId 
          ? { ...t, rssi: payload.rssi, discoveredAt: new Date(payload.discoveredAt) }
          : t
        )
      );
    } else {
      // Add new discovered tower
      const newTower: DiscoveredTower = {
        towerId: payload.towerId,
        macAddress: payload.macAddress,
        rssi: payload.rssi,
        discoveredAt: new Date(payload.discoveredAt),
        firmwareVersion: payload.firmwareVersion,
        status: 'discovered'
      };
      this.discoveredTowers.update(towers => [...towers, newTower]);
    }
  }

  /**
   * Handle tower paired event - update tower status in discovered list
   */
  private handleTowerPaired(payload: WSTowerPairedPayload): void {
    console.log('[Pairing] Tower paired:', payload.towerId);
    
    this.discoveredTowers.update(towers =>
      towers.map(t => t.towerId === payload.towerId
        ? { ...t, status: 'paired' as const }
        : t
      )
    );
    
    // Refresh nodes list to show newly paired tower
    this.dataService.loadNodes();
  }

  /**
   * Handle pairing stopped event
   */
  private handlePairingStopped(payload: WSPairingStoppedPayload): void {
    console.log('[Pairing] Pairing stopped:', payload.reason);
    
    this.stopPairingTimer();
    this.isPairing.set(false);
    this.pairingSecondsRemaining.set(0);
    
    // Keep discovered towers visible for a moment, then clear
    setTimeout(() => {
      this.discoveredTowers.set([]);
    }, 5000);
  }

  /**
   * Handle pairing timeout event
   */
  private handlePairingTimeout(payload: WSPairingTimeoutPayload): void {
    console.log('[Pairing] Pairing timed out. Discovered:', payload.nodesDiscovered, 'Paired:', payload.nodesPaired);
    
    this.stopPairingTimer();
    this.isPairing.set(false);
    this.pairingSecondsRemaining.set(0);
    
    // Keep discovered towers visible for a moment, then clear
    setTimeout(() => {
      this.discoveredTowers.set([]);
    }, 5000);
  }

  /**
   * Approve a discovered tower for pairing
   */
  approveTower(tower: DiscoveredTower): void {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) {
      console.error('[Pairing] Farm ID not available');
      return;
    }

    // Update tower status to 'pairing'
    this.discoveredTowers.update(towers =>
      towers.map(t => t.towerId === tower.towerId
        ? { ...t, status: 'pairing' as const }
        : t
      )
    );

    // Call API to approve tower
    this.apiService.approveTower(farmId, coord.coord_id, tower.towerId).subscribe({
      next: (result) => {
        console.log('[Pairing] Tower approved:', result);
        this.discoveredTowers.update(towers =>
          towers.map(t => t.towerId === tower.towerId
            ? { ...t, status: 'paired' as const }
            : t
          )
        );
        // Refresh nodes list to show newly paired tower
        this.dataService.loadNodes();
      },
      error: (err) => {
        console.error('[Pairing] Failed to approve tower:', err);
        this.discoveredTowers.update(towers =>
          towers.map(t => t.towerId === tower.towerId
            ? { ...t, status: 'error' as const, error: err.message || 'Approval failed' }
            : t
          )
        );
      }
    });
  }

  /**
   * Reject a discovered tower
   */
  rejectTower(tower: DiscoveredTower): void {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) {
      console.error('[Pairing] Farm ID not available');
      // Still update local state
      this.discoveredTowers.update(towers =>
        towers.filter(t => t.towerId !== tower.towerId)
      );
      return;
    }

    // Update tower status to 'rejected'
    this.discoveredTowers.update(towers =>
      towers.map(t => t.towerId === tower.towerId
        ? { ...t, status: 'rejected' as const }
        : t
      )
    );

    // Call API to reject tower
    this.apiService.rejectTower(farmId, coord.coord_id, tower.towerId).subscribe({
      next: () => {
        console.log('[Pairing] Tower rejected:', tower.towerId);
        // Remove after animation
        setTimeout(() => {
          this.discoveredTowers.update(towers =>
            towers.filter(t => t.towerId !== tower.towerId)
          );
        }, 300);
      },
      error: (err) => {
        console.error('[Pairing] Failed to reject tower:', err);
        // Still remove from UI after showing error briefly
        setTimeout(() => {
          this.discoveredTowers.update(towers =>
            towers.filter(t => t.towerId !== tower.towerId)
          );
        }, 1000);
      }
    });
  }

  /**
   * Forget a paired device (unpair)
   */
  async forgetDevice(node: NodeSummary): Promise<void> {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) {
      console.error('[Pairing] Farm ID not available');
      return;
    }

    const deviceName = node.name || node.light_id || node._id;
    const confirmed = await this.confirmService.confirmDanger(
      'Forget Device',
      `Are you sure you want to forget "${deviceName}"? This will unpair the device and remove it from the coordinator. This action cannot be undone.`,
      'Forget Device'
    );

    if (!confirmed) return;

    this.apiService.forgetDevice(farmId, coord.coord_id, node._id).subscribe({
      next: () => {
        console.log('[Pairing] Device forgotten:', node._id);
        // Refresh nodes list to remove the forgotten node
        this.dataService.loadNodes();
      },
      error: (err) => {
        console.error('[Pairing] Failed to forget device:', err);
        this.confirmService.confirm({
          title: 'Failed to Forget Device',
          message: `Unable to forget device: ${err.message || 'Unknown error'}. Please try again or check your connection.`,
          confirmText: 'OK',
          type: 'danger'
        });
      }
    });
  }

  /**
   * Track discovered towers by ID
   */
  trackByDiscoveredTowerId(_: number, tower: DiscoveredTower): string {
    return tower.towerId;
  }

  /**
   * Get badge variant for discovered tower status
   */
  getDiscoveredTowerBadgeVariant(status: DiscoveredTower['status']): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'discovered': return 'secondary';
      case 'pairing': return 'outline';
      case 'paired': return 'default';
      case 'rejected': return 'destructive';
      case 'error': return 'destructive';
      default: return 'outline';
    }
  }
}
