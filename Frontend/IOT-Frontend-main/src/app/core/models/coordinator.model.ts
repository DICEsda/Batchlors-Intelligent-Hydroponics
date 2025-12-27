/**
 * Coordinator Models for Hydroponic Farm System
 * Coordinators manage reservoir systems and connected towers
 */

// ============================================================================
// Reservoir State
// ============================================================================

export interface ReservoirState {
  ph: number;
  ec: number;              // Electrical conductivity (mS/cm)
  temperature: number;     // Celsius
  waterLevel: number;      // Percentage (0-100)
  lastUpdated: Date;
}

export interface ReservoirThresholds {
  phMin: number;
  phMax: number;
  ecMin: number;
  ecMax: number;
  tempMin: number;
  tempMax: number;
  waterLevelMin: number;
}

// ============================================================================
// Coordinator
// ============================================================================

export type CoordinatorStatus = 'online' | 'offline' | 'warning' | 'error';

export interface Coordinator {
  _id: string;
  coordId: string;
  name: string;
  macAddress: string;
  ipAddress?: string;
  
  // Connection info
  wifiSsid?: string;
  wifiRssi?: number;
  mqttBroker?: string;
  mqttConnected: boolean;
  
  // Firmware
  firmwareVersion: string;
  hardwareRevision?: string;
  
  // System health
  status: CoordinatorStatus;
  uptime: number;           // Seconds
  heapFree: number;         // Bytes
  cpuTemp?: number;         // Celsius
  
  // Reservoir
  reservoir: ReservoirState;
  thresholds: ReservoirThresholds;
  
  // Connected towers
  towerIds: string[];
  towerCount: number;
  
  // Timestamps
  lastSeen: Date;
  createdAt: Date;
  updatedAt: Date;
}

// ============================================================================
// Coordinator Summary (for list views)
// ============================================================================

export interface CoordinatorSummary {
  _id: string;
  coordId: string;
  name: string;
  status: CoordinatorStatus;
  towerCount: number;
  reservoir: {
    ph: number;
    ec: number;
    temperature: number;
    waterLevel: number;
  };
  lastSeen: Date;
}

// ============================================================================
// Coordinator Commands
// ============================================================================

export interface ReservoirAdjustCommand {
  coordId: string;
  phTarget?: number;
  ecTarget?: number;
  addNutrients?: boolean;
  addPhUp?: boolean;
  addPhDown?: boolean;
}

export interface CoordinatorConfigUpdate {
  coordId: string;
  name?: string;
  thresholds?: Partial<ReservoirThresholds>;
  mqttBroker?: string;
}
