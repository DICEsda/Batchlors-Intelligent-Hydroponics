import { Component } from '@angular/core';
import { MainLayoutComponent } from './components/layout';
import { ConfirmDialogComponent } from './components/ui/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-root',
  imports: [MainLayoutComponent, ConfirmDialogComponent],
  template: `
    <app-main-layout />
    <app-confirm-dialog />
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
    }
  `]
})
export class AppComponent {
  title = 'HydroFarm Dashboard';
}
