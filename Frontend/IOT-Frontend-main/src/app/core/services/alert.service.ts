import { Injectable, inject, signal, computed } from '@angular/core';
import { Subject, firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import {
  Alert,
  AlertSeverity,
  AlertStatus,
  AlertCategory,
  AlertStats,
  AlertFilter,
} from '../models';

/**
 * Alert Service - System Alerts and Notifications Management
 * Centralized state management using Angular signals
 * Handles alert fetching, filtering, and actions (acknowledge, resolve, dismiss)
 */
@Injectable({
  providedIn: 'root'
})
export class AlertService {
  private readonly api = inject(ApiService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals
  // ============================================================================

  // Loading states
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  // Alerts list
  readonly alerts = signal<Alert[]>([]);

  // Filter state
  readonly filterSeverity = signal<AlertSeverity | 'all'>('all');
  readonly filterStatus = signal<AlertStatus | 'all'>('all');
  readonly filterCategory = signal<AlertCategory | 'all'>('all');
  readonly searchTerm = signal('');

  // ============================================================================
  // Computed Signals
  // ============================================================================

  // Filtered alerts based on current filters
  readonly filteredAlerts = computed(() => {
    let result = this.alerts();

    // Filter by severity
    const severity = this.filterSeverity();
    if (severity !== 'all') {
      result = result.filter(a => a.severity === severity);
    }

    // Filter by status
    const status = this.filterStatus();
    if (status !== 'all') {
      result = result.filter(a => a.status === status);
    }

    // Filter by category
    const category = this.filterCategory();
    if (category !== 'all') {
      result = result.filter(a => a.category === category);
    }

    // Filter by search term
    const search = this.searchTerm().toLowerCase().trim();
    if (search) {
      result = result.filter(a =>
        (a.title ?? '').toLowerCase().includes(search) ||
        (a.message ?? '').toLowerCase().includes(search) ||
        (a.source?.name ?? '').toLowerCase().includes(search)
      );
    }

    return result;
  });

  // Alerts by status
  readonly activeAlerts = computed(() =>
    this.alerts().filter(a => a.status === 'active')
  );

  readonly acknowledgedAlerts = computed(() =>
    this.alerts().filter(a => a.status === 'acknowledged')
  );

  readonly resolvedAlerts = computed(() =>
    this.alerts().filter(a => a.status === 'resolved')
  );

  // Alerts by severity
  readonly criticalAlerts = computed(() =>
    this.alerts().filter(a => a.severity === 'critical')
  );

  readonly warningAlerts = computed(() =>
    this.alerts().filter(a => a.severity === 'warning')
  );

  readonly infoAlerts = computed(() =>
    this.alerts().filter(a => a.severity === 'info')
  );

  // Active alerts by severity (most important for dashboard)
  readonly activeCriticalAlerts = computed(() =>
    this.activeAlerts().filter(a => a.severity === 'critical')
  );

  readonly activeWarningAlerts = computed(() =>
    this.activeAlerts().filter(a => a.severity === 'warning')
  );

  readonly activeInfoAlerts = computed(() =>
    this.activeAlerts().filter(a => a.severity === 'info')
  );

  // Alert statistics
  readonly alertStats = computed<AlertStats>(() => {
    const all = this.alerts();
    return {
      total: all.length,
      bySeverity: {
        critical: all.filter(a => a.severity === 'critical').length,
        warning: all.filter(a => a.severity === 'warning').length,
        info: all.filter(a => a.severity === 'info').length,
      },
      byStatus: {
        active: all.filter(a => a.status === 'active').length,
        acknowledged: all.filter(a => a.status === 'acknowledged').length,
        resolved: all.filter(a => a.status === 'resolved').length,
      },
      byCategory: {
        sensor: all.filter(a => a.category === 'sensor').length,
        system: all.filter(a => a.category === 'system').length,
        network: all.filter(a => a.category === 'network').length,
        maintenance: all.filter(a => a.category === 'maintenance').length,
        security: all.filter(a => a.category === 'security').length,
      },
    };
  });

  // Unread/active count (for badge display)
  readonly activeCount = computed(() => this.activeAlerts().length);
  readonly criticalCount = computed(() => this.activeCriticalAlerts().length);

  // Recent alerts (last 5)
  readonly recentAlerts = computed(() =>
    [...this.alerts()]
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, 5)
  );

  // ============================================================================
  // Data Loading Methods
  // ============================================================================

  /**
   * Load all alerts from API
   */
  async loadAlerts(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      const response: any = await firstValueFrom(this.api.getAlerts({ page: 1, pageSize: 100 }));
      const raw = response.items ?? response.data ?? [];
      const alerts: Alert[] = (Array.isArray(raw) ? raw : []).map((a: any) => this.normalizeAlert(a));
      this.alerts.set(alerts);
    } catch (err) {
      console.error('Failed to load alerts:', err);
      this.error.set('Failed to load alerts. Please check if the server is running.');
      this.alerts.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Refresh alerts data
   */
  async refresh(): Promise<void> {
    await this.loadAlerts();
  }

  // ============================================================================
  // Filter Methods
  // ============================================================================

  /**
   * Set severity filter
   */
  setSeverityFilter(severity: AlertSeverity | 'all'): void {
    this.filterSeverity.set(severity);
  }

  /**
   * Set status filter
   */
  setStatusFilter(status: AlertStatus | 'all'): void {
    this.filterStatus.set(status);
  }

  /**
   * Set category filter
   */
  setCategoryFilter(category: AlertCategory | 'all'): void {
    this.filterCategory.set(category);
  }

  /**
   * Set search term
   */
  setSearchTerm(term: string): void {
    this.searchTerm.set(term);
  }

  /**
   * Clear all filters
   */
  clearFilters(): void {
    this.filterSeverity.set('all');
    this.filterStatus.set('all');
    this.filterCategory.set('all');
    this.searchTerm.set('');
  }

  // ============================================================================
  // Action Methods
  // ============================================================================

  /**
   * Acknowledge an alert
   */
  async acknowledgeAlert(alertId: string, acknowledgedBy: string = 'current-user'): Promise<boolean> {
    try {
      await firstValueFrom(this.api.updateAlert(alertId, {
        status: 'acknowledged',
        acknowledgedBy,
      }));
      await this.loadAlerts();
      return true;
    } catch (err) {
      console.error('Failed to acknowledge alert:', err);
      this.error.set('Failed to acknowledge alert');
      return false;
    }
  }

  /**
   * Resolve an alert
   */
  async resolveAlert(alertId: string, resolvedBy: string = 'current-user'): Promise<boolean> {
    try {
      await firstValueFrom(this.api.updateAlert(alertId, {
        status: 'resolved',
        resolvedBy,
      }));
      await this.loadAlerts();
      return true;
    } catch (err) {
      console.error('Failed to resolve alert:', err);
      this.error.set('Failed to resolve alert');
      return false;
    }
  }

  /**
   * Dismiss/delete an alert (remove from list)
   */
  async dismissAlert(alertId: string): Promise<boolean> {
    try {
      await firstValueFrom(this.api.deleteAlert(alertId));
      this.alerts.update(alerts => alerts.filter(a => a._id !== alertId));
      return true;
    } catch (err) {
      console.error('Failed to dismiss alert:', err);
      this.error.set('Failed to dismiss alert');
      return false;
    }
  }

  /**
   * Acknowledge all active alerts
   */
  async acknowledgeAllActive(acknowledgedBy: string = 'current-user'): Promise<boolean> {
    try {
      const activeAlertIds = this.activeAlerts().map(a => a._id);

      // Bulk acknowledge via API
      for (const id of activeAlertIds) {
        await firstValueFrom(this.api.updateAlert(id, {
          status: 'acknowledged',
          acknowledgedBy,
        }));
      }
      await this.loadAlerts();
      return true;
    } catch (err) {
      console.error('Failed to acknowledge all alerts:', err);
      this.error.set('Failed to acknowledge all alerts');
      return false;
    }
  }

  /**
   * Resolve all acknowledged alerts
   */
  async resolveAllAcknowledged(resolvedBy: string = 'current-user'): Promise<boolean> {
    try {
      const acknowledgedAlertIds = this.acknowledgedAlerts().map(a => a._id);

      // Bulk resolve via API
      for (const id of acknowledgedAlertIds) {
        await firstValueFrom(this.api.updateAlert(id, {
          status: 'resolved',
          resolvedBy,
        }));
      }
      await this.loadAlerts();
      return true;
    } catch (err) {
      console.error('Failed to resolve all alerts:', err);
      this.error.set('Failed to resolve all alerts');
      return false;
    }
  }

  // ============================================================================
  // Real-time Updates (from WebSocket)
  // ============================================================================

  /**
   * Add new alert from WebSocket
   */
  addAlert(alert: Alert): void {
    const normalised = this.normalizeAlert(alert);
    this.alerts.update(alerts => [normalised, ...alerts]);
  }

  /**
   * Update alert from WebSocket
   */
  updateAlert(alertId: string, updates: Partial<Alert>): void {
    this.alerts.update(alerts =>
      alerts.map(a =>
        a._id === alertId ? { ...a, ...updates } : a
      )
    );
  }

  /**
   * Remove alert from WebSocket notification
   */
  removeAlert(alertId: string): void {
    this.alerts.update(alerts => alerts.filter(a => a._id !== alertId));
  }

  // ============================================================================
  // Data Normalisation
  // ============================================================================

  /**
   * Map the flat backend alert shape into the rich Alert interface the
   * template expects.  The backend returns:
   *   { id, farmId, coordId, severity, status, message, category, createdAt, alertKey }
   * (after the snakeCaseInterceptor converts snake_case keys to camelCase).
   */
  private normalizeAlert(raw: any): Alert {
    const farmId: string = raw.farmId ?? raw.farm_id ?? '';
    const coordId: string = raw.coordId ?? raw.coord_id ?? '';

    // Already normalised (has source object)?
    if (raw.source && typeof raw.source === 'object') {
      return {
        ...raw,
        _id: raw._id ?? raw.id ?? '',
        farmId: raw.farmId ?? raw.farm_id ?? farmId,
        coordId: raw.coordId ?? raw.coord_id ?? coordId,
      } as Alert;
    }

    const category: string = raw.category ?? 'sensor';

    // Derive a human-readable title from the category
    const title = this.categoryToTitle(category);

    return {
      _id: raw._id ?? raw.id ?? '',
      title,
      message: raw.message ?? '',
      severity: raw.severity ?? 'warning',
      status: raw.status ?? 'active',
      category: (category as AlertCategory) ?? 'sensor',
      source: {
        type: 'coordinator' as const,
        id: coordId,
        name: coordId || 'System',
      },
      createdAt: raw.createdAt ?? raw.created_at ?? new Date().toISOString(),
      acknowledgedAt: raw.acknowledgedAt ?? raw.acknowledged_at,
      resolvedAt: raw.resolvedAt ?? raw.resolved_at,
      metadata: raw.metadata ?? undefined,
      farmId,
      coordId,
    };
  }

  private categoryToTitle(category: string): string {
    switch (category) {
      case 'ph_out_of_range':      return 'pH Out of Range';
      case 'temperature_high':     return 'High Temperature';
      case 'temperature_low':      return 'Low Temperature';
      case 'water_level_low':      return 'Low Water Level';
      case 'ec_out_of_range':      return 'EC Out of Range';
      case 'pump_failure':         return 'Pump Failure';
      case 'battery_low':          return 'Low Battery';
      case 'device_offline':       return 'Device Offline';
      default:                     return category.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
    }
  }

  // ============================================================================
  // Utility Methods
  // ============================================================================

  /**
   * Get alert by ID
   */
  getAlertById(alertId: string): Alert | undefined {
    return this.alerts().find(a => a._id === alertId);
  }

  /**
   * Get alerts by source
   */
  getAlertsBySource(sourceId: string): Alert[] {
    return this.alerts().filter(a => a.source.id === sourceId);
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
