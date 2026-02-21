import { Injectable, inject, signal, computed } from '@angular/core';
import { Subject, takeUntil, firstValueFrom, catchError, of } from 'rxjs';
import { ApiService } from './api.service';
import { WebSocketService } from './websocket.service';
import {
  TowerTwin,
  CoordinatorTwin,
  FarmTwinsResponse,
  TowerDesiredState,
  CoordinatorDesiredState,
  TowerDeltaResponse,
  CoordinatorDeltaResponse,
  TwinUpdatePayload,
} from '../models/digital-twin.model';

/**
 * Twin Service - Digital Twin State Management
 *
 * Provides signal-based reactive state for coordinator and tower twins.
 * Fetches data from /api/twins/* REST endpoints and subscribes to
 * real-time WebSocket updates.
 *
 * All property access uses camelCase. The snakeCaseInterceptor handles
 * the snake_case <-> camelCase conversion for REST API calls.
 *
 * Usage:
 *   readonly twinService = inject(TwinService);
 *   // Load twins for a farm:
 *   await twinService.loadFarmTwins('farm-1');
 *   // Access reactive state:
 *   twinService.towerTwins()    // Signal<TowerTwin[]>
 *   twinService.coordTwins()    // Signal<CoordinatorTwin[]>
 */
@Injectable({
  providedIn: 'root'
})
export class TwinService {
  private readonly api = inject(ApiService);
  private readonly ws = inject(WebSocketService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals
  // ============================================================================

  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentFarmId = signal<string | null>(null);

  // Twin collections
  readonly towerTwins = signal<TowerTwin[]>([]);
  readonly coordTwins = signal<CoordinatorTwin[]>([]);

  // Selected twins (for detail views)
  readonly selectedTower = signal<TowerTwin | null>(null);
  readonly selectedCoordinator = signal<CoordinatorTwin | null>(null);

  // ============================================================================
  // Computed Signals
  // ============================================================================

  /** Total number of towers across all coordinators */
  readonly totalTowers = computed(() => this.towerTwins().length);

  /** Total number of coordinators */
  readonly totalCoordinators = computed(() => this.coordTwins().length);

  /** Towers grouped by coordinator ID */
  readonly towersByCoordinator = computed(() => {
    const map = new Map<string, TowerTwin[]>();
    for (const tower of this.towerTwins()) {
      const list = map.get(tower.coordId) || [];
      list.push(tower);
      map.set(tower.coordId, list);
    }
    return map;
  });

  /** Number of connected towers (metadata.isConnected) */
  readonly connectedTowers = computed(() =>
    this.towerTwins().filter(t => t.metadata?.isConnected).length
  );

  /** Number of connected coordinators */
  readonly connectedCoordinators = computed(() =>
    this.coordTwins().filter(c => c.metadata?.isConnected).length
  );

  /** Number of twins with pending sync */
  readonly pendingSyncCount = computed(() =>
    this.towerTwins().filter(t => t.metadata?.syncStatus === 'pending').length +
    this.coordTwins().filter(c => c.metadata?.syncStatus === 'pending').length
  );

  /**
   * Average health score across all towers with ML predictions.
   * ML service returns 0-1 scale values. We normalise to 0-100 for display.
   * Returns an integer like 85, or null if no predictions exist.
   */
  readonly averageHealthScore = computed(() => {
    const towers = this.towerTwins().filter(t => t.mlPredictions?.healthScore != null);
    if (towers.length === 0) return null;
    const sum = towers.reduce((acc, t) => {
      const raw = t.mlPredictions!.healthScore ?? 0;
      return acc + (raw <= 1 ? raw * 100 : raw);
    }, 0);
    return Math.round(sum / towers.length);
  });

  constructor() {
    this.subscribeToWebSocket();
  }

  // ============================================================================
  // Data Loading
  // ============================================================================

  /**
   * Load all twins for a farm (coordinators + towers in one call).
   */
  async loadFarmTwins(farmId: string): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);
    this.currentFarmId.set(farmId);

    try {
      const response = await firstValueFrom(
        this.api.getFarmTwins(farmId).pipe(
          catchError(err => {
            console.error('Failed to load farm twins:', err);
            this.error.set('Failed to load digital twins');
            return of(null);
          })
        )
      );

      if (response) {
        this.coordTwins.set(response.coordinators ?? []);
        this.towerTwins.set(response.towers ?? []);
      }
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load towers for a specific coordinator.
   */
  async loadTowerTwins(farmId: string, coordId: string): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      const towers = await firstValueFrom(
        this.api.getTowerTwins(farmId, coordId).pipe(
          catchError(err => {
            console.error('Failed to load tower twins:', err);
            this.error.set('Failed to load tower twins');
            return of([]);
          })
        )
      );

      this.towerTwins.set(towers);
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load a single tower twin and set it as selected.
   */
  async selectTower(towerId: string): Promise<TowerTwin | null> {
    try {
      const tower = await firstValueFrom(
        this.api.getTowerTwin(towerId).pipe(
          catchError(() => of(null))
        )
      );
      this.selectedTower.set(tower);
      return tower;
    } catch {
      return null;
    }
  }

  /**
   * Load a single coordinator twin and set it as selected.
   */
  async selectCoordinator(coordId: string): Promise<CoordinatorTwin | null> {
    try {
      const coord = await firstValueFrom(
        this.api.getCoordinatorTwin(coordId).pipe(
          catchError(() => of(null))
        )
      );
      this.selectedCoordinator.set(coord);
      return coord;
    } catch {
      return null;
    }
  }

  // ============================================================================
  // Commands (desired state)
  // ============================================================================

  /**
   * Set desired state for a tower (e.g., toggle pump, set light brightness).
   */
  async setTowerDesired(towerId: string, desired: Partial<TowerDesiredState>): Promise<boolean> {
    try {
      await firstValueFrom(this.api.setTowerDesiredState(towerId, desired));
      // Optimistic update: mark the tower as pending sync
      this.towerTwins.update(towers =>
        towers.map(t => t.towerId === towerId
          ? { ...t, desired: { ...t.desired, ...desired }, metadata: { ...t.metadata, syncStatus: 'pending' as const } }
          : t
        )
      );
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Set desired state for a coordinator (e.g., toggle pumps, update setpoints).
   */
  async setCoordinatorDesired(coordId: string, desired: Partial<CoordinatorDesiredState>): Promise<boolean> {
    try {
      await firstValueFrom(this.api.setCoordinatorDesiredState(coordId, desired));
      // Optimistic update
      this.coordTwins.update(coords =>
        coords.map(c => c.coordId === coordId
          ? { ...c, desired: { ...c.desired, ...desired }, metadata: { ...c.metadata, syncStatus: 'pending' as const } }
          : c
        )
      );
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Get the desired vs reported delta for a tower.
   */
  async getTowerDelta(towerId: string): Promise<TowerDeltaResponse | null> {
    try {
      return await firstValueFrom(
        this.api.getTowerDelta(towerId).pipe(catchError(() => of(null)))
      );
    } catch {
      return null;
    }
  }

  /**
   * Get the desired vs reported delta for a coordinator.
   */
  async getCoordinatorDelta(coordId: string): Promise<CoordinatorDeltaResponse | null> {
    try {
      return await firstValueFrom(
        this.api.getCoordinatorDelta(coordId).pipe(catchError(() => of(null)))
      );
    } catch {
      return null;
    }
  }

  // ============================================================================
  // WebSocket Real-Time Updates
  // NOTE: WebSocket payloads already use camelCase (WsBroadcaster uses CamelCase policy)
  // ============================================================================

  private subscribeToWebSocket(): void {
    this.ws.digitalTwinUpdates$
      .pipe(takeUntil(this.destroy$))
      .subscribe(payload => this.handleTwinUpdate(payload as TwinUpdatePayload));
  }

  private handleTwinUpdate(payload: TwinUpdatePayload): void {
    if (!payload?.changeType || !payload?.deviceId) return;

    switch (payload.changeType) {
      case 'TowerTelemetry':
        this.updateTowerReported(payload.deviceId, payload.towerReported);
        break;

      case 'CoordinatorTelemetry':
        this.updateCoordinatorReported(payload.deviceId, payload.coordinatorReported);
        break;

      case 'TowerPaired':
      case 'TowerUpsert':
        if (payload.towerTwin) {
          this.upsertTower(payload.towerTwin);
        }
        break;

      case 'TowerRemoved':
        this.towerTwins.update(towers => towers.filter(t => t.towerId !== payload.deviceId));
        break;

      case 'CoordinatorRegistered':
      case 'CoordinatorUpsert':
        if (payload.coordinatorTwin) {
          this.upsertCoordinator(payload.coordinatorTwin);
        }
        break;

      case 'CoordinatorRemoved':
        this.coordTwins.update(coords => coords.filter(c => c.coordId !== payload.deviceId));
        break;

      case 'TowerDesiredStateChanged':
      case 'CoordinatorDesiredStateChanged':
        // Refresh from API to get the latest state
        if (this.currentFarmId()) {
          this.loadFarmTwins(this.currentFarmId()!);
        }
        break;
    }
  }

  private updateTowerReported(towerId: string, reported: any): void {
    if (!reported) return;
    this.towerTwins.update(towers =>
      towers.map(t => t.towerId === towerId
        ? { ...t, reported: { ...t.reported, ...reported } }
        : t
      )
    );
  }

  private updateCoordinatorReported(coordId: string, reported: any): void {
    if (!reported) return;
    this.coordTwins.update(coords =>
      coords.map(c => c.coordId === coordId
        ? { ...c, reported: { ...c.reported, ...reported } }
        : c
      )
    );
  }

  private upsertTower(tower: TowerTwin): void {
    this.towerTwins.update(towers => {
      const idx = towers.findIndex(t => t.towerId === tower.towerId);
      if (idx >= 0) {
        const updated = [...towers];
        updated[idx] = tower;
        return updated;
      }
      return [...towers, tower];
    });
  }

  private upsertCoordinator(coord: CoordinatorTwin): void {
    this.coordTwins.update(coords => {
      const idx = coords.findIndex(c => c.coordId === coord.coordId);
      if (idx >= 0) {
        const updated = [...coords];
        updated[idx] = coord;
        return updated;
      }
      return [...coords, coord];
    });
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  clearSelection(): void {
    this.selectedTower.set(null);
    this.selectedCoordinator.set(null);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
