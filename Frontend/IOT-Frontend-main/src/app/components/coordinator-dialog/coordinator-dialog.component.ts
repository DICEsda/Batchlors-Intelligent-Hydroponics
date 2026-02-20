import { Component, computed, inject, input, output, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideX,
  lucideWaves,
  lucideMapPin,
  lucideTag,
  lucideCheck,
  lucidePalette,
  lucideServer,
  lucideInfo
} from '@ng-icons/lucide';
import { HlmButtonDirective } from '../ui/button';
import { HlmInputDirective } from '../ui/input';
import { HlmLabelDirective } from '../ui/label';
import { HlmBadgeDirective } from '../ui/badge';

export interface CoordinatorDialogData {
  _id: string;
  coord_id: string;
  name?: string;
  description?: string;
  location?: string;
  tags?: string[];
  color?: string;
}

@Component({
  selector: 'app-coordinator-dialog',
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
      lucideWaves,
      lucideMapPin,
      lucideTag,
      lucideCheck,
      lucidePalette,
      lucideServer,
      lucideInfo
    })
  ],
  templateUrl: './coordinator-dialog.component.html',
  styleUrls: ['./coordinator-dialog.component.scss']
})
export class CoordinatorDialogComponent {
  // Inputs
  readonly isOpen = input<boolean>(false);
  readonly editData = input<CoordinatorDialogData | null>(null);

  // Outputs
  readonly closed = output<void>();
  readonly saved = output<CoordinatorDialogData>();

  // Animation state
  readonly isClosing = signal(false);
  
  // Animation duration in ms (should match CSS)
  private readonly ANIMATION_DURATION = 200;

  // Form data
  readonly formData = signal<CoordinatorDialogData>({
    _id: '',
    coord_id: '',
    name: '',
    description: '',
    location: '',
    tags: [],
    color: '#3b82f6'
  });

  // New tag input
  readonly newTag = signal<string>('');

  constructor() {
    // Watch for dialog open/editData changes to initialize form
    effect(() => {
      const open = this.isOpen();
      const edit = this.editData();
      
      if (open && edit) {
        // Reset closing state when opening
        this.isClosing.set(false);
        
        // Populate with existing data
        this.formData.set({
          _id: edit._id,
          coord_id: edit.coord_id,
          name: edit.name || '',
          description: edit.description || '',
          location: edit.location || '',
          tags: edit.tags || [],
          color: edit.color || '#3b82f6'
        });
        this.newTag.set('');
      }
    });
  }

  // Preset colors
  readonly colorPresets = [
    '#3b82f6', // Blue
    '#22c55e', // Green  
    '#f59e0b', // Amber
    '#ef4444', // Red
    '#8b5cf6', // Purple
    '#06b6d4', // Cyan
    '#ec4899', // Pink
    '#84cc16', // Lime
  ];

  // Computed: Check if form is valid
  readonly isFormValid = computed(() => {
    const data = this.formData();
    return data.name && data.name.trim().length > 0;
  });

  updateField<K extends keyof CoordinatorDialogData>(field: K, value: CoordinatorDialogData[K]): void {
    this.formData.update(data => ({
      ...data,
      [field]: value
    }));
  }

  selectColor(color: string): void {
    this.updateField('color', color);
  }

  addTag(): void {
    const tag = this.newTag().trim();
    if (tag && !this.formData().tags!.includes(tag)) {
      this.formData.update(data => ({
        ...data,
        tags: [...(data.tags || []), tag]
      }));
      this.newTag.set('');
    }
  }

  removeTag(tag: string): void {
    this.formData.update(data => ({
      ...data,
      tags: (data.tags || []).filter(t => t !== tag)
    }));
  }

  onTagKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.addTag();
    }
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
