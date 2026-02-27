import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Subscription, interval, switchMap, tap, catchError, of, Subject, takeUntil, firstValueFrom, forkJoin } from 'rxjs';
import { ApiService } from './api.service';
import { EnvironmentService } from './environment.service';
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
import { TowerTwin } from '../models/digital-twin.model';

/**
 * IoT Data Service - Smart Tile System
 * Centralized state management using Angular signals
 * Provides reactive data access and automatic refresh
 */
@Injectable({
  providedIn: 'root'
})
export class IoTDataService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly env = inject(EnvironmentService);
  private readonly destroy$ = new Subject<void>();
  private autoRefreshSub: Subscription | null = null;

  // ============================================================================
  // State Signals
  // ============================================================================

  // Loading states
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

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

  // Nodes / Towers (NodeSummary serves both smart tiles and hydroponic towers via NodesController shim)
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
    this.coordinators().filter(c =>
      c.status === 'online' || (c as any).status_mode === 'operational'
    ).length
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

  // Tower aliases — point to nodes signal (NodesController returns Tower objects)
  readonly towers = this.nodes;
  readonly onlineTowerCount = this.onlineNodeCount;
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
      await this.checkHealth();

      // Load coordinators first — loadNodes may need them for the twin fallback
      await this.loadCoordinators().catch(err => {
        console.warn('Failed to load coordinators:', err);
        this.coordinators.set([]);
      });

      await Promise.all([
        this.loadNodes().catch(err => {
          console.warn('Failed to load nodes:', err);
          this.nodes.set([]);
        }),
        this.loadAlerts().catch(err => {
          console.warn('Failed to load alerts:', err);
          this.alerts.set([]);
        }),
        this.loadSites().catch(err => {
          console.warn('Failed to load farms:', err);
          this.sites.set([]);
        }),
        this.loadZones().catch(err => {
          console.warn('Failed to load zones:', err);
          this.zones.set([]);
        }),
      ]);
    } catch (err) {
      console.error('Backend unavailable:', err);
      this.error.set('Backend unavailable. Please check if the server is running.');
    } finally {
      this.isLoading.set(false);
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
   * Load all nodes — falls back to tower digital twins when the nodes
   * collection is empty (e.g. simulator creates towers via telemetry
   * rather than the pairing flow).
   */
  loadNodes(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getNodes().subscribe({
        next: (data) => {
          if (data.length > 0) {
            this.nodes.set(data);
            resolve();
          } else {
            // Nodes collection empty — build list from tower twins
            this.loadNodesFromTwins().then(resolve).catch(reject);
          }
        },
        error: (err) => {
          console.error('Failed to load nodes:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Build NodeSummary[] from tower digital twins for every known coordinator.
   */
  private loadNodesFromTwins(): Promise<void> {
    const coords = this.coordinators();
    if (coords.length === 0) {
      return Promise.resolve();
    }

    // The snakeCaseInterceptor converts response keys from snake_case to camelCase,
    // so runtime objects have siteId/coordId/farmId despite the TypeScript interfaces
    // still declaring snake_case names (site_id/coord_id/farm_id).
    const twinRequests = coords.map(c => {
      const a = c as any;
      const farmId = a.siteId || a.site_id || a.farmId || a.farm_id || '';
      const coordId = a.coordId || a.coord_id || '';
      return this.api.getTowerTwins(farmId, coordId).pipe(
        catchError(() => of([] as TowerTwin[]))
      );
    });

    return new Promise((resolve) => {
      forkJoin(twinRequests).subscribe({
        next: (results) => {
          const nodes: NodeSummary[] = results.flat().map(twin => {
            // API may return camelCase or snake_case — handle both via any
            const t = twin as any;
            const rep = t.reported ?? {};
            const meta = t.metadata ?? {};
            return {
              _id: t._id || t.towerId || t.tower_id,
              light_id: t.towerId || t.tower_id,
              name: t.name || t.towerId || t.tower_id,
              status_mode: rep.statusMode ?? rep.status_mode ?? 'operational',
              temp_c: rep.airTempC ?? rep.air_temp_c ?? 0,
              vbat_mv: rep.vbatMv ?? rep.vbat_mv ?? 0,
              avg_r: rep.signalQuality ?? rep.signal_quality ?? 0,
              coordinator_id: t.coordId || t.coord_id,
              last_seen: new Date(meta.lastReportedAt || meta.last_reported_at || meta.updatedAt || meta.updated_at || Date.now()),
            };
          });
          this.nodes.set(nodes);
          resolve();
        },
        error: () => resolve()
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
   * Load all zones
   */
  loadZones(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getZones().subscribe({
        next: (data) => {
          this.zones.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load zones:', err);
          reject(err);
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
        next: (data: any) => {
          // Backend returns { data: [...], page, page_size, total_count, total_pages }
          // PaginatedResponse interface expects { items: [...] }
          const alerts = data.items ?? data.data ?? [];
          this.alerts.set(Array.isArray(alerts) ? alerts : []);
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
    this.stopAutoRefresh();
    this.autoRefreshSub = this.refreshInterval$.pipe(
      switchMap(() => this.api.getCoordinators()),
      tap(data => {
        if (Array.isArray(data)) {
          this.coordinators.set(data);
        }
      }),
      switchMap(() => this.api.getNodes()),
      tap(data => {
        if (data.length > 0) {
          this.nodes.set(data);
        }
        // If nodes API returns empty, keep existing twin-based nodes (don't overwrite)
      }),
      switchMap(() => this.api.getAlerts({ page: 1, pageSize: 50 })),
      tap((data: any) => {
        if (Array.isArray(data)) {
          this.alerts.set(data);
        } else {
          const alerts = data.items ?? data.data ?? [];
          this.alerts.set(Array.isArray(alerts) ? alerts : []);
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
    this.autoRefreshSub?.unsubscribe();
    this.autoRefreshSub = null;
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
   * Update coordinator metadata (name, description, location, etc.)
   */
  async updateCoordinator(coordId: string, updates: Partial<{
    name?: string;
    description?: string;
    location?: string;
    tags?: string[];
    color?: string;
  }>): Promise<void> {
    try {
      // Call backend API to update coordinator using Angular HttpClient
      // (ensures snakeCaseInterceptor processes the request)
      await firstValueFrom(
        this.http.patch(
          `${this.env.apiUrl}/api/coordinators/${encodeURIComponent(coordId)}`,
          updates
        )
      );

      // Update local state
      const coords = this.coordinators();
      const idx = coords.findIndex(c => c._id === coordId || c.coord_id === coordId);
      if (idx !== -1) {
        const updated = [...coords];
        updated[idx] = {
          ...updated[idx],
          ...updates,
        };
        this.coordinators.set(updated);
      }
    } catch (error) {
      console.error('[IoTDataService] Failed to update coordinator:', error);
      throw error;
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
  // Tower convenience methods (delegate to node methods — NodesController returns Tower objects)
  // ============================================================================

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

  loadTower(towerId: string): Promise<Node> {
    return this.loadNode(towerId);
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.stopAutoRefresh();
    this.destroy$.next();
    this.destroy$.complete();
  }
}
