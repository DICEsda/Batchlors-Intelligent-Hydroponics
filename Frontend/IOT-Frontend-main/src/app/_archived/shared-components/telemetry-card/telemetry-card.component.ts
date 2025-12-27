import { Component, computed, input } from '@angular/core';
import { hlm } from '../../ui/utils';
import { 
  HlmCardComponent, 
  HlmCardHeaderComponent, 
  HlmCardTitleComponent, 
  HlmCardDescriptionComponent,
  HlmCardContentComponent 
} from '../../ui/card';

export interface TelemetryReading {
  label: string;
  value: string | number | null;
  unit?: string;
  icon?: string;
  status?: 'normal' | 'warning' | 'error';
}

/**
 * Telemetry Card Component for displaying sensor readings
 * Shows multiple readings in a grid format with optional icons and status
 */
@Component({
  selector: 'app-telemetry-card',
  standalone: true,
  imports: [
    HlmCardComponent,
    HlmCardHeaderComponent,
    HlmCardTitleComponent,
    HlmCardDescriptionComponent,
    HlmCardContentComponent,
  ],
  template: `
    <div hlmCard [class]="_computedClass()">
      @if (title()) {
        <div hlmCardHeader class="pb-2">
          <h4 hlmCardTitle class="text-sm font-medium">{{ title() }}</h4>
          @if (description()) {
            <p hlmCardDescription>{{ description() }}</p>
          }
        </div>
      }
      <div hlmCardContent [class]="contentClass()">
        <div [class]="gridClass()">
          @for (reading of readings(); track reading.label) {
            <div class="telemetry-item flex flex-col gap-1">
              <span class="text-xs text-muted-foreground">{{ reading.label }}</span>
              <span [class]="getValueClass(reading)">
                {{ formatValue(reading) }}
              </span>
            </div>
          }
        </div>
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    .telemetry-item {
      min-width: 0;
    }
  `],
})
export class TelemetryCardComponent {
  public readonly title = input<string>('');
  public readonly description = input<string>('');
  public readonly readings = input<TelemetryReading[]>([]);
  public readonly columns = input<1 | 2 | 3 | 4>(2);
  public readonly compact = input<boolean>(false);
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('', this.userClass())
  );

  protected contentClass = computed(() =>
    this.compact() ? 'p-3' : 'p-4 pt-0'
  );

  protected gridClass = computed(() => {
    const colClasses: Record<number, string> = {
      1: 'grid-cols-1',
      2: 'grid-cols-2',
      3: 'grid-cols-3',
      4: 'grid-cols-4',
    };
    return hlm('grid gap-4', colClasses[this.columns()]);
  });

  protected formatValue(reading: TelemetryReading): string {
    if (reading.value === null || reading.value === undefined) {
      return 'No data';
    }
    if (reading.unit) {
      return `${reading.value}${reading.unit}`;
    }
    return String(reading.value);
  }

  protected getValueClass(reading: TelemetryReading): string {
    const baseClass = 'text-sm font-medium';
    if (reading.value === null || reading.value === undefined) {
      return `${baseClass} text-muted-foreground`;
    }
    const statusClasses = {
      normal: 'text-foreground',
      warning: 'text-amber-500',
      error: 'text-destructive',
    };
    return `${baseClass} ${statusClasses[reading.status ?? 'normal']}`;
  }
}
