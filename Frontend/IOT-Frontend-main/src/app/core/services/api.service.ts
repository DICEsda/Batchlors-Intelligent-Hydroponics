import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, catchError, retry, timeout } from 'rxjs';
import { EnvironmentService } from './environment.service';
import {
  // Coordinator
  Coordinator,
  CoordinatorSummary,
  ReservoirAdjustCommand,
  CoordinatorConfigUpdate,
  // Tower
  Tower,
  TowerSummary,
  TowerLedCommand,
  TowerConfigUpdate,
  PlantSlotUpdate,
  // Telemetry
  ReservoirTelemetry,
  TowerTelemetry,
  ReservoirHistory,
  TowerHistory,
  FarmMetrics,
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
  // Digital Twin
  FarmTopology,
  DigitalTwinState,
  // Common
  ApiResponse,
  PaginatedResponse,
  HealthStatus,
  PaginationParams,
  TelemetryQueryParams,
} from '../models';

/**
 * API Service for Hydroponic Farm System
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

  getFarmMetrics(): Observable<FarmMetrics> {
    return this.get<FarmMetrics>('/api/v1/metrics');
  }

  // ============================================================================
  // Coordinators API
  // ============================================================================

  getCoordinators(): Observable<CoordinatorSummary[]> {
    return this.get<CoordinatorSummary[]>('/api/v1/coordinators');
  }

  getCoordinator(coordId: string): Observable<Coordinator> {
    return this.get<Coordinator>(`/api/v1/coordinators/${coordId}`);
  }

  updateCoordinator(config: CoordinatorConfigUpdate): Observable<Coordinator> {
    return this.put<Coordinator>(`/api/v1/coordinators/${config.coordId}`, config);
  }

  adjustReservoir(command: ReservoirAdjustCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/v1/coordinators/reservoir/adjust', command);
  }

  // ============================================================================
  // Towers API
  // ============================================================================

  getTowers(coordId?: string): Observable<TowerSummary[]> {
    const params: Record<string, string> = coordId ? { coordId } : {};
    return this.get<TowerSummary[]>('/api/v1/towers', params);
  }

  getTower(towerId: string): Observable<Tower> {
    return this.get<Tower>(`/api/v1/towers/${towerId}`);
  }

  getTowersByCoordinator(coordId: string): Observable<TowerSummary[]> {
    return this.get<TowerSummary[]>(`/api/v1/coordinators/${coordId}/towers`);
  }

  updateTower(config: TowerConfigUpdate): Observable<Tower> {
    return this.put<Tower>(`/api/v1/towers/${config.towerId}`, config);
  }

  controlTowerLed(command: TowerLedCommand): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/v1/towers/led', command);
  }

  updatePlantSlot(update: PlantSlotUpdate): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>('/api/v1/towers/plants', update);
  }

  // ============================================================================
  // Telemetry API
  // ============================================================================

  getReservoirTelemetry(coordId: string): Observable<ReservoirTelemetry> {
    return this.get<ReservoirTelemetry>(`/api/v1/telemetry/reservoir/${coordId}`);
  }

  getTowerTelemetry(towerId: string): Observable<TowerTelemetry> {
    return this.get<TowerTelemetry>(`/api/v1/telemetry/tower/${towerId}`);
  }

  getReservoirHistory(coordId: string, timeRange: TimeRange): Observable<ReservoirHistory> {
    return this.get<ReservoirHistory>(`/api/v1/telemetry/reservoir/${coordId}/history`, {
      start: timeRange.start.toISOString(),
      end: timeRange.end.toISOString(),
      interval: timeRange.interval
    });
  }

  getTowerHistory(towerId: string, timeRange: TimeRange): Observable<TowerHistory> {
    return this.get<TowerHistory>(`/api/v1/telemetry/tower/${towerId}/history`, {
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
    return this.get<PaginatedResponse<Alert>>('/api/v1/alerts', queryParams);
  }

  acknowledgeAlert(alertId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/v1/alerts/${alertId}/acknowledge`, {});
  }

  // ============================================================================
  // OTA API
  // ============================================================================

  getFirmwareVersions(targetType?: 'coordinator' | 'tower'): Observable<FirmwareVersion[]> {
    const params: Record<string, string> = targetType ? { targetType } : {};
    return this.get<FirmwareVersion[]>('/api/v1/ota/firmware', params);
  }

  getOtaJobs(params?: PaginationParams & { status?: string }): Observable<PaginatedResponse<OtaJob>> {
    const queryParams: Record<string, string> = {};
    if (params) {
      if (params.page !== undefined) queryParams['page'] = params.page.toString();
      if (params.pageSize !== undefined) queryParams['pageSize'] = params.pageSize.toString();
      if (params.status !== undefined) queryParams['status'] = params.status;
    }
    return this.get<PaginatedResponse<OtaJob>>('/api/v1/ota/jobs', queryParams);
  }

  getOtaJob(jobId: string): Observable<OtaJob> {
    return this.get<OtaJob>(`/api/v1/ota/jobs/${jobId}`);
  }

  startOtaUpdate(request: StartOtaRequest): Observable<OtaJob> {
    return this.post<OtaJob>('/api/v1/ota/start', request);
  }

  cancelOtaJob(jobId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/v1/ota/jobs/${jobId}/cancel`, {});
  }

  getOtaCampaigns(): Observable<OtaCampaign[]> {
    return this.get<OtaCampaign[]>('/api/v1/ota/campaigns');
  }

  createOtaCampaign(request: CreateCampaignRequest): Observable<OtaCampaign> {
    return this.post<OtaCampaign>('/api/v1/ota/campaigns', request);
  }

  getOtaStatistics(): Observable<OtaStatistics> {
    return this.get<OtaStatistics>('/api/v1/ota/statistics');
  }

  getDeviceFirmwareStatus(): Observable<DeviceFirmwareStatus[]> {
    return this.get<DeviceFirmwareStatus[]>('/api/v1/ota/devices/status');
  }

  // ============================================================================
  // ML Predictions API
  // ============================================================================

  getPrediction(towerId: string, slotIndex?: number): Observable<HeightPrediction> {
    const params: Record<string, string> = slotIndex !== undefined ? { slotIndex: slotIndex.toString() } : {};
    return this.get<HeightPrediction>(`/api/v1/predictions/tower/${towerId}`, params);
  }

  getTowerPredictions(towerId: string): Observable<TowerPredictions> {
    return this.get<TowerPredictions>(`/api/v1/predictions/tower/${towerId}/all`);
  }

  getFarmPredictionSummary(): Observable<FarmPredictionSummary> {
    return this.get<FarmPredictionSummary>('/api/v1/predictions/summary');
  }

  requestPrediction(request: PredictionRequest): Observable<HeightPrediction> {
    return this.post<HeightPrediction>('/api/v1/predictions/request', request);
  }

  requestBatchPredictions(request: BatchPredictionRequest): Observable<TowerPredictions[]> {
    return this.post<TowerPredictions[]>('/api/v1/predictions/batch', request);
  }

  getGrowthAnalysis(towerId: string, slotIndex: number): Observable<GrowthAnalysis> {
    return this.get<GrowthAnalysis>(`/api/v1/predictions/analysis/${towerId}/${slotIndex}`);
  }

  getAnomalies(params?: PaginationParams & { severity?: string; status?: string }): Observable<PaginatedResponse<GrowthAnomaly>> {
    const queryParams: Record<string, string> = {};
    if (params) {
      if (params.page !== undefined) queryParams['page'] = params.page.toString();
      if (params.pageSize !== undefined) queryParams['pageSize'] = params.pageSize.toString();
      if (params.severity !== undefined) queryParams['severity'] = params.severity;
      if (params.status !== undefined) queryParams['status'] = params.status;
    }
    return this.get<PaginatedResponse<GrowthAnomaly>>('/api/v1/predictions/anomalies', queryParams);
  }

  acknowledgeAnomaly(anomalyId: string): Observable<ApiResponse<void>> {
    return this.post<ApiResponse<void>>(`/api/v1/predictions/anomalies/${anomalyId}/acknowledge`, {});
  }

  getMLModels(): Observable<MLModelInfo[]> {
    return this.get<MLModelInfo[]>('/api/v1/predictions/models');
  }

  // ============================================================================
  // Digital Twin API
  // ============================================================================

  getFarmTopology(): Observable<FarmTopology> {
    return this.get<FarmTopology>('/api/v1/digital-twin/topology');
  }

  getDigitalTwinState(): Observable<DigitalTwinState> {
    return this.get<DigitalTwinState>('/api/v1/digital-twin/state');
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
