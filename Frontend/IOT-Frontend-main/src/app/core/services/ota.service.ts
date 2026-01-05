import { Injectable, inject, signal, computed } from '@angular/core';
import { Subject, takeUntil, firstValueFrom, of, delay } from 'rxjs';
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
 * Mock OTA Data for development when backend is unavailable
 */
const MOCK_FIRMWARE_VERSIONS: FirmwareVersion[] = [
  {
    _id: 'fw-001',
    version: '2.1.0',
    targetType: 'coordinator',
    fileUrl: '/firmware/coordinator-2.1.0.bin',
    fileSize: 1024000,
    checksum: 'sha256:abc123...',
    releaseNotes: 'Added new pairing protocol, improved WiFi stability',
    isStable: true,
    isMandatory: false,
    releasedAt: new Date('2024-12-01'),
    createdAt: new Date('2024-12-01'),
  },
  {
    _id: 'fw-002',
    version: '2.0.5',
    targetType: 'coordinator',
    fileUrl: '/firmware/coordinator-2.0.5.bin',
    fileSize: 980000,
    checksum: 'sha256:def456...',
    releaseNotes: 'Bug fixes and performance improvements',
    isStable: true,
    isMandatory: false,
    releasedAt: new Date('2024-11-15'),
    createdAt: new Date('2024-11-15'),
  },
  {
    _id: 'fw-003',
    version: '1.5.2',
    targetType: 'tower',
    fileUrl: '/firmware/node-1.5.2.bin',
    fileSize: 512000,
    checksum: 'sha256:ghi789...',
    releaseNotes: 'LED driver improvements, battery optimization',
    isStable: true,
    isMandatory: false,
    releasedAt: new Date('2024-12-10'),
    createdAt: new Date('2024-12-10'),
  },
  {
    _id: 'fw-004',
    version: '1.5.1',
    targetType: 'tower',
    fileUrl: '/firmware/node-1.5.1.bin',
    fileSize: 508000,
    checksum: 'sha256:jkl012...',
    releaseNotes: 'Minor bug fixes',
    isStable: true,
    isMandatory: false,
    releasedAt: new Date('2024-11-20'),
    createdAt: new Date('2024-11-20'),
  },
];

const MOCK_DEVICE_STATUS: DeviceFirmwareStatus[] = [
  {
    deviceId: 'coord-001',
    deviceType: 'coordinator',
    deviceName: 'Lobby Controller',
    currentVersion: '2.1.0',
    latestVersion: '2.1.0',
    updateAvailable: false,
  },
  {
    deviceId: 'coord-002',
    deviceType: 'coordinator',
    deviceName: 'Conference Room Hub',
    currentVersion: '2.1.0',
    latestVersion: '2.1.0',
    updateAvailable: false,
  },
  {
    deviceId: 'coord-003',
    deviceType: 'coordinator',
    deviceName: 'Warehouse Section A',
    currentVersion: '2.0.5',
    latestVersion: '2.1.0',
    updateAvailable: true,
  },
  {
    deviceId: 'node-001',
    deviceType: 'tower',
    deviceName: 'Entry Tile 1',
    currentVersion: '1.5.2',
    latestVersion: '1.5.2',
    updateAvailable: false,
  },
  {
    deviceId: 'node-002',
    deviceType: 'tower',
    deviceName: 'Entry Tile 2',
    currentVersion: '1.5.2',
    latestVersion: '1.5.2',
    updateAvailable: false,
  },
  {
    deviceId: 'node-003',
    deviceType: 'tower',
    deviceName: 'Reception Tile 1',
    currentVersion: '1.5.1',
    latestVersion: '1.5.2',
    updateAvailable: true,
  },
  {
    deviceId: 'node-008',
    deviceType: 'tower',
    deviceName: 'Aisle A-1',
    currentVersion: '1.5.1',
    latestVersion: '1.5.2',
    updateAvailable: true,
  },
  {
    deviceId: 'node-010',
    deviceType: 'tower',
    deviceName: 'Loading Dock 1',
    currentVersion: '1.5.0',
    latestVersion: '1.5.2',
    updateAvailable: true,
    pendingJobId: 'job-001',
    lastUpdateStatus: 'downloading',
  },
];

const MOCK_OTA_JOBS: OtaJob[] = [
  {
    _id: 'job-001',
    jobId: 'OTA-20241215-001',
    targetType: 'tower',
    targetId: 'node-010',
    targetName: 'Loading Dock 1',
    fromVersion: '1.5.0',
    toVersion: '1.5.2',
    firmwareUrl: '/firmware/node-1.5.2.bin',
    status: 'downloading',
    progress: 45,
    createdAt: new Date(Date.now() - 300000),
    startedAt: new Date(Date.now() - 180000),
    updatedAt: new Date(Date.now() - 5000),
  },
  {
    _id: 'job-002',
    jobId: 'OTA-20241215-002',
    targetType: 'coordinator',
    targetId: 'coord-003',
    targetName: 'Warehouse Section A',
    fromVersion: '2.0.5',
    toVersion: '2.1.0',
    firmwareUrl: '/firmware/coordinator-2.1.0.bin',
    status: 'pending',
    progress: 0,
    createdAt: new Date(Date.now() - 60000),
    updatedAt: new Date(Date.now() - 60000),
  },
  {
    _id: 'job-003',
    jobId: 'OTA-20241214-001',
    targetType: 'tower',
    targetId: 'node-006',
    targetName: 'Conf Room Tile 1',
    fromVersion: '1.5.1',
    toVersion: '1.5.2',
    firmwareUrl: '/firmware/node-1.5.2.bin',
    status: 'completed',
    progress: 100,
    createdAt: new Date(Date.now() - 86400000),
    startedAt: new Date(Date.now() - 86400000 + 60000),
    completedAt: new Date(Date.now() - 86400000 + 300000),
    updatedAt: new Date(Date.now() - 86400000 + 300000),
  },
  {
    _id: 'job-004',
    jobId: 'OTA-20241213-001',
    targetType: 'tower',
    targetId: 'node-005',
    targetName: 'Faulty Tile',
    fromVersion: '1.4.8',
    toVersion: '1.5.2',
    firmwareUrl: '/firmware/node-1.5.2.bin',
    status: 'failed',
    progress: 23,
    errorMessage: 'Connection lost during download',
    errorCode: 'ERR_CONN_LOST',
    createdAt: new Date(Date.now() - 172800000),
    startedAt: new Date(Date.now() - 172800000 + 60000),
    updatedAt: new Date(Date.now() - 172800000 + 180000),
  },
];

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
  readonly usingMockData = signal(false);

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
      this.usingMockData.set(false);
    } catch (err) {
      console.warn('OTA API unavailable, falling back to mock data');
      this.usingMockData.set(true);
      this.loadMockData();
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * Load mock data as fallback
   */
  private loadMockData(): void {
    this.firmwareVersions.set([...MOCK_FIRMWARE_VERSIONS]);
    this.deviceStatus.set([...MOCK_DEVICE_STATUS]);
    this.otaJobs.set([...MOCK_OTA_JOBS]);
    this.error.set(null);
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
      if (this.usingMockData()) {
        // Simulate OTA job creation
        const newJob: OtaJob = {
          _id: `job-${Date.now()}`,
          jobId: `OTA-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(Math.random() * 1000)}`,
          targetType: request.targetType,
          targetId: request.targetId,
          targetName: this.deviceStatus().find(d => d.deviceId === request.targetId)?.deviceName || 'Unknown',
          fromVersion: this.deviceStatus().find(d => d.deviceId === request.targetId)?.currentVersion || '0.0.0',
          toVersion: request.version,
          firmwareUrl: `/firmware/${request.targetType}-${request.version}.bin`,
          status: 'pending',
          progress: 0,
          createdAt: new Date(),
          updatedAt: new Date(),
        };
        
        // Add to jobs list
        this.otaJobs.update(jobs => [newJob, ...jobs]);
        
        // Update device status
        this.deviceStatus.update(devices =>
          devices.map(d =>
            d.deviceId === request.targetId
              ? { ...d, pendingJobId: newJob._id, lastUpdateStatus: 'pending' as OtaJobStatus }
              : d
          )
        );
        
        // Simulate progress updates
        this.simulateOtaProgress(newJob._id);
        
        return newJob;
      }

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
      if (this.usingMockData()) {
        this.otaJobs.update(jobs =>
          jobs.map(j =>
            j._id === jobId
              ? { ...j, status: 'cancelled' as OtaJobStatus, updatedAt: new Date() }
              : j
          )
        );
        return true;
      }

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
  // Simulation (for mock data)
  // ============================================================================

  private simulateOtaProgress(jobId: string): void {
    const statusSequence: OtaJobStatus[] = ['queued', 'downloading', 'verifying', 'installing', 'rebooting', 'completed'];
    let currentIndex = 0;
    let progress = 0;

    const interval = setInterval(() => {
      if (currentIndex >= statusSequence.length) {
        clearInterval(interval);
        return;
      }

      const status = statusSequence[currentIndex];
      
      if (status === 'downloading') {
        progress += 10;
        if (progress >= 50) {
          currentIndex++;
          progress = 50;
        }
      } else if (status === 'verifying') {
        progress = 60;
        currentIndex++;
      } else if (status === 'installing') {
        progress += 10;
        if (progress >= 90) {
          currentIndex++;
          progress = 90;
        }
      } else if (status === 'rebooting') {
        progress = 95;
        currentIndex++;
      } else if (status === 'completed') {
        progress = 100;
        currentIndex++;
      } else {
        currentIndex++;
      }

      this.otaJobs.update(jobs =>
        jobs.map(j =>
          j._id === jobId
            ? {
                ...j,
                status,
                progress,
                startedAt: j.startedAt || new Date(),
                completedAt: status === 'completed' ? new Date() : undefined,
                updatedAt: new Date(),
              }
            : j
        )
      );

      if (status === 'completed') {
        // Update device status
        const job = this.otaJobs().find(j => j._id === jobId);
        if (job) {
          this.deviceStatus.update(devices =>
            devices.map(d =>
              d.deviceId === job.targetId
                ? {
                    ...d,
                    currentVersion: job.toVersion,
                    updateAvailable: false,
                    pendingJobId: undefined,
                    lastUpdateStatus: 'completed' as OtaJobStatus,
                    lastUpdateAttempt: new Date(),
                  }
                : d
            )
          );
        }
        clearInterval(interval);
      }
    }, 2000);
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
    if (!this.usingMockData()) {
      this.loadDeviceStatus();
    }
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
