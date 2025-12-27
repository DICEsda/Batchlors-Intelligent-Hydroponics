/**
 * Digital Twin Models for Hydroponic Farm System
 * System topology, real-time state, and 3D visualization
 */

// ============================================================================
// Farm Topology
// ============================================================================

export interface FarmTopology {
  farmId: string;
  farmName: string;
  
  // Coordinators with their towers
  coordinators: CoordinatorNode[];
  
  // Connections (for graph visualization)
  connections: TopologyConnection[];
  
  // Metadata
  lastUpdated: Date;
}

export interface CoordinatorNode {
  coordId: string;
  name: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  position?: Position3D;
  
  // Quick stats
  ph: number;
  ec: number;
  waterLevel: number;
  
  // Connected towers
  towers: TowerNode[];
}

export interface TowerNode {
  towerId: string;
  name: string;
  coordId: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  position?: Position3D;
  
  // Quick stats
  occupiedSlots: number;
  totalSlots: number;
  averageHealthScore: number;
  
  // Plant slots for 3D visualization
  slots: SlotNode[];
}

export interface SlotNode {
  slotIndex: number;
  isEmpty: boolean;
  plantType?: string;
  heightCm?: number;
  healthScore?: number;
  position?: Position3D;
}

export interface TopologyConnection {
  sourceId: string;
  sourceType: 'farm' | 'coordinator' | 'tower';
  targetId: string;
  targetType: 'coordinator' | 'tower' | 'slot';
  connectionType: 'control' | 'data' | 'power';
  status: 'active' | 'inactive' | 'error';
}

// ============================================================================
// 3D Positioning
// ============================================================================

export interface Position3D {
  x: number;
  y: number;
  z: number;
}

export interface Rotation3D {
  x: number;                // Pitch
  y: number;                // Yaw
  z: number;                // Roll
}

export interface Transform3D {
  position: Position3D;
  rotation: Rotation3D;
  scale: Position3D;
}

// ============================================================================
// Digital Twin State
// ============================================================================

export interface DigitalTwinState {
  timestamp: Date;
  
  // Farm-level state
  farmStatus: 'normal' | 'warning' | 'critical';
  activeAlertCount: number;
  
  // Aggregated metrics
  metrics: {
    totalCoordinators: number;
    onlineCoordinators: number;
    totalTowers: number;
    onlineTowers: number;
    totalPlants: number;
    averageHealthScore: number;
  };
  
  // Device states (keyed by device ID)
  coordinatorStates: Map<string, CoordinatorTwinState>;
  towerStates: Map<string, TowerTwinState>;
}

export interface CoordinatorTwinState {
  coordId: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  reservoir: {
    ph: number;
    ec: number;
    temperature: number;
    waterLevel: number;
  };
  lastUpdate: Date;
}

export interface TowerTwinState {
  towerId: string;
  coordId: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  sensors: {
    ambientTemp: number;
    humidity: number;
    lightLevel: number;
  };
  ledsActive: boolean;
  slotStates: SlotTwinState[];
  lastUpdate: Date;
}

export interface SlotTwinState {
  slotIndex: number;
  isEmpty: boolean;
  plantType?: string;
  heightCm?: number;
  healthScore?: number;
}

// ============================================================================
// 3D Model Configuration
// ============================================================================

export interface TowerModelConfig {
  slotCount: number;
  slotSpacing: number;      // Vertical spacing in units
  towerRadius: number;
  towerHeight: number;
  baseHeight: number;
  
  // Materials
  towerMaterial: string;
  slotMaterial: string;
  plantMaterials: Record<string, string>;
  
  // Animation
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
// Visualization Events
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
