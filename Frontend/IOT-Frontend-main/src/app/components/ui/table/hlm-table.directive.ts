import { Component, computed, Directive, input } from '@angular/core';
import { hlm } from '../utils';

@Directive({
  selector: 'table[hlmTable]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTableDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('w-full caption-bottom text-sm', this.userClass())
  );
}

@Component({
  selector: 'hlm-table-wrapper',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTableWrapperComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('relative w-full overflow-auto', this.userClass())
  );
}

@Directive({
  selector: 'thead[hlmThead]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTheadDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('[&_tr]:border-b', this.userClass())
  );
}

@Directive({
  selector: 'tbody[hlmTbody]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTbodyDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('[&_tr:last-child]:border-0', this.userClass())
  );
}

@Directive({
  selector: 'tr[hlmTr]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTrDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm(
      'border-b border-border transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted',
      this.userClass()
    )
  );
}

@Directive({
  selector: 'th[hlmTh]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmThDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm(
      'h-12 px-4 text-left align-middle font-medium text-muted-foreground [&:has([role=checkbox])]:pr-0',
      this.userClass()
    )
  );
}

@Directive({
  selector: 'td[hlmTd]',
  standalone: true,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmTdDirective {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('p-4 align-middle [&:has([role=checkbox])]:pr-0', this.userClass())
  );
}

@Component({
  selector: 'hlm-caption',
  standalone: true,
  template: `<ng-content />`,
  host: {
    '[class]': '_computedClass()',
  },
})
export class HlmCaptionComponent {
  public readonly userClass = input<string>('', { alias: 'class' });

  protected _computedClass = computed(() =>
    hlm('mt-4 text-sm text-muted-foreground', this.userClass())
  );
}
