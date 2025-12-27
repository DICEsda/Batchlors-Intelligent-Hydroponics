import { computed, Directive, input } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const labelVariants = cva(
  'text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70',
  {
    variants: {
      variant: {
        default: 'text-foreground',
        muted: 'text-muted-foreground',
        error: 'text-destructive',
      },
      size: {
        default: 'text-sm',
        sm: 'text-xs',
        lg: 'text-base',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
);

export type LabelVariants = VariantProps<typeof labelVariants>;

@Directive({
  selector: '[hlmLabel]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmLabelDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<LabelVariants['variant']>('default');
  public readonly size = input<LabelVariants['size']>('default');

  protected _computedClass = computed(() =>
    hlm(
      labelVariants({ variant: this.variant(), size: this.size() }),
      this.userClass()
    )
  );
}
