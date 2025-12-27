import { Component } from '@angular/core';
import { MainLayoutComponent } from './components/layout';

@Component({
  selector: 'app-root',
  imports: [MainLayoutComponent],
  template: '<app-main-layout />',
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
