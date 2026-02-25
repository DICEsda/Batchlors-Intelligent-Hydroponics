/**
 * Coordinator Models for Smart Tile IoT System
 * Coordinators manage zones and connected nodes (smart tiles)
 */

// ============================================================================
// Coordinator Status
// ============================================================================

export type CoordinatorStatus = 'online' | 'offline' | 'warning' | 'error';

// ============================================================================
// Coordinator
// ============================================================================

export interface Coordinator {
  _id: string;
  coord_id: string;
  site_id: string;
  fw_version: string;
  nodes_online: number;
  wifi_rssi: number;
  mmwave_event_rate: number;    // mmWave radar event rate
  light_lux: number;            // Ambient light level
  temp_c: number;               // Temperature in Celsius
  last_seen: Date;
  
  // Optional fields from extended coordinator info
  name?: string;
  ip_address?: string;
  mac_address?: string;
  
  // Status field - can be computed or stored
  status?: CoordinatorStatus;
}

// ============================================================================
// Coordinator Summary (for list views)
// ============================================================================

export interface CoordinatorSummary {
  _id: string;
  coord_id: string;
  name?: string;
  site_id: string;
  status: CoordinatorStatus;
  nodes_online: number;
  wifi_rssi: number;
  light_lux: number;
  temp_c: number;
  last_seen: Date;
}

// ============================================================================
// Coordinator Telemetry (real-time data)
// ============================================================================

export interface CoordinatorTelemetry {
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
// Coordinator Commands
// ============================================================================

/**
 * Command to start pairing mode on a coordinator.
 * Matches backend StartPairingRequest in coordinator_handlers.go
 */
export interface CoordinatorPairCommand {
  site_id: string;
  coordinator_id: string;
  duration_ms?: number;  // defaults to 60000 (60 seconds) on backend
}

/**
 * Default pairing duration in milliseconds
 */
export const DEFAULT_PAIRING_DURATION_MS = 60000;

export interface CoordinatorRestartCommand {
  coord_id: string;
}

export interface CoordinatorWifiCommand {
  coord_id: string;
  ssid: string;
  password: string;
}

export interface CoordinatorConfigUpdate {
  coord_id: string;
  name?: string;
}

// ============================================================================
// mmWave Radar Data
// ============================================================================

export interface MmWaveTarget {
  x: number;
  y: number;
  speed: number;
  resolution: number;
}

export interface MmWaveFrame {
  coord_id: string;
  timestamp: Date;
  targets: MmWaveTarget[];
}

// ============================================================================
// Status Helper Functions
// ============================================================================

export function getCoordinatorStatus(coordinator: Coordinator | CoordinatorSummary): CoordinatorStatus {
  const lastSeen = new Date(coordinator.last_seen);
  const now = new Date();
  const diffMs = now.getTime() - lastSeen.getTime();
  const diffMinutes = diffMs / (1000 * 60);
  
  if (diffMinutes > 5) return 'offline';
  if (diffMinutes > 2) return 'warning';
  if (coordinator.wifi_rssi < -80) return 'warning';
  return 'online';
}

export function getSignalStrength(rssi: number): 'excellent' | 'good' | 'fair' | 'poor' {
  if (rssi >= -50) return 'excellent';
  if (rssi >= -60) return 'good';
  if (rssi >= -70) return 'fair';
  return 'poor';
}
