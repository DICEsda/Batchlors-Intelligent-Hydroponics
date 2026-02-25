/**
 * Tower Models for Hydroponic Farm System
 * Towers are vertical growing units connected to coordinators
 */

// ============================================================================
// Plant Metrics
// ============================================================================

export interface PlantMetrics {
  heightCm: number;
  leafCount?: number;
  healthScore: number;      // 0-100
  growthRate?: number;      // cm/day
  daysSincePlanting: number;
  lastMeasured: Date;
}

export interface PlantSlot {
  slotIndex: number;        // Position in tower (1-based)
  plantType?: string;       // e.g., "lettuce", "basil", "tomato"
  plantedDate?: Date;
  metrics?: PlantMetrics;
  isEmpty: boolean;
}

// ============================================================================
// Tower Sensor Data
// ============================================================================

export interface TowerSensorData {
  ambientTemp: number;      // Celsius
  humidity: number;         // Percentage
  lightLevel: number;       // Lux
  soilMoisture?: number;    // Percentage (if applicable)
  co2Level?: number;        // PPM
  lastUpdated: Date;
}

// ============================================================================
// Tower
// ============================================================================

export type TowerStatus = 'online' | 'offline' | 'warning' | 'error';

export interface Tower {
  _id: string;
  towerId: string;
  name: string;
  macAddress: string;
  
  // Parent coordinator
  coordId: string;
  coordinatorName?: string;
  
  // Firmware
  firmwareVersion: string;
  hardwareRevision?: string;
  
  // System health
  status: TowerStatus;
  uptime: number;           // Seconds
  heapFree: number;         // Bytes
  batteryVoltage?: number;
  batteryPercent?: number;
  
  // Tower configuration
  slotCount: number;        // Total plant slots
  activeLeds: boolean;
  ledBrightness: number;    // 0-100
  ledSchedule?: LedSchedule;
  
  // Sensor data
  sensors: TowerSensorData;
  
  // Plants
  plants: PlantSlot[];
  occupiedSlots: number;
  
  // Timestamps
  lastSeen: Date;
  createdAt: Date;
  updatedAt: Date;
}

// ============================================================================
// LED Schedule
// ============================================================================

export interface LedSchedule {
  enabled: boolean;
  onTime: string;           // HH:MM format
  offTime: string;          // HH:MM format
  brightness: number;       // 0-100
  spectrum?: LedSpectrum;
}

export interface LedSpectrum {
  red: number;              // 0-100
  blue: number;             // 0-100
  white: number;            // 0-100
  uv?: number;              // 0-100
}

// ============================================================================
// Tower Summary (for list views)
// ============================================================================

export interface TowerSummary {
  _id: string;
  towerId: string;
  name: string;
  coordId: string;
  status: TowerStatus;
  occupiedSlots: number;
  slotCount: number;
  sensors: {
    ambientTemp: number;
    humidity: number;
    lightLevel: number;
  };
  lastSeen: Date;
}

// ============================================================================
// Tower Commands
// ============================================================================

export interface TowerLedCommand {
  towerId: string;
  activeLeds?: boolean;
  brightness?: number;
  spectrum?: LedSpectrum;
}

export interface TowerConfigUpdate {
  towerId: string;
  name?: string;
  ledSchedule?: LedSchedule;
}

export interface PlantSlotUpdate {
  towerId: string;
  slotIndex: number;
  plantType?: string;
  plantedDate?: Date;
  metrics?: Partial<PlantMetrics>;
  clear?: boolean;          // Set to true to clear the slot
}
