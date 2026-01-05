import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
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
import { HlmSkeletonComponent } from '../../components/ui/skeleton';
import { HlmInputDirective } from '../../components/ui/input';
import { AlertService } from '../../core/services';
import {
  Alert,
  AlertSeverity,
  AlertStatus,
  AlertCategory,
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
    HlmSkeletonComponent,
    HlmInputDirective,
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
export class AlertsComponent implements OnInit {
  private readonly alertService = inject(AlertService);

  // ============================================================================
  // State from service
  // ============================================================================
  readonly isLoading = this.alertService.isLoading;
  readonly error = this.alertService.error;
  readonly usingMockData = this.alertService.usingMockData;

  // Alerts data
  readonly alerts = this.alertService.alerts;
  readonly filteredAlerts = this.alertService.filteredAlerts;
  readonly alertStats = this.alertService.alertStats;

  // Counts
  readonly activeCount = this.alertService.activeCount;
  readonly criticalCount = this.alertService.criticalCount;
  readonly activeAlerts = this.alertService.activeAlerts;
  readonly acknowledgedAlerts = this.alertService.acknowledgedAlerts;
  readonly resolvedAlerts = this.alertService.resolvedAlerts;

  // Filters from service
  readonly filterSeverity = this.alertService.filterSeverity;
  readonly filterStatus = this.alertService.filterStatus;
  readonly filterCategory = this.alertService.filterCategory;
  readonly searchTerm = this.alertService.searchTerm;

  // ============================================================================
  // Local UI state
  // ============================================================================
  readonly expandedAlertId = signal<string | null>(null);
  readonly showFilters = signal(true);

  // ============================================================================
  // Computed values
  // ============================================================================
  readonly hasActiveFilters = computed(() => {
    return (
      this.filterSeverity() !== 'all' ||
      this.filterStatus() !== 'all' ||
      this.filterCategory() !== 'all' ||
      this.searchTerm().trim() !== ''
    );
  });

  readonly sortedAlerts = computed(() => {
    return [...this.filteredAlerts()].sort((a, b) => {
      // Sort by status first (active > acknowledged > resolved)
      const statusOrder: Record<AlertStatus, number> = {
        active: 0,
        acknowledged: 1,
        resolved: 2,
      };
      const statusDiff = statusOrder[a.status] - statusOrder[b.status];
      if (statusDiff !== 0) return statusDiff;

      // Then by severity (critical > warning > info)
      const severityOrder: Record<AlertSeverity, number> = {
        critical: 0,
        warning: 1,
        info: 2,
      };
      const severityDiff = severityOrder[a.severity] - severityOrder[b.severity];
      if (severityDiff !== 0) return severityDiff;

      // Finally by date (newest first)
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    });
  });

  // ============================================================================
  // Lifecycle
  // ============================================================================
  ngOnInit(): void {
    this.loadData();
  }

  // ============================================================================
  // Data loading
  // ============================================================================
  async loadData(): Promise<void> {
    await this.alertService.loadAlerts();
  }

  async refreshData(): Promise<void> {
    await this.alertService.refresh();
  }

  // ============================================================================
  // Filter actions
  // ============================================================================
  setSeverityFilter(severity: AlertSeverity | 'all'): void {
    this.alertService.setSeverityFilter(severity);
  }

  setStatusFilter(status: AlertStatus | 'all'): void {
    this.alertService.setStatusFilter(status);
  }

  setCategoryFilter(category: AlertCategory | 'all'): void {
    this.alertService.setCategoryFilter(category);
  }

  onSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.alertService.setSearchTerm(input.value);
  }

  clearFilters(): void {
    this.alertService.clearFilters();
  }

  toggleFilters(): void {
    this.showFilters.update(v => !v);
  }

  // ============================================================================
  // Alert actions
  // ============================================================================
  async acknowledgeAlert(alertId: string, event?: Event): Promise<void> {
    event?.stopPropagation();
    await this.alertService.acknowledgeAlert(alertId);
  }

  async resolveAlert(alertId: string, event?: Event): Promise<void> {
    event?.stopPropagation();
    await this.alertService.resolveAlert(alertId);
  }

  async dismissAlert(alertId: string, event?: Event): Promise<void> {
    event?.stopPropagation();
    await this.alertService.dismissAlert(alertId);
  }

  async acknowledgeAllActive(): Promise<void> {
    await this.alertService.acknowledgeAllActive();
  }

  async resolveAllAcknowledged(): Promise<void> {
    await this.alertService.resolveAllAcknowledged();
  }

  // ============================================================================
  // UI helpers
  // ============================================================================
  toggleAlertExpand(alertId: string): void {
    this.expandedAlertId.update(current => (current === alertId ? null : alertId));
  }

  isExpanded(alertId: string): boolean {
    return this.expandedAlertId() === alertId;
  }

  // ============================================================================
  // Display helpers
  // ============================================================================
  getSeverityIcon(severity: AlertSeverity): string {
    switch (severity) {
      case 'critical':
        return 'lucideAlertCircle';
      case 'warning':
        return 'lucideAlertTriangle';
      case 'info':
        return 'lucideInfo';
      default:
        return 'lucideBell';
    }
  }

  getSeverityBadgeVariant(severity: AlertSeverity): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (severity) {
      case 'critical':
        return 'destructive';
      case 'warning':
        return 'secondary';
      case 'info':
        return 'outline';
      default:
        return 'default';
    }
  }

  getStatusBadgeVariant(status: AlertStatus): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'active':
        return 'destructive';
      case 'acknowledged':
        return 'secondary';
      case 'resolved':
        return 'default';
      default:
        return 'outline';
    }
  }

  getCategoryIcon(category: AlertCategory): string {
    switch (category) {
      case 'sensor':
        return 'lucideCpu';
      case 'system':
        return 'lucideServer';
      case 'network':
        return 'lucideWifi';
      case 'maintenance':
        return 'lucideWrench';
      case 'security':
        return 'lucideShield';
      default:
        return 'lucideBell';
    }
  }

  getSourceIcon(sourceType: string): string {
    switch (sourceType) {
      case 'coordinator':
        return 'lucideServer';
      case 'tower':
      case 'node':
        return 'lucideRadio';
      case 'system':
        return 'lucideCpu';
      case 'backend':
        return 'lucideServer';
      default:
        return 'lucideBell';
    }
  }

  formatDate(date: Date | string | undefined): string {
    if (!date) return 'N/A';
    const d = new Date(date);
    return d.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
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

  capitalizeFirst(str: string): string {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }

  // ============================================================================
  // TrackBy functions
  // ============================================================================
  trackByAlertId(index: number, alert: Alert): string {
    return alert._id;
  }
}
