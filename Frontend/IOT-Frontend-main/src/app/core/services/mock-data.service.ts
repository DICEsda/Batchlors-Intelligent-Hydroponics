import { Injectable, inject } from '@angular/core';
import { Observable, of, delay } from 'rxjs';
import { EnvironmentService } from './environment.service';
import {
  Site,
  SiteSummary,
  Coordinator,
  CoordinatorSummary,
  Node,
  NodeSummary,
  Zone,
  ZoneSummary,
  Alert,
  SystemMetrics,
  HealthStatus,
} from '../models';

/**
 * Mock Data Service for Smart Tile IoT System
 * Provides realistic mock data for development when backend is unavailable
 */
@Injectable({
  providedIn: 'root'
})
export class MockDataService {
  private readonly env = inject(EnvironmentService);
  
  // Simulated network delay (ms)
  private readonly mockDelay = 200;

  // ============================================================================
  // Mock Data Storage
  // ============================================================================

  private readonly mockSites: Site[] = [
    {
      _id: 'site-001',
      name: 'Main Office Building',
      location: 'Floor 1-3, Building A',
      config: '{"theme": "corporate", "brightness_default": 80}',
      created_at: new Date('2024-01-15T10:00:00Z'),
      updated_at: new Date('2024-12-01T14:30:00Z'),
    },
    {
      _id: 'site-002',
      name: 'Warehouse Complex',
      location: 'Industrial Zone B',
      config: '{"theme": "industrial", "brightness_default": 100}',
      created_at: new Date('2024-03-20T08:00:00Z'),
      updated_at: new Date('2024-11-28T09:15:00Z'),
    },
  ];

  private readonly mockCoordinators: Coordinator[] = [
    {
      _id: 'coord-001',
      coord_id: 'COORD-A1B2C3',
      site_id: 'site-001',
      name: 'Lobby Controller',
      fw_version: '2.1.0',
      nodes_online: 8,
      wifi_rssi: -45,
      mmwave_event_rate: 2.5,
      light_lux: 450,
      temp_c: 23.5,
      last_seen: new Date(Date.now() - 30000), // 30 seconds ago
    },
    {
      _id: 'coord-002',
      coord_id: 'COORD-D4E5F6',
      site_id: 'site-001',
      name: 'Conference Room Hub',
      fw_version: '2.1.0',
      nodes_online: 4,
      wifi_rssi: -52,
      mmwave_event_rate: 1.2,
      light_lux: 320,
      temp_c: 22.8,
      last_seen: new Date(Date.now() - 45000), // 45 seconds ago
    },
    {
      _id: 'coord-003',
      coord_id: 'COORD-G7H8I9',
      site_id: 'site-002',
      name: 'Warehouse Section A',
      fw_version: '2.0.5',
      nodes_online: 12,
      wifi_rssi: -58,
      mmwave_event_rate: 0.8,
      light_lux: 680,
      temp_c: 19.2,
      last_seen: new Date(Date.now() - 60000), // 1 minute ago
    },
  ];

  private readonly mockNodes: Node[] = [
    // Lobby Controller nodes
    {
      _id: 'node-001',
      light_id: 'TILE-001',
      status_mode: 'operational',
      avg_r: -42,
      temp_c: 24.1,
      vbat_mv: 3850,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 15000),
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      zone_id: 'zone-001',
      name: 'Entry Tile 1',
    },
    {
      _id: 'node-002',
      light_id: 'TILE-002',
      status_mode: 'operational',
      avg_r: -48,
      temp_c: 23.8,
      vbat_mv: 3920,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 20000),
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      zone_id: 'zone-001',
      name: 'Entry Tile 2',
    },
    {
      _id: 'node-003',
      light_id: 'TILE-003',
      status_mode: 'operational',
      avg_r: -55,
      temp_c: 24.5,
      vbat_mv: 3680,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 25000),
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      zone_id: 'zone-002',
      name: 'Reception Tile 1',
    },
    {
      _id: 'node-004',
      light_id: 'TILE-004',
      status_mode: 'pairing',
      avg_r: -62,
      temp_c: 23.2,
      vbat_mv: 4050,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 10000),
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      name: 'New Tile (Pairing)',
    },
    {
      _id: 'node-005',
      light_id: 'TILE-005',
      status_mode: 'error',
      avg_r: -78,
      temp_c: 28.1,
      vbat_mv: 2950,
      fw_version: '1.4.8',
      last_seen: new Date(Date.now() - 300000), // 5 minutes ago
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      zone_id: 'zone-001',
      name: 'Faulty Tile',
    },
    // Conference Room nodes
    {
      _id: 'node-006',
      light_id: 'TILE-006',
      status_mode: 'operational',
      avg_r: -44,
      temp_c: 22.5,
      vbat_mv: 3780,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 30000),
      site_id: 'site-001',
      coordinator_id: 'coord-002',
      zone_id: 'zone-003',
      name: 'Conf Room Tile 1',
    },
    {
      _id: 'node-007',
      light_id: 'TILE-007',
      status_mode: 'operational',
      avg_r: -46,
      temp_c: 22.7,
      vbat_mv: 3890,
      fw_version: '1.5.2',
      last_seen: new Date(Date.now() - 35000),
      site_id: 'site-001',
      coordinator_id: 'coord-002',
      zone_id: 'zone-003',
      name: 'Conf Room Tile 2',
    },
    // Warehouse nodes
    {
      _id: 'node-008',
      light_id: 'TILE-008',
      status_mode: 'operational',
      avg_r: -56,
      temp_c: 19.0,
      vbat_mv: 3950,
      fw_version: '1.5.1',
      last_seen: new Date(Date.now() - 40000),
      site_id: 'site-002',
      coordinator_id: 'coord-003',
      zone_id: 'zone-004',
      name: 'Aisle A-1',
    },
    {
      _id: 'node-009',
      light_id: 'TILE-009',
      status_mode: 'operational',
      avg_r: -58,
      temp_c: 18.8,
      vbat_mv: 3820,
      fw_version: '1.5.1',
      last_seen: new Date(Date.now() - 45000),
      site_id: 'site-002',
      coordinator_id: 'coord-003',
      zone_id: 'zone-004',
      name: 'Aisle A-2',
    },
    {
      _id: 'node-010',
      light_id: 'TILE-010',
      status_mode: 'ota',
      avg_r: -52,
      temp_c: 19.5,
      vbat_mv: 3700,
      fw_version: '1.5.0',
      last_seen: new Date(Date.now() - 50000),
      site_id: 'site-002',
      coordinator_id: 'coord-003',
      zone_id: 'zone-005',
      name: 'Loading Dock 1',
    },
  ];

  private readonly mockZones: Zone[] = [
    {
      _id: 'zone-001',
      name: 'Main Entrance',
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      created_at: new Date('2024-01-20T10:00:00Z'),
      updated_at: new Date('2024-06-15T14:30:00Z'),
      // UI extensions
      color: '#3b82f6',
      description: 'Entry area with motion-activated lighting',
      node_ids: ['node-001', 'node-002', 'node-005'],
      brightness: 85,
    },
    {
      _id: 'zone-002',
      name: 'Reception Area',
      site_id: 'site-001',
      coordinator_id: 'coord-001',
      created_at: new Date('2024-01-20T10:00:00Z'),
      updated_at: new Date('2024-06-15T14:30:00Z'),
      // UI extensions
      color: '#10b981',
      description: 'Warm ambient lighting for guest comfort',
      node_ids: ['node-003'],
      brightness: 70,
    },
    {
      _id: 'zone-003',
      name: 'Conference Room A',
      site_id: 'site-001',
      coordinator_id: 'coord-002',
      created_at: new Date('2024-02-10T09:00:00Z'),
      updated_at: new Date('2024-07-20T11:00:00Z'),
      // UI extensions
      color: '#8b5cf6',
      description: 'Adjustable lighting for presentations and meetings',
      node_ids: ['node-006', 'node-007'],
      brightness: 100,
    },
    {
      _id: 'zone-004',
      name: 'Storage Aisles',
      site_id: 'site-002',
      coordinator_id: 'coord-003',
      created_at: new Date('2024-03-25T08:00:00Z'),
      updated_at: new Date('2024-08-10T16:00:00Z'),
      // UI extensions
      color: '#f59e0b',
      description: 'High-intensity lighting for warehouse operations',
      node_ids: ['node-008', 'node-009'],
      brightness: 100,
    },
    {
      _id: 'zone-005',
      name: 'Loading Area',
      site_id: 'site-002',
      coordinator_id: 'coord-003',
      created_at: new Date('2024-03-25T08:00:00Z'),
      updated_at: new Date('2024-08-10T16:00:00Z'),
      // UI extensions
      color: '#ef4444',
      description: 'Bright safety lighting for loading dock operations',
      node_ids: ['node-010'],
      brightness: 95,
    },
  ];

  private readonly mockAlerts: Alert[] = [
    {
      _id: 'alert-001',
      title: 'Low Battery Level',
      severity: 'warning',
      status: 'active',
      category: 'sensor',
      source: { type: 'node', id: 'node-005', name: 'Faulty Tile' },
      message: 'Battery level critical (2950mV)',
      metadata: { value: 2950, threshold: 3200 },
      createdAt: new Date(Date.now() - 3600000), // 1 hour ago
    },
    {
      _id: 'alert-002',
      title: 'High Temperature',
      severity: 'warning',
      status: 'active',
      category: 'sensor',
      source: { type: 'node', id: 'node-005', name: 'Faulty Tile' },
      message: 'Temperature above threshold (28.1C)',
      metadata: { value: 28.1, threshold: 27 },
      createdAt: new Date(Date.now() - 1800000), // 30 minutes ago
    },
    {
      _id: 'alert-003',
      title: 'Low Signal Strength',
      severity: 'info',
      status: 'active',
      category: 'network',
      source: { type: 'node', id: 'node-005', name: 'Faulty Tile' },
      message: 'Weak signal strength (-78 dBm)',
      metadata: { value: -78, threshold: -70 },
      createdAt: new Date(Date.now() - 7200000), // 2 hours ago
    },
  ];

  // ============================================================================
  // Public API Methods
  // ============================================================================

  /**
   * Check if mock data should be used
   */
  shouldUseMockData(): boolean {
    // Use mock data in development mode or when explicitly enabled
    return this.env.isDevelopment || (window as any).USE_MOCK_DATA === true;
  }

  /**
   * Get health status
   */
  getHealth(): Observable<HealthStatus> {
    const health: HealthStatus = {
      status: 'healthy',
      service: 'smart-tile-backend (MOCK)',
      version: '1.0.0-mock',
      uptime: 86400,
      checks: {
        database: true,
        mqtt: true,
        redis: true,
      },
      timestamp: new Date(),
    };
    return of(health).pipe(delay(this.mockDelay));
  }

  /**
   * Get all sites
   */
  getSites(): Observable<Site[]> {
    return of([...this.mockSites]).pipe(delay(this.mockDelay));
  }

  /**
   * Get site by ID
   */
  getSite(siteId: string): Observable<Site | undefined> {
    const site = this.mockSites.find(s => s._id === siteId);
    return of(site).pipe(delay(this.mockDelay));
  }

  /**
   * Get all coordinators
   */
  getCoordinators(): Observable<CoordinatorSummary[]> {
    const summaries: CoordinatorSummary[] = this.mockCoordinators.map(c => ({
      _id: c._id,
      coord_id: c.coord_id,
      name: c.name,
      site_id: c.site_id,
      status: this.getCoordinatorStatus(c),
      nodes_online: c.nodes_online,
      wifi_rssi: c.wifi_rssi,
      light_lux: c.light_lux,
      temp_c: c.temp_c,
      last_seen: c.last_seen,
    }));
    return of(summaries).pipe(delay(this.mockDelay));
  }

  /**
   * Get coordinator by site and coord ID
   */
  getCoordinator(siteId: string, coordId: string): Observable<Coordinator | undefined> {
    const coordinator = this.mockCoordinators.find(
      c => c.site_id === siteId && (c.coord_id === coordId || c._id === coordId)
    );
    return of(coordinator).pipe(delay(this.mockDelay));
  }

  /**
   * Get coordinators for a site
   */
  getCoordinatorsBySite(siteId: string): Observable<CoordinatorSummary[]> {
    const coordinators = this.mockCoordinators
      .filter(c => c.site_id === siteId)
      .map(c => ({
        _id: c._id,
        coord_id: c.coord_id,
        name: c.name,
        site_id: c.site_id,
        status: this.getCoordinatorStatus(c),
        nodes_online: c.nodes_online,
        wifi_rssi: c.wifi_rssi,
        light_lux: c.light_lux,
        temp_c: c.temp_c,
        last_seen: c.last_seen,
      }));
    return of(coordinators).pipe(delay(this.mockDelay));
  }

  /**
   * Get all nodes
   */
  getNodes(): Observable<NodeSummary[]> {
    const summaries: NodeSummary[] = this.mockNodes.map(n => ({
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
    return of(summaries).pipe(delay(this.mockDelay));
  }

  /**
   * Get nodes for a coordinator
   */
  getNodesByCoordinator(siteId: string, coordId: string): Observable<Node[]> {
    const nodes = this.mockNodes.filter(
      n => n.site_id === siteId && (n.coordinator_id === coordId || n.coordinator_id === `coord-${coordId.split('-').pop()}`)
    );
    // Also try matching by coord_id from coordinators
    const coordinator = this.mockCoordinators.find(c => c.coord_id === coordId || c._id === coordId);
    if (coordinator) {
      const coordNodes = this.mockNodes.filter(n => n.coordinator_id === coordinator._id);
      return of(coordNodes).pipe(delay(this.mockDelay));
    }
    return of(nodes).pipe(delay(this.mockDelay));
  }

  /**
   * Get node by ID
   */
  getNode(nodeId: string): Observable<Node | undefined> {
    const node = this.mockNodes.find(n => n._id === nodeId || n.light_id === nodeId);
    return of(node).pipe(delay(this.mockDelay));
  }

  /**
   * Get all zones
   */
  getZones(): Observable<Zone[]> {
    return of([...this.mockZones]).pipe(delay(this.mockDelay));
  }

  /**
   * Get zone by ID
   */
  getZone(zoneId: string): Observable<Zone | undefined> {
    const zone = this.mockZones.find(z => z._id === zoneId);
    return of(zone).pipe(delay(this.mockDelay));
  }

  /**
   * Get alerts
   */
  getAlerts(): Observable<Alert[]> {
    return of([...this.mockAlerts]).pipe(delay(this.mockDelay));
  }

  /**
   * Get system metrics
   */
  getSystemMetrics(): Observable<SystemMetrics> {
    const operationalNodes = this.mockNodes.filter(n => n.status_mode === 'operational').length;
    const lowBatteryNodes = this.mockNodes.filter(n => n.vbat_mv < 3200).length;
    
    const metrics: SystemMetrics = {
      timestamp: new Date(),
      totalSites: this.mockSites.length,
      totalCoordinators: this.mockCoordinators.length,
      onlineCoordinators: this.mockCoordinators.filter(c => this.getCoordinatorStatus(c) === 'online').length,
      averageTemp: this.mockCoordinators.reduce((sum, c) => sum + c.temp_c, 0) / this.mockCoordinators.length,
      averageLightLux: this.mockCoordinators.reduce((sum, c) => sum + c.light_lux, 0) / this.mockCoordinators.length,
      totalNodes: this.mockNodes.length,
      onlineNodes: operationalNodes,
      nodesInPairing: this.mockNodes.filter(n => n.status_mode === 'pairing').length,
      nodesInError: this.mockNodes.filter(n => n.status_mode === 'error').length,
      averageNodeTemp: this.mockNodes.reduce((sum, n) => sum + n.temp_c, 0) / this.mockNodes.length,
      lowBatteryNodes,
      activeAlerts: this.mockAlerts.filter(a => a.status === 'active').length,
      criticalAlerts: this.mockAlerts.filter(a => a.severity === 'critical' && a.status === 'active').length,
    };
    
    return of(metrics).pipe(delay(this.mockDelay));
  }

  // ============================================================================
  // Helper Methods
  // ============================================================================

  private getCoordinatorStatus(coordinator: Coordinator): 'online' | 'offline' | 'warning' | 'error' {
    const lastSeen = new Date(coordinator.last_seen);
    const now = new Date();
    const diffMs = now.getTime() - lastSeen.getTime();
    const diffMinutes = diffMs / (1000 * 60);
    
    if (diffMinutes > 5) return 'offline';
    if (diffMinutes > 2) return 'warning';
    if (coordinator.wifi_rssi < -80) return 'warning';
    return 'online';
  }
}
