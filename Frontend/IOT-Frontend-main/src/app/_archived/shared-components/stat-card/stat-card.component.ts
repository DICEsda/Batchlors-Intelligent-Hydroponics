import { Component, computed, input } from '@angular/core';
import { hlm } from '../../ui/utils';
import { 
  HlmCardComponent, 
  HlmCardHeaderComponent, 
  HlmCardTitleComponent, 
  HlmCardContentComponent 
} from '../../ui/card';

export interface StatItem {
  label: string;
  value: string | number | null;
  unit?: string;
  status?: 'normal' | 'warning' | 'error' | 'success';
}

/**
 * Stat Card Component for displaying statistics
 * Used for showing device counts, sensor readings summary, etc.
 */
@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [
    HlmCardComponent,
    HlmCardHeaderComponent,
    HlmCardTitleComponent,
    HlmCardContentComponent,
  ],
  template: `
    <div hlmCard [class]="_computedClass()">
      <div hlmCardContent class="p-4">
        <div class="flex items-center justify-between">
          <div class="flex flex-col gap-1">
            <span class="text-2xl font-bold" [class]="valueClass()">
              {{ displayValue() }}
            </span>
            <span class="text-xs text-muted-foreground uppercase tracking-wide">
              {{ label() }}
            </span>
          </div>
          @if (icon()) {
            <div class="p-2 rounded-lg bg-muted/50">
              <ng-content select="[slot=icon]"></ng-content>
            </div>
          }
        </div>
        @if (subtitle()) {
          <p class="text-xs text-muted-foreground mt-2">{{ subtitle() }}</p>
        }
      </div>
    </div>
  `,
})
export class StatCardComponent {
  public readonly label = input.required<string>();
  public readonly value = input<string | number | null>(null);
  public readonly unit = input<string>('');
  public readonly subtitle = input<string>('');
  public readonly status = input<'normal' | 'warning' | 'error' | 'success'>('normal');
  public readonly icon = input<boolean>(false);
  public readonly userClass = input<string>('', { alias: 'class' });

  protected displayValue = computed(() => {
    const val = this.value();
    if (val === null || val === undefined) return '-';
    return this.unit() ? `${val}${this.unit()}` : val;
  });

  protected valueClass = computed(() => {
    const statusMap = {
      normal: 'text-foreground',
      warning: 'text-amber-500',
      error: 'text-destructive',
      success: 'text-green-500',
    };
    return statusMap[this.status()];
  });

  protected _computedClass = computed(() =>
    hlm('transition-shadow hover:shadow-md', this.userClass())
  );
}
