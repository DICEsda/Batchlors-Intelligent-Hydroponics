import { Component, computed, Directive, input, output, signal, inject, TemplateRef, ViewContainerRef } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { hlm } from '../utils';

// Dialog Overlay
export const dialogOverlayVariants = cva(
  'fixed inset-0 z-50 bg-black/80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0'
);

// Dialog Content
export const dialogContentVariants = cva(
  'fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border bg-background p-6 shadow-lg duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%] sm:rounded-lg',
  {
    variants: {
      size: {
        default: 'max-w-lg',
        sm: 'max-w-md',
        lg: 'max-w-xl',
        xl: 'max-w-2xl',
        full: 'max-w-[calc(100vw-2rem)] max-h-[calc(100vh-2rem)]',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  }
);

export type DialogContentVariants = VariantProps<typeof dialogContentVariants>;

@Directive({
  selector: '[hlmDialogOverlay]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    '[attr.data-state]': 'state()',
  },
})
export class HlmDialogOverlayDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly state = input<'open' | 'closed'>('open');

  protected _computedClass = computed(() =>
    hlm(dialogOverlayVariants(), this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogContent]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    '[attr.data-state]': 'state()',
    role: 'dialog',
    'aria-modal': 'true',
  },
})
export class HlmDialogContentDirective {
  public readonly userClass = input<string>('', { alias: 'class' });
  public readonly size = input<DialogContentVariants['size']>('default');
  public readonly state = input<'open' | 'closed'>('open');

  protected _computedClass = computed(() =>
    hlm(dialogContentVariants({ size: this.size() }), this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogHeader]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmDialogHeaderDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('flex flex-col space-y-1.5 text-center sm:text-left', this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogTitle]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmDialogTitleDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('text-lg font-semibold leading-none tracking-tight', this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogDescription]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmDialogDescriptionDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('text-sm text-muted-foreground', this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogFooter]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmDialogFooterDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2', this.userClass())
  );
}

@Directive({
  selector: '[hlmDialogClose]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
    type: 'button',
  },
})
export class HlmDialogCloseDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm(
      'absolute right-4 top-4 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:pointer-events-none data-[state=open]:bg-accent data-[state=open]:text-muted-foreground',
      this.userClass()
    )
  );
}

/**
 * Simple Dialog Component
 * For full dialog functionality with portal support, use @spartan-ng/ui-dialog-brain
 */
@Component({
  selector: 'hlm-dialog',
  standalone: true,
  imports: [
    HlmDialogOverlayDirective,
    HlmDialogContentDirective,
  ],
  template: `
    @if (isOpen()) {
      <div hlmDialogOverlay (click)="onOverlayClick()"></div>
      <div hlmDialogContent [size]="size()">
        <ng-content />
      </div>
    }
  `,
})
export class HlmDialogComponent {
  public readonly isOpen = input<boolean>(false);
  public readonly size = input<DialogContentVariants['size']>('default');
  public readonly closeOnOverlay = input<boolean>(true);

  public readonly closed = output<void>();

  onOverlayClick() {
    if (this.closeOnOverlay()) {
      this.closed.emit();
    }
  }
}
