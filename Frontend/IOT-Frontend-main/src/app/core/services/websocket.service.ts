import { Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { EnvironmentService } from './environment.service';
import {
  ReservoirTelemetry,
  TowerTelemetry,
  Alert,
  WSMessage,
  WSMessageType
} from '../models';

/**
 * WebSocket Message Types for Hydroponic Farm System
 */
export interface WSReservoirTelemetryMessage extends WSMessage {
  type: 'reservoir_telemetry';
  payload: ReservoirTelemetry;
}

export interface WSTowerTelemetryMessage extends WSMessage {
  type: 'tower_telemetry';
  payload: TowerTelemetry;
}

export interface WSAlertMessage extends WSMessage {
  type: 'alert';
  payload: Alert;
}

export interface WSDeviceStatusMessage extends WSMessage {
  type: 'device_status';
  payload: {
    deviceId: string;
    deviceType: 'coordinator' | 'tower';
    status: 'online' | 'offline' | 'error';
    timestamp: string;
  };
}

export interface WSOtaProgressMessage extends WSMessage {
  type: 'ota_progress';
  payload: {
    jobId: string;
    deviceId: string;
    progress: number;
    status: 'downloading' | 'installing' | 'completed' | 'failed';
    error?: string;
  };
}

export interface WSDigitalTwinUpdateMessage extends WSMessage {
  type: 'digital_twin_update';
  payload: {
    coordinatorId?: string;
    towerId?: string;
    updates: Record<string, any>;
  };
}

export interface WSPredictionUpdateMessage extends WSMessage {
  type: 'prediction_update';
  payload: {
    towerId: string;
    plantSlot: number;
    predictedHeight: number;
    confidence: number;
    timestamp: string;
  };
}

/**
 * Union type for all WebSocket messages
 */
export type HydroponicWSMessage =
  | WSReservoirTelemetryMessage
  | WSTowerTelemetryMessage
  | WSAlertMessage
  | WSDeviceStatusMessage
  | WSOtaProgressMessage
  | WSDigitalTwinUpdateMessage
  | WSPredictionUpdateMessage
  | WSMessage;

/**
 * WebSocket Service
 * Manages real-time bidirectional communication with the hydroponic farm backend
 * Handles automatic reconnection and message routing
 */
@Injectable({
  providedIn: 'root'
})
export class WebSocketService {
  private readonly env = inject(EnvironmentService);
  private ws: WebSocket | null = null;
  private reconnectAttempts = 0;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private isIntentionalClose = false;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;

  // Connection state signals
  public readonly connected = signal<boolean>(false);
  public readonly connecting = signal<boolean>(false);
  public readonly connectionError = signal<string | null>(null);
  public readonly lastMessageTime = signal<Date | null>(null);

  // Message streams for different event types
  private readonly messageSubject = new Subject<HydroponicWSMessage>();
  private readonly reservoirTelemetrySubject = new Subject<ReservoirTelemetry>();
  private readonly towerTelemetrySubject = new Subject<TowerTelemetry>();
  private readonly alertSubject = new Subject<Alert>();
  private readonly deviceStatusSubject = new Subject<WSDeviceStatusMessage['payload']>();
  private readonly otaProgressSubject = new Subject<WSOtaProgressMessage['payload']>();
  private readonly digitalTwinSubject = new Subject<WSDigitalTwinUpdateMessage['payload']>();
  private readonly predictionSubject = new Subject<WSPredictionUpdateMessage['payload']>();
  private readonly errorSubject = new Subject<{ message: string; error?: unknown }>();

  // Observable streams for consumers
  public readonly messages$ = this.messageSubject.asObservable();
  public readonly reservoirTelemetry$ = this.reservoirTelemetrySubject.asObservable();
  public readonly towerTelemetry$ = this.towerTelemetrySubject.asObservable();
  public readonly alerts$ = this.alertSubject.asObservable();
  public readonly deviceStatus$ = this.deviceStatusSubject.asObservable();
  public readonly otaProgress$ = this.otaProgressSubject.asObservable();
  public readonly digitalTwinUpdates$ = this.digitalTwinSubject.asObservable();
  public readonly predictions$ = this.predictionSubject.asObservable();
  public readonly errors$ = this.errorSubject.asObservable();

  constructor() {
    if (this.env.isDevelopment) {
      console.log('[WebSocket] Hydroponic Farm WebSocket service initialized');
    }
  }

  /**
   * Connect to WebSocket server
   */
  connect(): void {
    if (this.ws && (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING)) {
      console.warn('[WebSocket] Already connected or connecting');
      return;
    }

    this.isIntentionalClose = false;
    this.connecting.set(true);
    this.connectionError.set(null);

    try {
      this.ws = new WebSocket(this.env.wsUrl);
      this.setupEventHandlers();

      if (this.env.isDevelopment) {
        console.log('[WebSocket] Connecting to:', this.env.wsUrl);
      }
    } catch (error) {
      this.handleConnectionError(error);
    }
  }

  /**
   * Disconnect from WebSocket server
   */
  disconnect(): void {
    this.isIntentionalClose = true;
    this.clearReconnectTimer();
    this.stopHeartbeat();

    if (this.ws) {
      this.ws.close(1000, 'Client disconnect');
      this.ws = null;
    }

    this.connected.set(false);
    this.connecting.set(false);

    if (this.env.isDevelopment) {
      console.log('[WebSocket] Disconnected');
    }
  }

  /**
   * Send message to server
   */
  send(message: unknown): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      const error = 'WebSocket is not connected';
      this.errorSubject.next({ message: error });
      throw new Error(error);
    }

    try {
      const payload = typeof message === 'string' ? message : JSON.stringify(message);
      this.ws.send(payload);

      if (this.env.isDevelopment) {
        console.log('[WebSocket] Sent:', message);
      }
    } catch (error) {
      this.errorSubject.next({
        message: 'Failed to send message',
        error
      });
      throw error;
    }
  }

  /**
   * Subscribe to specific coordinator updates
   */
  subscribeToCoordinator(coordinatorId: string): void {
    this.send({
      type: 'subscribe',
      target: 'coordinator',
      id: coordinatorId
    });
  }

  /**
   * Subscribe to specific tower updates
   */
  subscribeToTower(towerId: string): void {
    this.send({
      type: 'subscribe',
      target: 'tower',
      id: towerId
    });
  }

  /**
   * Subscribe to all alerts
   */
  subscribeToAlerts(): void {
    this.send({
      type: 'subscribe',
      target: 'alerts'
    });
  }

  /**
   * Subscribe to OTA progress updates
   */
  subscribeToOta(jobId?: string): void {
    this.send({
      type: 'subscribe',
      target: 'ota',
      id: jobId
    });
  }

  /**
   * Subscribe to digital twin updates
   */
  subscribeToDigitalTwin(): void {
    this.send({
      type: 'subscribe',
      target: 'digital_twin'
    });
  }

  /**
   * Unsubscribe from a specific target
   */
  unsubscribe(target: string, id?: string): void {
    this.send({
      type: 'unsubscribe',
      target,
      id
    });
  }

  /**
   * Setup WebSocket event handlers
   */
  private setupEventHandlers(): void {
    if (!this.ws) return;

    this.ws.onopen = () => {
      this.connected.set(true);
      this.connecting.set(false);
      this.reconnectAttempts = 0;
      this.connectionError.set(null);
      this.startHeartbeat();

      if (this.env.isDevelopment) {
        console.log('[WebSocket] Connected to hydroponic farm backend');
      }

      // Auto-subscribe to alerts on connect
      this.subscribeToAlerts();
    };

    this.ws.onmessage = (event: MessageEvent) => {
      try {
        const data = JSON.parse(event.data) as HydroponicWSMessage;
        this.lastMessageTime.set(new Date());
        this.handleMessage(data);
      } catch (error) {
        console.error('[WebSocket] Failed to parse message:', error);
        this.errorSubject.next({
          message: 'Failed to parse message',
          error
        });
      }
    };

    this.ws.onerror = (event: Event) => {
      console.error('[WebSocket] Error:', event);
      this.connectionError.set('WebSocket error occurred');
      this.errorSubject.next({
        message: 'WebSocket error occurred',
        error: event
      });
    };

    this.ws.onclose = (event: CloseEvent) => {
      this.connected.set(false);
      this.connecting.set(false);
      this.stopHeartbeat();

      if (this.env.isDevelopment) {
        console.log('[WebSocket] Closed:', event.code, event.reason);
      }

      if (!this.isIntentionalClose) {
        this.attemptReconnect();
      }
    };
  }

  /**
   * Handle incoming WebSocket message and route to appropriate stream
   */
  private handleMessage(data: HydroponicWSMessage): void {
    // Emit to general message stream
    this.messageSubject.next(data);

    // Route to specific streams based on message type
    switch (data.type) {
      case 'reservoir_telemetry':
        const reservoirMsg = data as WSReservoirTelemetryMessage;
        this.reservoirTelemetrySubject.next(reservoirMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Reservoir telemetry:', reservoirMsg.payload.coordId);
        }
        break;

      case 'tower_telemetry':
        const towerMsg = data as WSTowerTelemetryMessage;
        this.towerTelemetrySubject.next(towerMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Tower telemetry:', towerMsg.payload.towerId);
        }
        break;

      case 'alert':
        const alertMsg = data as WSAlertMessage;
        this.alertSubject.next(alertMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Alert:', alertMsg.payload.severity, alertMsg.payload.message);
        }
        break;

      case 'device_status':
        const statusMsg = data as WSDeviceStatusMessage;
        this.deviceStatusSubject.next(statusMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Device status:', statusMsg.payload.deviceId, statusMsg.payload.status);
        }
        break;

      case 'ota_progress':
        const otaMsg = data as WSOtaProgressMessage;
        this.otaProgressSubject.next(otaMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] OTA progress:', otaMsg.payload.deviceId, otaMsg.payload.progress + '%');
        }
        break;

      case 'digital_twin_update':
        const twinMsg = data as WSDigitalTwinUpdateMessage;
        this.digitalTwinSubject.next(twinMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Digital twin update received');
        }
        break;

      case 'prediction_update':
        const predMsg = data as WSPredictionUpdateMessage;
        this.predictionSubject.next(predMsg.payload);
        if (this.env.isDevelopment) {
          console.log('[WebSocket] Prediction update:', predMsg.payload.towerId);
        }
        break;

      case 'pong':
        // Heartbeat response - no action needed
        break;

      case 'error':
        this.errorSubject.next({
          message: (data.payload as { message?: string })?.message || 'Unknown error',
          error: data.payload
        });
        break;

      default:
        if (this.env.isDevelopment) {
          console.warn('[WebSocket] Unknown message type:', data.type);
        }
    }
  }

  /**
   * Start heartbeat to keep connection alive
   */
  private startHeartbeat(): void {
    this.stopHeartbeat();
    this.heartbeatTimer = setInterval(() => {
      if (this.connected()) {
        this.send({ type: 'ping' });
      }
    }, 30000); // 30 seconds
  }

  /**
   * Stop heartbeat timer
   */
  private stopHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }

  /**
   * Attempt to reconnect to WebSocket server
   */
  private attemptReconnect(): void {
    if (this.reconnectAttempts >= this.env.wsMaxReconnectAttempts) {
      const error = 'Max reconnection attempts reached';
      console.error('[WebSocket]', error);
      this.connectionError.set(error);
      this.errorSubject.next({ message: error });
      return;
    }

    this.reconnectAttempts++;
    const delay = this.env.wsReconnectDelay * Math.min(this.reconnectAttempts, 5);

    if (this.env.isDevelopment) {
      console.log(`[WebSocket] Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts}/${this.env.wsMaxReconnectAttempts})`);
    }

    this.clearReconnectTimer();
    this.reconnectTimer = setTimeout(() => {
      this.connect();
    }, delay);
  }

  /**
   * Clear reconnection timer
   */
  private clearReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  /**
   * Handle connection error
   */
  private handleConnectionError(error: unknown): void {
    console.error('[WebSocket] Connection error:', error);
    this.connecting.set(false);
    this.connectionError.set((error as Error)?.message || 'Connection failed');
    this.errorSubject.next({
      message: 'Failed to connect to WebSocket',
      error
    });

    if (!this.isIntentionalClose) {
      this.attemptReconnect();
    }
  }

  /**
   * Get current connection state
   */
  getConnectionState(): {
    connected: boolean;
    connecting: boolean;
    error: string | null;
    attempts: number;
    lastMessage: Date | null;
  } {
    return {
      connected: this.connected(),
      connecting: this.connecting(),
      error: this.connectionError(),
      attempts: this.reconnectAttempts,
      lastMessage: this.lastMessageTime()
    };
  }

  /**
   * Reset connection (disconnect and reconnect)
   */
  reset(): void {
    this.reconnectAttempts = 0;
    this.disconnect();
    setTimeout(() => this.connect(), 100);
  }
}
