import { computed, Directive, input } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const badgeVariants = cva(
  'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground hover:bg-primary/80',
        secondary: 'border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80',
        destructive: 'border-transparent bg-destructive text-destructive-foreground hover:bg-destructive/80',
        outline: 'text-foreground',
        success: 'border-transparent bg-green-500/20 text-green-400 border-green-500/30',
        warning: 'border-transparent bg-amber-500/20 text-amber-400 border-amber-500/30',
        error: 'border-transparent bg-red-500/20 text-red-400 border-red-500/30',
        info: 'border-transparent bg-blue-500/20 text-blue-400 border-blue-500/30',
        // IoT specific status badges
        online: 'border-transparent bg-green-500/20 text-green-400 border-green-500/30',
        offline: 'border-transparent bg-red-500/20 text-red-400 border-red-500/30',
        pairing: 'border-transparent bg-amber-500/20 text-amber-400 border-amber-500/30 animate-pulse',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

export type BadgeVariants = VariantProps<typeof badgeVariants>;

@Directive({
  selector: '[hlmBadge]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmBadgeDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<BadgeVariants['variant']>('default');

  protected _computedClass = computed(() =>
    hlm(badgeVariants({ variant: this.variant() }), this.userClass())
  );
}
