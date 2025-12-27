import { Component, computed, input } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const iconVariants = cva('inline-flex shrink-0', {
  variants: {
    size: {
      xs: 'h-3 w-3',
      sm: 'h-4 w-4',
      default: 'h-5 w-5',
      lg: 'h-6 w-6',
      xl: 'h-8 w-8',
      '2xl': 'h-10 w-10',
    },
    variant: {
      default: 'text-current',
      muted: 'text-muted-foreground',
      primary: 'text-primary',
      success: 'text-green-500',
      warning: 'text-amber-500',
      error: 'text-destructive',
    },
  },
  defaultVariants: {
    size: 'default',
    variant: 'default',
  },
});

export type IconVariants = VariantProps<typeof iconVariants>;

/**
 * Wrapper component for lucide-angular icons
 * Usage: <hlm-icon size="lg" variant="success"><lucide-icon name="check" /></hlm-icon>
 */
@Component({
  selector: 'hlm-icon',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmIconComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly size = input<IconVariants['size']>('default');
  public readonly variant = input<IconVariants['variant']>('default');

  protected _computedClass = computed(() =>
    hlm(
      iconVariants({ size: this.size(), variant: this.variant() }),
      this.userClass()
    )
  );
}

/**
 * Directive version for direct usage on lucide icons
 * Usage: <lucide-icon hlmIcon size="lg" name="check" />
 */
@Component({
  selector: '[hlmIcon]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmIconDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly size = input<IconVariants['size']>('default');
  public readonly variant = input<IconVariants['variant']>('default');

  protected _computedClass = computed(() =>
    hlm(
      iconVariants({ size: this.size(), variant: this.variant() }),
      this.userClass()
    )
  );
}
