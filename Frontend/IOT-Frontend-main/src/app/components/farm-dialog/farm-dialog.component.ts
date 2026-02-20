import { Component, computed, inject, input, output, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideX,
  lucideBuilding,
  lucideWaves,
  lucideLeaf,
  lucideMapPin,
  lucideSprout,
  lucideCheck,
  lucidePalette
} from '@ng-icons/lucide';
import { HlmButtonDirective } from '../ui/button';
import { HlmInputDirective } from '../ui/input';
import { HlmLabelDirective } from '../ui/label';
import { HlmBadgeDirective } from '../ui/badge';
import { IoTDataService } from '../../core/services/iot-data.service';
import { CoordinatorSummary, NodeSummary } from '../../core/models';
import { CreateFarmRequest } from '../../core/models/farm.model';

export interface FarmDialogData {
  _id?: string;
  name: string;
  description: string;
  location: string;
  plantType: string;
  color: string;
  reservoir_ids: string[];
}

@Component({
  selector: 'app-farm-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgIcon,
    HlmButtonDirective,
    HlmInputDirective,
    HlmLabelDirective,
    HlmBadgeDirective
  ],
  providers: [
    provideIcons({
      lucideX,
      lucideBuilding,
      lucideWaves,
      lucideLeaf,
      lucideMapPin,
      lucideSprout,
      lucideCheck,
      lucidePalette
    })
  ],
  templateUrl: './farm-dialog.component.html',
  styleUrls: ['./farm-dialog.component.scss']
})
export class FarmDialogComponent {
  private readonly dataService = inject(IoTDataService);

  // Inputs
  readonly isOpen = input<boolean>(false);
  readonly editData = input<FarmDialogData | null>(null);

  // Outputs
  readonly closed = output<void>();
  readonly saved = output<FarmDialogData>();

  // Animation state
  readonly isClosing = signal(false);
  
  // Animation duration in ms (should match CSS)
  private readonly ANIMATION_DURATION = 200;

  // Form data
  readonly formData = signal<FarmDialogData>({
    name: '',
    description: '',
    location: '',
    plantType: '',
    color: '#22c55e',
    reservoir_ids: []
  });

  constructor() {
    // Watch for dialog open/editData changes to initialize form
    effect(() => {
      const open = this.isOpen();
      const edit = this.editData();
      
      if (open) {
        // Reset closing state when opening
        this.isClosing.set(false);
        
        if (edit) {
          // Edit mode - populate with existing data
          this.formData.set({ ...edit });
        } else {
          // Create mode - reset to defaults
          this.formData.set({
            name: '',
            description: '',
            location: '',
            plantType: '',
            color: '#22c55e',
            reservoir_ids: []
          });
        }
      }
    });
  }

  // Available reservoirs from data service
  readonly availableReservoirs = this.dataService.coordinators;
  readonly allNodes = this.dataService.nodes;

  // Preset colors
  readonly colorPresets = [
    '#22c55e', // Green
    '#3b82f6', // Blue
    '#f59e0b', // Amber
    '#ef4444', // Red
    '#8b5cf6', // Purple
    '#06b6d4', // Cyan
    '#ec4899', // Pink
    '#84cc16', // Lime
  ];

  // Plant type options
  readonly plantTypes = [
    'Lettuce',
    'Basil',
    'Tomatoes',
    'Strawberries',
    'Peppers',
    'Spinach',
    'Herbs Mix',
    'Microgreens',
    'Other'
  ];

  // Computed: Get towers for selected reservoirs
  readonly selectedReservoirTowers = computed(() => {
    const selectedIds = this.formData().reservoir_ids;
    const reservoirs = this.availableReservoirs();
    const nodes = this.allNodes();

    // Get all towers connected to selected reservoirs
    const towers: NodeSummary[] = [];
    for (const resId of selectedIds) {
      const reservoir = reservoirs.find(r => r._id === resId || r.coord_id === resId);
      if (reservoir) {
        const resTowers = nodes.filter(n => 
          n.coordinator_id === reservoir._id || n.coordinator_id === reservoir.coord_id
        );
        towers.push(...resTowers);
      }
    }
    return towers;
  });

  // Computed: Check if form is valid
  readonly isFormValid = computed(() => {
    const data = this.formData();
    return data.name.trim().length > 0;
  });

  // Computed: Is edit mode
  readonly isEditMode = computed(() => !!this.editData()?._id);

  updateField<K extends keyof FarmDialogData>(field: K, value: FarmDialogData[K]): void {
    this.formData.update(data => ({
      ...data,
      [field]: value
    }));
  }

  toggleReservoir(reservoirId: string): void {
    this.formData.update(data => {
      const ids = [...data.reservoir_ids];
      const index = ids.indexOf(reservoirId);
      if (index === -1) {
        ids.push(reservoirId);
      } else {
        ids.splice(index, 1);
      }
      return { ...data, reservoir_ids: ids };
    });
  }

  isReservoirSelected(reservoirId: string): boolean {
    return this.formData().reservoir_ids.includes(reservoirId);
  }

  selectColor(color: string): void {
    this.updateField('color', color);
  }

  getTowersForReservoir(reservoir: CoordinatorSummary): NodeSummary[] {
    return this.allNodes().filter(n => 
      n.coordinator_id === reservoir._id || n.coordinator_id === reservoir.coord_id
    );
  }

  onOverlayClick(event: MouseEvent): void {
    // Only close if clicking the overlay itself, not the content
    if ((event.target as HTMLElement).classList.contains('dialog-overlay')) {
      this.close();
    }
  }

  close(): void {
    // Trigger closing animation
    this.isClosing.set(true);
    
    // Wait for animation to complete before emitting close event
    setTimeout(() => {
      this.closed.emit();
    }, this.ANIMATION_DURATION);
  }

  save(): void {
    if (this.isFormValid()) {
      // Trigger closing animation
      this.isClosing.set(true);
      
      // Wait for animation to complete before emitting save event
      setTimeout(() => {
        this.saved.emit(this.formData());
      }, this.ANIMATION_DURATION);
    }
  }
}
