import { Component, inject, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom, catchError, of } from 'rxjs';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideServer,
  lucideLayoutGrid,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideThermometer,
  lucideDroplet,
  lucideSun,
  lucideBattery,
  lucideRefreshCw,
  lucideArrowLeft,
  lucideFlower2,
  lucideGauge,
  lucideAlertTriangle,
  lucideClock,
  lucideLeaf,
  lucideChevronDown,
  lucideChevronRight,
} from '@ng-icons/lucide';
import { HlmCardDirective } from '../../components/ui/card';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';
import { TwinService } from '../../core/services/twin.service';
import { IoTDataService } from '../../core/services/iot-data.service';
import { ApiService } from '../../core/services/api.service';
import { TowerTwin } from '../../core/models/digital-twin.model';

@Component({
  selector: 'app-digital-twin',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgIcon,
    HlmCardDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
  ],
  providers: [
    provideIcons({
      lucideServer,
      lucideLayoutGrid,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideThermometer,
      lucideDroplet,
      lucideSun,
      lucideBattery,
      lucideRefreshCw,
      lucideArrowLeft,
      lucideFlower2,
      lucideGauge,
      lucideAlertTriangle,
      lucideClock,
      lucideLeaf,
      lucideChevronDown,
      lucideChevronRight,
    }),
  ],
  templateUrl: './digital-twin.component.html',
  styleUrl: './digital-twin.component.scss',
})
export class DigitalTwinComponent implements OnInit {
  private readonly twinService = inject(TwinService);
  private readonly dataService = inject(IoTDataService);
  private readonly api = inject(ApiService);

  // Coordinator expand/collapse state (coordId -> expanded)
  expandedCoords = new Set<string>();

  // Expose twin service signals
  readonly isLoading = this.twinService.isLoading;
  readonly error = this.twinService.error;
  readonly coordTwins = this.twinService.coordTwins;
  readonly towersByCoordinator = this.twinService.towersByCoordinator;

  // Use the first site's farmId, or fallback
  readonly farmId = computed(() => {
    const sites = this.dataService.sites();
    return sites.length > 0 ? (sites[0] as any).farmId ?? (sites[0] as any).farm_id ?? sites[0]._id : null;
  });

  ngOnInit(): void {
    this.resolveFarmAndLoad();
  }

  /**
   * Resolve the farmId from sites or the farms API, then load twins.
   */
  private async resolveFarmAndLoad(): Promise<void> {
    // 1. Try sites already in memory
    const sites = this.dataService.sites();
    if (sites.length > 0) {
      const fId = (sites[0] as any).farmId ?? (sites[0] as any).farm_id ?? sites[0]._id;
      if (fId) { this.twinService.loadFarmTwins(fId); return; }
    }

    // 2. Try loading dashboard data (populates sites if backend supports /sites)
    await this.dataService.loadDashboardData();
    const s = this.dataService.sites();
    if (s.length > 0) {
      const fId = (s[0] as any).farmId ?? (s[0] as any).farm_id ?? s[0]._id;
      if (fId) { this.twinService.loadFarmTwins(fId); return; }
    }

    // 3. Fallback: query the farms API directly for the first farm
    try {
      const farms = await firstValueFrom(this.api.getFarms().pipe(catchError(() => of([]))));
      if (farms.length > 0) {
        const fId = farms[0].farmId ?? farms[0]._id;
        if (fId) { this.twinService.loadFarmTwins(fId); return; }
      }
    } catch {
      // Farms endpoint unavailable — no data to show
    }
  }

  async refresh(): Promise<void> {
    const fId = this.farmId();
    if (fId) {
      await this.twinService.loadFarmTwins(fId);
    }
  }

  getSyncBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'in_sync':
      case 'insync': return 'default';
      case 'pending': return 'secondary';
      case 'stale': return 'outline';
      case 'conflict': return 'destructive';
      case 'offline': return 'outline';
      default: return 'secondary';
    }
  }

  formatSyncLabel(status: string): string {
    switch (status) {
      case 'in_sync':
      case 'insync': return 'In Sync';
      case 'pending': return 'Pending';
      case 'stale': return 'Stale';
      case 'conflict': return 'Conflict';
      case 'offline': return 'Offline';
      default: return status;
    }
  }

  formatTemp(value: number | null): string {
    return value != null ? `${value.toFixed(1)}` : '--';
  }

  formatPercent(value: number | null): string {
    return value != null ? `${value.toFixed(0)}%` : '--';
  }

  formatNumber(value: number | null, decimals = 1): string {
    return value != null ? value.toFixed(decimals) : '--';
  }

  /**
   * Format a health score for display.
   * ML service returns values on a 0-1 scale (e.g. 0.85).
   * If the value is <= 1 we treat it as a fraction and multiply by 100.
   * Returns an integer string like "85".
   */
  formatHealthScore(value: number | null | undefined): string {
    if (value == null) return '--';
    const pct = value <= 1 ? value * 100 : value;
    return `${Math.round(pct)}%`;
  }

  getTowersForCoord(coordId: string): TowerTwin[] {
    return this.towersByCoordinator().get(coordId) || [];
  }

  toggleCoord(coordId: string): void {
    if (this.expandedCoords.has(coordId)) {
      this.expandedCoords.delete(coordId);
    } else {
      this.expandedCoords.add(coordId);
    }
  }

  isCoordExpanded(coordId: string): boolean {
    return this.expandedCoords.has(coordId);
  }
}
