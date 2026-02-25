import { Injectable, inject, signal, computed } from '@angular/core';
import { Subject, takeUntil, firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import {
  FirmwareVersion,
  OtaJob,
  OtaCampaign,
  OtaStatistics,
  DeviceFirmwareStatus,
  StartOtaRequest,
  CreateCampaignRequest,
  OtaJobStatus,
} from '../models';

/**
 * OTA Service - Firmware Update Management
 * Centralized state management using Angular signals
 * Handles firmware versions, update jobs, and device status
 */
@Injectable({
  providedIn: 'root'
})
export class OtaService {
  private readonly api = inject(ApiService);
  private readonly destroy$ = new Subject<void>();

  // ============================================================================
  // State Signals
  // ============================================================================

  // Loading states
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  // Firmware versions
  readonly firmwareVersions = signal<FirmwareVersion[]>([]);
  readonly selectedFirmware = signal<FirmwareVersion | null>(null);

  // OTA Jobs
  readonly otaJobs = signal<OtaJob[]>([]);
  readonly selectedJob = signal<OtaJob | null>(null);

  // Device Status
  readonly deviceStatus = signal<DeviceFirmwareStatus[]>([]);

  // Campaigns
  readonly campaigns = signal<OtaCampaign[]>([]);

  // Statistics
  readonly statistics = signal<OtaStatistics | null>(null);

  // ============================================================================
  // Computed Signals
  // ============================================================================

  // Firmware by type
  readonly coordinatorFirmware = computed(() =>
    this.firmwareVersions().filter(f => f.targetType === 'coordinator')
  );

  readonly nodeFirmware = computed(() =>
    this.firmwareVersions().filter(f => f.targetType === 'tower')
  );

  readonly latestCoordinatorVersion = computed(() => {
    const versions = this.coordinatorFirmware().filter(f => f.isStable);
    return versions.length > 0 ? versions[0] : null;
  });

  readonly latestNodeVersion = computed(() => {
    const versions = this.nodeFirmware().filter(f => f.isStable);
    return versions.length > 0 ? versions[0] : null;
  });

  // Jobs by status
  readonly activeJobs = computed(() =>
    this.otaJobs().filter(j => 
      ['pending', 'queued', 'downloading', 'verifying', 'installing', 'rebooting'].includes(j.status)
    )
  );

  readonly completedJobs = computed(() =>
    this.otaJobs().filter(j => j.status === 'completed')
  );

  readonly failedJobs = computed(() =>
    this.otaJobs().filter(j => j.status === 'failed')
  );

  readonly recentJobs = computed(() =>
    this.otaJobs().slice(0, 10)
  );

  // Device counts
  readonly coordinatorStatus = computed(() =>
    this.deviceStatus().filter(d => d.deviceType === 'coordinator')
  );

  readonly nodeStatus = computed(() =>
    this.deviceStatus().filter(d => d.deviceType === 'tower')
  );

  readonly devicesWithUpdates = computed(() =>
    this.deviceStatus().filter(d => d.updateAvailable)
  );

  readonly coordinatorsWithUpdates = computed(() =>
    this.coordinatorStatus().filter(d => d.updateAvailable)
  );

  readonly nodesWithUpdates = computed(() =>
    this.nodeStatus().filter(d => d.updateAvailable)
  );

  readonly devicesUpdating = computed(() =>
    this.deviceStatus().filter(d => d.pendingJobId)
  );

  // Summary stats
  readonly totalDevices = computed(() => this.deviceStatus().length);
  readonly upToDateCount = computed(() =>
    this.deviceStatus().filter(d => !d.updateAvailable && !d.pendingJobId).length
  );
  readonly updateAvailableCount = computed(() =>
    this.deviceStatus().filter(d => d.updateAvailable && !d.pendingJobId).length
  );
  readonly updatingCount = computed(() =>
    this.deviceStatus().filter(d => d.pendingJobId).length
  );

  // ============================================================================
  // Data Loading Methods
  // ============================================================================

  /**
   * Load all OTA data
   */
  async loadAllData(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      await Promise.all([
        this.loadFirmwareVersions(),
        this.loadOtaJobs(),
        this.loadDeviceStatus(),
      ]);
    } catch (err) {
      console.error('Failed to load OTA data:', err);
      this.error.set('Failed to load OTA data. Please check if the server is running.');
      this.firmwareVersions.set([]);
      this.otaJobs.set([]);
      this.deviceStatus.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load firmware versions
   */
  async loadFirmwareVersions(): Promise<void> {
    try {
      const versions = await firstValueFrom(this.api.getFirmwareVersions());
      this.firmwareVersions.set(versions);
    } catch (err) {
      console.error('Failed to load firmware versions:', err);
      throw err;
    }
  }

  /**
   * Load OTA jobs
   */
  async loadOtaJobs(): Promise<void> {
    try {
      const response = await firstValueFrom(this.api.getOtaJobs({ page: 1, pageSize: 50 }));
      this.otaJobs.set(response.items);
    } catch (err) {
      console.error('Failed to load OTA jobs:', err);
      throw err;
    }
  }

  /**
   * Load device firmware status
   */
  async loadDeviceStatus(): Promise<void> {
    try {
      const status = await firstValueFrom(this.api.getDeviceFirmwareStatus());
      this.deviceStatus.set(status);
    } catch (err) {
      console.error('Failed to load device status:', err);
      throw err;
    }
  }

  /**
   * Load OTA statistics
   */
  async loadStatistics(): Promise<void> {
    try {
      const stats = await firstValueFrom(this.api.getOtaStatistics());
      this.statistics.set(stats);
    } catch (err) {
      console.error('Failed to load OTA statistics:', err);
    }
  }

  // ============================================================================
  // Action Methods
  // ============================================================================

  /**
   * Start OTA update for a device
   */
  async startUpdate(request: StartOtaRequest): Promise<OtaJob | null> {
    try {
      const job = await firstValueFrom(this.api.startOtaUpdate(request));
      this.otaJobs.update(jobs => [job, ...jobs]);
      await this.loadDeviceStatus();
      return job;
    } catch (err) {
      console.error('Failed to start OTA update:', err);
      this.error.set('Failed to start firmware update');
      return null;
    }
  }

  /**
   * Cancel OTA job
   */
  async cancelJob(jobId: string): Promise<boolean> {
    try {
      await firstValueFrom(this.api.cancelOtaJob(jobId));
      await this.loadOtaJobs();
      await this.loadDeviceStatus();
      return true;
    } catch (err) {
      console.error('Failed to cancel OTA job:', err);
      this.error.set('Failed to cancel update');
      return false;
    }
  }

  /**
   * Retry failed OTA job
   */
  async retryJob(job: OtaJob): Promise<OtaJob | null> {
    return this.startUpdate({
      targetType: job.targetType,
      targetId: job.targetId,
      version: job.toVersion,
    });
  }

  /**
   * Update all devices with available updates
   */
  async updateAllDevices(): Promise<void> {
    const devicesToUpdate = this.devicesWithUpdates();
    for (const device of devicesToUpdate) {
      await this.startUpdate({
        targetType: device.deviceType,
        targetId: device.deviceId,
        version: device.latestVersion,
      });
    }
  }

  // ============================================================================
  // Real-time Updates (from WebSocket)
  // ============================================================================

  /**
   * Update job progress from WebSocket
   */
  updateJobProgress(jobId: string, status: OtaJobStatus, progress: number): void {
    this.otaJobs.update(jobs =>
      jobs.map(j =>
        j._id === jobId || j.jobId === jobId
          ? { ...j, status, progress, updatedAt: new Date() }
          : j
      )
    );
  }

  /**
   * Handle job completion from WebSocket
   */
  handleJobComplete(jobId: string, success: boolean, errorMessage?: string): void {
    this.otaJobs.update(jobs =>
      jobs.map(j =>
        j._id === jobId || j.jobId === jobId
          ? {
              ...j,
              status: success ? 'completed' : 'failed',
              progress: success ? 100 : j.progress,
              errorMessage: errorMessage,
              completedAt: new Date(),
              updatedAt: new Date(),
            }
          : j
      )
    );

    // Refresh device status
    this.loadDeviceStatus();
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
