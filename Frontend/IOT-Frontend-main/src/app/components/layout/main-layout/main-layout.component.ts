import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';

interface Breadcrumb {
  label: string;
  route?: string;
}

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent, HeaderComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit {
  private readonly router = inject(Router);
  
  breadcrumbs: Breadcrumb[] = [
    { label: 'Hydroponic Farm' },
    { label: 'Dashboard' }
  ];

  // Route to breadcrumb mapping
  private readonly routeBreadcrumbs: Record<string, Breadcrumb[]> = {
    '/overview': [
      { label: 'Hydroponic Farm' },
      { label: 'Dashboard' }
    ],
    '/farms': [
      { label: 'Hydroponic Farm' },
      { label: 'Farms', route: '/farms' }
    ],
    '/reservoirs': [
      { label: 'Hydroponic Farm' },
      { label: 'Farms', route: '/farms' },
      { label: 'Reservoirs' }
    ],
    '/logs': [
      { label: 'Hydroponic Farm' },
      { label: 'Farms', route: '/farms' },
      { label: 'Reservoirs', route: '/reservoirs' },
      { label: 'Logs' }
    ],
    '/towers': [
      { label: 'Hydroponic Farm' },
      { label: 'Farms', route: '/farms' },
      { label: 'Towers' }
    ],
    '/ota': [
      { label: 'Hydroponic Farm' },
      { label: 'OTA Updates' }
    ],
    '/settings': [
      { label: 'Hydroponic Farm' },
      { label: 'Settings' }
    ],
    '/digital-twin': [
      { label: 'Hydroponic Farm' },
      { label: 'Projects', route: '/digital-twin' },
      { label: 'Digital Twin' }
    ],
    '/radar': [
      { label: 'Hydroponic Farm' },
      { label: 'Projects', route: '/radar' },
      { label: 'Radar View' }
    ],
    '/machine-learning': [
      { label: 'Hydroponic Farm' },
      { label: 'Projects', route: '/machine-learning' },
      { label: 'Machine Learning' }
    ]
  };

  ngOnInit(): void {
    // Set initial breadcrumbs
    this.updateBreadcrumbs(this.router.url);

    // Listen to route changes
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.updateBreadcrumbs(event.urlAfterRedirects);
      });
  }

  private updateBreadcrumbs(url: string): void {
    // Remove query parameters
    const path = url.split('?')[0];
    
    // Check for detail routes (e.g., /reservoirs/123, /towers/456)
    if (path.startsWith('/reservoirs/')) {
      this.breadcrumbs = [
        { label: 'Hydroponic Farm' },
        { label: 'Farms', route: '/farms' },
        { label: 'Reservoirs', route: '/reservoirs' },
        { label: 'Coordinator Detail' }
      ];
    } else if (path.startsWith('/towers/')) {
      this.breadcrumbs = [
        { label: 'Hydroponic Farm' },
        { label: 'Farms', route: '/farms' },
        { label: 'Towers', route: '/towers' },
        { label: 'Tower Detail' }
      ];
    } else if (path.startsWith('/farms/')) {
      this.breadcrumbs = [
        { label: 'Hydroponic Farm' },
        { label: 'Farms', route: '/farms' },
        { label: 'Farm Detail' }
      ];
    } else {
      // Use predefined breadcrumbs
      this.breadcrumbs = this.routeBreadcrumbs[path] || [
        { label: 'Hydroponic Farm' },
        { label: 'Dashboard' }
      ];
    }
  }
}
