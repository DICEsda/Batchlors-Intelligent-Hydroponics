/**
 * Pairing Models for Hydroponic Farm System
 * Models for coordinator-tower pairing workflow
 */

// ============================================================================
// Pairing Session (from backend API response)
// ============================================================================

/**
 * Represents an active pairing session for a coordinator.
 * Tracks when the coordinator enters pairing mode and any pending tower requests.
 */
export interface PairingSession {
  _id?: string;
  
  /** Coordinator ID that initiated the pairing session */
  coord_id: string;
  
  /** Farm ID for the coordinator */
  farm_id: string;
  
  /** Session status: "active", "completed", "expired", "cancelled" */
  status: 'active' | 'completed' | 'expired' | 'cancelled';
  
  /** When the pairing session started */
  started_at: string;
  
  /** When the pairing session expires (auto-timeout) */
  expires_at: string;
  
  /** Duration of the pairing window in seconds */
  duration_s: number;
  
  /** When the session was completed or cancelled */
  ended_at?: string;
  
  /** List of pending tower pairing requests for this session */
  pending_requests: TowerPairingRequest[];
  
  /** List of tower IDs that were approved during this session */
  approved_towers: string[];
  
  /** List of tower IDs that were rejected during this session */
  rejected_towers: string[];
}

// ============================================================================
// Tower Pairing Request
// ============================================================================

/**
 * Tower capabilities reported during pairing
 */
export interface TowerCapabilities {
  has_pump: boolean;
  has_light: boolean;
  slot_count: number;
  sensor_types: string[];
}

/**
 * Represents a tower's request to pair with a coordinator.
 * Received via MQTT when a tower broadcasts its pairing request.
 */
export interface TowerPairingRequest {
  /** Unique identifier for this request (generated) */
  request_id: string;
  
  /** Tower's unique ID (MAC-based or assigned) */
  tower_id: string;
  
  /** Tower's MAC address */
  mac_address: string;
  
  /** Request status: "pending", "approved", "rejected", "expired" */
  status: 'pending' | 'approved' | 'rejected' | 'expired';
  
  /** When the pairing request was received */
  requested_at: string;
  
  /** When the request was approved/rejected */
  resolved_at?: string;
  
  /** Firmware version reported by the tower */
  fw_version?: string;
  
  /** Hardware capabilities reported by the tower */
  capabilities?: TowerCapabilities;
  
  /** Signal strength (RSSI) during the pairing request */
  rssi?: number;
}

// ============================================================================
// Tower Model (for API response when approving pairing)
// ============================================================================

/**
 * Basic tower model returned when approving a pairing request
 */
export interface Tower {
  _id?: string;
  tower_id: string;
  coord_id: string;
  farm_id: string;
  name?: string;
  mac_address?: string;
  fw_version?: string;
  status_mode?: 'operational' | 'pairing' | 'ota' | 'error' | 'idle';
  last_seen?: string;
  created_at?: string;
  updated_at?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Calculate remaining seconds in a pairing session
 */
export function getPairingSecondsRemaining(session: PairingSession): number {
  if (session.status !== 'active') return 0;
  
  const expiresAt = new Date(session.expires_at).getTime();
  const now = Date.now();
  const remaining = Math.max(0, Math.floor((expiresAt - now) / 1000));
  
  return remaining;
}

/**
 * Check if a pairing session is still active
 */
export function isPairingSessionActive(session: PairingSession): boolean {
  if (session.status !== 'active') return false;
  
  const expiresAt = new Date(session.expires_at).getTime();
  return Date.now() < expiresAt;
}

/**
 * Format remaining time as MM:SS
 */
export function formatPairingCountdown(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}
