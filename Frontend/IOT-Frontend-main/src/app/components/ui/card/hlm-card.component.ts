import { Component, computed, input } from '@angular/core';
import { hlm } from '../utils';

@Component({
  selector: 'hlm-card, [hlmCard]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm(
      'rounded-lg border border-border bg-card text-card-foreground shadow-sm',
      this.userClass()
    )
  );
}

@Component({
  selector: 'hlm-card-header, [hlmCardHeader]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardHeaderComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('flex flex-col space-y-1.5 p-6', this.userClass())
  );
}

@Component({
  selector: 'hlm-card-title, [hlmCardTitle]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardTitleComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('text-lg font-semibold leading-none tracking-tight', this.userClass())
  );
}

@Component({
  selector: 'hlm-card-description, [hlmCardDescription]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardDescriptionComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('text-sm text-muted-foreground', this.userClass())
  );
}

@Component({
  selector: 'hlm-card-content, [hlmCardContent]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardContentComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('p-6 pt-0', this.userClass())
  );
}

@Component({
  selector: 'hlm-card-footer, [hlmCardFooter]',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCardFooterComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('flex items-center p-6 pt-0', this.userClass())
  );
}

// Directive aliases for backward compatibility (pages import as Directive)
export const HlmCardDirective = HlmCardComponent;
export const HlmCardHeaderDirective = HlmCardHeaderComponent;
export const HlmCardTitleDirective = HlmCardTitleComponent;
export const HlmCardDescriptionDirective = HlmCardDescriptionComponent;
export const HlmCardContentDirective = HlmCardContentComponent;
export const HlmCardFooterDirective = HlmCardFooterComponent;
