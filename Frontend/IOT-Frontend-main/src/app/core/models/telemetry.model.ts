/**
 * Telemetry Models for Smart Tile IoT System
 * Real-time sensor data from coordinators and nodes
 */

// ============================================================================
// Coordinator Telemetry
// ============================================================================

export interface CoordinatorTelemetryData {
  coord_id: string;
  site_id: string;
  timestamp: Date;
  nodes_online: number;
  wifi_rssi: number;
  mmwave_event_rate: number;
  light_lux: number;
  temp_c: number;
}

// ============================================================================
// Node Telemetry
// ============================================================================

export interface NodeTelemetryData {
  node_id: string;
  light_id: string;
  coordinator_id: string;
  timestamp: Date;
  temp_c: number;
  vbat_mv: number;
  avg_r: number;
  status_mode: string;
}

// ============================================================================
// Legacy Hydroponic Telemetry (kept for compatibility)
// ============================================================================

export interface ReservoirTelemetry {
  coordId: string;
  timestamp: Date;
  
  // Reservoir readings (legacy - not used in Smart Tile system)
  ph: number;
  ec: number;
  temperature: number;
  waterLevel: number;
  
  // System metrics
  uptime: number;
  heapFree: number;
  wifiRssi: number;
  mqttConnected: boolean;
  
  // Optional extended metrics
  pumpStatus?: 'on' | 'off' | 'error';
  dosingStatus?: DosingStatus;
}

export interface DosingStatus {
  phUpActive: boolean;
  phDownActive: boolean;
  nutrientAActive: boolean;
  nutrientBActive: boolean;
  lastDosingTime?: Date;
}

export interface TowerTelemetry {
  towerId: string;
  coordId: string;
  timestamp: Date;
  
  // Environmental readings (legacy)
  ambientTemp: number;
  humidity: number;
  lightLevel: number;
  soilMoisture?: number;
  co2Level?: number;
  
  // System metrics
  uptime: number;
  heapFree: number;
  batteryVoltage?: number;
  batteryPercent?: number;
  
  // LED status
  ledsActive: boolean;
  ledBrightness: number;
}

// ============================================================================
// Historical Telemetry
// ============================================================================

export interface TelemetryDataPoint {
  timestamp: Date;
  value: number;
}

export interface CoordinatorHistory {
  coord_id: string;
  site_id: string;
  timeRange: TimeRange;
  temp_c: TelemetryDataPoint[];
  light_lux: TelemetryDataPoint[];
  wifi_rssi: TelemetryDataPoint[];
  nodes_online: TelemetryDataPoint[];
}

export interface NodeHistory {
  node_id: string;
  coordinator_id: string;
  timeRange: TimeRange;
  temp_c: TelemetryDataPoint[];
  vbat_mv: TelemetryDataPoint[];
  avg_r: TelemetryDataPoint[];
}

// Legacy history types (kept for compatibility)
export interface ReservoirHistory {
  coordId: string;
  timeRange: TimeRange;
  ph: TelemetryDataPoint[];
  ec: TelemetryDataPoint[];
  temperature: TelemetryDataPoint[];
  waterLevel: TelemetryDataPoint[];
}

export interface TowerHistory {
  towerId: string;
  timeRange: TimeRange;
  ambientTemp: TelemetryDataPoint[];
  humidity: TelemetryDataPoint[];
  lightLevel: TelemetryDataPoint[];
}

export interface TimeRange {
  start: Date;
  end: Date;
  interval: '1m' | '5m' | '15m' | '1h' | '6h' | '1d';
}

// ============================================================================
// Aggregated Metrics
// ============================================================================

export interface SystemMetrics {
  timestamp: Date;
  
  // Site aggregates
  totalSites: number;
  
  // Coordinator aggregates
  totalCoordinators: number;
  onlineCoordinators: number;
  averageTemp: number;
  averageLightLux: number;
  
  // Node aggregates
  totalNodes: number;
  onlineNodes: number;
  nodesInPairing: number;
  nodesInError: number;
  averageNodeTemp: number;
  lowBatteryNodes: number;
  
  // Alerts
  activeAlerts: number;
  criticalAlerts: number;
}

// Legacy FarmMetrics (kept for compatibility)
export interface FarmMetrics {
  timestamp: Date;
  
  // Coordinator aggregates (mapped from Smart Tile data)
  totalCoordinators: number;
  onlineCoordinators: number;
  averagePh: number;           // Not used in Smart Tile - kept for compatibility
  averageEc: number;           // Not used in Smart Tile - kept for compatibility
  averageReservoirTemp: number; // Maps to averageTemp
  lowWaterLevelCount: number;  // Not used in Smart Tile
  
  // Tower aggregates (mapped from Node data)
  totalTowers: number;          // Maps to totalNodes
  onlineTowers: number;         // Maps to onlineNodes
  totalPlantSlots: number;      // Not used in Smart Tile
  occupiedSlots: number;        // Not used in Smart Tile
  averageAmbientTemp: number;   // Maps to averageNodeTemp
  averageHumidity: number;      // Not used in Smart Tile
  
  // Alerts
  activeAlerts: number;
  criticalAlerts: number;
}

// ============================================================================
// Alerts - MOVED TO alert.model.ts
// ============================================================================
// Alert, AlertSeverity, AlertStatus, AlertCategory, and related types
// are now defined in ./alert.model.ts for a more comprehensive alert system.
