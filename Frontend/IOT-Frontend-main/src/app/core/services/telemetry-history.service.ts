import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { EnvironmentService } from './environment.service';
import { ReservoirTelemetry, TowerTelemetry } from '../models/telemetry.model';

/**
 * Telemetry History Service
 * HTTP client for the new TelemetryHistoryController endpoints.
 * Fetches time-series reservoir and tower telemetry from MongoDB.
 */
@Injectable({
  providedIn: 'root'
})
export class TelemetryHistoryService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(EnvironmentService);

  /**
   * Fetch reservoir telemetry history for a coordinator.
   * GET /api/telemetry/reservoir/history?coordId=...&farmId=...&minutes=...
   */
  getReservoirHistory(
    coordId: string,
    farmId: string = 'farm-001',
    minutes: number = 60
  ): Observable<ReservoirTelemetry[]> {
    const params = new HttpParams()
      .set('coordId', coordId)
      .set('farmId', farmId)
      .set('minutes', minutes.toString());

    return this.http.get<ReservoirTelemetry[]>(
      `${this.env.apiUrl}/api/telemetry/reservoir/history`,
      { params }
    );
  }

  /**
   * Fetch tower telemetry history.
   * GET /api/telemetry/tower/history?towerId=...&farmId=...&coordId=...&minutes=...
   */
  getTowerHistory(
    towerId: string,
    farmId: string = 'farm-001',
    coordId: string = 'coord-001',
    minutes: number = 60
  ): Observable<TowerTelemetry[]> {
    const params = new HttpParams()
      .set('towerId', towerId)
      .set('farmId', farmId)
      .set('coordId', coordId)
      .set('minutes', minutes.toString());

    return this.http.get<TowerTelemetry[]>(
      `${this.env.apiUrl}/api/telemetry/tower/history`,
      { params }
    );
  }

  /**
   * Fetch latest reservoir telemetry for a coordinator.
   * GET /api/telemetry/reservoir/latest?farmId=...&coordId=...
   */
  getLatestReservoir(
    farmId: string,
    coordId: string
  ): Observable<ReservoirTelemetry> {
    const params = new HttpParams()
      .set('farmId', farmId)
      .set('coordId', coordId);

    return this.http.get<ReservoirTelemetry>(
      `${this.env.apiUrl}/api/telemetry/reservoir/latest`,
      { params }
    );
  }
}
