import { computed, Directive, input } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const separatorVariants = cva('shrink-0 bg-border', {
  variants: {
    orientation: {
      horizontal: 'h-[1px] w-full',
      vertical: 'h-full w-[1px]',
    },
  },
  defaultVariants: {
    orientation: 'horizontal',
  },
});

export type SeparatorVariants = VariantProps<typeof separatorVariants>;

@Directive({
  selector: '[hlmSeparator]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    role: 'separator',
    '[attr.aria-orientation]': 'orientation()',
  },
})
export class HlmSeparatorDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly orientation = input<SeparatorVariants['orientation']>('horizontal');

  protected _computedClass = computed(() =>
    hlm(separatorVariants({ orientation: this.orientation() }), this.userClass())
  );
}
