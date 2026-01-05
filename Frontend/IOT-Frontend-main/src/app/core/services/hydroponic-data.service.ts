import { Injectable, inject, signal, computed } from '@angular/core';
import { Subject } from 'rxjs';
import { IoTDataService } from './iot-data.service';
import {
  Coordinator,
  CoordinatorSummary,
  Node,
  NodeSummary,
  NodeTelemetry,
  Zone,
  Alert,
  HealthStatus,
} from '../models';

/**
 * Hydroponic Data Service
 * @deprecated Use IoTDataService instead. This service is kept for backward compatibility.
 * 
 * This is a thin wrapper around IoTDataService that provides the old API
 * for components that haven't been migrated yet.
 */
@Injectable({
  providedIn: 'root'
})
export class HydroponicDataService {
  private readonly iotService = inject(IoTDataService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals - Delegated to IoTDataService
  // ============================================================================

  // Loading states
  readonly isLoading = this.iotService.isLoading;
  readonly error = this.iotService.error;

  // Health
  readonly healthStatus = this.iotService.healthStatus;

  // Coordinators - mapped from IoTDataService
  readonly coordinators = this.iotService.coordinators;
  readonly selectedCoordinator = this.iotService.selectedCoordinator;

  // Towers/Nodes - mapped for backward compatibility
  /** @deprecated Use nodes from IoTDataService */
  readonly towers = this.iotService.nodes;
  /** @deprecated Use selectedNode from IoTDataService */
  readonly selectedTower = this.iotService.selectedNode;

  // For legacy code expecting TowerSummary with certain fields
  readonly towerSummaries = computed(() => {
    return this.iotService.nodes().map(n => ({
      _id: n._id,
      towerId: n.light_id,
      name: n.name || `Node ${n.light_id}`,
      coordId: n.coordinator_id,
      status: this.mapNodeStatusToTowerStatus(n.status_mode),
      occupiedSlots: 0,
      slotCount: 0,
      sensors: {
        ambientTemp: n.temp_c ?? 0,
        humidity: 0,
        lightLevel: 0,
      }
    }));
  });

  // Telemetry - simplified versions for backward compat
  readonly coordinatorTelemetry = signal<Map<string, any>>(new Map());
  readonly towerTelemetry = signal<Map<string, any>>(new Map());

  // Alerts
  readonly alerts = this.iotService.alerts;
  readonly unacknowledgedAlertCount = this.iotService.unacknowledgedAlertCount;
  readonly criticalAlertCount = this.iotService.criticalAlertCount;
  readonly activeAlerts = this.iotService.activeAlerts;

  // Loading alias
  readonly loading = this.isLoading;

  // Farm metrics (stub for backward compat)
  readonly farmMetrics = signal<any>(null);

  // ============================================================================
  // Computed Signals
  // ============================================================================

  readonly onlineCoordinatorCount = this.iotService.onlineCoordinatorCount;
  readonly totalCoordinatorCount = this.iotService.totalCoordinatorCount;
  readonly onlineTowerCount = this.iotService.onlineNodeCount;
  readonly totalTowerCount = this.iotService.totalNodeCount;

  readonly totalOccupiedSlots = computed(() => 0);
  readonly totalSlots = computed(() => 0);

  readonly averagePh = computed(() => 0);
  readonly averageEc = computed(() => 0);

  // ============================================================================
  // Data Loading Methods - Delegated to IoTDataService
  // ============================================================================

  async loadDashboardData(): Promise<void> {
    return this.iotService.loadDashboardData();
  }

  loadCoordinators(): Promise<void> {
    return this.iotService.loadCoordinators();
  }

  loadCoordinator(coordId: string): Promise<Coordinator> {
    return this.iotService.loadCoordinatorById(coordId);
  }

  loadCoordinatorById(coordId: string): Promise<Coordinator> {
    return this.iotService.loadCoordinatorById(coordId);
  }

  clearSelectedCoordinator(): void {
    this.iotService.clearSelectedCoordinator();
  }

  /** @deprecated Use loadNodes from IoTDataService */
  loadTowers(coordId?: string): Promise<void> {
    return this.iotService.loadTowers(coordId);
  }

  /** @deprecated Use loadNode from IoTDataService */
  loadTower(towerId: string): Promise<Node> {
    return this.iotService.loadTower(towerId);
  }

  loadFarmMetrics(): Promise<void> {
    // No-op for backward compat - Smart Tile system doesn't have farm metrics
    return Promise.resolve();
  }

  loadAlerts(): Promise<void> {
    return this.iotService.loadAlerts();
  }

  loadActiveAlerts(): Promise<void> {
    return this.iotService.loadActiveAlerts();
  }

  checkHealth(): Promise<void> {
    return this.iotService.checkHealth();
  }

  // ============================================================================
  // Auto-Refresh - Delegated
  // ============================================================================

  startAutoRefresh(): void {
    this.iotService.startAutoRefresh();
  }

  stopAutoRefresh(): void {
    this.iotService.stopAutoRefresh();
  }

  // ============================================================================
  // Telemetry Updates - Adapted for legacy interfaces
  // ============================================================================

  updateCoordinatorTelemetry(telemetry: any): void {
    const coordId = telemetry.coordId || telemetry.coord_id;
    if (coordId) {
      this.iotService.updateCoordinatorTelemetry(coordId, telemetry);
    }
  }

  updateTowerTelemetry(telemetry: any): void {
    const nodeId = telemetry.towerId || telemetry.node_id;
    if (nodeId) {
      this.iotService.updateNodeTelemetry({
        node_id: nodeId,
        light_id: nodeId,
        temp_c: telemetry.ambientTemp ?? telemetry.temp_c ?? 0,
        vbat_mv: telemetry.vbat_mv ?? 0,
        avg_r: telemetry.rssi_dbm ?? telemetry.avg_r ?? 0,
        rssi_dbm: telemetry.rssi_dbm ?? 0,
        light_lux: telemetry.lightLevel ?? telemetry.light_lux ?? 0,
        timestamp: telemetry.timestamp || new Date(),
        status_mode: telemetry.status_mode || 'operational',
      });
    }
  }

  addAlert(alert: Alert): void {
    this.iotService.addAlert(alert);
  }

  updateDeviceStatus(deviceType: 'coordinator' | 'tower', deviceId: string, status: string): void {
    const mappedType = deviceType === 'tower' ? 'node' : deviceType;
    this.iotService.updateDeviceStatus(mappedType, deviceId, status);
  }

  // ============================================================================
  // Helper Methods
  // ============================================================================

  private mapNodeStatusToTowerStatus(nodeStatus: string): string {
    switch (nodeStatus) {
      case 'operational': return 'online';
      case 'pairing': return 'pairing';
      case 'error': return 'error';
      case 'offline': return 'offline';
      default: return 'unknown';
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
