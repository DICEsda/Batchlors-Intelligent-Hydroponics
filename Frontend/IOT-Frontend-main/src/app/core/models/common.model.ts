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
  | 'device_status'
  | 'alert'
  | 'ota_progress'
  | 'prediction_update'
  | 'command_ack'
  | 'error'
  | 'subscribed'
  | 'unsubscribed'
  | 'heartbeat'
  | 'digital_twin_update'
  | 'pong';

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

// ============================================================================
// SignalR Hub Models
// ============================================================================

export interface SignalRConnection {
  connectionId: string;
  userId?: string;
  connectedAt: Date;
  groups: string[];
}

export interface HubSubscription {
  hubName: 'telemetry' | 'alerts' | 'ota' | 'predictions';
  groups: string[];         // e.g., ['coordinator:coord-001', 'tower:tower-001']
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
