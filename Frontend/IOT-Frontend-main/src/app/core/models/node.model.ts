/**
 * Node Models for Smart Tile IoT System
 * Nodes are individual smart tile/lighting units connected to coordinators
 */

// ============================================================================
// Node Status
// ============================================================================

export type NodeStatusMode = 'operational' | 'pairing' | 'ota' | 'error';

// ============================================================================
// Node
// ============================================================================

export interface Node {
  _id: string;
  light_id: string;
  status_mode: NodeStatusMode;
  avg_r: number;              // Average RSSI
  temp_c: number;             // Temperature in Celsius
  vbat_mv: number;            // Battery voltage in millivolts
  fw_version: string;
  last_seen: Date;
  site_id: string;
  coordinator_id: string;
  zone_id?: string;
  name?: string;              // User-assigned name
}

// ============================================================================
// Node Summary (for list views)
// ============================================================================

export interface NodeSummary {
  _id: string;
  light_id: string;
  name?: string;
  status_mode: NodeStatusMode;
  temp_c: number;
  vbat_mv: number;
  avg_r: number;              // Signal strength (RSSI)
  coordinator_id: string;
  zone_id?: string;
  last_seen: Date;
}

// ============================================================================
// Node Telemetry (real-time data)
// ============================================================================

export interface NodeTelemetry {
  node_id: string;
  light_id: string;
  timestamp: Date;
  temp_c: number;
  vbat_mv: number;
  avg_r: number;
  rssi_dbm?: number;       // RSSI in dBm (alias for avg_r in some contexts)
  light_lux?: number;      // Light sensor reading
  status_mode: NodeStatusMode;
}

// ============================================================================
// LED Control
// ============================================================================

export interface LedColor {
  r: number;    // Red (0-255)
  g: number;    // Green (0-255)
  b: number;    // Blue (0-255)
  w?: number;   // White (0-255) for RGBW LEDs
}

export interface LedControlCommand {
  node_id: string;
  color?: LedColor;
  brightness?: number;        // 0-100
  effect?: LedEffect;
}

export type LedEffect = 'solid' | 'breathe' | 'rainbow' | 'pulse' | 'off';

export interface TestColorCommand {
  node_id: string;
  color: LedColor;
  duration_ms?: number;
}

export interface BrightnessCommand {
  node_id: string;
  brightness: number;         // 0-100
}

// ============================================================================
// Node Configuration
// ============================================================================

export interface NodeConfigUpdate {
  node_id: string;
  name?: string;
  zone_id?: string;
}

export interface NodeNameUpdate {
  node_id: string;
  name: string;
}

export interface NodeZoneUpdate {
  node_id: string;
  zone_id: string;
}

// ============================================================================
// Battery Status Helper
// ============================================================================

export function getBatteryPercent(vbat_mv: number): number {
  // Typical LiPo battery: 3.0V (empty) to 4.2V (full)
  const minVoltage = 3000;
  const maxVoltage = 4200;
  const percent = ((vbat_mv - minVoltage) / (maxVoltage - minVoltage)) * 100;
  return Math.max(0, Math.min(100, Math.round(percent)));
}

export function getBatteryStatus(vbat_mv: number): 'good' | 'low' | 'critical' {
  const percent = getBatteryPercent(vbat_mv);
  if (percent > 30) return 'good';
  if (percent > 10) return 'low';
  return 'critical';
}
