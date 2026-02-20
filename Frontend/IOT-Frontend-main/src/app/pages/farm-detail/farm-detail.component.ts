import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBuilding,
  lucideServer,
  lucideLightbulb,
  lucideChevronLeft,
  lucideRefreshCw,
  lucidePencil,
  lucideMapPin,
  lucideSettings
} from '@ng-icons/lucide';
import { HlmButtonDirective } from '../../components/ui/button';

@Component({
  selector: 'app-farm-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, NgIcon, HlmButtonDirective],
  providers: [
    provideIcons({
      lucideBuilding,
      lucideServer,
      lucideLightbulb,
      lucideChevronLeft,
      lucideRefreshCw,
      lucidePencil,
      lucideMapPin,
      lucideSettings
    })
  ],
  template: `
    <div class="farm-detail-page">
      <div class="page-header">
        <div class="header-content">
          <a routerLink="/farms" class="back-link">
            <ng-icon name="lucideChevronLeft" size="20" />
          </a>
          <div class="header-text">
            <h1>{{ farmName() }}</h1>
            <p class="header-description">Farm details and management</p>
          </div>
        </div>
        <div class="header-actions">
          <button hlmBtn variant="outline" size="sm">
            <ng-icon name="lucideSettings" size="16" />
            Settings
          </button>
        </div>
      </div>

      <div class="coming-soon">
        <ng-icon name="lucideBuilding" size="64" />
        <h2>Farm Detail View</h2>
        <p>Detailed farm management coming soon. Use the Farms list to view and manage your farms.</p>
        <a routerLink="/farms" hlmBtn variant="default">
          Back to Farms
        </a>
      </div>
    </div>
  `,
  styles: [`
    .farm-detail-page {
      padding: 1.5rem;
      height: 100%;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
    }

    .header-content {
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
    }

    .back-link {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 0.375rem;
      color: var(--muted-foreground);
      text-decoration: none;
      
      &:hover {
        background: var(--secondary);
        color: var(--foreground);
      }
    }

    .header-text h1 {
      font-size: 1.5rem;
      font-weight: 600;
      margin: 0;
      color: var(--foreground);
    }

    .header-description {
      font-size: 0.875rem;
      color: var(--muted-foreground);
      margin: 0.25rem 0 0 0;
    }

    .header-actions {
      display: flex;
      gap: 0.75rem;
    }

    .coming-soon {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 4rem;
      background: var(--card);
      border: 1px solid var(--border);
      border-radius: 0.75rem;
      text-align: center;
      color: var(--muted-foreground);

      h2 {
        margin: 1rem 0 0.5rem;
        color: var(--foreground);
      }

      p {
        margin: 0 0 1.5rem;
        max-width: 400px;
      }
    }
  `]
})
export class FarmDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  
  readonly farmId = signal<string>('');
  readonly farmName = signal<string>('Farm');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.farmId.set(id);
      // In real app, load farm details from service
      this.farmName.set(`Farm ${id}`);
    }
  }
}
