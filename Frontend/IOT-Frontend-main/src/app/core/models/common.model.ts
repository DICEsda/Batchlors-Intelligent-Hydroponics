/**
 * Common/Shared Models for Hydroponic Farm System
 * Base types, API responses, and WebSocket messages
 */

// ============================================================================
// API Response Models
// ============================================================================

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
  timestamp: Date;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
  totalPages: number;
}

export interface ValidationError {
  field: string;
  message: string;
  code: string;
}

// ============================================================================
// Health Status
// ============================================================================

export type ServiceHealth = 'healthy' | 'degraded' | 'unhealthy';

export interface HealthStatus {
  status: ServiceHealth;
  service: string;
  version: string;
  uptime: number;
  checks: {
    database: boolean;
    mqtt: boolean;
    redis?: boolean;
    mlService?: boolean;
  };
  timestamp: Date;
}

// ============================================================================
// WebSocket Message Models
// ============================================================================

export type WSMessageType = 
  | 'reservoir_telemetry'
  | 'tower_telemetry'
  | 'node_telemetry'
  | 'coord_telemetry'
  | 'device_status'
  | 'connection_status'
  | 'alert'
  | 'ota_progress'
  | 'prediction_update'
  | 'command_ack'
  | 'error'
  | 'subscribed'
  | 'unsubscribed'
  | 'heartbeat'
  | 'digital_twin_update'
  | 'coordinator_log'
  | 'pong'
  // Pairing events
  | 'pairing_started'
  | 'pairing_stopped'
  | 'node_discovered'
  | 'node_paired'
  | 'pairing_timeout'
  // Coordinator registration events
  | 'coordinator_registration_request'
  | 'coordinator_registered'
  | 'coordinator_rejected'
  | 'coordinator_removed'
  // Diagnostics
  | 'diagnostics_update'
  // Throttled telemetry batch
  | 'telemetry_batch';

export interface WSMessage<T = unknown> {
  type: WSMessageType;
  payload: T;
  timestamp: Date;
  correlationId?: string;
}

export interface WSDeviceStatusPayload {
  deviceType: 'coordinator' | 'tower';
  deviceId: string;
  status: 'online' | 'offline' | 'warning' | 'error';
  previousStatus?: string;
}

export interface WSOtaProgressPayload {
  jobId: string;
  targetId: string;
  status: string;
  progress: number;
  message?: string;
}

export interface WSCommandAckPayload {
  commandId: string;
  success: boolean;
  error?: string;
}

export interface WSConnectionStatusPayload {
  ts: number;
  coordId: string;
  farmId: string;
  event: 'wifi_connected' | 'wifi_disconnected' | 'mqtt_connected' | 'mqtt_disconnected' | 'wifi_got_ip' | 'wifi_lost_ip';
  wifiConnected: boolean;
  wifiRssi: number;
  mqttConnected: boolean;
  uptimeMs: number;
  freeHeap: number;
  reason?: string;
}

// ============================================================================
// Pairing WebSocket Message Payloads
// ============================================================================

/**
 * Payload for pairing_started event - sent when coordinator enters pairing mode
 */
export interface WSPairingStartedPayload {
  coordinatorId: string;
  siteId: string;
  durationMs: number;
  startedAt: string;
}

/**
 * Payload for pairing_stopped event - sent when pairing mode ends
 */
export interface WSPairingStoppedPayload {
  coordinatorId: string;
  siteId: string;
  reason: 'timeout' | 'cancelled' | 'completed';
  nodesDiscovered: number;
  nodesPaired: number;
}

/**
 * Payload for node_discovered event - sent when a new tower is found during pairing
 * (WS event name kept as 'node_discovered' for backend compatibility)
 */
export interface WSTowerDiscoveredPayload {
  coordinatorId: string;
  towerId: string;
  macAddress: string;
  rssi: number;
  discoveredAt: string;
  firmwareVersion?: string;
}

/**
 * Payload for node_paired event - sent when a discovered tower is approved and paired
 * (WS event name kept as 'node_paired' for backend compatibility)
 */
export interface WSTowerPairedPayload {
  coordinatorId: string;
  towerId: string;
  macAddress: string;
  assignedName?: string;
  pairedAt: string;
}

/**
 * Payload for pairing_timeout event - sent when pairing times out
 */
export interface WSPairingTimeoutPayload {
  coordinatorId: string;
  siteId: string;
  nodesDiscovered: number;
  nodesPaired: number;
}

/**
 * Discovered tower during pairing (for UI state)
 */
export interface DiscoveredTower {
  towerId: string;
  macAddress: string;
  rssi: number;
  discoveredAt: Date;
  firmwareVersion?: string;
  status: 'discovered' | 'pairing' | 'paired' | 'rejected' | 'error';
  error?: string;
}

// ============================================================================
// Query Parameters
// ============================================================================

export interface PaginationParams {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

export interface DateRangeParams {
  startDate: Date;
  endDate: Date;
}

export interface TelemetryQueryParams extends PaginationParams, DateRangeParams {
  deviceId: string;
  deviceType: 'coordinator' | 'tower';
  metrics?: string[];       // Specific metrics to retrieve
  interval?: '1m' | '5m' | '15m' | '1h' | '6h' | '1d';
}

// ============================================================================
// Common Enums (as const objects for type safety)
// ============================================================================

export const DeviceTypes = {
  COORDINATOR: 'coordinator',
  TOWER: 'tower'
} as const;

export type DeviceType = typeof DeviceTypes[keyof typeof DeviceTypes];

export const StatusValues = {
  ONLINE: 'online',
  OFFLINE: 'offline',
  WARNING: 'warning',
  ERROR: 'error'
} as const;

export type StatusValue = typeof StatusValues[keyof typeof StatusValues];

// ============================================================================
// Coordinator Registration Models
// ============================================================================

export interface WSCoordinatorRegistrationPayload {
  coordId: string;
  fwVersion?: string;
  chipModel?: string;
  wifiRssi: number;
  ip?: string;
  freeHeap: number;
  firstSeenAt: string;
}

export interface WSCoordinatorRegisteredPayload {
  coordId: string;
  farmId: string;
  name: string;
  description?: string;
  color?: string;
  location?: string;
  registeredAt: string;
}

export interface ApproveCoordinatorRequest {
  coordId: string;
  farmId: string;
  name: string;
  description?: string;
  color?: string;
  tags?: string[];
  location?: string;
}
