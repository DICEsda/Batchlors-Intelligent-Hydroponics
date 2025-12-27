import { Injectable, inject, signal, computed } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { interval, switchMap, tap, catchError, of, Subject, takeUntil } from 'rxjs';
import { ApiService } from './api.service';
import {
  Coordinator,
  CoordinatorSummary,
  Tower,
  TowerSummary,
  ReservoirTelemetry,
  TowerTelemetry,
  FarmMetrics,
  Alert,
  HealthStatus,
} from '../models';

/**
 * Hydroponic Data Service
 * Centralized state management using Angular signals
 * Provides reactive data access and automatic refresh
 */
@Injectable({
  providedIn: 'root'
})
export class HydroponicDataService {
  private readonly api = inject(ApiService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals
  // ============================================================================

  // Loading states
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  // Health
  readonly healthStatus = signal<HealthStatus | null>(null);
  
  // Farm metrics
  readonly farmMetrics = signal<FarmMetrics | null>(null);

  // Coordinators
  readonly coordinators = signal<CoordinatorSummary[]>([]);
  readonly selectedCoordinator = signal<Coordinator | null>(null);
  readonly coordinatorTelemetry = signal<Map<string, ReservoirTelemetry>>(new Map());

  // Towers
  readonly towers = signal<TowerSummary[]>([]);
  readonly selectedTower = signal<Tower | null>(null);
  readonly towerTelemetry = signal<Map<string, TowerTelemetry>>(new Map());

  // Alerts
  readonly alerts = signal<Alert[]>([]);
  readonly unacknowledgedAlertCount = computed(() => 
    this.alerts().filter(a => !a.acknowledged).length
  );
  readonly criticalAlertCount = computed(() =>
    this.alerts().filter(a => a.severity === 'critical' && !a.acknowledged).length
  );
  
  // Active alerts (unacknowledged) - alias for backward compatibility
  readonly activeAlerts = computed(() =>
    this.alerts().filter(a => !a.acknowledged)
  );
  
  // Loading alias for backward compatibility
  readonly loading = this.isLoading;

  // ============================================================================
  // Computed Signals
  // ============================================================================

  readonly onlineCoordinatorCount = computed(() => 
    this.coordinators().filter(c => c.status === 'online').length
  );

  readonly totalCoordinatorCount = computed(() => 
    this.coordinators().length
  );

  readonly onlineTowerCount = computed(() => 
    this.towers().filter(t => t.status === 'online').length
  );

  readonly totalTowerCount = computed(() => 
    this.towers().length
  );

  readonly totalOccupiedSlots = computed(() =>
    this.towers().reduce((sum, t) => sum + t.occupiedSlots, 0)
  );

  readonly totalSlots = computed(() =>
    this.towers().reduce((sum, t) => sum + t.slotCount, 0)
  );

  readonly averagePh = computed(() => {
    const coords = this.coordinators();
    if (coords.length === 0) return 0;
    return coords.reduce((sum, c) => sum + c.reservoir.ph, 0) / coords.length;
  });

  readonly averageEc = computed(() => {
    const coords = this.coordinators();
    if (coords.length === 0) return 0;
    return coords.reduce((sum, c) => sum + c.reservoir.ec, 0) / coords.length;
  });

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
      await Promise.all([
        this.loadCoordinators(),
        this.loadTowers(),
        this.loadFarmMetrics(),
        this.loadAlerts(),
      ]);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load dashboard data');
    } finally {
      this.isLoading.set(false);
    }
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
  loadCoordinator(coordId: string): Promise<Coordinator> {
    return new Promise((resolve, reject) => {
      this.api.getCoordinator(coordId).subscribe({
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
   * Load coordinator details by ID (alias for loadCoordinator)
   */
  loadCoordinatorById(coordId: string): Promise<Coordinator> {
    return this.loadCoordinator(coordId);
  }

  /**
   * Clear selected coordinator
   */
  clearSelectedCoordinator(): void {
    this.selectedCoordinator.set(null);
  }

  /**
   * Load all towers
   */
  loadTowers(coordId?: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getTowers(coordId).subscribe({
        next: (data) => {
          this.towers.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load towers:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load tower details
   */
  loadTower(towerId: string): Promise<Tower> {
    return new Promise((resolve, reject) => {
      this.api.getTower(towerId).subscribe({
        next: (data) => {
          this.selectedTower.set(data);
          resolve(data);
        },
        error: (err) => {
          console.error('Failed to load tower:', err);
          reject(err);
        }
      });
    });
  }

  /**
   * Load farm-wide metrics
   */
  loadFarmMetrics(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.getFarmMetrics().subscribe({
        next: (data) => {
          this.farmMetrics.set(data);
          resolve();
        },
        error: (err) => {
          console.error('Failed to load farm metrics:', err);
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
      switchMap(() => this.api.getCoordinators()),
      tap(data => this.coordinators.set(data)),
      switchMap(() => this.api.getTowers()),
      tap(data => this.towers.set(data)),
      switchMap(() => this.api.getAlerts({ page: 1, pageSize: 50 })),
      tap(data => this.alerts.set(data.items)),
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
  updateCoordinatorTelemetry(telemetry: ReservoirTelemetry): void {
    const current = this.coordinatorTelemetry();
    const updated = new Map(current);
    updated.set(telemetry.coordId, telemetry);
    this.coordinatorTelemetry.set(updated);

    // Update coordinator summary if exists
    const coords = this.coordinators();
    const idx = coords.findIndex(c => c.coordId === telemetry.coordId);
    if (idx !== -1) {
      const updated = [...coords];
      updated[idx] = {
        ...updated[idx],
        reservoir: {
          ph: telemetry.ph,
          ec: telemetry.ec,
          temperature: telemetry.temperature,
          waterLevel: telemetry.waterLevel,
        }
      };
      this.coordinators.set(updated);
    }
  }

  /**
   * Update tower telemetry from WebSocket
   */
  updateTowerTelemetry(telemetry: TowerTelemetry): void {
    const current = this.towerTelemetry();
    const updated = new Map(current);
    updated.set(telemetry.towerId, telemetry);
    this.towerTelemetry.set(updated);

    // Update tower summary if exists
    const twrs = this.towers();
    const idx = twrs.findIndex(t => t.towerId === telemetry.towerId);
    if (idx !== -1) {
      const updated = [...twrs];
      updated[idx] = {
        ...updated[idx],
        sensors: {
          ambientTemp: telemetry.ambientTemp,
          humidity: telemetry.humidity,
          lightLevel: telemetry.lightLevel,
        }
      };
      this.towers.set(updated);
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
  updateDeviceStatus(deviceType: 'coordinator' | 'tower', deviceId: string, status: string): void {
    if (deviceType === 'coordinator') {
      const coords = this.coordinators();
      const idx = coords.findIndex(c => c.coordId === deviceId);
      if (idx !== -1) {
        const updated = [...coords];
        updated[idx] = { ...updated[idx], status: status as any };
        this.coordinators.set(updated);
      }
    } else {
      const twrs = this.towers();
      const idx = twrs.findIndex(t => t.towerId === deviceId);
      if (idx !== -1) {
        const updated = [...twrs];
        updated[idx] = { ...updated[idx], status: status as any };
        this.towers.set(updated);
      }
    }
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
