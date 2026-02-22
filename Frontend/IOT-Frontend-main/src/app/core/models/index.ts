/**
 * Core Models Barrel Export - Smart Tile IoT System
 * Import models from a single location
 */

// Site models
export * from './site.model';

// Farm models (top-level organization)
export * from './farm.model';

// Coordinator models (Smart Tile system)
export * from './coordinator.model';

// Node models (Smart Tiles/Lights)
export * from './node.model';

// Zone models (legacy - use Farm instead)
export * from './zone.model';

// Telemetry and Historical Data
export * from './telemetry.model';

// OTA (Firmware Updates)
export * from './ota.model';

// ML Predictions and Growth Analysis
export * from './prediction.model';

// Digital Twin and 3D Visualization
export * from './digital-twin.model';

// Alerts and Notifications
export * from './alert.model';

// Pairing workflow
export * from './pairing.model';

// WebSocket real-time messages
export * from './websocket.model';

// Diagnostics (backend performance metrics)
export * from './diagnostics.model';

// Common/Shared types
export * from './common.model';
