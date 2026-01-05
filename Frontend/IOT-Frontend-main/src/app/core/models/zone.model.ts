/**
 * Zone Models for Smart Tile IoT System
 * Zones are logical groupings of nodes within a coordinator
 */

// ============================================================================
// Zone
// ============================================================================

export interface Zone {
  _id: string;
  name: string;
  site_id: string;
  coordinator_id: string;
  created_at: Date;
  updated_at: Date;
  // UI-specific properties (frontend extensions)
  color?: string;           // Zone color for visual grouping
  description?: string;     // Optional description
  node_ids?: string[];      // IDs of nodes in this zone
  brightness?: number;      // Current brightness level (0-100)
}

// ============================================================================
// Zone Summary (for list views)
// ============================================================================

export interface ZoneSummary {
  _id: string;
  name: string;
  site_id: string;
  coordinator_id: string;
  nodeCount?: number;
}

// ============================================================================
// Zone Commands
// ============================================================================

export interface CreateZoneRequest {
  name: string;
  site_id: string;
  coordinator_id: string;
}

export interface UpdateZoneRequest {
  name?: string;
}

export interface AssignNodeToZoneRequest {
  node_id: string;
  zone_id: string;
}
