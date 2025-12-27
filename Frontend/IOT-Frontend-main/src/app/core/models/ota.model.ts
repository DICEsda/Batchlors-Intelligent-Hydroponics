/**
 * OTA (Over-The-Air) Update Models for Hydroponic Farm System
 * Firmware management and update distribution
 */

// ============================================================================
// Firmware Version
// ============================================================================

export interface FirmwareVersion {
  _id: string;
  version: string;          // Semantic version (e.g., "1.2.3")
  targetType: 'coordinator' | 'tower';
  
  // File info
  fileUrl: string;
  fileSize: number;         // Bytes
  checksum: string;         // SHA256
  
  // Metadata
  releaseNotes?: string;
  minPreviousVersion?: string;
  isStable: boolean;
  isMandatory: boolean;
  
  // Timestamps
  releasedAt: Date;
  createdAt: Date;
}

export interface FirmwareVersionSummary {
  version: string;
  targetType: 'coordinator' | 'tower';
  isStable: boolean;
  releasedAt: Date;
  deviceCount: number;      // Devices running this version
}

// ============================================================================
// OTA Job
// ============================================================================

export type OtaJobStatus = 
  | 'pending'
  | 'queued'
  | 'downloading'
  | 'verifying'
  | 'installing'
  | 'rebooting'
  | 'completed'
  | 'failed'
  | 'cancelled';

export interface OtaJob {
  _id: string;
  jobId: string;
  
  // Target
  targetType: 'coordinator' | 'tower';
  targetId: string;
  targetName: string;
  
  // Firmware
  fromVersion: string;
  toVersion: string;
  firmwareUrl: string;
  
  // Status
  status: OtaJobStatus;
  progress: number;         // 0-100
  errorMessage?: string;
  errorCode?: string;
  
  // Timestamps
  createdAt: Date;
  startedAt?: Date;
  completedAt?: Date;
  updatedAt: Date;
}

// ============================================================================
// OTA Campaign (Batch Updates)
// ============================================================================

export interface OtaCampaign {
  _id: string;
  campaignId: string;
  name: string;
  description?: string;
  
  // Target
  targetType: 'coordinator' | 'tower';
  targetVersion: string;    // Firmware version to install
  
  // Selection criteria
  targetDeviceIds?: string[];
  targetFromVersions?: string[];
  
  // Rollout strategy
  strategy: 'immediate' | 'staged' | 'scheduled';
  stagePercentage?: number;
  scheduledTime?: Date;
  
  // Status
  status: 'draft' | 'active' | 'paused' | 'completed' | 'cancelled';
  totalDevices: number;
  completedDevices: number;
  failedDevices: number;
  
  // Timestamps
  createdAt: Date;
  startedAt?: Date;
  completedAt?: Date;
}

// ============================================================================
// Device Firmware Status
// ============================================================================

export interface DeviceFirmwareStatus {
  deviceId: string;
  deviceType: 'coordinator' | 'tower';
  deviceName: string;
  
  currentVersion: string;
  latestVersion: string;
  updateAvailable: boolean;
  
  lastUpdateAttempt?: Date;
  lastUpdateStatus?: OtaJobStatus;
  pendingJobId?: string;
}

// ============================================================================
// OTA Commands
// ============================================================================

export interface StartOtaRequest {
  targetType: 'coordinator' | 'tower';
  targetId: string;
  version: string;
}

export interface CreateCampaignRequest {
  name: string;
  description?: string;
  targetType: 'coordinator' | 'tower';
  targetVersion: string;
  targetDeviceIds?: string[];
  targetFromVersions?: string[];
  strategy: 'immediate' | 'staged' | 'scheduled';
  stagePercentage?: number;
  scheduledTime?: Date;
}

// ============================================================================
// OTA Statistics
// ============================================================================

export interface OtaStatistics {
  coordinators: {
    total: number;
    upToDate: number;
    updateAvailable: number;
    updating: number;
    failed: number;
  };
  towers: {
    total: number;
    upToDate: number;
    updateAvailable: number;
    updating: number;
    failed: number;
  };
  recentJobs: OtaJob[];
  activeCampaigns: OtaCampaign[];
}
