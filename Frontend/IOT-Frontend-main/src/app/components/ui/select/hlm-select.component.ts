import { Component, computed, Directive, input, output, signal } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

export const selectTriggerVariants = cva(
  'flex h-10 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      size: {
        default: 'h-10',
        sm: 'h-8 text-xs',
        lg: 'h-12',
      },
      error: {
        true: 'border-destructive focus:ring-destructive',
        false: '',
      },
    },
    defaultVariants: {
      size: 'default',
      error: false,
    },
  }
);

export const selectContentVariants = cva(
  'relative z-50 max-h-96 min-w-[8rem] overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md animate-in fade-in-0 zoom-in-95'
);

export const selectItemVariants = cva(
  'relative flex w-full cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50 hover:bg-accent hover:text-accent-foreground'
);

export type SelectTriggerVariants = VariantProps<typeof selectTriggerVariants>;

/**
 * Select Trigger - the button that opens the dropdown
 */
@Directive({
  selector: '[hlmSelectTrigger]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    type: 'button',
    role: 'combobox',
    '[attr.aria-expanded]': 'isOpen()',
  },
})
export class HlmSelectTriggerDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly size = input<SelectTriggerVariants['size']>('default');
  public readonly error = input<boolean>(false);
  public readonly isOpen = input<boolean>(false);

  protected _computedClass = computed(() =>
    hlm(
      selectTriggerVariants({ size: this.size(), error: this.error() }),
      this.userClass()
    )
  );
}

/**
 * Select Content - the dropdown container
 */
@Directive({
  selector: '[hlmSelectContent]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    role: 'listbox',
  },
})
export class HlmSelectContentDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm(selectContentVariants(), this.userClass())
  );
}

/**
 * Select Item - individual option
 */
@Directive({
  selector: '[hlmSelectItem]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    role: 'option',
    '[attr.aria-selected]': 'isSelected()',
    '[attr.data-selected]': 'isSelected()',
  },
})
export class HlmSelectItemDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly isSelected = input<boolean>(false);
  public readonly isDisabled = input<boolean>(false);

  protected _computedClass = computed(() =>
    hlm(selectItemVariants(), this.userClass())
  );
}

/**
 * Select Label - group label
 */
@Directive({
  selector: '[hlmSelectLabel]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmSelectLabelDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('py-1.5 pl-8 pr-2 text-sm font-semibold', this.userClass())
  );
}

/**
 * Select Value - displays the selected value
 */
@Component({
  selector: 'hlm-select-value',
  standalone: true,
  template: `
    @if (value()) {
      {{ value() }}
    } @else {
      <span class="text-muted-foreground">{{ placeholder() }}</span>
    }
  `,
  host: {
    class: 'flex-1 text-left truncate',
  },
})
export class HlmSelectValueComponent {
  public readonly value = input<string>('');
  public readonly placeholder = input<string>('Select...');
}
