/**
 * Digital Twin Models for Hydroponic Farm System
 *
 * These interfaces use camelCase property names (Angular convention).
 * The snake_case REST API responses are automatically transformed
 * by the snakeCaseInterceptor (see core/snake-case.interceptor.ts).
 *
 * Backend source: Backend/src/IoT.Backend/Models/DigitalTwin/
 */

// ============================================================================
// Enums
// ============================================================================

/** Sync status values — stored lowercase in MongoDB, serialized lowercase by REST */
/** Backend serializes InSync as "insync" (no underscore). Accept both forms. */
export type SyncStatus = 'in_sync' | 'insync' | 'pending' | 'stale' | 'conflict' | 'offline';

// ============================================================================
// Twin Metadata
// ============================================================================

export interface TwinMetadata {
  version: number;
  syncStatus: SyncStatus;
  lastReportedAt: string | null;
  lastDesiredAt: string | null;
  lastSyncAttempt: string | null;
  syncRetryCount: number;
  createdAt: string;
  updatedAt: string;
  isConnected: boolean;
  connectionQuality: number;
}

// ============================================================================
// Tower Twin (matches TowerTwin.cs)
// ============================================================================

export interface TowerReportedState {
  airTempC: number | null;
  humidityPct: number | null;
  lightLux: number | null;
  pumpOn: boolean | null;
  lightOn: boolean | null;
  lightBrightness: number | null;
  statusMode: string | null;
  vbatMv: number | null;
  fwVersion: string | null;
  uptimeS: number | null;
  signalQuality: number | null;
}

export interface TowerDesiredState {
  pumpOn: boolean | null;
  lightOn: boolean | null;
  lightBrightness: number | null;
  statusMode: string | null;
}

export interface TowerCapabilities {
  slotCount: number;
  hasPump: boolean;
  hasLight: boolean;
  hasTemperatureSensor: boolean;
  hasHumiditySensor: boolean;
  hasLightSensor: boolean;
}

export interface MlPredictions {
  predictedHeightCm: number | null;
  expectedHarvestDate: string | null;
  daysToHarvest: number | null;
  growthRateCmPerDay: number | null;
  healthScore: number | null;
  confidence: number | null;
  modelName: string | null;
  modelVersion: string | null;
  lastUpdatedAt: string | null;
  inputAvgTempC: number | null;
  inputAvgHumidityPct: number | null;
  inputAvgLightLux: number | null;
}

export interface TowerTwin {
  _id: string;
  towerId: string;
  coordId: string;
  farmId: string;
  name: string;
  reported: TowerReportedState;
  desired: TowerDesiredState;
  metadata: TwinMetadata;
  capabilities: TowerCapabilities | null;
  cropType: string | null;
  plantingDate: string | null;
  lastHeightCm: number | null;
  lastHeightAt: string | null;
  predictedHeightCm: number | null;
  expectedHarvestDate: string | null;
  mlPredictions: MlPredictions | null;
}

// ============================================================================
// Coordinator Twin (matches CoordinatorTwin.cs)
// ============================================================================

export interface CoordinatorReportedState {
  fwVersion: string | null;
  towersOnline: number;
  nodesOnline: number;
  wifiRssi: number | null;
  statusMode: string | null;
  uptimeS: number | null;
  lightLux: number | null;
  tempC: number | null;
  // Reservoir sensors
  ph: number | null;
  ecMsCm: number | null;
  tdsPpm: number | null;
  waterTempC: number | null;
  waterLevelPct: number | null;
  waterLevelCm: number | null;
  lowWaterAlert: boolean | null;
  // Actuators
  mainPumpOn: boolean | null;
  dosingPumpPhOn: boolean | null;
  dosingPumpNutrientOn: boolean | null;
}

export interface ReservoirSetpoints {
  phTarget: number;
  phTolerance: number;
  ecTarget: number;
  ecTolerance: number;
  waterLevelMinPct: number;
  waterTempTargetC: number;
}

export interface CoordinatorDesiredState {
  mainPumpOn: boolean | null;
  dosingPumpPhOn: boolean | null;
  dosingPumpNutrientOn: boolean | null;
  statusMode: string | null;
  setpoints: ReservoirSetpoints | null;
}

export interface CoordinatorCapabilities {
  phSensor: boolean;
  ecSensor: boolean;
  waterTempSensor: boolean;
  waterLevelSensor: boolean;
  mainPump: boolean;
  phDosingPump: boolean;
  nutrientDosingPump: boolean;
  maxTowers: number;
  lightSensor: boolean;
}

export interface CoordinatorTwin {
  _id: string;
  coordId: string;
  siteId: string;
  farmId: string;
  name: string;
  reported: CoordinatorReportedState;
  desired: CoordinatorDesiredState;
  metadata: TwinMetadata;
  capabilities: CoordinatorCapabilities | null;
}

// ============================================================================
// API Response Types (matches Responses.cs)
// ============================================================================

export interface FarmTwinsResponse {
  farmId: string;
  coordinators: CoordinatorTwin[];
  towers: TowerTwin[];
}

export interface TowerDeltaResponse {
  towerId: string;
  syncStatus: SyncStatus;
  isInSync: boolean;
  delta: TowerDesiredState | null;
}

export interface CoordinatorDeltaResponse {
  coordId: string;
  syncStatus: SyncStatus;
  isInSync: boolean;
  delta: CoordinatorDesiredState | null;
}

// ============================================================================
// WebSocket Event Payload (matches backend WsBroadcaster camelCase shape)
// NOTE: WebSocket already uses camelCase (WsBroadcaster uses CamelCase policy)
// ============================================================================

export interface TwinUpdatePayload {
  changeType: string;
  deviceId: string;
  farmId?: string;
  coordId?: string;
  towerTwin?: TowerTwin;
  coordinatorTwin?: CoordinatorTwin;
  towerReported?: TowerReportedState;
  coordinatorReported?: CoordinatorReportedState;
  timestamp?: string;
}

// ============================================================================
// 3D Positioning (for future 3D visualization)
// ============================================================================

export interface Position3D {
  x: number;
  y: number;
  z: number;
}

export interface Rotation3D {
  x: number;
  y: number;
  z: number;
}

export interface Transform3D {
  position: Position3D;
  rotation: Rotation3D;
  scale: Position3D;
}

// ============================================================================
// 3D Model Configuration (for future 3D visualization)
// ============================================================================

export interface TowerModelConfig {
  slotCount: number;
  slotSpacing: number;
  towerRadius: number;
  towerHeight: number;
  baseHeight: number;
  towerMaterial: string;
  slotMaterial: string;
  plantMaterials: Record<string, string>;
  rotationSpeed?: number;
  highlightOnHover: boolean;
}

export interface FarmLayoutConfig {
  gridRows: number;
  gridCols: number;
  cellWidth: number;
  cellDepth: number;
  coordinatorSpacing: number;
  towerSpacing: number;
}

// ============================================================================
// Visualization Events (for future 3D visualization)
// ============================================================================

export interface VisualizationEvent {
  type: 'select' | 'hover' | 'click' | 'zoom' | 'pan';
  target?: {
    type: 'coordinator' | 'tower' | 'slot' | 'plant';
    id: string;
    slotIndex?: number;
  };
  position?: Position3D;
  timestamp: Date;
}

export interface SelectionState {
  selectedType?: 'coordinator' | 'tower' | 'slot';
  selectedId?: string;
  selectedSlotIndex?: number;
  hoveredType?: 'coordinator' | 'tower' | 'slot';
  hoveredId?: string;
  hoveredSlotIndex?: number;
}
