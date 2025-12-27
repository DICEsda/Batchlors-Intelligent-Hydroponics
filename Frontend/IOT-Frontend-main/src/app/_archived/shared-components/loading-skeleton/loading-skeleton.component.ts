import { Component, input, computed } from '@angular/core';
import { hlm } from '../../ui/utils';
import { HlmSkeletonComponent } from '../../ui/skeleton';

/**
 * Loading Skeleton Component for IoT Dashboard
 * Provides pre-built skeleton patterns for common components
 */
@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  imports: [HlmSkeletonComponent],
  template: `
    @switch (variant()) {
      @case ('card') {
        <div [class]="_computedClass()">
          <div class="rounded-lg border bg-card p-4 space-y-3">
            <div class="flex items-center justify-between">
              <hlm-skeleton class="h-5 w-32" />
              <hlm-skeleton class="h-5 w-16 rounded-full" />
            </div>
            <hlm-skeleton class="h-4 w-full" />
            <hlm-skeleton class="h-4 w-3/4" />
            <div class="grid grid-cols-2 gap-2 pt-2">
              <hlm-skeleton class="h-8 w-full" />
              <hlm-skeleton class="h-8 w-full" />
            </div>
          </div>
        </div>
      }
      @case ('device-card') {
        <div [class]="_computedClass()">
          <div class="rounded-lg border bg-card p-4 space-y-4">
            <div class="flex items-start justify-between">
              <div class="space-y-2">
                <hlm-skeleton class="h-5 w-28" />
                <hlm-skeleton class="h-3 w-20" />
              </div>
              <hlm-skeleton class="h-5 w-16 rounded-full" />
            </div>
            <div class="space-y-2">
              <hlm-skeleton class="h-3 w-full" />
              <hlm-skeleton class="h-2 w-full rounded-full" />
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1">
                <hlm-skeleton class="h-3 w-16" />
                <hlm-skeleton class="h-4 w-12" />
              </div>
              <div class="space-y-1">
                <hlm-skeleton class="h-3 w-16" />
                <hlm-skeleton class="h-4 w-20" />
              </div>
            </div>
          </div>
        </div>
      }
      @case ('coordinator') {
        <div [class]="_computedClass()">
          <div class="rounded-lg border bg-card p-6 space-y-4">
            <div class="flex items-center justify-between">
              <div class="space-y-2">
                <hlm-skeleton class="h-3 w-20" />
                <hlm-skeleton class="h-6 w-32" />
                <hlm-skeleton class="h-3 w-24" />
              </div>
              <hlm-skeleton class="h-6 w-20 rounded-full" />
            </div>
            <div class="grid grid-cols-3 gap-4 pt-2">
              @for (i of [1, 2, 3]; track i) {
                <div class="space-y-1">
                  <hlm-skeleton class="h-3 w-16" />
                  <hlm-skeleton class="h-4 w-20" />
                </div>
              }
            </div>
          </div>
        </div>
      }
      @case ('stat') {
        <div [class]="_computedClass()">
          <div class="rounded-lg border bg-card p-4">
            <div class="flex items-center justify-between">
              <div class="space-y-2">
                <hlm-skeleton class="h-8 w-12" />
                <hlm-skeleton class="h-3 w-16" />
              </div>
              <hlm-skeleton class="h-10 w-10 rounded-lg" />
            </div>
          </div>
        </div>
      }
      @case ('table-row') {
        <div [class]="_computedClass()">
          <div class="flex items-center gap-4 p-4 border-b">
            <hlm-skeleton class="h-8 w-8 rounded-full" />
            <hlm-skeleton class="h-4 w-32 flex-1" />
            <hlm-skeleton class="h-4 w-20" />
            <hlm-skeleton class="h-5 w-16 rounded-full" />
          </div>
        </div>
      }
      @default {
        <hlm-skeleton [class]="_computedClass()" />
      }
    }
  `,
})
export class LoadingSkeletonComponent {
  public readonly variant = input<'default' | 'card' | 'device-card' | 'coordinator' | 'stat' | 'table-row'>('default');
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() => hlm(this.userClass()));
}

/**
 * Multiple skeletons for grid layouts
 */
@Component({
  selector: 'app-skeleton-grid',
  standalone: true,
  imports: [LoadingSkeletonComponent],
  template: `
    <div [class]="gridClass()">
      @for (i of items(); track i) {
        <app-loading-skeleton [variant]="variant()" />
      }
    </div>
  `,
})
export class SkeletonGridComponent {
  public readonly count = input<number>(3);
  public readonly variant = input<'card' | 'device-card' | 'stat'>('card');
  public readonly columns = input<1 | 2 | 3 | 4>(3);
  public readonly userClass = input<string>('', { alias: 'class' });

  protected items = computed(() => Array.from({ length: this.count() }, (_, i) => i));

  protected gridClass = computed(() => {
    const colClasses: Record<number, string> = {
      1: 'grid-cols-1',
      2: 'grid-cols-1 sm:grid-cols-2',
      3: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
      4: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-4',
    };
    return hlm('grid gap-4', colClasses[this.columns()], this.userClass());
  });
}
