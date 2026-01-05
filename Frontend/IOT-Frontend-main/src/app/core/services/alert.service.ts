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
 * Mock Alert Data for development when backend is unavailable
 */
const MOCK_ALERTS: Alert[] = [
  {
    _id: 'alert-001',
    title: 'High pH Level Detected',
    message: 'pH level in Tower A-1 has exceeded the safe threshold of 7.5. Current reading: 8.2. Immediate attention required.',
    severity: 'critical',
    status: 'active',
    category: 'sensor',
    source: {
      type: 'tower',
      id: 'tower-001',
      name: 'Tower A-1',
    },
    createdAt: new Date(Date.now() - 300000), // 5 mins ago
    metadata: { reading: 8.2, threshold: 7.5, unit: 'pH' },
  },
  {
    _id: 'alert-002',
    title: 'Low Nutrient Solution Level',
    message: 'Nutrient reservoir is running low. Current level: 15%. Recommended to refill soon.',
    severity: 'warning',
    status: 'active',
    category: 'maintenance',
    source: {
      type: 'coordinator',
      id: 'coord-001',
      name: 'Main Greenhouse Controller',
    },
    createdAt: new Date(Date.now() - 1800000), // 30 mins ago
    metadata: { level: 15, unit: '%' },
  },
  {
    _id: 'alert-003',
    title: 'Node Communication Lost',
    message: 'Tower B-3 has not reported telemetry data for the past 10 minutes. Check network connectivity.',
    severity: 'critical',
    status: 'acknowledged',
    category: 'network',
    source: {
      type: 'tower',
      id: 'tower-007',
      name: 'Tower B-3',
    },
    createdAt: new Date(Date.now() - 3600000), // 1 hour ago
    acknowledgedAt: new Date(Date.now() - 3000000),
    acknowledgedBy: 'admin@farm.local',
    metadata: { lastSeen: new Date(Date.now() - 3600000) },
  },
  {
    _id: 'alert-004',
    title: 'Temperature Above Optimal Range',
    message: 'Ambient temperature in Zone 2 is above optimal growing conditions. Current: 32°C, Optimal: 18-28°C.',
    severity: 'warning',
    status: 'active',
    category: 'sensor',
    source: {
      type: 'coordinator',
      id: 'coord-002',
      name: 'Zone 2 Controller',
    },
    createdAt: new Date(Date.now() - 7200000), // 2 hours ago
    metadata: { reading: 32, min: 18, max: 28, unit: '°C' },
  },
  {
    _id: 'alert-005',
    title: 'Firmware Update Available',
    message: 'New firmware version 2.1.0 is available for your coordinators. Update recommended for improved stability.',
    severity: 'info',
    status: 'active',
    category: 'system',
    source: {
      type: 'system',
      id: 'system',
      name: 'System',
    },
    createdAt: new Date(Date.now() - 86400000), // 1 day ago
    metadata: { currentVersion: '2.0.5', availableVersion: '2.1.0' },
  },
  {
    _id: 'alert-006',
    title: 'Pump Maintenance Due',
    message: 'Scheduled maintenance for nutrient pump #2 is due. Last maintenance: 30 days ago.',
    severity: 'info',
    status: 'active',
    category: 'maintenance',
    source: {
      type: 'coordinator',
      id: 'coord-001',
      name: 'Main Greenhouse Controller',
    },
    createdAt: new Date(Date.now() - 43200000), // 12 hours ago
    metadata: { pumpId: 'pump-002', daysSinceMaintenance: 30 },
  },
  {
    _id: 'alert-007',
    title: 'EC Level Normalized',
    message: 'EC level in Tower C-2 has returned to normal range after auto-correction.',
    severity: 'info',
    status: 'resolved',
    category: 'sensor',
    source: {
      type: 'tower',
      id: 'tower-012',
      name: 'Tower C-2',
    },
    createdAt: new Date(Date.now() - 172800000), // 2 days ago
    resolvedAt: new Date(Date.now() - 169200000),
    resolvedBy: 'system',
    metadata: { previousReading: 3.2, currentReading: 2.4, unit: 'mS/cm' },
  },
  {
    _id: 'alert-008',
    title: 'Unauthorized Access Attempt',
    message: 'Multiple failed login attempts detected from IP 192.168.1.105. Account temporarily locked.',
    severity: 'critical',
    status: 'resolved',
    category: 'security',
    source: {
      type: 'backend',
      id: 'backend',
      name: 'Backend Server',
    },
    createdAt: new Date(Date.now() - 259200000), // 3 days ago
    resolvedAt: new Date(Date.now() - 255600000),
    resolvedBy: 'admin@farm.local',
    metadata: { attempts: 5, ipAddress: '192.168.1.105' },
  },
  {
    _id: 'alert-009',
    title: 'Light Sensor Calibration Needed',
    message: 'Light sensor on Tower A-2 is showing inconsistent readings. Calibration recommended.',
    severity: 'warning',
    status: 'acknowledged',
    category: 'sensor',
    source: {
      type: 'tower',
      id: 'tower-002',
      name: 'Tower A-2',
    },
    createdAt: new Date(Date.now() - 14400000), // 4 hours ago
    acknowledgedAt: new Date(Date.now() - 10800000),
    acknowledgedBy: 'tech@farm.local',
    metadata: { variance: 15, unit: '%' },
  },
  {
    _id: 'alert-010',
    title: 'Database Backup Completed',
    message: 'Weekly automated database backup completed successfully. Size: 2.4 GB.',
    severity: 'info',
    status: 'resolved',
    category: 'system',
    source: {
      type: 'backend',
      id: 'backend',
      name: 'Backend Server',
    },
    createdAt: new Date(Date.now() - 345600000), // 4 days ago
    resolvedAt: new Date(Date.now() - 345600000),
    resolvedBy: 'system',
    metadata: { backupSize: '2.4 GB', duration: '45 minutes' },
  },
];

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
  readonly usingMockData = signal(false);

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
        a.title.toLowerCase().includes(search) ||
        a.message.toLowerCase().includes(search) ||
        a.source.name.toLowerCase().includes(search)
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
   * Load all alerts from API or mock data
   */
  async loadAlerts(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      // Try to load from API
      const response = await firstValueFrom(this.api.getAlerts({ page: 1, pageSize: 100 }));
      this.alerts.set(response.items || []);
      this.usingMockData.set(false);
    } catch (err) {
      console.warn('Alerts API unavailable, falling back to mock data');
      this.usingMockData.set(true);
      this.loadMockData();
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load mock data as fallback
   */
  private loadMockData(): void {
    this.alerts.set([...MOCK_ALERTS]);
    this.error.set(null);
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
      if (this.usingMockData()) {
        this.alerts.update(alerts =>
          alerts.map(a =>
            a._id === alertId
              ? {
                  ...a,
                  status: 'acknowledged' as AlertStatus,
                  acknowledgedAt: new Date(),
                  acknowledgedBy,
                }
              : a
          )
        );
        return true;
      }

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
      if (this.usingMockData()) {
        this.alerts.update(alerts =>
          alerts.map(a =>
            a._id === alertId
              ? {
                  ...a,
                  status: 'resolved' as AlertStatus,
                  resolvedAt: new Date(),
                  resolvedBy,
                }
              : a
          )
        );
        return true;
      }

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
      if (this.usingMockData()) {
        this.alerts.update(alerts => alerts.filter(a => a._id !== alertId));
        return true;
      }

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

      if (this.usingMockData()) {
        this.alerts.update(alerts =>
          alerts.map(a =>
            activeAlertIds.includes(a._id)
              ? {
                  ...a,
                  status: 'acknowledged' as AlertStatus,
                  acknowledgedAt: new Date(),
                  acknowledgedBy,
                }
              : a
          )
        );
        return true;
      }

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

      if (this.usingMockData()) {
        this.alerts.update(alerts =>
          alerts.map(a =>
            acknowledgedAlertIds.includes(a._id)
              ? {
                  ...a,
                  status: 'resolved' as AlertStatus,
                  resolvedAt: new Date(),
                  resolvedBy,
                }
              : a
          )
        );
        return true;
      }

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
    this.alerts.update(alerts => [alert, ...alerts]);
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
