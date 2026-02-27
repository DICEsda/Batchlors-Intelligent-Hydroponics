import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, catchError, retry, timeout, of } from 'rxjs';
import { EnvironmentService } from './environment.service';
import {
  // Site
  Site,
  // Farm
  Farm,
  // Coordinator
  Coordinator,
  CoordinatorSummary,
  CoordinatorRestartCommand,
  CoordinatorWifiCommand,
  // Node
  Node,
  NodeSummary,
  LedControlCommand,
  TestColorCommand,
  BrightnessCommand,
  NodeNameUpdate,
  NodeZoneUpdate,
  // Zone
  Zone,
  CreateZoneRequest,
  UpdateZoneRequest,
  // Telemetry
  CoordinatorTelemetryData,
  NodeTelemetryData,
  CoordinatorHistory,
  NodeHistory,
  SystemMetrics,
  Alert,
  TimeRange,
  // OTA
  FirmwareVersion,
  OtaJob,
  OtaCampaign,
  OtaStatistics,
  StartOtaRequest,
  CreateCampaignRequest,
  DeviceFirmwareStatus,
  // Predictions
  HeightPrediction,
  GrowthAnalysis,
  TowerPredictions,
  FarmPredictionSummary,
  GrowthAnomaly,
  PredictionRequest,
  BatchPredictionRequest,
  MLModelInfo,
  // Digital Twin (REST API contract types)
  FarmTwinsResponse,
  TowerTwin,
  CoordinatorTwin,
  TowerDesiredState,
  CoordinatorDesiredState,
  TowerDeltaResponse,
  CoordinatorDeltaResponse,
  // Pairing
  PairingSession,
  TowerPairingRequest,
  PairedTower,
  // Common
  ApiResponse,
  PaginatedResponse,
  HealthStatus,
  MlHealthStatus,
  PaginationParams,
  // Coordinator Registration
  ApproveCoordinatorRequest,
} from '../models';

/**
 * Settings interface for Smart Tile system
 */
export interface SmartTileSettings {
  brightness_default: number;
  motion_sensitivity: number;
  occupancy_timeout_sec: number;
  ambient_light_threshold: number;
  [key: string]: any;
}

/**
 * API Service for Smart Tile IoT System
 * Handles all HTTP communication with the backend API
 */
@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(EnvironmentService);
  
  private get baseUrl(): string {
    return this.env.apiUrl;
  }

  // ============================================================================
  // Health & System
  // ============================================================================

  getHealth(): Observable<HealthStatus> {
    return this.get<HealthStatus>('/health');
  }

  getMlHealth(): Observable<MlHealthStatus> {
    return this.get<MlHealthStatus>('/api/ml/health');
  }

  getSystemMetrics(): Observable<SystemMetrics> {
    return this.get<SystemMetrics>('/api/metrics');
  }

  // ============================================================================
  // Sites API — redirected to /api/farms (legacy "sites" concept = farms)
  // ============================================================================

  getSites(): Observable<Site[]> {
    return this.get<Site[]>('/api/farms');
  }

  getSite(siteId: string): Observable<Site> {
    return this.get<Site>(`/api/farms/${siteId}`);
  }

  // ============================================================================
  // Coordinators API
  // ============================================================================

  /**
   * Get all coordinators (aggregated from all sites)
   * Note: Backend doesn't have a direct /coordinators endpoint for list,
   * so this may need to aggregate from sites or use a custom endpoint
   */
  getCoordinators(): Observable<CoordinatorSummary[]> {
    // The backend has GET /coordinators/{id} for single coordinator
    // For list, we need to go through sites
    return this.get<CoordinatorSummary[]>('/api/coordinators');
  }

  /**
   * Get coordinator by site and coordinator ID
   * (Legacy: /api/sites/{siteId}/coordinators/{coordId} no longer exists,
   *  use the flat coordinators endpoint instead)
   */
  getCoordinator(siteId: string, coordId: string): Observable<Coordinator> {
    return this.get<Coordinator>(`/api/coordinators/${coordId}`);
  }

  /**
   * Get coordinator by ID only (when site is unknown)
   */
  getCoordinatorById(coordId: string): Observable<Coordinator> {
    return this.get<Coordinator>(`/api/coordinators/${coordId}`);
  }

  // ============================================================================
  // Pairing API
  // ============================================================================

  /**
   * Start pairing mode on coordinator.
   * Puts the coordinator into pairing mode for the specified duration.
   */
  startPairing(farmId: string, coordId: string, durationSeconds = 60): Observable<PairingSession> {
    return this.post<PairingSession>('/api/pairing/start', {
      farmId,
      coordId,
      durationSeconds
    });
  }

  /**
   * Stop an active pairing session.
   * Exits the coordinator from pairing mode.
   */
  stopPairing(farmId: string, coordId: string): Observable<PairingSession> {
    return this.post<PairingSession>('/api/pairing/stop', {
      farmId,
      coordId
    });
  }

  /**
   * Get the active pairing session for a coordinator.
   */
  getPairingSession(farmId: string, coordId: string): Observable<PairingSession | null> {
    return this.get<PairingSession>(`/api/pairing/session/${farmId}/${coordId}`).pipe(
      catchError((err) => {
        // 404 means no active session, return null
        if (err.message?.includes('404')) {
          return of(null);
        }
        throw err;
      })
    );
  }

  /**
   * Get all pending tower pairing requests for a coordinator.
   */
  getPendingPairingRequests(farmId: string, coordId: string): Observable<TowerPairingRequest[]> {
    return this.get<TowerPairingRequest[]>(`/api/pairing/requests/${farmId}/${coordId}`);
  }

  /**
   * Approve a pending tower pairing request.
   * Creates the tower entity and sends approval to the coordinator.
   */
  approveTower(farmId: string, coordId: string, towerId: string): Observable<PairedTower> {
    return this.post<PairedTower>('/api/pairing/approve', {
      farmId,
      coordId,
      towerId
    });
  }

  /**
   * Reject a pending tower pairing request.
   * Sends rejection to the coordinator - tower will go to idle state.
   */
  rejectTower(farmId: string, coordId: string, towerId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/pairing/reject', {
      farmId,
      coordId,
      towerId
    });
  }

  /**
   * Forget a paired device.
   * Removes the tower from the backend and sends a command to the coordinator
   * to notify the tower to wipe its credentials.
   */
  forgetDevice(farmId: string, coordId: string, towerId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/pairing/forget', {
      farmId,
      coordId,
      towerId
    });
  }

  /**
   * Restart coordinator
   */
  restartCoordinator(command: CoordinatorRestartCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/coordinator/restart', command);
  }

  /**
   * Update coordinator WiFi settings
   */
  updateCoordinatorWifi(command: CoordinatorWifiCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/coordinator/wifi', command);
  }

  /**
   * Update coordinator metadata (name, description, etc.)
   */
  updateCoordinator(coordId: string, data: any): Observable<any> {
    return this.put(`/api/coordinators/${encodeURIComponent(coordId)}`, data);
  }

  /**
   * Update coordinator operational configuration (intervals, listening mode, etc.)
   */
  updateCoordinatorConfig(coordId: string, config: any): Observable<any> {
    return this.put(`/api/coordinators/${encodeURIComponent(coordId)}/config`, config);
  }

  // ============================================================================
  // Nodes API
  // ============================================================================

  /**
   * Get all nodes (system-wide) or nodes for a specific coordinator
   */
  getNodes(): Observable<NodeSummary[]>;
  getNodes(siteId: string, coordId: string): Observable<Node[]>;
  getNodes(siteId?: string, coordId?: string): Observable<Node[] | NodeSummary[]> {
    if (siteId && coordId) {
      return this.get<Node[]>(`/api/nodes/farm/${siteId}/coord/${coordId}`);
    }
    // Get all nodes across the system
    return this.get<NodeSummary[]>('/api/nodes');
  }

  /**
   * Get node by ID
   */
  getNode(nodeId: string): Observable<Node> {
    return this.get<Node>(`/api/nodes/${nodeId}`);
  }

  /**
   * Delete a node
   */
  deleteNode(siteId: string, coordId: string, nodeId: string): Observable<ApiResponse<void>> {
    return this.delete<ApiResponse<void>>(`/api/towers/${siteId}/${coordId}/${nodeId}`);
  }

  /**
   * Update node name
   */
  updateNodeName(update: NodeNameUpdate): Observable<ApiResponse<void>> {
    return this.put<ApiResponse<void>>('/api/node/name', update);
  }

  /**
   * Assign node to zone
   */
  updateNodeZone(update: NodeZoneUpdate): Observable<ApiResponse<void>> {
    return this.put<ApiResponse<void>>('/api/node/zone', update);
  }

  /**
   * Test node color (temporary)
   */
  testNodeColor(command: TestColorCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/node/test-color', command);
  }

  /**
   * Turn off node LED
   */
  turnOffNode(nodeId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/node/off', { nodeId });
  }

  /**
   * Set node brightness
   */
  setNodeBrightness(command: BrightnessCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/node/brightness', command);
  }

  /**
   * Control node light (full control)
   */
  controlNodeLight(command: LedControlCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/node/light/control', command);
  }

  // ============================================================================
  // Zones API
  // ============================================================================

  /**
   * Get all zones
   */
  getZones(): Observable<Zone[]> {
    return this.get<Zone[]>('/api/zones');
  }

  /**
   * Create a zone
   */
  createZone(request: CreateZoneRequest): Observable<Zone> {
    return this.post<Zone>('/api/zones', request);
  }

  /**
   * Get zone by ID
   */
  getZone(zoneId: string): Observable<Zone> {
    return this.get<Zone>(`/api/zones/${zoneId}`);
  }

  /**
   * Update a zone
   */
  updateZone(zoneId: string, request: UpdateZoneRequest): Observable<Zone> {
    return this.put<Zone>(`/api/zones/${zoneId}`, request);
  }

  /**
   * Delete a zone
   */
  deleteZone(zoneId: string): Observable<ApiResponse<void>> {
    return this.delete<ApiResponse<void>>(`/api/zones/${zoneId}`);
  }

  // ============================================================================
  // Settings API
  // ============================================================================

  /**
   * Get system settings
   */
  getSettings(): Observable<SmartTileSettings> {
    return this.get<SmartTileSettings>('/api/settings');
  }

  /**
   * Update system settings
   */
  updateSettings(settings: Partial<SmartTileSettings>): Observable<SmartTileSettings> {
    return this.put<SmartTileSettings>('/api/settings', settings);
  }

  // ============================================================================
  // Telemetry API (if available)
  // ============================================================================

  getCoordinatorTelemetry(coordId: string): Observable<CoordinatorTelemetryData> {
    return this.get<CoordinatorTelemetryData>(`/api/telemetry/coordinator/${coordId}`);
  }

  getNodeTelemetry(nodeId: string): Observable<NodeTelemetryData> {
    return this.get<NodeTelemetryData>(`/api/telemetry/node/${nodeId}`);
  }

  getCoordinatorHistory(coordId: string, timeRange: TimeRange): Observable<CoordinatorHistory> {
    return this.get<CoordinatorHistory>(`/api/telemetry/coordinator/${coordId}/history`, {
      start: timeRange.start.toISOString(),
      end: timeRange.end.toISOString(),
      interval: timeRange.interval
    });
  }

  getNodeHistory(nodeId: string, timeRange: TimeRange): Observable<NodeHistory> {
    return this.get<NodeHistory>(`/api/telemetry/node/${nodeId}/history`, {
      start: timeRange.start.toISOString(),
      end: timeRange.end.toISOString(),
      interval: timeRange.interval
    });
  }

  // ============================================================================
  // Alerts API
  // ============================================================================

  getAlerts(params?: PaginationParams & { severity?: string; acknowledged?: boolean }): Observable<PaginatedResponse<Alert>> {
    const queryParams: Record<string, string> = {};
    if (params) {
      if (params.page !== undefined) queryParams['page'] = params.page.toString();
      if (params.pageSize !== undefined) queryParams['pageSize'] = params.pageSize.toString();
      if (params.severity !== undefined) queryParams['severity'] = params.severity;
      if (params.acknowledged !== undefined) queryParams['acknowledged'] = params.acknowledged.toString();
    }
    return this.get<PaginatedResponse<Alert>>('/api/alerts', queryParams);
  }

  acknowledgeAlert(alertId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/alerts/${alertId}/acknowledge`, {});
  }

  updateAlert(alertId: string, update: { status?: string; acknowledgedBy?: string; resolvedBy?: string }): Observable<Alert> {
    return this.put<Alert>(`/api/alerts/${alertId}`, update);
  }

  deleteAlert(alertId: string): Observable<ApiResponse<void>> {
    return this.delete<ApiResponse<void>>(`/api/alerts/${alertId}`);
  }

  // ============================================================================
  // OTA API
  // ============================================================================

  getFirmwareVersions(targetType?: 'coordinator' | 'node'): Observable<FirmwareVersion[]> {
    const params: Record<string, string> = targetType ? { targetType } : {};
    return this.get<FirmwareVersion[]>('/api/ota/firmware', params);
  }

  getOtaJobs(params?: PaginationParams & { status?: string }): Observable<PaginatedResponse<OtaJob>> {
    const queryParams: Record<string, string> = {};
    if (params) {
      if (params.page !== undefined) queryParams['page'] = params.page.toString();
      if (params.pageSize !== undefined) queryParams['pageSize'] = params.pageSize.toString();
      if (params.status !== undefined) queryParams['status'] = params.status;
    }
    return this.get<PaginatedResponse<OtaJob>>('/api/ota/jobs', queryParams);
  }

  getOtaJob(jobId: string): Observable<OtaJob> {
    return this.get<OtaJob>(`/api/ota/jobs/${jobId}`);
  }

  startOtaUpdate(request: StartOtaRequest): Observable<OtaJob> {
    return this.post<OtaJob>('/api/ota/start', request);
  }

  cancelOtaJob(jobId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/ota/jobs/${jobId}/cancel`, {});
  }

  getOtaCampaigns(): Observable<OtaCampaign[]> {
    return this.get<OtaCampaign[]>('/api/ota/campaigns');
  }

  createOtaCampaign(request: CreateCampaignRequest): Observable<OtaCampaign> {
    return this.post<OtaCampaign>('/api/ota/campaigns', request);
  }

  getOtaStatistics(): Observable<OtaStatistics> {
    return this.get<OtaStatistics>('/api/ota/statistics');
  }

  getDeviceFirmwareStatus(): Observable<DeviceFirmwareStatus[]> {
    return this.get<DeviceFirmwareStatus[]>('/api/ota/devices/status');
  }

  // ============================================================================
  // ML Predictions API (if available)
  // ============================================================================

  getPrediction(towerId: string): Observable<HeightPrediction> {
    return this.get<HeightPrediction>(`/api/predictions/node/${towerId}`);
  }

  getFarmPredictionSummary(): Observable<FarmPredictionSummary> {
    return this.get<FarmPredictionSummary>('/api/predictions/summary');
  }

  requestPrediction(request: PredictionRequest): Observable<HeightPrediction> {
    return this.post<HeightPrediction>('/api/predictions/request', request);
  }

  requestBatchPredictions(request: BatchPredictionRequest): Observable<TowerPredictions[]> {
    return this.post<TowerPredictions[]>('/api/predictions/batch', request);
  }

  getGrowthAnalysis(towerId: string): Observable<GrowthAnalysis> {
    return this.get<GrowthAnalysis>(`/api/predictions/analysis/${towerId}`);
  }

  getAnomalies(params?: PaginationParams & { severity?: string; status?: string }): Observable<PaginatedResponse<GrowthAnomaly>> {
    const queryParams: Record<string, string> = {};
    if (params) {
      if (params.page !== undefined) queryParams['page'] = params.page.toString();
      if (params.pageSize !== undefined) queryParams['pageSize'] = params.pageSize.toString();
      if (params.severity !== undefined) queryParams['severity'] = params.severity;
      if (params.status !== undefined) queryParams['status'] = params.status;
    }
    return this.get<PaginatedResponse<GrowthAnomaly>>('/api/predictions/anomalies', queryParams);
  }

  acknowledgeAnomaly(anomalyId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/predictions/anomalies/${anomalyId}/acknowledge`, {});
  }

  getMLModels(): Observable<MLModelInfo[]> {
    return this.get<MLModelInfo[]>('/api/predictions/models');
  }

  // ============================================================================
  // Farms API
  // ============================================================================

  /** Get all farms. */
  getFarms(): Observable<Farm[]> {
    return this.get<Farm[]>('/api/farms');
  }

  // ============================================================================
  // Digital Twin API (/api/twins/*)
  // ============================================================================

  /** Get all twins for a farm (coordinators + towers in one call). */
  getFarmTwins(farmId: string): Observable<FarmTwinsResponse> {
    return this.get<FarmTwinsResponse>(`/api/twins/farms/${farmId}`);
  }

  /** Get a single tower twin by ID. */
  getTowerTwin(towerId: string): Observable<TowerTwin> {
    return this.get<TowerTwin>(`/api/twins/towers/${towerId}`);
  }

  /** Get tower twins for a coordinator (requires farmId + coordId). */
  getTowerTwins(farmId: string, coordId: string): Observable<TowerTwin[]> {
    return this.get<TowerTwin[]>('/api/twins/towers', { farmId, coordId });
  }

  /** Set desired state for a tower. */
  setTowerDesiredState(towerId: string, desired: Partial<TowerDesiredState>): Observable<void> {
    return this.put<void>(`/api/twins/towers/${towerId}/desired`, desired);
  }

  /** Get the desired vs reported delta for a tower. */
  getTowerDelta(towerId: string): Observable<TowerDeltaResponse> {
    return this.get<TowerDeltaResponse>(`/api/twins/towers/${towerId}/delta`);
  }

  /** Get a single coordinator twin by ID. */
  getCoordinatorTwin(coordId: string): Observable<CoordinatorTwin> {
    return this.get<CoordinatorTwin>(`/api/twins/coordinators/${coordId}`);
  }

  /** Get coordinator twins for a farm. */
  getCoordinatorTwins(farmId: string): Observable<CoordinatorTwin[]> {
    return this.get<CoordinatorTwin[]>('/api/twins/coordinators', { farmId });
  }

  /** Set desired state for a coordinator. */
  setCoordinatorDesiredState(coordId: string, desired: Partial<CoordinatorDesiredState>): Observable<void> {
    return this.put<void>(`/api/twins/coordinators/${coordId}/desired`, desired);
  }

  /** Get the desired vs reported delta for a coordinator. */
  getCoordinatorDelta(coordId: string): Observable<CoordinatorDeltaResponse> {
    return this.get<CoordinatorDeltaResponse>(`/api/twins/coordinators/${coordId}/delta`);
  }

  // ============================================================================
  // Coordinator Registration API
  // ============================================================================

  /**
   * Get all pending coordinator registration requests
   */
  getPendingCoordinatorRegistrations(): Observable<any[]> {
    return this.get<any[]>('/api/coordinators/pending');
  }

  /**
   * Approve a pending coordinator registration
   */
  approveCoordinatorRegistration(request: ApproveCoordinatorRequest): Observable<any> {
    // The snakeCaseInterceptor converts camelCase keys to snake_case automatically
    return this.post('/api/coordinators/register/approve', request);
  }

  /**
   * Reject a pending coordinator registration
   */
  rejectCoordinatorRegistration(coordId: string): Observable<any> {
    return this.post('/api/coordinators/register/reject', { coordId });
  }

  /**
   * Remove a registered coordinator
   */
  removeCoordinator(coordId: string): Observable<any> {
    return this.delete(`/api/coordinators/${encodeURIComponent(coordId)}`);
  }

  // ============================================================================
  // System Operations
  // ============================================================================

  /**
   * Factory reset — drops all MongoDB collections and clears caches
   */
  factoryReset(): Observable<{ success: boolean; collectionsDropped: string[]; message: string }> {
    return this.post('/api/system/factory-reset', {});
  }

  // ============================================================================
  // Private HTTP Methods
  // ============================================================================

  private get<T>(endpoint: string, params?: Record<string, string>): Observable<T> {
    let httpParams = new HttpParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          httpParams = httpParams.set(key, value);
        }
      });
    }

    return this.http.get<T>(`${this.baseUrl}${endpoint}`, { params: httpParams }).pipe(
      timeout(this.env.apiTimeout),
      retry({ count: 2, delay: 1000 }),
      catchError(this.handleError)
    );
  }

  private post<T>(endpoint: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${endpoint}`, body).pipe(
      timeout(this.env.apiTimeout),
      catchError(this.handleError)
    );
  }

  private put<T>(endpoint: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${endpoint}`, body).pipe(
      timeout(this.env.apiTimeout),
      catchError(this.handleError)
    );
  }

  private delete<T>(endpoint: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${endpoint}`).pipe(
      timeout(this.env.apiTimeout),
      catchError(this.handleError)
    );
  }

  private handleError = (error: HttpErrorResponse): Observable<never> => {
    let errorMessage = 'An unexpected error occurred';
    
    if (error.error instanceof ErrorEvent) {
      // Client-side error
      errorMessage = error.error.message;
    } else {
      // Server-side error
      errorMessage = error.error?.message || `Error ${error.status}: ${error.statusText}`;
    }

    if (this.env.isDevelopment) {
      console.error('API Error:', error);
    }

    return throwError(() => new Error(errorMessage));
  };
}
