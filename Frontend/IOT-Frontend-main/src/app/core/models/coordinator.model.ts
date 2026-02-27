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
// Status Helper Functions
// ============================================================================

/**
 * Compute coordinator connection status from last_seen timestamp.
 * Thresholds aligned with the backend TwinSyncBackgroundService stale check
 * (default StaleThresholdSeconds = 120).
 */
export function getCoordinatorStatus(coordinator: Coordinator | CoordinatorSummary): CoordinatorStatus {
  // Handle the snakeCaseInterceptor: runtime may have camelCase keys
  const a = coordinator as any;
  const raw = a.lastSeen ?? a.last_seen ?? coordinator.last_seen;
  if (!raw) return 'offline';

  const lastSeen = new Date(raw);
  const now = new Date();
  const diffMs = now.getTime() - lastSeen.getTime();
  const diffSeconds = diffMs / 1000;

  // >120s  (2 min) without telemetry = offline  (matches backend stale threshold)
  if (diffSeconds > 120) return 'offline';
  // >60s   (1 min) = warning (may be lagging)
  if (diffSeconds > 60) return 'warning';
  // Very weak signal
  const rssi = a.wifiRssi ?? a.wifi_rssi ?? coordinator.wifi_rssi;
  if (rssi != null && rssi < -80) return 'warning';
  return 'online';
}

export function getSignalStrength(rssi: number): 'excellent' | 'good' | 'fair' | 'poor' {
  if (rssi >= -50) return 'excellent';
  if (rssi >= -60) return 'good';
  if (rssi >= -70) return 'fair';
  return 'poor';
}
