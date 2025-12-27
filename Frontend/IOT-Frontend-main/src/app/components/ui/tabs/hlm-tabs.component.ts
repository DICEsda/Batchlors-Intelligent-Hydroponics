import { Component, computed, Directive, input, output, signal, contentChildren, AfterContentInit } from '@angular/core';
import { cva } from 'class-variance-authority';
import { hlm } from '../utils';

// Tabs List
export const tabsListVariants = cva(
  'inline-flex h-10 items-center justify-center rounded-md bg-muted p-1 text-muted-foreground',
  {
    variants: {
      variant: {
        default: '',
        underline: 'bg-transparent rounded-none border-b border-border p-0 h-auto',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

// Tabs Trigger
export const tabsTriggerVariants = cva(
  'inline-flex items-center justify-center whitespace-nowrap px-3 py-1.5 text-sm font-medium ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default: 'rounded-sm data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm',
        underline: 'rounded-none border-b-2 border-transparent pb-3 pt-2 data-[state=active]:border-primary data-[state=active]:text-foreground',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

// Tabs Content
export const tabsContentVariants = cva(
  'mt-2 ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
);

@Directive({
  selector: '[hlmTabsList]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    role: 'tablist',
  },
})
export class HlmTabsListDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<'default' | 'underline'>('default');

  protected _computedClass = computed(() =>
    hlm(tabsListVariants({ variant: this.variant() }), this.userClass())
  );
}

@Directive({
  selector: '[hlmTabsTrigger]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    '[attr.data-state]': 'isActive() ? "active" : "inactive"',
    '[attr.aria-selected]': 'isActive()',
    role: 'tab',
    type: 'button',
    '(click)': 'onClick()',
  },
})
export class HlmTabsTriggerDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly variant = input<'default' | 'underline'>('default');
  public readonly value = input.required<string>();
  public readonly isActive = input<boolean>(false);
  
  public readonly activated = output<string>();

  protected _computedClass = computed(() =>
    hlm(tabsTriggerVariants({ variant: this.variant() }), this.userClass())
  );

  onClick() {
    this.activated.emit(this.value());
  }
}

@Directive({
  selector: '[hlmTabsContent]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    '[hidden]': '!isActive()',
    '[attr.data-state]': 'isActive() ? "active" : "inactive"',
    role: 'tabpanel',
  },
})
export class HlmTabsContentDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly value = input.required<string>();
  public readonly isActive = input<boolean>(false);

  protected _computedClass = computed(() =>
    hlm(tabsContentVariants(), this.userClass())
  );
}

/**
 * Container component for tabs - manages active state
 */
@Component({
  selector: 'hlm-tabs',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
    '[attr.data-orientation]': 'orientation()',
  },
})
export class HlmTabsComponent {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly orientation = input<'horizontal' | 'vertical'>('horizontal');
  public readonly defaultValue = input<string>('');

  public readonly activeTab = signal<string>('');
  public readonly tabChange = output<string>();

  protected _computedClass = computed(() => hlm('w-full', this.userClass()));

  constructor() {
    // Initialize with default value
    if (this.defaultValue()) {
      this.activeTab.set(this.defaultValue());
    }
  }

  setActiveTab(value: string) {
    this.activeTab.set(value);
    this.tabChange.emit(value);
  }
}
