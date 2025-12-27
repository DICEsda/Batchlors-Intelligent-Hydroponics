import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideLayoutDashboard,
  lucideSettings,
  lucideBox,
  lucideChevronDown,
  lucideChevronRight,
  lucideFlower2,
  lucideServer,
  lucideBell,
  lucideCommand,
  lucideCircle,
  lucideWifi,
  lucideDatabase,
  lucideUsers,
  lucideShield,
  lucideUser,
  lucideBuilding,
  lucideBeaker
} from '@ng-icons/lucide';
import { WebSocketService } from '../../../core/services/websocket.service';
import { HydroponicDataService } from '../../../core/services/hydroponic-data.service';
import { Subject, interval, takeUntil } from 'rxjs';

interface NavItem {
  label: string;
  icon: string;
  route?: string;
  children?: NavItem[];
  expanded?: boolean;
}

interface ProjectItem {
  label: string;
  icon: string;
  route: string;
}

type UserRole = 'admin' | 'user';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, NgIcon],
  providers: [
    provideIcons({
      lucideLayoutDashboard,
      lucideSettings,
      lucideBox,
      lucideChevronDown,
      lucideChevronRight,
      lucideFlower2,
      lucideServer,
      lucideBell,
      lucideCommand,
      lucideCircle,
      lucideWifi,
      lucideDatabase,
      lucideUsers,
      lucideShield,
      lucideUser,
      lucideBuilding,
      lucideBeaker
    })
  ],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit, OnDestroy {
  private readonly wsService = inject(WebSocketService);
  private readonly dataService = inject(HydroponicDataService);
  private readonly destroy$ = new Subject<void>();

  // Navigation structure - Dashboard standalone, Devices with Coordinators/Nodes
  platformItems = signal<NavItem[]>([
    {
      label: 'Dashboard',
      icon: 'lucideLayoutDashboard',
      route: '/overview'
    },
    {
      label: 'Devices',
      icon: 'lucideBox',
      expanded: true,
      children: [
        { label: 'Coordinators', icon: 'lucideServer', route: '/coordinators' },
        { label: 'Nodes', icon: 'lucideFlower2', route: '/nodes' }
      ]
    },
    {
      label: 'Alerts',
      icon: 'lucideBell',
      route: '/alerts'
    },
    {
      label: 'Settings',
      icon: 'lucideSettings',
      route: '/settings'
    }
  ]);

  // Projects section - Greenhouses and Hydroponics Lab
  projects = signal<ProjectItem[]>([
    { label: 'Greenhouses', icon: 'lucideBuilding', route: '/greenhouses' },
    { label: 'Hydroponics Lab', icon: 'lucideBeaker', route: '/digital-twin' }
  ]);

  // System status signals
  readonly wsConnected = this.wsService.connected;
  readonly backendConnected = signal<boolean>(false);
  
  // Device counts from data service
  readonly onlineCoordinators = this.dataService.onlineCoordinatorCount;
  readonly totalCoordinators = this.dataService.totalCoordinatorCount;
  readonly onlineNodes = this.dataService.onlineTowerCount;
  readonly totalNodes = this.dataService.totalTowerCount;

  // User role management
  currentRole = signal<UserRole>('admin');
  roleDropdownOpen = signal(false);

  readonly roleOptions: { value: UserRole; label: string; icon: string }[] = [
    { value: 'admin', label: 'Admin', icon: 'lucideShield' },
    { value: 'user', label: 'User', icon: 'lucideUser' }
  ];

  // Computed for display
  readonly currentRoleDisplay = computed(() => {
    const role = this.currentRole();
    return this.roleOptions.find(r => r.value === role);
  });

  ngOnInit(): void {
    // Check backend health periodically
    this.checkBackendHealth();
    interval(30000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.checkBackendHealth());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  toggleExpand(item: NavItem): void {
    if (item.children) {
      item.expanded = !item.expanded;
      // Trigger signal update
      this.platformItems.update(items => [...items]);
    }
  }

  toggleRoleDropdown(): void {
    this.roleDropdownOpen.update(open => !open);
  }

  selectRole(role: UserRole): void {
    this.currentRole.set(role);
    this.roleDropdownOpen.set(false);
    // Here you could emit an event or call a service to apply role-based permissions
    console.log('Role switched to:', role);
  }

  closeDropdown(): void {
    this.roleDropdownOpen.set(false);
  }

  private async checkBackendHealth(): Promise<void> {
    try {
      await this.dataService.checkHealth();
      this.backendConnected.set(true);
    } catch {
      this.backendConnected.set(false);
    }
  }
}
