import { Component, computed, input } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const skeletonVariants = cva('animate-pulse rounded-md bg-muted', {
  variants: {
    variant: {
      default: '',
      circle: 'rounded-full',
      text: 'h-4',
      card: 'h-32',
    },
  },
  defaultVariants: {
    variant: 'default',
  },
});

export type SkeletonVariants = VariantProps<typeof skeletonVariants>;

@Component({
  selector: 'hlm-skeleton',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmSkeletonComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<SkeletonVariants['variant']>('default');

  protected _computedClass = computed(() =>
    hlm(skeletonVariants({ variant: this.variant() }), this.userClass())
  );
}

/**
 * Skeleton directive for inline usage
 */
@Component({
  selector: '[hlmSkeleton]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmSkeletonDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<SkeletonVariants['variant']>('default');

  protected _computedClass = computed(() =>
    hlm(skeletonVariants({ variant: this.variant() }), this.userClass())
  );
}
