import { Injectable, inject, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { WebSocketService } from './websocket.service';
import { ToastService } from './toast.service';
import { NotificationService } from './notification.service';
import { WSCoordinatorRegistrationPayload } from '../models';

/**
 * Callback signature for opening the coordinator registration dialog.
 * The HeaderComponent registers itself via setRegistrationHandler().
 */
export type CoordinatorRegistrationHandler = (reg: WSCoordinatorRegistrationPayload) => void;

/**
 * Global notification listener that subscribes to WebSocket events
 * and shows toast notifications for important events like tower discovery
 * and coordinator registration.
 */
@Injectable({
  providedIn: 'root'
})
export class NotificationListenerService implements OnDestroy {
  private readonly wsService = inject(WebSocketService);
  private readonly toastService = inject(ToastService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);
  private subscriptions: Subscription[] = [];
  private initialized = false;

  /** Track active registration toast IDs per coordId to prevent duplicates */
  private activeRegistrationToasts = new Map<string, string>();

  /**
   * Optional callback set by HeaderComponent so the urgent toast
   * "Register" button can open the registration dialog.
   */
  private registrationHandler: CoordinatorRegistrationHandler | null = null;

  /** Allow HeaderComponent to register itself as the dialog opener */
  setRegistrationHandler(handler: CoordinatorRegistrationHandler): void {
    this.registrationHandler = handler;
  }

  /** Clear handler when HeaderComponent is destroyed */
  clearRegistrationHandler(): void {
    this.registrationHandler = null;
  }

  /**
   * Initialize the notification listener
   * Should be called once at app startup
   */
  initialize(): void {
    if (this.initialized) return;
    this.initialized = true;

    // Connect to WebSocket if not already connected
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }

    this.subscribeToEvents();
    console.log('[NotificationListener] Initialized and listening for events');
  }

  private subscribeToEvents(): void {
    // Listen for new tower discoveries - use urgent toast for pairing action
    this.subscriptions.push(
      this.wsService.towerDiscovered$.subscribe((payload) => {
        const rssiText = payload.rssi ? ` (RSSI: ${payload.rssi} dBm)` : '';
        const towerId = payload.towerId || payload.macAddress;
        
        this.toastService.urgent(
          'New Tower Discovered!',
          `Tower ${towerId}${rssiText} is requesting to pair with Coordinator ${this.formatCoordId(payload.coordinatorId)}. Approve pairing?`,
          'Approve Pairing',
          () => {
            console.log(`Navigating to approve pairing for tower ${towerId}`);
            this.router.navigate(['/coordinator', payload.coordinatorId]);
          }
        );
      })
    );

    // Listen for successful pairing
    this.subscriptions.push(
      this.wsService.towerPaired$.subscribe((payload) => {
        this.toastService.success(
          'Tower Paired Successfully',
          `Tower ${payload.towerId} has been paired with the reservoir`
        );
      })
    );

    // Listen for pairing started
    this.subscriptions.push(
      this.wsService.pairingStarted$.subscribe((payload) => {
        this.toastService.info(
          'Pairing Mode Active',
          `Reservoir ${this.formatCoordId(payload.coordinatorId)} is now searching for towers...`
        );
      })
    );

    // Listen for pairing timeout
    this.subscriptions.push(
      this.wsService.pairingTimeout$.subscribe((payload) => {
        this.toastService.warning(
          'Pairing Timed Out',
          `Pairing mode has expired for reservoir ${this.formatCoordId(payload.coordinatorId)}`
        );
      })
    );

    // Listen for device status changes (online/offline)
    this.subscriptions.push(
      this.wsService.deviceStatus$.subscribe((payload) => {
        if (payload.status === 'offline') {
          this.toastService.warning(
            `${payload.deviceType === 'coordinator' ? 'Reservoir' : 'Tower'} Offline`,
            `Device ${payload.deviceId} has gone offline`
          );
        } else if (payload.status === 'online') {
          // Show urgent toast for coordinator connecting to MQTT
          if (payload.deviceType === 'coordinator') {
            this.toastService.urgent(
              'Coordinator Connected to MQTT',
              `Coordinator ${payload.deviceId} has successfully paired to the MQTT broker. Configure now?`,
              'Configure Now',
              () => {
                console.log(`Navigating to configure coordinator ${payload.deviceId}`);
                this.router.navigate(['/coordinator', payload.deviceId]);
              }
            );
          } else {
            this.toastService.success(
              'Tower Online',
              `Device ${payload.deviceId} is now online`
            );
          }
        } else if (payload.status === 'error') {
          // Show urgent toast for coordinator errors with action button
          if (payload.deviceType === 'coordinator') {
            this.toastService.urgent(
              'Coordinator Error - Action Required',
              `Coordinator ${payload.deviceId} requires immediate attention. Click to view details.`,
              'View Coordinator',
              () => {
                this.router.navigate(['/coordinator', payload.deviceId]);
              }
            );
          } else {
            this.toastService.error(
              'Tower Error',
              `Device ${payload.deviceId} reported an error`
            );
          }
        }
      })
    );

    // ================================================================
    // Coordinator Registration — urgent toast (mirrors tower pairing)
    // Deduplicated: only one toast per coordId at a time.
    // ================================================================
    this.subscriptions.push(
      this.wsService.coordinatorRegistration$.subscribe((registration) => {
        // Skip if we already have an active toast for this coordinator
        if (this.activeRegistrationToasts.has(registration.coordId)) {
          console.log(`[NotificationListener] Skipping duplicate toast for ${registration.coordId}`);
          return;
        }

        const rssiText = registration.wifiRssi ? ` (RSSI: ${registration.wifiRssi} dBm)` : '';
        const ipText = registration.ip ? ` at ${registration.ip}` : '';
        const fwText = registration.fwVersion ? ` · FW ${registration.fwVersion}` : '';

        const toastId = this.toastService.urgent(
          'New Coordinator Detected!',
          `Coordinator ${this.formatCoordId(registration.coordId)}${ipText}${rssiText}${fwText} is requesting to join your farm. Register now?`,
          'Register',
          () => {
            // Remove from active tracking when user clicks Register
            this.activeRegistrationToasts.delete(registration.coordId);
            // If the HeaderComponent registered a handler, open the dialog
            if (this.registrationHandler) {
              this.registrationHandler(registration);
            } else {
              this.router.navigate(['/overview']);
            }
          },
          0, // duration = 0 → never auto-dismiss
          'Dismiss',
          () => {
            // Remove from active tracking when user dismisses
            this.activeRegistrationToasts.delete(registration.coordId);
            console.log(`Coordinator ${registration.coordId} registration dismissed (still pending)`);
          }
        );

        // Track this toast
        this.activeRegistrationToasts.set(registration.coordId, toastId);

        // Also add to notification center (only once per coordId)
        this.notificationService.discovery(
          'New Coordinator Detected',
          `Coordinator ${registration.coordId}${ipText} is requesting registration`,
          'Register',
          () => {
            if (this.registrationHandler) {
              this.registrationHandler(registration);
            } else {
              this.router.navigate(['/overview']);
            }
          }
        );
      })
    );

    // Listen for coordinator registration approval
    this.subscriptions.push(
      this.wsService.coordinatorRegistered$.subscribe((registered) => {
        // Dismiss the pending registration toast if one exists
        const existingToastId = this.activeRegistrationToasts.get(registered.coordId);
        if (existingToastId) {
          this.toastService.dismiss(existingToastId);
          this.activeRegistrationToasts.delete(registered.coordId);
        }

        this.toastService.success(
          'Coordinator Registered',
          `${registered.name} (${this.formatCoordId(registered.coordId)}) has been registered to farm ${registered.farmId}.`
        );
      })
    );

    // Listen for alerts
    this.subscriptions.push(
      this.wsService.alerts$.subscribe((alert) => {
        // Show urgent toast for critical coordinator alerts that require action
        if (alert.severity === 'critical' && alert.source?.type === 'coordinator') {
          this.toastService.urgent(
            alert.title || 'Critical Alert - Coordinator',
            alert.message || 'Immediate action required on coordinator',
            'View Coordinator',
            () => {
              if (alert.source?.id) {
                this.router.navigate(['/coordinator', alert.source.id]);
              } else {
                this.router.navigate(['/coordinators']);
              }
            }
          );
        } else {
          // Standard toast for other alerts
          const type = alert.severity === 'critical' ? 'error' 
                     : alert.severity === 'warning' ? 'warning' 
                     : 'info';
          this.toastService.show({
            type,
            title: alert.title || `${alert.severity.charAt(0).toUpperCase() + alert.severity.slice(1)} Alert`,
            message: alert.message,
            duration: alert.severity === 'critical' ? 10000 : 6000
          });
        }
      })
    );
  }

  /**
   * Format coordinator ID for display (truncate long MAC addresses)
   */
  private formatCoordId(coordId: string): string {
    if (!coordId) return 'Unknown';
    // If it looks like a MAC address, show last 4 chars
    if (coordId.includes(':') && coordId.length > 10) {
      return `...${coordId.slice(-5)}`;
    }
    return coordId;
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.subscriptions = [];
  }
}
