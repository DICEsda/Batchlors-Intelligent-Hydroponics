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
