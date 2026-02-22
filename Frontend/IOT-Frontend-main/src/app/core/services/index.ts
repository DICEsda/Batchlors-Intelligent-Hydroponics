/**
 * Core Services Barrel Export - Smart Tile IoT System
 * Import services from a single location
 */

// Environment configuration
export * from './environment.service';

// REST API service for Smart Tile IoT
export * from './api.service';

// Mock data service for development
export * from './mock-data.service';

// WebSocket service for real-time updates
export * from './websocket.service';

// Main IoT state management service (primary)
export * from './iot-data.service';

// Legacy hydroponic data service (deprecated - kept for backward compatibility)
export * from './hydroponic-data.service';

// OTA firmware update management service
export * from './ota.service';

// Alert and notification management service
export * from './alert.service';

// Digital Twin state management service
export * from './twin.service';

// Diagnostics (backend performance metrics via WS + REST)
export * from './diagnostics.service';

// Telemetry history (reservoir + tower time-series from MongoDB)
export * from './telemetry-history.service';
