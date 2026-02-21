import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { throwError } from 'rxjs';
import { AlertService } from './alert.service';
import { ApiService } from './api.service';
import { Alert } from '../models';

/**
 * Unit tests for AlertService
 *
 * ApiService is replaced with a Jasmine spy object so no HTTP calls are made.
 * The service uses `inject(ApiService)`, so it must be provided in TestBed.
 */
describe('AlertService', () => {
  let service: AlertService;
  let mockApiService: jasmine.SpyObj<ApiService>;

  /** Build a minimal Alert with sensible defaults. */
  function makeAlert(overrides: Partial<Alert> = {}): Alert {
    return {
      _id: 'a-' + Math.random().toString(36).slice(2, 8),
      title: 'Test Alert',
      message: 'Something happened',
      severity: 'warning',
      status: 'active',
      category: 'sensor',
      source: { type: 'tower', id: 'tower-001', name: 'Tower A-1' },
      createdAt: new Date().toISOString(),
      ...overrides,
    };
  }

  beforeEach(() => {
    mockApiService = jasmine.createSpyObj('ApiService', [
      'getAlerts',
      'updateAlert',
      'deleteAlert',
    ]);
    // Default: API always fails → forces mock-data fallback path
    mockApiService.getAlerts.and.returnValue(
      throwError(() => new Error('no backend'))
    );

    TestBed.configureTestingModule({
      providers: [
        provideExperimentalZonelessChangeDetection(),
        AlertService,
        { provide: ApiService, useValue: mockApiService },
      ],
    });

    service = TestBed.inject(AlertService);
  });

  // ==========================================================================
  // Initial state
  // ==========================================================================

  it('should have empty alerts, loading false, and no error initially', () => {
    expect(service.alerts()).toEqual([]);
    expect(service.isLoading()).toBeFalse();
    expect(service.error()).toBeNull();
  });

  // ==========================================================================
  // loadAlerts → API failure → mock fallback
  // ==========================================================================

  it('should fall back to mock data when API fails', async () => {
    await service.loadAlerts();

    expect(service.usingMockData()).toBeTrue();
    expect(service.alerts().length).toBe(10);
    expect(service.isLoading()).toBeFalse();
  });

  // ==========================================================================
  // loadMockData — 10 items
  // ==========================================================================

  it('should have 10 mock alerts after loadAlerts with API failure', async () => {
    await service.loadAlerts();
    expect(service.alerts().length).toBe(10);
  });

  // ==========================================================================
  // filteredAlerts — no filters returns all
  // ==========================================================================

  it('should return all alerts when no filters are set', async () => {
    await service.loadAlerts();
    expect(service.filteredAlerts().length).toBe(service.alerts().length);
  });

  // ==========================================================================
  // Severity filter — 'critical'
  // ==========================================================================

  it('should filter by severity "critical"', async () => {
    await service.loadAlerts();
    service.setSeverityFilter('critical');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.severity === 'critical')).toBeTrue();
  });

  // ==========================================================================
  // Severity filter — 'warning'
  // ==========================================================================

  it('should filter by severity "warning"', async () => {
    await service.loadAlerts();
    service.setSeverityFilter('warning');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.severity === 'warning')).toBeTrue();
  });

  // ==========================================================================
  // Status filter — 'active'
  // ==========================================================================

  it('should filter by status "active"', async () => {
    await service.loadAlerts();
    service.setStatusFilter('active');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.status === 'active')).toBeTrue();
  });

  // ==========================================================================
  // Status filter — 'resolved'
  // ==========================================================================

  it('should filter by status "resolved"', async () => {
    await service.loadAlerts();
    service.setStatusFilter('resolved');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.status === 'resolved')).toBeTrue();
  });

  // ==========================================================================
  // Category filter — 'sensor'
  // ==========================================================================

  it('should filter by category "sensor"', async () => {
    await service.loadAlerts();
    service.setCategoryFilter('sensor');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.category === 'sensor')).toBeTrue();
  });

  // ==========================================================================
  // Search term — 'pH'
  // ==========================================================================

  it('should filter alerts by search term matching title, message, or source name', async () => {
    await service.loadAlerts();
    service.setSearchTerm('pH');

    const result = service.filteredAlerts();
    expect(result.length).toBeGreaterThan(0);
    expect(
      result.every(a =>
        a.title.toLowerCase().includes('ph') ||
        a.message.toLowerCase().includes('ph') ||
        a.source.name.toLowerCase().includes('ph')
      )
    ).toBeTrue();
  });

  // ==========================================================================
  // Multiple filters combined — severity + status
  // ==========================================================================

  it('should combine severity and status filters', async () => {
    await service.loadAlerts();
    service.setSeverityFilter('critical');
    service.setStatusFilter('active');

    const result = service.filteredAlerts();
    expect(result.every(a => a.severity === 'critical' && a.status === 'active')).toBeTrue();
  });

  // ==========================================================================
  // clearFilters — resets all to 'all' / ''
  // ==========================================================================

  it('should reset all filters when clearFilters is called', async () => {
    await service.loadAlerts();
    service.setSeverityFilter('critical');
    service.setStatusFilter('resolved');
    service.setCategoryFilter('network');
    service.setSearchTerm('test');

    service.clearFilters();

    expect(service.filterSeverity()).toBe('all');
    expect(service.filterStatus()).toBe('all');
    expect(service.filterCategory()).toBe('all');
    expect(service.searchTerm()).toBe('');
    expect(service.filteredAlerts().length).toBe(service.alerts().length);
  });

  // ==========================================================================
  // alertStats — correct totals
  // ==========================================================================

  it('should compute correct alert statistics', async () => {
    await service.loadAlerts();
    const stats = service.alertStats();

    expect(stats.total).toBe(10);

    // Verify sum consistency
    const severitySum = stats.bySeverity.critical + stats.bySeverity.warning + stats.bySeverity.info;
    expect(severitySum).toBe(10);

    const statusSum = stats.byStatus.active + stats.byStatus.acknowledged + stats.byStatus.resolved;
    expect(statusSum).toBe(10);

    const categorySum = stats.byCategory.sensor + stats.byCategory.system +
      stats.byCategory.network + stats.byCategory.maintenance + stats.byCategory.security;
    expect(categorySum).toBe(10);

    // Spot-check known counts from mock data
    // critical: alert-001, alert-003, alert-008 => 3
    expect(stats.bySeverity.critical).toBe(3);
    // warning: alert-002, alert-004, alert-009 => 3
    expect(stats.bySeverity.warning).toBe(3);
    // info: alert-005, alert-006, alert-007, alert-010 => 4
    expect(stats.bySeverity.info).toBe(4);
  });

  // ==========================================================================
  // activeAlerts computed
  // ==========================================================================

  it('should compute activeAlerts as only alerts with status "active"', async () => {
    await service.loadAlerts();
    const active = service.activeAlerts();

    expect(active.length).toBeGreaterThan(0);
    expect(active.every(a => a.status === 'active')).toBeTrue();
  });

  // ==========================================================================
  // criticalAlerts computed
  // ==========================================================================

  it('should compute criticalAlerts as only alerts with severity "critical"', async () => {
    await service.loadAlerts();
    const critical = service.criticalAlerts();

    expect(critical.length).toBe(3);
    expect(critical.every(a => a.severity === 'critical')).toBeTrue();
  });

  // ==========================================================================
  // addAlert — prepends
  // ==========================================================================

  it('should prepend a new alert to the front of the list', async () => {
    await service.loadAlerts();
    const before = service.alerts().length;

    const newAlert = makeAlert({ _id: 'new-alert', title: 'Brand New' });
    service.addAlert(newAlert);

    expect(service.alerts().length).toBe(before + 1);
    expect(service.alerts()[0]._id).toBe('new-alert');
  });

  // ==========================================================================
  // updateAlert — updates matching alert
  // ==========================================================================

  it('should update an existing alert by _id', async () => {
    await service.loadAlerts();
    service.updateAlert('alert-001', { status: 'resolved' });

    const updated = service.alerts().find(a => a._id === 'alert-001');
    expect(updated).toBeDefined();
    expect(updated!.status).toBe('resolved');
  });

  // ==========================================================================
  // removeAlert — removes by _id
  // ==========================================================================

  it('should remove an alert by _id', async () => {
    await service.loadAlerts();
    const before = service.alerts().length;

    service.removeAlert('alert-001');

    expect(service.alerts().length).toBe(before - 1);
    expect(service.alerts().find(a => a._id === 'alert-001')).toBeUndefined();
  });

  // ==========================================================================
  // getAlertById — finds by _id
  // ==========================================================================

  it('should find an alert by _id', async () => {
    await service.loadAlerts();
    const found = service.getAlertById('alert-003');

    expect(found).toBeDefined();
    expect(found!._id).toBe('alert-003');
    expect(found!.title).toContain('Node Communication Lost');
  });

  it('should return undefined for a non-existent _id', async () => {
    await service.loadAlerts();
    expect(service.getAlertById('does-not-exist')).toBeUndefined();
  });

  // ==========================================================================
  // getAlertsBySource — filters by source.id
  // ==========================================================================

  it('should return alerts matching source.id', async () => {
    await service.loadAlerts();
    const result = service.getAlertsBySource('coord-001');

    expect(result.length).toBeGreaterThan(0);
    expect(result.every(a => a.source.id === 'coord-001')).toBeTrue();
  });

  // ==========================================================================
  // recentAlerts — sorted by createdAt desc, max 5
  // ==========================================================================

  it('should return at most 5 recent alerts sorted by createdAt descending', async () => {
    await service.loadAlerts();
    const recent = service.recentAlerts();

    expect(recent.length).toBeLessThanOrEqual(5);
    // Verify descending order
    for (let i = 1; i < recent.length; i++) {
      const prev = new Date(recent[i - 1].createdAt).getTime();
      const curr = new Date(recent[i].createdAt).getTime();
      expect(prev).toBeGreaterThanOrEqual(curr);
    }
  });

  // ==========================================================================
  // acknowledgedAlerts computed
  // ==========================================================================

  it('should compute acknowledgedAlerts correctly', async () => {
    await service.loadAlerts();
    const ack = service.acknowledgedAlerts();

    expect(ack.every(a => a.status === 'acknowledged')).toBeTrue();
    // Mock data: alert-003, alert-009 => 2 acknowledged
    expect(ack.length).toBe(2);
  });

  // ==========================================================================
  // resolvedAlerts computed
  // ==========================================================================

  it('should compute resolvedAlerts correctly', async () => {
    await service.loadAlerts();
    const resolved = service.resolvedAlerts();

    expect(resolved.every(a => a.status === 'resolved')).toBeTrue();
    // Mock data: alert-007, alert-008, alert-010 => 3 resolved
    expect(resolved.length).toBe(3);
  });

  // ==========================================================================
  // Info severity filter
  // ==========================================================================

  it('should filter by severity "info"', async () => {
    await service.loadAlerts();
    service.setSeverityFilter('info');

    const result = service.filteredAlerts();
    expect(result.length).toBe(4);
    expect(result.every(a => a.severity === 'info')).toBeTrue();
  });

  // ==========================================================================
  // addAlert on empty list
  // ==========================================================================

  it('should add alert to an initially empty list', () => {
    expect(service.alerts().length).toBe(0);

    const alert = makeAlert({ _id: 'first' });
    service.addAlert(alert);

    expect(service.alerts().length).toBe(1);
    expect(service.alerts()[0]._id).toBe('first');
  });
});
