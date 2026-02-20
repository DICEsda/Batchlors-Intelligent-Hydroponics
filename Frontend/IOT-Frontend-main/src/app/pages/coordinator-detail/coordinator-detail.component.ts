import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
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
  DiscoveredNode,
  WSNodeDiscoveredPayload,
  WSNodePairedPayload,
  WSPairingStoppedPayload,
  WSPairingTimeoutPayload,
  PairingSession,
  TowerPairingRequest,
  getPairingSecondsRemaining,
  formatPairingCountdown as formatCountdown
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
  lucideInfo
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
      lucideInfo
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
  readonly discoveredNodes = signal<DiscoveredNode[]>([]);
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
  }

  private subscribeToRealTimeUpdates(id: string): void {
    // Subscribe to coordinator WebSocket updates
    this.wsService.subscribeToCoordinator(id);
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
            const nodes: DiscoveredNode[] = session.pending_requests.map(req => ({
              nodeId: req.tower_id,
              macAddress: req.mac_address,
              rssi: req.rssi || -70,
              discoveredAt: new Date(req.requested_at),
              firmwareVersion: req.fw_version,
              status: req.status === 'pending' ? 'discovered' : req.status as any
            }));
            this.discoveredNodes.set(nodes);
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
    this.discoveredNodes.set([]);

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
    
    // Subscribe to node_discovered events for this coordinator
    const nodeDiscoveredSub = this.wsService.nodeDiscovered$.pipe(
      filter((payload: WSNodeDiscoveredPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handleNodeDiscovered(payload);
    });
    this.subscriptions.push(nodeDiscoveredSub);

    // Subscribe to node_paired events for this coordinator
    const nodePairedSub = this.wsService.nodePaired$.pipe(
      filter((payload: WSNodePairedPayload) => payload.coordinatorId === coordId())
    ).subscribe((payload) => {
      this.handleNodePaired(payload);
    });
    this.subscriptions.push(nodePairedSub);

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
   * Handle node discovered event - add to discovered nodes list
   */
  private handleNodeDiscovered(payload: WSNodeDiscoveredPayload): void {
    console.log('[Pairing] Node discovered:', payload.nodeId);
    
    // Check if node already exists in discovered list
    const existing = this.discoveredNodes().find(n => n.nodeId === payload.nodeId);
    if (existing) {
      // Update existing entry
      this.discoveredNodes.update(nodes => 
        nodes.map(n => n.nodeId === payload.nodeId 
          ? { ...n, rssi: payload.rssi, discoveredAt: new Date(payload.discoveredAt) }
          : n
        )
      );
    } else {
      // Add new discovered node
      const newNode: DiscoveredNode = {
        nodeId: payload.nodeId,
        macAddress: payload.macAddress,
        rssi: payload.rssi,
        discoveredAt: new Date(payload.discoveredAt),
        firmwareVersion: payload.firmwareVersion,
        status: 'discovered'
      };
      this.discoveredNodes.update(nodes => [...nodes, newNode]);
    }
  }

  /**
   * Handle node paired event - update node status in discovered list
   */
  private handleNodePaired(payload: WSNodePairedPayload): void {
    console.log('[Pairing] Node paired:', payload.nodeId);
    
    this.discoveredNodes.update(nodes =>
      nodes.map(n => n.nodeId === payload.nodeId
        ? { ...n, status: 'paired' as const }
        : n
      )
    );
    
    // Refresh nodes list to show newly paired node
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
    
    // Keep discovered nodes visible for a moment, then clear
    setTimeout(() => {
      this.discoveredNodes.set([]);
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
    
    // Keep discovered nodes visible for a moment, then clear
    setTimeout(() => {
      this.discoveredNodes.set([]);
    }, 5000);
  }

  /**
   * Approve a discovered node for pairing
   */
  approveNode(node: DiscoveredNode): void {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) {
      console.error('[Pairing] Farm ID not available');
      return;
    }

    // Update node status to 'pairing'
    this.discoveredNodes.update(nodes =>
      nodes.map(n => n.nodeId === node.nodeId
        ? { ...n, status: 'pairing' as const }
        : n
      )
    );

    // Call API to approve node
    this.apiService.approveNode(farmId, coord.coord_id, node.nodeId).subscribe({
      next: (tower) => {
        console.log('[Pairing] Node approved:', tower);
        this.discoveredNodes.update(nodes =>
          nodes.map(n => n.nodeId === node.nodeId
            ? { ...n, status: 'paired' as const }
            : n
          )
        );
        // Refresh nodes list to show newly paired node
        this.dataService.loadNodes();
      },
      error: (err) => {
        console.error('[Pairing] Failed to approve node:', err);
        this.discoveredNodes.update(nodes =>
          nodes.map(n => n.nodeId === node.nodeId
            ? { ...n, status: 'error' as const, error: err.message || 'Approval failed' }
            : n
          )
        );
      }
    });
  }

  /**
   * Reject a discovered node
   */
  rejectNode(node: DiscoveredNode): void {
    const coord = this.coordinator();
    if (!coord) return;

    const farmId = coord.site_id;
    if (!farmId) {
      console.error('[Pairing] Farm ID not available');
      // Still update local state
      this.discoveredNodes.update(nodes =>
        nodes.filter(n => n.nodeId !== node.nodeId)
      );
      return;
    }

    // Update node status to 'rejected'
    this.discoveredNodes.update(nodes =>
      nodes.map(n => n.nodeId === node.nodeId
        ? { ...n, status: 'rejected' as const }
        : n
      )
    );

    // Call API to reject node
    this.apiService.rejectNode(farmId, coord.coord_id, node.nodeId).subscribe({
      next: () => {
        console.log('[Pairing] Node rejected:', node.nodeId);
        // Remove after animation
        setTimeout(() => {
          this.discoveredNodes.update(nodes =>
            nodes.filter(n => n.nodeId !== node.nodeId)
          );
        }, 300);
      },
      error: (err) => {
        console.error('[Pairing] Failed to reject node:', err);
        // Still remove from UI after showing error briefly
        setTimeout(() => {
          this.discoveredNodes.update(nodes =>
            nodes.filter(n => n.nodeId !== node.nodeId)
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
   * Track discovered nodes by ID
   */
  trackByDiscoveredNodeId(_: number, node: DiscoveredNode): string {
    return node.nodeId;
  }

  /**
   * Get badge variant for discovered node status
   */
  getDiscoveredNodeBadgeVariant(status: DiscoveredNode['status']): 'default' | 'secondary' | 'destructive' | 'outline' {
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
