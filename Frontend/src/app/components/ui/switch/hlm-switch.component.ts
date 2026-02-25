import { Component, computed, input, output, signal } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const switchVariants = cva(
  'peer inline-flex shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      size: {
        default: 'h-6 w-11',
        sm: 'h-5 w-9',
        lg: 'h-7 w-14',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  }
);

export const switchThumbVariants = cva(
  'pointer-events-none block rounded-full bg-background shadow-lg ring-0 transition-transform',
  {
    variants: {
      size: {
        default: 'h-5 w-5',
        sm: 'h-4 w-4',
        lg: 'h-6 w-6',
      },
      checked: {
        true: '',
        false: '',
      },
    },
    compoundVariants: [
      { size: 'default', checked: true, class: 'translate-x-5' },
      { size: 'default', checked: false, class: 'translate-x-0' },
      { size: 'sm', checked: true, class: 'translate-x-4' },
      { size: 'sm', checked: false, class: 'translate-x-0' },
      { size: 'lg', checked: true, class: 'translate-x-7' },
      { size: 'lg', checked: false, class: 'translate-x-0' },
    ],
    defaultVariants: {
      size: 'default',
      checked: false,
    },
  }
);

export type SwitchVariants = VariantProps<typeof switchVariants>;

@Component({
  selector: 'hlm-switch',
  standalone: true,
  template: `
    <button
      type="button"
      role="switch"
      [attr.aria-checked]="checked()"
      [disabled]="disabled()"
      [class]="_computedClass()"
      (click)="toggle()"
    >
      <span [class]="_thumbClass()"></span>
    </button>
  `,
  host: {
    class: 'inline-block',
  },
})
export class HlmSwitchComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly size = input<SwitchVariants['size']>('default');
  public readonly checked = input<boolean>(false);
  public readonly disabled = input<boolean>(false);

  public readonly checkedChange = output<boolean>();

  private readonly _internalChecked = signal(false);

  protected _computedClass = computed(() =>
    hlm(
      switchVariants({ size: this.size() }),
      this.checked() ? 'bg-primary' : 'bg-input',
      this.userClass()
    )
  );

  protected _thumbClass = computed(() =>
    hlm(switchThumbVariants({ size: this.size(), checked: this.checked() }))
  );

  toggle() {
    if (!this.disabled()) {
      this.checkedChange.emit(!this.checked());
    }
  }
}
