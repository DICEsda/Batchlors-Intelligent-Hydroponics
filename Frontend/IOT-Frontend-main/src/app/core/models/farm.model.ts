/**
 * Farm Models for Smart Tile IoT System
 * Farms are the top-level organizational unit containing reservoirs (coordinators) and towers (nodes)
 * 
 * Hierarchy: Farm -> Reservoir(s) -> Tower(s)
 */

// ============================================================================
// Farm
// ============================================================================

export interface Farm {
  _id: string;
  name: string;
  description?: string;
  location?: string;
  plantType?: string;              // Type of plants grown in this farm
  created_at: Date;
  updated_at: Date;
  
  // Relationships
  reservoir_ids: string[];      // IDs of reservoirs (coordinators) in this farm
  
  // UI-specific properties
  color?: string;               // Farm color for visual grouping
  image_url?: string;           // Optional farm image
  
  // Computed/aggregated stats (populated by service)
  stats?: FarmStats;
}

export interface FarmStats {
  totalReservoirs: number;
  onlineReservoirs: number;
  totalTowers: number;
  onlineTowers: number;
  activeAlerts: number;
}

// ============================================================================
// Farm Summary (for list views)
// ============================================================================

export interface FarmSummary {
  _id: string;
  name: string;
  description?: string;
  location?: string;
  plantType?: string;
  reservoirCount: number;
  towerCount: number;
  onlineReservoirs: number;
  onlineTowers: number;
  color?: string;
}

// ============================================================================
// Farm with Nested Data (for detail view)
// ============================================================================

export interface FarmWithReservoirs extends Farm {
  reservoirs: ReservoirInFarm[];
}

export interface ReservoirInFarm {
  _id: string;
  coord_id: string;
  name?: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  towers: TowerInReservoir[];
  // Telemetry summary
  light_lux?: number;
  temp_c?: number;
  last_seen?: Date;
}

export interface TowerInReservoir {
  _id: string;
  light_id: string;
  name?: string;
  status_mode: 'operational' | 'pairing' | 'ota' | 'error' | 'offline';
  // Telemetry summary
  temp_c?: number;
  vbat_mv?: number;
  last_seen?: Date;
}

// ============================================================================
// Farm Commands
// ============================================================================

export interface CreateFarmRequest {
  name: string;
  description?: string;
  location?: string;
  plantType?: string;
  color?: string;
  reservoir_ids?: string[];
}

export interface UpdateFarmRequest {
  name?: string;
  description?: string;
  location?: string;
  plantType?: string;
  color?: string;
  reservoir_ids?: string[];
}

export interface AssignReservoirToFarmRequest {
  farm_id: string;
  reservoir_id: string;
}

export interface RemoveReservoirFromFarmRequest {
  farm_id: string;
  reservoir_id: string;
}
