import { Component, inject, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideRefreshCw,
  lucideDownload,
  lucideCheck,
  lucideCheckCircle,
  lucideX,
  lucideAlertTriangle,
  lucideUpload,
  lucideServer,
  lucideCpu,
  lucideRadio,
  lucideClock,
  lucideLoader2,
  lucidePlay,
  lucideSquare,
  lucideRotateCcw,
  lucideChevronRight,
  lucidePackage,
  lucideFileText,
  lucideZap,
} from '@ng-icons/lucide';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
  HlmCardContentDirective,
} from '../../components/ui/card';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import {
  HlmTabsListDirective,
  HlmTabsTriggerDirective,
  HlmTabsContentDirective,
} from '../../components/ui/tabs';
import { OtaService } from '../../core/services';
import {
  FirmwareVersion,
  OtaJob,
  DeviceFirmwareStatus,
  OtaJobStatus,
} from '../../core/models';

@Component({
  selector: 'app-ota-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgIcon,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
    HlmSkeletonComponent,
    HlmTabsListDirective,
    HlmTabsTriggerDirective,
    HlmTabsContentDirective,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideRefreshCw,
      lucideDownload,
      lucideCheck,
      lucideCheckCircle,
      lucideX,
      lucideAlertTriangle,
      lucideUpload,
      lucideServer,
      lucideCpu,
      lucideRadio,
      lucideClock,
      lucideLoader2,
      lucidePlay,
      lucideSquare,
      lucideRotateCcw,
      lucideChevronRight,
      lucidePackage,
      lucideFileText,
      lucideZap,
    }),
  ],
  templateUrl: './ota-dashboard.component.html',
  styleUrls: ['./ota-dashboard.component.scss'],
})
export class OtaDashboardComponent implements OnInit {
  private readonly otaService = inject(OtaService);

  // ============================================================================
  // State from service
  // ============================================================================
  readonly isLoading = this.otaService.isLoading;
  readonly error = this.otaService.error;

  // Firmware
  readonly firmwareVersions = this.otaService.firmwareVersions;
  readonly coordinatorFirmware = this.otaService.coordinatorFirmware;
  readonly nodeFirmware = this.otaService.nodeFirmware;
  readonly latestCoordinatorVersion = this.otaService.latestCoordinatorVersion;
  readonly latestNodeVersion = this.otaService.latestNodeVersion;

  // Jobs
  readonly otaJobs = this.otaService.otaJobs;
  readonly activeJobs = this.otaService.activeJobs;
  readonly completedJobs = this.otaService.completedJobs;
  readonly failedJobs = this.otaService.failedJobs;
  readonly recentJobs = this.otaService.recentJobs;

  // Device status
  readonly deviceStatus = this.otaService.deviceStatus;
  readonly coordinatorStatus = this.otaService.coordinatorStatus;
  readonly nodeStatus = this.otaService.nodeStatus;
  readonly devicesWithUpdates = this.otaService.devicesWithUpdates;

  // Summary stats
  readonly totalDevices = this.otaService.totalDevices;
  readonly upToDateCount = this.otaService.upToDateCount;
  readonly updateAvailableCount = this.otaService.updateAvailableCount;
  readonly updatingCount = this.otaService.updatingCount;

  // ============================================================================
  // Local state
  // ============================================================================
  readonly activeTab = signal<'coordinator' | 'node'>('coordinator');
  readonly deviceTypeFilter = signal<'all' | 'coordinator' | 'node'>('all');

  // ============================================================================
  // Computed
  // ============================================================================
  readonly filteredDevices = computed(() => {
    const filter = this.deviceTypeFilter();
    const devices = this.deviceStatus();
    if (filter === 'all') return devices;
    return devices.filter(d => 
      filter === 'coordinator' ? d.deviceType === 'coordinator' : d.deviceType === 'tower'
    );
  });

  readonly failedCount = computed(() => 
    this.deviceStatus().filter(d => d.lastUpdateStatus === 'failed').length
  );

  // ============================================================================
  // Lifecycle
  // ============================================================================
  ngOnInit(): void {
    this.loadData();
  }

  // ============================================================================
  // Actions
  // ============================================================================
  async loadData(): Promise<void> {
    await this.otaService.loadAllData();
  }

  async refreshData(): Promise<void> {
    await this.loadData();
  }

  async startUpdate(device: DeviceFirmwareStatus): Promise<void> {
    await this.otaService.startUpdate({
      targetType: device.deviceType,
      targetId: device.deviceId,
      version: device.latestVersion,
    });
  }

  async cancelJob(jobId: string): Promise<void> {
    await this.otaService.cancelJob(jobId);
  }

  async retryJob(job: OtaJob): Promise<void> {
    await this.otaService.retryJob(job);
  }

  async updateAllDevices(): Promise<void> {
    await this.otaService.updateAllDevices();
  }

  setActiveTab(tab: 'coordinator' | 'node'): void {
    this.activeTab.set(tab);
  }

  setDeviceFilter(filter: 'all' | 'coordinator' | 'node'): void {
    this.deviceTypeFilter.set(filter);
  }

  // ============================================================================
  // Helpers
  // ============================================================================
  getStatusBadgeVariant(status: OtaJobStatus): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'completed':
        return 'default';
      case 'failed':
      case 'cancelled':
        return 'destructive';
      case 'pending':
      case 'queued':
        return 'secondary';
      default:
        return 'outline';
    }
  }

  getDeviceStatusBadgeVariant(device: DeviceFirmwareStatus): 'default' | 'secondary' | 'destructive' | 'outline' {
    if (device.pendingJobId) return 'outline';
    if (device.updateAvailable) return 'secondary';
    if (device.lastUpdateStatus === 'failed') return 'destructive';
    return 'default';
  }

  getDeviceStatusText(device: DeviceFirmwareStatus): string {
    if (device.pendingJobId) return 'Updating';
    if (device.lastUpdateStatus === 'failed') return 'Failed';
    if (device.updateAvailable) return 'Update Available';
    return 'Up to Date';
  }

  getStatusIcon(status: OtaJobStatus): string {
    switch (status) {
      case 'completed':
        return 'lucideCheckCircle';
      case 'failed':
      case 'cancelled':
        return 'lucideX';
      case 'pending':
      case 'queued':
        return 'lucideClock';
      case 'downloading':
      case 'verifying':
      case 'installing':
      case 'rebooting':
        return 'lucideLoader2';
      default:
        return 'lucideClock';
    }
  }

  isJobActive(status: OtaJobStatus): boolean {
    return ['pending', 'queued', 'downloading', 'verifying', 'installing', 'rebooting'].includes(status);
  }

  formatDate(date: Date | string | undefined): string {
    if (!date) return 'N/A';
    const d = new Date(date);
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  }

  formatTimeAgo(date: Date | string | undefined): string {
    if (!date) return 'Unknown';
    const now = new Date();
    const then = new Date(date);
    const diffMs = now.getTime() - then.getTime();
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);
    const diffDay = Math.floor(diffHour / 24);

    if (diffSec < 60) return 'Just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    if (diffHour < 24) return `${diffHour}h ago`;
    if (diffDay < 7) return `${diffDay}d ago`;
    return this.formatDate(date);
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  trackByFirmwareId(index: number, item: FirmwareVersion): string {
    return item._id;
  }

  trackByJobId(index: number, item: OtaJob): string {
    return item._id;
  }

  trackByDeviceId(index: number, item: DeviceFirmwareStatus): string {
    return item.deviceId;
  }
}
