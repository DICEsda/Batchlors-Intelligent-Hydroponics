import { Component, computed, input, output, signal } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const sliderVariants = cva('relative flex w-full touch-none select-none items-center', {
  variants: {
    orientation: {
      horizontal: 'h-5',
      vertical: 'h-full w-5 flex-col',
    },
  },
  defaultVariants: {
    orientation: 'horizontal',
  },
});

export const sliderTrackVariants = cva(
  'relative grow overflow-hidden rounded-full bg-secondary',
  {
    variants: {
      orientation: {
        horizontal: 'h-2 w-full',
        vertical: 'h-full w-2',
      },
    },
    defaultVariants: {
      orientation: 'horizontal',
    },
  }
);

export const sliderRangeVariants = cva('absolute bg-primary', {
  variants: {
    orientation: {
      horizontal: 'h-full',
      vertical: 'w-full',
    },
  },
  defaultVariants: {
    orientation: 'horizontal',
  },
});

export const sliderThumbVariants = cva(
  'block rounded-full border-2 border-primary bg-background ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      size: {
        default: 'h-5 w-5',
        sm: 'h-4 w-4',
        lg: 'h-6 w-6',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  }
);

export type SliderVariants = VariantProps<typeof sliderVariants>;
export type SliderThumbVariants = VariantProps<typeof sliderThumbVariants>;

@Component({
  selector: 'hlm-slider',
  standalone: true,
  template: `
    <div [class]="_containerClass()">
      <div [class]="_trackClass()">
        <div [class]="_rangeClass()" [style.width.%]="percentage()"></div>
      </div>
      <input
        type="range"
        [min]="min()"
        [max]="max()"
        [step]="step()"
        [value]="value()"
        [disabled]="disabled()"
        (input)="onInput($event)"
        class="absolute inset-0 h-full w-full cursor-pointer opacity-0"
      />
      <div 
        [class]="_thumbClass()" 
        [style.left.%]="percentage()"
        [style.transform]="'translateX(-50%)'"
      ></div>
    </div>
  `,
  host: {
    class: 'block w-full',
  },
})
export class HlmSliderComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly orientation = input<SliderVariants['orientation']>('horizontal');
  public readonly thumbSize = input<SliderThumbVariants['size']>('default');
  
  public readonly min = input<number>(0);
  public readonly max = input<number>(100);
  public readonly step = input<number>(1);
  public readonly value = input<number>(0);
  public readonly disabled = input<boolean>(false);

  public readonly valueChange = output<number>();

  protected percentage = computed(() => {
    const min = this.min();
    const max = this.max();
    const value = this.value();
    return ((value - min) / (max - min)) * 100;
  });

  protected _containerClass = computed(() =>
    hlm(sliderVariants({ orientation: this.orientation() }), 'relative', this.userClass())
  );

  protected _trackClass = computed(() =>
    hlm(sliderTrackVariants({ orientation: this.orientation() }))
  );

  protected _rangeClass = computed(() =>
    hlm(sliderRangeVariants({ orientation: this.orientation() }))
  );

  protected _thumbClass = computed(() =>
    hlm(sliderThumbVariants({ size: this.thumbSize() }), 'absolute top-1/2 -translate-y-1/2')
  );

  onInput(event: Event) {
    const target = event.target as HTMLInputElement;
    this.valueChange.emit(Number(target.value));
  }
}
