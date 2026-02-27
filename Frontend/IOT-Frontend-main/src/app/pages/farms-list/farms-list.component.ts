import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBuilding,
  lucideWaves,
  lucideLeaf,
  lucidePlus,
  lucideRefreshCw,
  lucideTrash2,
  lucidePencil,
  lucideChevronRight,
  lucideChevronDown,
  lucideMapPin,
  lucideAlertTriangle
} from '@ng-icons/lucide';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { IoTDataService } from '../../core/services/iot-data.service';
import { ApiService } from '../../core/services/api.service';
import { Farm, FarmSummary, CoordinatorSummary, NodeSummary, getCoordinatorStatus, CoordinatorStatus } from '../../core/models';
import { FarmDialogComponent, FarmDialogData } from '../../components/farm-dialog/farm-dialog.component';

@Component({
  selector: 'app-farms-list',
  standalone: true,
  imports: [CommonModule, RouterLink, NgIcon, HlmBadgeDirective, HlmButtonDirective, FarmDialogComponent],
  providers: [
    provideIcons({
      lucideBuilding,
      lucideWaves,
      lucideLeaf,
      lucidePlus,
      lucideRefreshCw,
      lucideTrash2,
      lucidePencil,
      lucideChevronRight,
      lucideChevronDown,
      lucideMapPin,
      lucideAlertTriangle
    })
  ],
  templateUrl: './farms-list.component.html',
  styleUrls: ['./farms-list.component.scss']
})
export class FarmsListComponent implements OnInit {
  private readonly dataService = inject(IoTDataService);
  private readonly apiService = inject(ApiService);

  readonly loading = signal(false);
  readonly farms = signal<FarmSummary[]>([]);
  readonly expandedFarms = signal<Set<string>>(new Set());
  
  // Dialog state
  readonly isDialogOpen = signal(false);
  readonly editingFarm = signal<FarmDialogData | null>(null);
  
  // Get coordinators and nodes from data service
  readonly coordinators = this.dataService.coordinators;
  readonly nodes = this.dataService.nodes;

  // Computed: farms with their reservoirs
  readonly farmsWithData = computed(() => {
    const farmsList = this.farms();
    const coords = this.coordinators();
    const nodesList = this.nodes();
    
    return farmsList.map(farm => {
      // Find reservoirs assigned to this farm
      const farmReservoirs = coords.filter(c => 
        this.getFarmReservoirIds(farm._id).includes(c._id) || 
        this.getFarmReservoirIds(farm._id).includes(c.coord_id)
      );
      
      // Find towers connected to these reservoirs
      // Note: nodes use coordinator_id which matches coordinator's _id (not coord_id)
      const farmTowers = nodesList.filter(n => 
        farmReservoirs.some(r => r._id === n.coordinator_id || r.coord_id === n.coordinator_id)
      );

      // Compute live connection status for each reservoir using getCoordinatorStatus
      const reservoirsWithStatus = farmReservoirs.map(r => ({
        ...r,
        status: getCoordinatorStatus(r)
      }));

      return {
        ...farm,
        reservoirs: reservoirsWithStatus,
        towers: farmTowers,
        onlineReservoirs: reservoirsWithStatus.filter(r => r.status === 'online').length,
        onlineTowers: farmTowers.filter(t => t.status_mode === 'operational').length
      };
    });
  });

  // Mock mapping of farm to reservoir IDs (in real app, this comes from backend)
  private farmReservoirMap: Record<string, string[]> = {};

  async ngOnInit(): Promise<void> {
    await this.loadData();
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    try {
      // Load dashboard data which includes coordinators and nodes
      await this.dataService.loadDashboardData();
      await this.loadFarmsFromApi();
    } finally {
      this.loading.set(false);
    }
  }

  private async loadFarmsFromApi(): Promise<void> {
    try {
      const farmsData = await firstValueFrom(this.apiService.getFarms());
      const coords = this.coordinators();
      const nodesList = this.dataService.nodes?.() ?? [];

      const farmSummaries: FarmSummary[] = (farmsData ?? []).map((farm: Farm) => {
        const farmId = farm._id;
        // Use reservoir_ids from the farm object to find coordinators
        const reservoirIds = farm.reservoir_ids ?? [];
        const farmCoords = coords.filter(c =>
          reservoirIds.includes(c.coord_id) || reservoirIds.includes(c._id)
        );
        const farmTowers = nodesList.filter(n =>
          farmCoords.some(c => c.coord_id === n.coordinator_id || c._id === n.coordinator_id)
        );

        return {
          _id: farmId,
          name: farm.name ?? `Farm ${farmId}`,
          description: farm.description ?? '',
          location: farm.location ?? '',
          plantType: farm.plantType,
          reservoirCount: farmCoords.length,
          towerCount: farmTowers.length,
          onlineReservoirs: farmCoords.filter(c => getCoordinatorStatus(c) === 'online').length,
          onlineTowers: farmTowers.filter(t => t.status_mode === 'operational').length,
          color: farm.color ?? '#22c55e'
        };
      });

      // Build reservoir map
      const resMap: Record<string, string[]> = {};
      for (const farm of farmsData ?? []) {
        resMap[farm._id] = farm.reservoir_ids ?? [];
      }
      this.farmReservoirMap = resMap;
      this.farms.set(farmSummaries);
    } catch (err) {
      console.error('Failed to load farms from API, using fallback', err);
      // Keep empty rather than mock
      this.farms.set([]);
    }
  }

  private getFarmReservoirIds(farmId: string): string[] {
    return this.farmReservoirMap[farmId] || [];
  }

  toggleFarmExpand(farmId: string): void {
    this.expandedFarms.update(set => {
      const newSet = new Set(set);
      if (newSet.has(farmId)) {
        newSet.delete(farmId);
      } else {
        newSet.add(farmId);
      }
      return newSet;
    });
  }

  isFarmExpanded(farmId: string): boolean {
    return this.expandedFarms().has(farmId);
  }

  createFarm(): void {
    this.editingFarm.set(null);
    this.isDialogOpen.set(true);
  }

  editFarm(farmId: string, event: Event): void {
    event.stopPropagation();
    
    // Find the farm data
    const farm = this.farms().find(f => f._id === farmId);
    if (farm) {
      const editData: FarmDialogData = {
        _id: farm._id,
        name: farm.name,
        description: farm.description || '',
        location: farm.location || '',
        plantType: (farm as any).plantType || '',
        color: farm.color || '#22c55e',
        reservoir_ids: this.getFarmReservoirIds(farm._id)
      };
      this.editingFarm.set(editData);
      this.isDialogOpen.set(true);
    }
  }

  closeDialog(): void {
    this.isDialogOpen.set(false);
    this.editingFarm.set(null);
  }

  saveFarm(data: FarmDialogData): void {
    console.log('Saving farm:', data);
    
    if (data._id) {
      // Update existing farm
      this.farms.update(farms => 
        farms.map(f => f._id === data._id ? {
          ...f,
          name: data.name,
          description: data.description,
          location: data.location,
          color: data.color,
          plantType: data.plantType
        } as FarmSummary : f)
      );
      
      // Update reservoir mapping
      this.farmReservoirMap[data._id] = data.reservoir_ids;
    } else {
      // Create new farm
      const newId = `farm-${Date.now()}`;
      const newFarm: FarmSummary = {
        _id: newId,
        name: data.name,
        description: data.description,
        location: data.location,
        color: data.color,
        reservoirCount: data.reservoir_ids.length,
        towerCount: 0,
        onlineReservoirs: 0,
        onlineTowers: 0
      };
      
      this.farms.update(farms => [...farms, newFarm]);
      this.farmReservoirMap[newId] = data.reservoir_ids;
    }
    
    this.closeDialog();
  }

  deleteFarm(farmId: string, event: Event): void {
    event.stopPropagation();
    if (confirm('Are you sure you want to delete this farm?')) {
      this.farms.update(farms => farms.filter(f => f._id !== farmId));
      delete this.farmReservoirMap[farmId];
    }
  }

  getStatusLabel(status: CoordinatorStatus): string {
    switch (status) {
      case 'online': return 'Online';
      case 'offline': return 'Offline';
      case 'warning': return 'Stale';
      case 'error': return 'Error';
      default: return status;
    }
  }

  getStatusBadgeVariant(status: string): 'default' | 'secondary' | 'destructive' | 'outline' {
    switch (status) {
      case 'online':
      case 'operational':
        return 'default';
      case 'offline':
      case 'error':
        return 'destructive';
      case 'warning':
      case 'pairing':
      case 'ota':
        return 'secondary';
      default:
        return 'outline';
    }
  }

  getNodeStatusDisplay(statusMode: string): string {
    switch (statusMode) {
      case 'operational':
        return 'Online';
      case 'pairing':
        return 'Pairing';
      case 'ota':
        return 'Updating';
      case 'error':
        return 'Error';
      default:
        return statusMode;
    }
  }

  formatLastSeen(lastSeen: Date | string | undefined): string {
    if (!lastSeen) return 'Never';
    const date = typeof lastSeen === 'string' ? new Date(lastSeen) : lastSeen;
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);

    if (diffSecs < 60) return `${diffSecs}s ago`;
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    return date.toLocaleDateString();
  }

  getTowersForReservoir(towers: NodeSummary[], reservoirId: string): NodeSummary[] {
    // Get the reservoir to find its _id (since nodes use coordinator_id = reservoir._id)
    const reservoir = this.coordinators().find(c => c.coord_id === reservoirId || c._id === reservoirId);
    if (!reservoir) return [];
    
    // Nodes use coordinator_id which matches reservoir's _id (not coord_id)
    return towers.filter(t => t.coordinator_id === reservoir._id || t.coordinator_id === reservoir.coord_id);
  }
}
