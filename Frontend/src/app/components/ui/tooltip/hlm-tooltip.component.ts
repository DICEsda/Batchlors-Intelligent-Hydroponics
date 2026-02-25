import { Component, computed, Directive, input, TemplateRef, ViewContainerRef, effect, signal } from '@angular/core';
import { cva } from 'class-variance-authority';
import { hlm } from '../utils';

export const tooltipVariants = cva(
  'z-50 overflow-hidden rounded-md border bg-popover px-3 py-1.5 text-sm text-popover-foreground shadow-md animate-in fade-in-0 zoom-in-95',
  {
    variants: {
      side: {
        top: 'slide-in-from-bottom-2',
        bottom: 'slide-in-from-top-2',
        left: 'slide-in-from-right-2',
        right: 'slide-in-from-left-2',
      },
    },
    defaultVariants: {
      side: 'top',
    },
  }
);

/**
 * Simple tooltip component
 * For full tooltip functionality, use @spartan-ng/ui-tooltip-brain
 */
@Component({
  selector: 'hlm-tooltip',
  standalone: true,
  template: `
    <div [class]="_computedClass()" role="tooltip">
      <ng-content />
    </div>
  `,
})
export class HlmTooltipComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly side = input<'top' | 'bottom' | 'left' | 'right'>('top');

  protected _computedClass = computed(() =>
    hlm(tooltipVariants({ side: this.side() }), this.userClass())
  );
}

/**
 * Simple tooltip trigger directive
 * Shows tooltip on hover using CSS
 */
@Directive({
  selector: '[hlmTooltipTrigger]',
  standalone: true,
  host: {
    class: 'relative inline-block group',
    '[attr.aria-describedby]': 'tooltipId()',
  },
})
export class HlmTooltipTriggerDirective {
  public readonly tooltip = input<string>('', { alias: 'hlmTooltipTrigger' });
  public readonly tooltipId = signal(`tooltip-${Math.random().toString(36).substr(2, 9)}`);
}
