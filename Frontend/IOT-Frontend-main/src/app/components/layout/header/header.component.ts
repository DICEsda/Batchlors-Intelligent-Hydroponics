import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideSearch,
  lucideChevronRight,
  lucidePanelLeft
} from '@ng-icons/lucide';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, NgIcon],
  providers: [
    provideIcons({
      lucideSearch,
      lucideChevronRight,
      lucidePanelLeft
    })
  ],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  @Input() breadcrumbs: { label: string; route?: string }[] = [
    { label: 'Hydroponic Farm' },
    { label: 'Overview' }
  ];

  searchQuery = '';
}
