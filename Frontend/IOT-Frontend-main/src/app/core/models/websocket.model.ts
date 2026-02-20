/**
 * WebSocket Models
 * Real-time communication payloads
 */

export interface CoordinatorLog {
  coordId: string;
  farmId: string;
  timestamp: number; // Unix timestamp in seconds
  level: 'DEBUG' | 'INFO' | 'WARN' | 'ERROR';
  message: string;
  tag?: string;
}

/**
 * Connection Status Event
 * Real-time WiFi/MQTT connection state changes
 * Note: Uses camelCase to match backend serialization (JsonNamingPolicy.CamelCase)
 */
export interface ConnectionStatus {
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
