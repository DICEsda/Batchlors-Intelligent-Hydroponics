import { Component, OnInit, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AlertService } from '../../core/services/alert.service';
import { Alert, AlertSeverity, AlertStatus } from '../../core/models';

import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
// Card styling is handled via custom SCSS classes matching the dashboard design language
import { HlmIconDirective } from '../../components/ui/icon';
import { HlmInputDirective } from '../../components/ui/input';
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import { provideIcons, NgIcon } from '@ng-icons/core';
import {
  lucideShieldAlert,
  lucideBellRing,
  lucideCheck,
  lucideCheckCheck,
  lucideX,
  lucideSearch,
  lucideFilter,
  lucideRefreshCw,
  lucideAlertTriangle,
  lucideInfo,
  lucideAlertCircle,
  lucideEye,
  lucideTrash2,
  lucideClock,
  lucideShieldCheck,
  lucideServer,
  lucideLightbulb,
  lucideMonitor,
} from '@ng-icons/lucide';

@Component({
  selector: 'app-alerts-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HlmBadgeDirective,
    HlmButtonDirective,

    HlmIconDirective,
    HlmInputDirective,
    HlmSkeletonComponent,
    NgIcon,
  ],
  providers: [
    provideIcons({
      lucideShieldAlert,
      lucideBellRing,
      lucideCheck,
      lucideCheckCheck,
      lucideX,
      lucideSearch,
      lucideFilter,
      lucideRefreshCw,
      lucideAlertTriangle,
      lucideInfo,
      lucideAlertCircle,
      lucideEye,
      lucideTrash2,
      lucideClock,
      lucideShieldCheck,
      lucideServer,
      lucideLightbulb,
      lucideMonitor,
    }),
  ],
  templateUrl: './alerts-dashboard.component.html',
  styleUrl: './alerts-dashboard.component.scss',
})
export class AlertsDashboardComponent implements OnInit {
  readonly alertService = inject(AlertService);

  // Expose service signals to template
  readonly stats = this.alertService.alertStats;
  readonly activeAlerts = this.alertService.activeAlerts;
  readonly acknowledgedAlerts = this.alertService.acknowledgedAlerts;
  readonly resolvedAlerts = this.alertService.resolvedAlerts;
  readonly filteredAlerts = this.alertService.filteredAlerts;
  readonly isLoading = this.alertService.isLoading;

  // Filter state
  readonly filterSeverity = this.alertService.filterSeverity;
  readonly filterStatus = this.alertService.filterStatus;
  readonly searchTerm = this.alertService.searchTerm;

  // Resolution rate
  readonly resolutionRate = computed(() => {
    const s = this.stats();
    const total = s.byStatus.active + s.byStatus.acknowledged + s.byStatus.resolved;
    if (total === 0) return 0;
    return Math.round((s.byStatus.resolved / total) * 100);
  });

  ngOnInit(): void {
    this.alertService.loadAlerts();
  }

  // ---- Filter Handlers ----
  onSeverityChange(value: string): void {
    this.alertService.setSeverityFilter(value as AlertSeverity | 'all');
  }

  onStatusChange(value: string): void {
    this.alertService.setStatusFilter(value as AlertStatus | 'all');
  }

  onSearchChange(value: string): void {
    this.alertService.setSearchTerm(value);
  }

  clearFilters(): void {
    this.alertService.clearFilters();
  }

  // ---- Action Handlers ----
  async acknowledge(alertId: string): Promise<void> {
    await this.alertService.acknowledgeAlert(alertId);
  }

  async resolve(alertId: string): Promise<void> {
    await this.alertService.resolveAlert(alertId);
  }

  async dismiss(alertId: string): Promise<void> {
    await this.alertService.dismissAlert(alertId);
  }

  async refresh(): Promise<void> {
    await this.alertService.refresh();
  }

  // ---- Template Helpers ----

  getSeverityIcon(severity: AlertSeverity): string {
    switch (severity) {
      case 'critical': return 'lucideAlertCircle';
      case 'warning': return 'lucideAlertTriangle';
      case 'info': return 'lucideInfo';
    }
  }

  getSourceIcon(sourceType: string): string {
    switch (sourceType) {
      case 'coordinator': return 'lucideServer';
      case 'tower': return 'lucideLightbulb';
      case 'node': return 'lucideLightbulb';
      default: return 'lucideMonitor';
    }
  }

  formatRelativeTime(date: Date | string): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffSecs < 60) return `${diffSecs}s ago`;
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return d.toLocaleDateString();
  }

  trackByAlertId(_index: number, alert: Alert): string {
    return alert._id;
  }
}
