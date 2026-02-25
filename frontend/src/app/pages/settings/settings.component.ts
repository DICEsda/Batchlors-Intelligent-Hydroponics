import { Component, signal, computed, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideSettings,
  lucideSave,
  lucideRefreshCw,
  lucideSliders,
  lucideBell,
  lucideSun,
  lucideWifi,
  lucideUser,
  lucideDatabase,
  lucideShield,
  lucideMonitor,
  lucideThermometer,
  lucideDroplet,
  lucideZap,
  lucideActivity,
  lucideClock,
  lucideInfo,
  lucideCheck,
  lucideAlertTriangle,
  lucideMoon,
  lucidePalette,
  lucideGlobe,
  lucideServer,
  lucideHardDrive,
  lucideTrash2,
  lucideDownload,
  lucideUpload,
} from '@ng-icons/lucide';
import { ThemeService, Theme } from '../../core/services/theme.service';
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
import { HlmInputDirective } from '../../components/ui/input';
import { HlmLabelDirective } from '../../components/ui/label';
import { HlmSwitchComponent } from '../../components/ui/switch';
import { HlmSeparatorDirective } from '../../components/ui/separator';

// Settings interfaces
interface ThresholdSetting {
  id: string;
  name: string;
  icon: string;
  unit: string;
  min: number;
  max: number;
  warningLow: number;
  warningHigh: number;
  criticalLow: number;
  criticalHigh: number;
}

interface NotificationSetting {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
}

interface NetworkSettings {
  mqttBroker: string;
  mqttPort: number;
  mqttUsername: string;
  apiEndpoint: string;
  wsEndpoint: string;
  connectionTimeout: number;
  reconnectInterval: number;
}

interface DisplaySettings {
  theme: 'light' | 'dark' | 'system';
  accentColor: string;
  language: string;
  temperatureUnit: 'celsius' | 'fahrenheit';
  dateFormat: string;
  refreshInterval: number;
  animationsEnabled: boolean;
  compactMode: boolean;
}

interface SystemInfo {
  version: string;
  buildDate: string;
  environment: string;
  apiVersion: string;
  totalCoordinators: number;
  totalNodes: number;
  uptime: string;
  lastBackup: string;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
    HlmInputDirective,
    HlmLabelDirective,
    HlmSwitchComponent,
    HlmSeparatorDirective,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideSettings,
      lucideSave,
      lucideRefreshCw,
      lucideSliders,
      lucideBell,
      lucideSun,
      lucideWifi,
      lucideUser,
      lucideDatabase,
      lucideShield,
      lucideMonitor,
      lucideThermometer,
      lucideDroplet,
      lucideZap,
      lucideActivity,
      lucideClock,
      lucideInfo,
      lucideCheck,
      lucideAlertTriangle,
      lucideMoon,
      lucidePalette,
      lucideGlobe,
      lucideServer,
      lucideHardDrive,
      lucideTrash2,
      lucideDownload,
      lucideUpload,
    }),
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  // Inject ThemeService
  readonly themeService = inject(ThemeService);
  
  // Active tab
  activeTab = signal<string>('thresholds');

  // Use accent colors from theme service
  get accentColors() {
    return this.themeService.accentColors;
  }

  ngOnInit() {
    // Sync display settings with theme service values
    this.displaySettings.update(settings => ({
      ...settings,
      theme: this.themeService.theme(),
      accentColor: this.themeService.accentColor()
    }));
  }

  // Tabs configuration
  tabs = [
    { id: 'thresholds', label: 'Thresholds', icon: 'lucideSliders' },
    { id: 'notifications', label: 'Notifications', icon: 'lucideBell' },
    { id: 'network', label: 'Network', icon: 'lucideWifi' },
    { id: 'display', label: 'Display', icon: 'lucideMonitor' },
    { id: 'system', label: 'System', icon: 'lucideDatabase' },
  ];

  // Threshold settings
  thresholds = signal<ThresholdSetting[]>([
    {
      id: 'temperature',
      name: 'Temperature',
      icon: 'lucideThermometer',
      unit: '°C',
      min: 0,
      max: 50,
      warningLow: 18,
      warningHigh: 28,
      criticalLow: 15,
      criticalHigh: 32,
    },
    {
      id: 'humidity',
      name: 'Humidity',
      icon: 'lucideDroplet',
      unit: '%',
      min: 0,
      max: 100,
      warningLow: 40,
      warningHigh: 70,
      criticalLow: 30,
      criticalHigh: 80,
    },
    {
      id: 'light',
      name: 'Light Level',
      icon: 'lucideSun',
      unit: 'lux',
      min: 0,
      max: 10000,
      warningLow: 500,
      warningHigh: 8000,
      criticalLow: 200,
      criticalHigh: 9500,
    },
    {
      id: 'battery',
      name: 'Battery Level',
      icon: 'lucideZap',
      unit: '%',
      min: 0,
      max: 100,
      warningLow: 20,
      warningHigh: 100,
      criticalLow: 10,
      criticalHigh: 100,
    },
    {
      id: 'signal',
      name: 'Signal Strength',
      icon: 'lucideActivity',
      unit: 'dBm',
      min: -100,
      max: 0,
      warningLow: -80,
      warningHigh: 0,
      criticalLow: -90,
      criticalHigh: 0,
    },
  ]);

  // Notification settings
  notifications = signal<NotificationSetting[]>([
    {
      id: 'critical_alerts',
      name: 'Critical Alerts',
      description: 'Receive notifications for critical system alerts',
      enabled: true,
    },
    {
      id: 'warning_alerts',
      name: 'Warning Alerts',
      description: 'Receive notifications for warning-level alerts',
      enabled: true,
    },
    {
      id: 'sensor_offline',
      name: 'Sensor Offline',
      description: 'Notify when a sensor goes offline',
      enabled: true,
    },
    {
      id: 'node_disconnected',
      name: 'Node Disconnected',
      description: 'Notify when a node loses connection',
      enabled: true,
    },
    {
      id: 'low_battery',
      name: 'Low Battery',
      description: 'Notify when node battery is low',
      enabled: true,
    },
    {
      id: 'ota_updates',
      name: 'OTA Updates',
      description: 'Notify about firmware update availability',
      enabled: false,
    },
    {
      id: 'daily_summary',
      name: 'Daily Summary',
      description: 'Receive daily system health summary',
      enabled: false,
    },
    {
      id: 'maintenance_reminders',
      name: 'Maintenance Reminders',
      description: 'Receive scheduled maintenance reminders',
      enabled: true,
    },
  ]);

  // Network settings
  networkSettings = signal<NetworkSettings>({
    mqttBroker: 'mqtt://localhost',
    mqttPort: 1883,
    mqttUsername: 'hydroponic_user',
    apiEndpoint: 'http://localhost:5000/api',
    wsEndpoint: 'ws://localhost:5000/ws',
    connectionTimeout: 30,
    reconnectInterval: 5,
  });

// Display settings
  displaySettings = signal<DisplaySettings>({
    theme: 'dark',
    accentColor: 'stone',
    language: 'en',
    temperatureUnit: 'celsius',
    dateFormat: 'DD/MM/YYYY',
    refreshInterval: 5,
    animationsEnabled: true,
    compactMode: false,
  });

  // System info
  systemInfo = signal<SystemInfo>({
    version: '1.0.0',
    buildDate: '2025-01-05',
    environment: 'Production',
    apiVersion: 'v1',
    totalCoordinators: 2,
    totalNodes: 12,
    uptime: '7 days, 4 hours',
    lastBackup: '2025-01-04 23:00',
  });

  // UI state
  hasChanges = signal(false);
  isSaving = signal(false);
  saveSuccess = signal(false);

  // Computed
  activeTabConfig = computed(() => 
    this.tabs.find(t => t.id === this.activeTab())
  );

  // Methods
  setActiveTab(tabId: string) {
    this.activeTab.set(tabId);
  }

  updateThreshold(index: number, field: keyof ThresholdSetting, value: number) {
    const current = this.thresholds();
    const updated = [...current];
    updated[index] = { ...updated[index], [field]: value };
    this.thresholds.set(updated);
    this.hasChanges.set(true);
  }

  toggleNotification(index: number) {
    const current = this.notifications();
    const updated = [...current];
    updated[index] = { ...updated[index], enabled: !updated[index].enabled };
    this.notifications.set(updated);
    this.hasChanges.set(true);
  }

  toggleAllNotifications() {
    const current = this.notifications();
    const allEnabled = current.every(n => n.enabled);
    const updated = current.map(n => ({ ...n, enabled: !allEnabled }));
    this.notifications.set(updated);
    this.hasChanges.set(true);
  }

  updateNetworkSetting(field: keyof NetworkSettings, value: string | number) {
    const current = this.networkSettings();
    this.networkSettings.set({ ...current, [field]: value });
    this.hasChanges.set(true);
  }

  updateDisplaySetting(field: keyof DisplaySettings, value: string | number | boolean) {
    const current = this.displaySettings();
    this.displaySettings.set({ ...current, [field]: value });
    this.hasChanges.set(true);

    // Apply theme immediately when changed via ThemeService
    if (field === 'theme') {
      this.themeService.setTheme(value as Theme);
    }

    // Apply accent color immediately when changed via ThemeService
    if (field === 'accentColor') {
      this.themeService.setAccentColor(value as string);
    }
  }

  async saveSettings() {
    this.isSaving.set(true);
    this.saveSuccess.set(false);

    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 1000));

    this.isSaving.set(false);
    this.saveSuccess.set(true);
    this.hasChanges.set(false);

    // Clear success message after 3 seconds
    setTimeout(() => this.saveSuccess.set(false), 3000);
  }

  resetToDefaults() {
    // Reset thresholds
    this.thresholds.set([
      {
        id: 'temperature',
        name: 'Temperature',
        icon: 'lucideThermometer',
        unit: '°C',
        min: 0,
        max: 50,
        warningLow: 18,
        warningHigh: 28,
        criticalLow: 15,
        criticalHigh: 32,
      },
      {
        id: 'humidity',
        name: 'Humidity',
        icon: 'lucideDroplet',
        unit: '%',
        min: 0,
        max: 100,
        warningLow: 40,
        warningHigh: 70,
        criticalLow: 30,
        criticalHigh: 80,
      },
      {
        id: 'light',
        name: 'Light Level',
        icon: 'lucideSun',
        unit: 'lux',
        min: 0,
        max: 10000,
        warningLow: 500,
        warningHigh: 8000,
        criticalLow: 200,
        criticalHigh: 9500,
      },
      {
        id: 'battery',
        name: 'Battery Level',
        icon: 'lucideZap',
        unit: '%',
        min: 0,
        max: 100,
        warningLow: 20,
        warningHigh: 100,
        criticalLow: 10,
        criticalHigh: 100,
      },
      {
        id: 'signal',
        name: 'Signal Strength',
        icon: 'lucideActivity',
        unit: 'dBm',
        min: -100,
        max: 0,
        warningLow: -80,
        warningHigh: 0,
        criticalLow: -90,
        criticalHigh: 0,
      },
    ]);
    this.hasChanges.set(true);
  }

  exportSettings() {
    const settings = {
      thresholds: this.thresholds(),
      notifications: this.notifications(),
      network: this.networkSettings(),
      display: this.displaySettings(),
      exportDate: new Date().toISOString(),
    };

    const blob = new Blob([JSON.stringify(settings, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `hydroponic-settings-${new Date().toISOString().split('T')[0]}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }

  importSettings(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const settings = JSON.parse(e.target?.result as string);
        if (settings.thresholds) this.thresholds.set(settings.thresholds);
        if (settings.notifications) this.notifications.set(settings.notifications);
        if (settings.network) this.networkSettings.set(settings.network);
        if (settings.display) this.displaySettings.set(settings.display);
        this.hasChanges.set(true);
      } catch (error) {
        console.error('Failed to import settings:', error);
      }
    };
    reader.readAsText(file);
  }

  clearAllData() {
    if (confirm('Are you sure you want to clear all cached data? This action cannot be undone.')) {
      localStorage.clear();
      sessionStorage.clear();
      window.location.reload();
    }
  }
}
