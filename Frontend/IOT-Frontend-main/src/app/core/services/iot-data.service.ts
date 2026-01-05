import { Injectable, inject, signal, computed } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { interval, switchMap, tap, catchError, of, Subject, takeUntil, firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import { MockDataService } from './mock-data.service';
import {
  Site,
  SiteSummary,
  Coordinator,
  CoordinatorSummary,
  Node,
  NodeSummary,
  NodeTelemetry,
  Zone,
  ZoneSummary,
  Alert,
  HealthStatus,
  SystemMetrics,
  getCoordinatorStatus,
  getBatteryStatus,
} from '../models';

/**
 * IoT Data Service - Smart Tile System
 * Centralized state management using Angular signals
 * Provides reactive data access and automatic refresh
 * Falls back to mock data when backend is unavailable
 */
@Injectable({
  providedIn: 'root'
})
export class IoTDataService {
  private readonly api = inject(ApiService);
  private readonly mockData = inject(MockDataService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals
  // ============================================================================

  // Loading states
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly usingMockData = signal(false);

  // Health
  readonly healthStatus = signal<HealthStatus | null>(null);

  // System metrics
  readonly systemMetrics = signal<SystemMetrics | null>(null);

  // Sites
  readonly sites = signal<Site[]>([]);
  readonly selectedSite = signal<Site | null>(null);

  // Coordinators
  readonly coordinators = signal<CoordinatorSummary[]>([]);
  readonly selectedCoordinator = signal<Coordinator | null>(null);

  // Nodes (replaces towers)
  readonly nodes = signal<NodeSummary[]>([]);
  readonly selectedNode = signal<Node | null>(null);
  readonly nodeTelemetry = signal<Map<string, NodeTelemetry>>(new Map());

  // Zones
  readonly zones = signal<Zone[]>([]);
  readonly selectedZone = signal<Zone | null>(null);

  // Alerts
  readonly alerts = signal<Alert[]>([]);
  readonly unacknowledgedAlertCount = computed(() =>
    this.alerts().filter(a => a.status === 'active').length
  );
  readonly criticalAlertCount = computed(() =>
    this.alerts().filter(a => a.severity === 'critical' && a.status === 'active').length
  );

  // Active alerts (unacknowledged)
  readonly activeAlerts = computed(() =>
    this.alerts().filter(a => a.status === 'active')
  );

  // Loading alias for backward compatibility
  readonly loading = this.isLoading;

  // ============================================================================
  // Computed Signals
  // ============================================================================

  readonly totalSiteCount = computed(() => this.sites().length);

  readonly onlineCoordinatorCount = computed(() =>
    this.coordinators().filter(c => c.status === 'online').length
  );

  readonly totalCoordinatorCount = computed(() =>
    this.coordinators().length
  );

  readonly onlineNodeCount = computed(() =>
    this.nodes().filter(n => n.status_mode === 'operational').length
  );

  readonly totalNodeCount = computed(() =>
    this.nodes().length
  );

  readonly pairingNodeCount = computed(() =>
    this.nodes().filter(n => n.status_mode === 'pairing').length
  );

  readonly errorNodeCount = computed(() =>
    this.nodes().filter(n => n.status_mode === 'error').length
  );

  readonly lowBatteryNodeCount = computed(() =>
    this.nodes().filter(n => (n.vbat_mv ?? 0) < 3200).length
  );

  readonly averageTemperature = computed(() => {
    const coords = this.coordinators();
    if (coords.length === 0) return 0;
    return coords.reduce((sum, c) => sum + (c.temp_c ?? 0), 0) / coords.length;
  });

  readonly averageLightLux = computed(() => {
    const coords = this.coordinators();
    if (coords.length === 0) return 0;
    return coords.reduce((sum, c) => sum + (c.light_lux ?? 0), 0) / coords.length;
  });

  // Backward compatibility aliases (for gradual migration)
  /** @deprecated Use nodes instead */
  readonly towers = this.nodes;
  /** @deprecated Use onlineNodeCount instead */
  readonly onlineTowerCount = this.onlineNodeCount;
  /** @deprecated Use totalNodeCount instead */
  readonly totalTowerCount = this.totalNodeCount;

  // ============================================================================
  // Data Loading Methods
  // ============================================================================

  /**
   * Load initial data for the dashboard
   */
  async loadDashboardData(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      // Try to check health first
      await this.checkHealth();

      // If health check passed, load real data
      await Promise.all([
        this.loadSites(),
        this.loadCoordinators(),
        this.loadNodes(),
        this.loadZones(),
        this.loadAlerts(),
      ]);
      this.usingMockData.set(false);
    } catch (err) {
      console.warn('Backend unavailable, falling back to mock data');
      this.usingMockData.set(true);
      await this.loadMockData();
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load mock data as fallback
   */
  private async loadMockData(): Promise<void> {
    try {
      const [sites, coordinators, nodes, zones, alerts, metrics] = await Promise.all([
        firstValueFrom(this.mockData.getSites()),
        firstValueFrom(this.mockData.getCoordinators()),
        firstValueFrom(this.mockData.getNodes()),
        firstValueFrom(this.mockData.getZones()),
        firstValueFrom(this.mockData.getAlerts()),
        firstValueFrom(this.mockData.getSystemMetrics()),
      ]);

      this.sites.set(sites);
      this.coordinators.set(coordinators);
      this.nodes.set(nodes);
      this.zones.set(zones);
      this.alerts.set(alerts);
      this.systemMetrics.set(metrics);
      this.error.set(null);
    } catch (err) {
      this.error.set('Failed to load mock data');
      console.error('Failed to load mock data:', err);
    }
  }

  /**
   * Load all sites
   */
  loadSites(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getSites().subscribe({
        next: (data) => {
          this.sites.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load sites:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load site by ID
   */
  loadSite(siteId: string): Promise<Site> {
    return new Promise((resolve, reject) => {
      this.api.getSite(siteId).subscribe({
        next: (data) => {
          this.selectedSite.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load site:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load all coordinators
   */
  loadCoordinators(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getCoordinators().subscribe({
        next: (data) => {
          this.coordinators.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load coordinators:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load coordinator details
   */
  loadCoordinator(siteId: string, coordId: string): Promise<Coordinator> {
    return new Promise((resolve, reject) => {
      this.api.getCoordinator(siteId, coordId).subscribe({
        next: (data) => {
          this.selectedCoordinator.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load coordinator:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load coordinator by ID only (finds site automatically)
   */
  loadCoordinatorById(coordId: string): Promise<Coordinator> {
    return new Promise((resolve, reject) => {
      this.api.getCoordinatorById(coordId).subscribe({
        next: (data) => {
          this.selectedCoordinator.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load coordinator:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Clear selected coordinator
   */
  clearSelectedCoordinator(): void {
    this.selectedCoordinator.set(null);
  }

  /**
   * Load all nodes
   */
  loadNodes(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getNodes().subscribe({
        next: (data) => {
          this.nodes.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load nodes:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load nodes for a specific coordinator
   */
  loadNodesByCoordinator(siteId: string, coordId: string): Promise<Node[]> {
    return new Promise((resolve, reject) => {
      this.api.getNodes(siteId, coordId).subscribe({
        next: (data) => {
          // Update the nodes signal with these nodes
          const currentNodes = this.nodes();
          const otherNodes = currentNodes.filter(n => n.coordinator_id !== coordId);
          const nodeSummaries: NodeSummary[] = data.map(n => ({
            _id: n._id,
            light_id: n.light_id,
            name: n.name,
            status_mode: n.status_mode,
            temp_c: n.temp_c,
            vbat_mv: n.vbat_mv,
            avg_r: n.avg_r,
            coordinator_id: n.coordinator_id,
            zone_id: n.zone_id,
            last_seen: n.last_seen,
          }));
          this.nodes.set([...otherNodes, ...nodeSummaries]);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load nodes:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load node details
   */
  loadNode(nodeId: string): Promise<Node> {
    return new Promise((resolve, reject) => {
      this.api.getNode(nodeId).subscribe({
        next: (data) => {
          this.selectedNode.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load node:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load all zones (with mock data fallback)
   */
  loadZones(): Promise<void> {
    return new Promise((resolve, reject) => {
      // If already using mock data, load from mock service directly
      if (this.usingMockData()) {
        this.mockData.getZones().subscribe({
          next: (data) => {
            this.zones.set(data);
            resolve();
          },
          error: (err) => {
            console.error('Failed to load mock zones:', err);
            reject(err);
          }
        });
        return;
      }

      // Try real API first
      this.api.getZones().subscribe({
        next: (data) => {
          this.zones.set(data);
          resolve();
        },
        error: (err) => {
          console.warn('Failed to load zones from API, falling back to mock data:', err);
          // Fallback to mock data
          this.mockData.getZones().subscribe({
            next: (data) => {
              this.zones.set(data);
              this.usingMockData.set(true);
              resolve();
            },
            error: (mockErr) => {
              console.error('Failed to load mock zones:', mockErr);
              reject(mockErr);
            }
          });
        }
      });
    });
  }

  /**
   * Load zone by ID
   */
  loadZone(zoneId: string): Promise<Zone> {
    return new Promise((resolve, reject) => {
      this.api.getZone(zoneId).subscribe({
        next: (data) => {
          this.selectedZone.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load zone:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load alerts
   */
  loadAlerts(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getAlerts({ page: 1, pageSize: 50 }).subscribe({
        next: (data) => {
          this.alerts.set(data.items);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load alerts:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load active (unacknowledged) alerts - alias for loadAlerts
   */
  loadActiveAlerts(): Promise<void> {
    return this.loadAlerts();
  }

  /**
   * Check backend health
   */
  checkHealth(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getHealth().subscribe({
        next: (data) => {
          this.healthStatus.set(data);
          resolve();
        },
        error: (err) => {
          this.healthStatus.set(null);
          reject(err);
        }
      });
    });
  }

  // ============================================================================
  // Auto-Refresh
  // ============================================================================

  private refreshInterval$ = interval(30000); // 30 seconds

  /**
   * Start auto-refresh of dashboard data
   */
  startAutoRefresh(): void {
    this.refreshInterval$.pipe(
      takeUntil(this.destroy$),
      switchMap(() => {
        if (this.usingMockData()) {
          // Try to reconnect to real backend
          return this.api.getHealth().pipe(
            tap(() => {
              // Backend is back! Switch to real data
              this.usingMockData.set(false);
              this.loadDashboardData();
            }),
            catchError(() => {
              // Still offline, refresh mock data
              return this.mockData.getCoordinators();
            })
          );
        }
        return this.api.getCoordinators();
      }),
      tap(data => {
        if (Array.isArray(data)) {
          this.coordinators.set(data);
        }
      }),
      switchMap(() => this.usingMockData() ? this.mockData.getNodes() : this.api.getNodes()),
      tap(data => this.nodes.set(data)),
      switchMap(() => this.usingMockData() ? this.mockData.getAlerts() : this.api.getAlerts({ page: 1, pageSize: 50 })),
      tap(data => {
        if (Array.isArray(data)) {
          this.alerts.set(data);
        } else {
          this.alerts.set(data.items);
        }
      }),
      catchError(err => {
        console.error('Auto-refresh failed:', err);
        return of(null);
      })
    ).subscribe();
  }

  /**
   * Stop auto-refresh
   */
  stopAutoRefresh(): void {
    this.destroy$.next();
  }

  // ============================================================================
  // Telemetry Updates (called from WebSocket service)
  // ============================================================================

  /**
   * Update coordinator telemetry from WebSocket
   */
  updateCoordinatorTelemetry(coordId: string, data: Partial<Coordinator>): void {
    const coords = this.coordinators();
    const idx = coords.findIndex(c => c.coord_id === coordId || c._id === coordId);
    if (idx !== -1) {
      const updated = [...coords];
      updated[idx] = {
        ...updated[idx],
        ...data,
        last_seen: new Date(),
      };
      this.coordinators.set(updated);
    }
  }

  /**
   * Update node telemetry from WebSocket
   */
  updateNodeTelemetry(telemetry: NodeTelemetry): void {
    const current = this.nodeTelemetry();
    const updated = new Map(current);
    updated.set(telemetry.node_id, telemetry);
    this.nodeTelemetry.set(updated);

    // Update node summary if exists
    const nodesList = this.nodes();
    const idx = nodesList.findIndex(n => n.light_id === telemetry.node_id || n._id === telemetry.node_id);
    if (idx !== -1) {
      const updatedNodes = [...nodesList];
      updatedNodes[idx] = {
        ...updatedNodes[idx],
        temp_c: telemetry.temp_c,
        vbat_mv: telemetry.vbat_mv,
        last_seen: telemetry.timestamp,
      };
      this.nodes.set(updatedNodes);
    }
  }

  /**
   * Add new alert from WebSocket
   */
  addAlert(alert: Alert): void {
    const current = this.alerts();
    this.alerts.set([alert, ...current]);
  }

  /**
   * Update device status from WebSocket
   */
  updateDeviceStatus(deviceType: 'coordinator' | 'node', deviceId: string, status: string): void {
    if (deviceType === 'coordinator') {
      const coords = this.coordinators();
      const idx = coords.findIndex(c => c.coord_id === deviceId || c._id === deviceId);
      if (idx !== -1) {
        const updated = [...coords];
        updated[idx] = { ...updated[idx], status: status as any };
        this.coordinators.set(updated);
      }
    } else {
      const nodesList = this.nodes();
      const idx = nodesList.findIndex(n => n.light_id === deviceId || n._id === deviceId);
      if (idx !== -1) {
        const updated = [...nodesList];
        updated[idx] = { ...updated[idx], status_mode: status as any };
        this.nodes.set(updated);
      }
    }
  }

  // ============================================================================
  // Backward Compatibility - Tower methods (delegates to node methods)
  // ============================================================================

  /** @deprecated Use loadNodes instead */
  loadTowers(coordId?: string): Promise<void> {
    if (coordId) {
      // Find the coordinator to get site_id
      const coord = this.coordinators().find(c => c.coord_id === coordId || c._id === coordId);
      if (coord && coord.site_id) {
        return this.loadNodesByCoordinator(coord.site_id, coordId).then(() => {});
      }
    }
    return this.loadNodes();
  }

  /** @deprecated Use loadNode instead */
  loadTower(towerId: string): Promise<Node> {
    return this.loadNode(towerId);
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
