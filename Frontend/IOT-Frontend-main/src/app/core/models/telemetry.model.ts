/**
 * Telemetry Models for Hydroponic Farm System
 * Real-time sensor data from coordinators and towers
 */

// ============================================================================
// Coordinator Telemetry
// ============================================================================

export interface ReservoirTelemetry {
  coordId: string;
  timestamp: Date;
  
  // Reservoir readings
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

// ============================================================================
// Tower Telemetry
// ============================================================================

export interface TowerTelemetry {
  towerId: string;
  coordId: string;
  timestamp: Date;
  
  // Environmental readings
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

export interface FarmMetrics {
  timestamp: Date;
  
  // Coordinator aggregates
  totalCoordinators: number;
  onlineCoordinators: number;
  averagePh: number;
  averageEc: number;
  averageReservoirTemp: number;
  lowWaterLevelCount: number;
  
  // Tower aggregates
  totalTowers: number;
  onlineTowers: number;
  totalPlantSlots: number;
  occupiedSlots: number;
  averageAmbientTemp: number;
  averageHumidity: number;
  
  // Alerts
  activeAlerts: number;
  criticalAlerts: number;
}

// ============================================================================
// Alerts
// ============================================================================

export type AlertSeverity = 'info' | 'warning' | 'critical';
export type AlertType = 
  | 'ph_high' | 'ph_low'
  | 'ec_high' | 'ec_low'
  | 'temp_high' | 'temp_low'
  | 'water_low'
  | 'device_offline'
  | 'pump_error'
  | 'sensor_error';

export interface Alert {
  _id: string;
  type: AlertType;
  severity: AlertSeverity;
  deviceType: 'coordinator' | 'tower';
  deviceId: string;
  deviceName: string;
  message: string;
  value?: number;
  threshold?: number;
  acknowledged: boolean;
  acknowledgedBy?: string;
  acknowledgedAt?: Date;
  createdAt: Date;
  resolvedAt?: Date;
}
