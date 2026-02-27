/**
 * Alert Models - Smart Tile IoT System
 * Defines types for system alerts and notifications
 */

// Alert severity levels
export type AlertSeverity = 'critical' | 'warning' | 'info';

// Alert status states
export type AlertStatus = 'active' | 'acknowledged' | 'resolved';

// Alert categories for filtering
export type AlertCategory = 'sensor' | 'system' | 'network' | 'maintenance' | 'security';

// Source types that can generate alerts
export type AlertSourceType = 'coordinator' | 'tower' | 'node' | 'system' | 'backend';

/**
 * Alert source information
 */
export interface AlertSource {
  type: AlertSourceType;
  id: string;
  name: string;
}

/**
 * Main Alert interface
 */
export interface Alert {
  _id: string;
  title: string;
  message: string;
  severity: AlertSeverity;
  status: AlertStatus;
  category: AlertCategory;
  source: AlertSource;
  createdAt: Date | string;
  acknowledgedAt?: Date | string;
  acknowledgedBy?: string;
  resolvedAt?: Date | string;
  resolvedBy?: string;
  metadata?: Record<string, unknown>;

  /** Farm / site identifier (populated from backend farm_id) */
  farmId?: string;
  /** Coordinator / reservoir identifier (populated from backend coord_id) */
  coordId?: string;
}

/**
 * Alert filter options for querying
 */
export interface AlertFilter {
  severity?: AlertSeverity[];
  status?: AlertStatus[];
  category?: AlertCategory[];
  sourceType?: AlertSourceType[];
  dateRange?: {
    start: Date;
    end: Date;
  };
  searchTerm?: string;
}

/**
 * Alert statistics summary
 */
export interface AlertStats {
  total: number;
  bySeverity: {
    critical: number;
    warning: number;
    info: number;
  };
  byStatus: {
    active: number;
    acknowledged: number;
    resolved: number;
  };
  byCategory: {
    sensor: number;
    system: number;
    network: number;
    maintenance: number;
    security: number;
  };
}

/**
 * Alert notification settings
 */
export interface AlertNotificationSettings {
  emailEnabled: boolean;
  emailAddresses: string[];
  smsEnabled: boolean;
  phoneNumbers: string[];
  pushEnabled: boolean;
  severityThreshold: AlertSeverity;
  quietHoursEnabled: boolean;
  quietHoursStart?: string; // HH:mm format
  quietHoursEnd?: string; // HH:mm format
}

/**
 * Alert rule for automated alert generation
 */
export interface AlertRule {
  _id: string;
  name: string;
  description: string;
  enabled: boolean;
  condition: {
    metric: string;
    operator: 'gt' | 'lt' | 'eq' | 'gte' | 'lte' | 'ne';
    threshold: number;
    duration?: number; // seconds the condition must persist
  };
  severity: AlertSeverity;
  category: AlertCategory;
  sourceFilter?: {
    types?: AlertSourceType[];
    ids?: string[];
  };
  createdAt: Date | string;
  updatedAt: Date | string;
}

/**
 * API response for paginated alerts
 */
export interface AlertsResponse {
  alerts: Alert[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

/**
 * Request to create a new alert
 */
export interface CreateAlertRequest {
  title: string;
  message: string;
  severity: AlertSeverity;
  category: AlertCategory;
  source: AlertSource;
  metadata?: Record<string, unknown>;
}

/**
 * Request to update an alert
 */
export interface UpdateAlertRequest {
  status?: AlertStatus;
  acknowledgedBy?: string;
  resolvedBy?: string;
  metadata?: Record<string, unknown>;
}
