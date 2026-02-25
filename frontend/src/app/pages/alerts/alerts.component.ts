import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideRefreshCw,
  lucideBell,
  lucideBellOff,
  lucideAlertTriangle,
  lucideAlertCircle,
  lucideInfo,
  lucideCheck,
  lucideCheckCircle,
  lucideX,
  lucideFilter,
  lucideSearch,
  lucideChevronDown,
  lucideChevronUp,
  lucideClock,
  lucideServer,
  lucideCpu,
  lucideRadio,
  lucideShield,
  lucideWrench,
  lucideWifi,
  lucideTrash2,
  lucideEye,
  lucideCheckCheck,
} from '@ng-icons/lucide';
import {
  HlmCardDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
} from '../../components/ui/card';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { AlertService, IoTDataService, WebSocketService } from '../../core/services';
import { LogStorageService } from '../../core/services/log-storage.service';
import {
  Alert,
  AlertSeverity,
  AlertStatus,
  AlertCategory,
  CoordinatorSummary,
  NodeSummary,
  CoordinatorLog,
} from '../../core/models';

@Component({
  selector: 'app-alerts',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    NgIcon,
    HlmCardDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideRefreshCw,
      lucideBell,
      lucideBellOff,
      lucideAlertTriangle,
      lucideAlertCircle,
      lucideInfo,
      lucideCheck,
      lucideCheckCircle,
      lucideX,
      lucideFilter,
      lucideSearch,
      lucideChevronDown,
      lucideChevronUp,
      lucideClock,
      lucideServer,
      lucideCpu,
      lucideRadio,
      lucideShield,
      lucideWrench,
      lucideWifi,
      lucideTrash2,
      lucideEye,
      lucideCheckCheck,
    }),
  ],
  templateUrl: './alerts.component.html',
  styleUrls: ['./alerts.component.scss'],
})
export class AlertsComponent implements OnInit, OnDestroy, AfterViewChecked {
  private readonly alertService = inject(AlertService);
  private readonly dataService = inject(IoTDataService);
  readonly wsService = inject(WebSocketService); // Public for template access
  private readonly logStorage = inject(LogStorageService);
  private logSubscription?: Subscription;
  private connectionStatusSubscription?: Subscription;

  @ViewChild('logsContainer') private logsContainer?: ElementRef<HTMLDivElement>;
  private shouldAutoScroll = true;
  private previousLogCount = 0;

  // ============================================================================
  // State from services
  // ============================================================================
  readonly isLoading = this.alertService.isLoading;
  readonly error = this.alertService.error;

  // Data from IoT service
  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;

  // ============================================================================
  // Local UI state for logs
  // ============================================================================
  readonly selectedCoordinatorId = signal<string>('');
  readonly logLevelFilter = signal<string[]>(['INFO', 'WARN', 'ERROR']); // Exclude DEBUG by default
  
  // Computed: Get logs from storage service, filtered by coordinator and level
  readonly coordinatorLogs = computed(() => {
    const selectedId = this.selectedCoordinatorId();
    const levelFilter = this.logLevelFilter();
    const allLogs = this.logStorage.logs();
    
    return allLogs
      .filter(log => !selectedId || log.coordId === selectedId)
      .filter(log => levelFilter.includes(log.level))
      .map(log => log.formattedMessage);
  });

  // ============================================================================
  // Computed values for dropdown
  // ============================================================================
  readonly selectedCoordinator = computed(() => {
    const id = this.selectedCoordinatorId();
    if (!id) return null;
    return this.coordinators().find(c => c._id === id || c.coord_id === id);
  });

  // ============================================================================
  // Lifecycle
  // ============================================================================
  ngOnInit(): void {
    this.loadData();
    // Default to "All" — user can pick a specific coordinator from the pill selector
    
    // Connect WebSocket if not connected
    if (!this.wsService.connected()) {
      this.wsService.connect();
    }
    
    // Subscribe to coordinator logs
    this.subscribeToLogs();
    
    // Subscribe to connection status events
    this.subscribeToConnectionEvents();
  }
  
  ngOnDestroy(): void {
    this.logSubscription?.unsubscribe();
    this.connectionStatusSubscription?.unsubscribe();
  }

  // ============================================================================
  // Data loading
  // ============================================================================
  async loadData(): Promise<void> {
    await this.dataService.loadDashboardData();
  }

  async refreshData(): Promise<void> {
    await this.dataService.loadDashboardData();
  }

  // ============================================================================
  // Real-time log streaming
  // ============================================================================
  private subscribeToLogs(): void {
    this.logSubscription = this.wsService.coordinatorLogs$
      .pipe(
        filter(log => log !== null)
      )
      .subscribe(log => {
        // Format log message with timestamp and level
        const timestamp = new Date(log.timestamp).toLocaleTimeString('en-US', {
          hour12: false,
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit'
        });
        const formattedLog = `[${timestamp}] [${log.level}] ${log.message}`;
        
        // Store in persistent storage service (last 100 logs)
        this.logStorage.addLog({
          timestamp: log.timestamp,
          level: log.level,
          message: log.message,
          coordId: log.coordId,
          formattedMessage: formattedLog
        });
      });
  }

  // ============================================================================
  // Real-time connection status events
  // ============================================================================
  private subscribeToConnectionEvents(): void {
    this.connectionStatusSubscription = this.wsService.connectionStatus$
      .pipe(
        filter(status => status !== null)
      )
      .subscribe(status => {
        // Format connection event as log entry
        const timestamp = new Date(status.ts * 1000).toLocaleTimeString('en-US', {
          hour12: false,
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit'
        });
        
        const level = this.getLogLevelForEvent(status.event);
        const icon = this.getIconForEvent(status.event);
        const message = this.formatConnectionMessage(status);
        
        const formattedLog = `[${timestamp}] [${level}] ${icon} ${message}`;
        
        // Store in log storage with special flag
        this.logStorage.addLog({
          timestamp: status.ts * 1000, // Convert seconds to milliseconds
          level: level,
          message: message,
          coordId: status.coordId,
          formattedMessage: formattedLog,
          isConnectionEvent: true
        });
      });
  }

  private getLogLevelForEvent(event: string): string {
    switch(event) {
      case 'wifi_disconnected':
      case 'mqtt_disconnected':
      case 'wifi_lost_ip':
        return 'ERROR';
      case 'wifi_connected':
      case 'mqtt_connected':
      case 'wifi_got_ip':
        return 'INFO';
      default:
        return 'INFO';
    }
  }

  private getIconForEvent(event: string): string {
    switch(event) {
      case 'wifi_connected': return '📶';
      case 'wifi_disconnected': return '📵';
      case 'mqtt_connected': return '📡';
      case 'mqtt_disconnected': return '🔴';
      case 'wifi_got_ip': return '🌐';
      case 'wifi_lost_ip': return '⚠️';
      default: return '🔄';
    }
  }

  private formatConnectionMessage(status: any): string {
    let msg = '';
    
    switch(status.event) {
      case 'wifi_connected':
        msg = `WiFi connected (RSSI: ${status.wifiRssi} dBm)`;
        break;
      case 'wifi_disconnected':
        msg = `WiFi disconnected${status.reason ? ': ' + status.reason : ''}`;
        break;
      case 'mqtt_connected':
        msg = 'MQTT broker connected';
        break;
      case 'mqtt_disconnected':
        msg = `MQTT connection lost${status.reason ? ': ' + status.reason : ''}`;
        break;
      case 'wifi_got_ip':
        msg = 'WiFi obtained IP address';
        break;
      case 'wifi_lost_ip':
        msg = 'WiFi lost IP address';
        break;
      default:
        msg = `Connection event: ${status.event}`;
    }
    
    return `[${status.coordId}] ${msg}`;
  }
  
  // ============================================================================
  // Coordinator selection
  // ============================================================================
  selectCoordinator(coordId: string): void {
    this.selectedCoordinatorId.set(coordId);
  }

  // ============================================================================
  // Auto-scroll on new logs
  // ============================================================================
  ngAfterViewChecked(): void {
    const currentLogCount = this.coordinatorLogs().length;
    if (currentLogCount > this.previousLogCount && this.shouldAutoScroll) {
      this.scrollToBottom();
    }
    this.previousLogCount = currentLogCount;
  }

  private scrollToBottom(): void {
    if (this.logsContainer) {
      const el = this.logsContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }

  onLogsScroll(): void {
    if (this.logsContainer) {
      const el = this.logsContainer.nativeElement;
      // Auto-scroll if user is near the bottom (within 100px)
      this.shouldAutoScroll = (el.scrollHeight - el.scrollTop - el.clientHeight) < 100;
    }
  }

  toggleLogLevel(level: string): void {
    this.logLevelFilter.update(levels => {
      if (levels.includes(level)) {
        return levels.filter(l => l !== level);
      } else {
        return [...levels, level];
      }
    });
  }
  
  clearLogs(): void {
    const selectedId = this.selectedCoordinatorId();
    if (selectedId) {
      this.logStorage.clearCoordinatorLogs(selectedId);
    } else {
      this.logStorage.clearLogs();
    }
  }

  // ============================================================================
  // TrackBy functions
  // ============================================================================
  trackByIndex(index: number): number {
    return index;
  }
}
