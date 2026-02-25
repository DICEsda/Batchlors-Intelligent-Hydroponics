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
  lucideBeaker,
  lucideBrain,
  lucideEgg,
  lucideLightbulb,
  lucideCpu,
  lucideGrid3x3,
  lucideDownload,
  lucideActivity,
  lucideBarChart3,
  lucideThermometer
} from '@ng-icons/lucide';
import { WebSocketService } from '../../../core/services/websocket.service';
import { IoTDataService } from '../../../core/services/iot-data.service';
import { SidebarService } from '../../../core/services/sidebar.service';
import { ToastService } from '../../../core/services/toast.service';
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
      lucideBeaker,
      lucideBrain,
      lucideEgg,
      lucideLightbulb,
      lucideCpu,
      lucideGrid3x3,
      lucideDownload,
      lucideActivity,
      lucideBarChart3,
      lucideThermometer
    })
  ],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit, OnDestroy {
  private readonly wsService = inject(WebSocketService);
  private readonly dataService = inject(IoTDataService);
  private readonly sidebarService = inject(SidebarService);
  private readonly toastService = inject(ToastService);
  private readonly destroy$ = new Subject<void>();

  // Expose collapsed state from service
  readonly collapsed = this.sidebarService.collapsed;

  // Navigation structure - Dashboard standalone, Farms as main hierarchy
  platformItems = signal<NavItem[]>([
    {
      label: 'Dashboard',
      icon: 'lucideLayoutDashboard',
      route: '/overview'
    },
    {
      label: 'Farms',
      icon: 'lucideBuilding',
      expanded: true,
      children: [
        { label: 'Farms', icon: 'lucideGrid3x3', route: '/farms' },
        { 
          label: 'Reservoirs', 
          icon: 'lucideServer', 
          route: '/reservoirs',
          expanded: true,
          children: [
            { label: 'Logs', icon: 'lucideBell', route: '/logs' }
          ]
        },
        { label: 'Towers', icon: 'lucideLightbulb', route: '/towers' }
      ]
    },
    {
      label: 'OTA Updates',
      icon: 'lucideDownload',
      route: '/ota'
    },
    {
      label: 'Settings',
      icon: 'lucideSettings',
      route: '/settings'
    }
  ]);

  // Projects section - Digital Twin (with Diagnostics), Machine Learning
  projects = signal<NavItem[]>([
    {
      label: 'Digital Twin',
      icon: 'lucideBeaker',
      route: '/digital-twin',
      expanded: true,
      children: [
        {
          label: 'Diagnostics',
          icon: 'lucideActivity',
          expanded: true,
          children: [
            { label: 'System', icon: 'lucideActivity', route: '/digital-twin/diagnostics/system' },
            { label: 'Sensors', icon: 'lucideThermometer', route: '/digital-twin/diagnostics/sensors' },
            { label: 'Scale Test', icon: 'lucideBarChart3', route: '/digital-twin/diagnostics/scale-test' }
          ]
        }
      ]
    },
    { label: 'Machine Learning', icon: 'lucideBrain', route: '/machine-learning' }
  ]);

  // System status signals
  readonly wsConnected = this.wsService.connected;
  readonly backendConnected = signal<boolean>(false);
  
  // Track previous states to detect changes
  private previousWsConnected = false;
  private previousBackendConnected = false;
  
  // Device counts from data service
  readonly onlineCoordinators = this.dataService.onlineCoordinatorCount;
  readonly totalCoordinators = this.dataService.totalCoordinatorCount;
  readonly onlineNodes = this.dataService.onlineNodeCount;
  readonly totalNodes = this.dataService.totalNodeCount;

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

  // Hover popup state for collapsed sidebar
  hoveredItem = signal<NavItem | ProjectItem | null>(null);
  popupPosition = signal<{ top: number } | null>(null);
  hoverTimeout: any = null;

  ngOnInit(): void {
    // Restore sidebar state from localStorage
    this.sidebarService.restoreState();
    
    // Initialize previous connection states
    this.previousWsConnected = this.wsConnected();
    this.previousBackendConnected = this.backendConnected();
    
    // Monitor WebSocket connection changes
    interval(1000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.checkConnectionStatus());
    
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
    // Don't expand/collapse when sidebar is collapsed - use popup instead
    if (this.collapsed()) {
      return;
    }
    
    if (item.children) {
      item.expanded = !item.expanded;
      // Trigger signal update for whichever section owns the item
      this.platformItems.update(items => [...items]);
      this.projects.update(items => [...items]);
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

  /**
   * Show hover popup for collapsed sidebar
   */
  onNavItemHover(item: NavItem | ProjectItem, event?: MouseEvent): void {
    // Only show popup when sidebar is collapsed
    if (!this.collapsed()) {
      return;
    }
    
    // Clear any existing timeout
    if (this.hoverTimeout) {
      clearTimeout(this.hoverTimeout);
    }
    
    // Calculate popup position IMMEDIATELY (before timeout) to avoid event becoming stale
    let calculatedTop = 100; // Fallback
    if (event) {
      const target = event.currentTarget as HTMLElement;
      if (target) {
        const rect = target.getBoundingClientRect();
        // Center the popup vertically relative to the icon
        calculatedTop = rect.top + (rect.height / 2);
      }
    }
    
    // Show popup after short delay (150ms) to prevent flickering
    this.hoverTimeout = setTimeout(() => {
      // Set position first
      this.popupPosition.set({ top: calculatedTop });
      // Then set hovered item to trigger popup display
      this.hoveredItem.set(item);
    }, 150);
  }

  /**
   * Hide hover popup (with delay to allow moving to popup)
   */
  onNavItemLeave(): void {
    // Clear timeout to prevent popup from showing
    if (this.hoverTimeout) {
      clearTimeout(this.hoverTimeout);
      this.hoverTimeout = null;
    }
    
    // Delay hiding to allow mouse to move to popup
    this.hoverTimeout = setTimeout(() => {
      this.hoveredItem.set(null);
      this.popupPosition.set(null);
    }, 200); // 200ms delay to allow moving to popup
  }
  
  /**
   * Keep popup visible when hovering over it
   */
  onPopupEnter(): void {
    // Cancel any pending hide timeout
    if (this.hoverTimeout) {
      clearTimeout(this.hoverTimeout);
      this.hoverTimeout = null;
    }
  }
  
  /**
   * Hide popup when leaving it
   */
  onPopupLeave(): void {
    // Hide immediately when leaving popup
    this.hoveredItem.set(null);
    this.popupPosition.set(null);
  }

  /**
   * Check if item has children
   */
  hasChildren(item: any): boolean {
    return !!(item as NavItem).children && (item as NavItem).children!.length > 0;
  }

  /**
   * Get children of a nav item (safely cast)
   */
  getChildren(item: NavItem | ProjectItem): NavItem[] {
    return (item as NavItem).children || [];
  }

  private async checkBackendHealth(): Promise<void> {
    try {
      await this.dataService.checkHealth();
      const wasConnected = this.backendConnected();
      this.backendConnected.set(true);
      
      // Show toast only on reconnection (was disconnected, now connected)
      if (!wasConnected && this.previousBackendConnected === false) {
        this.toastService.success(
          'Backend Connected',
          'Successfully connected to the IoT backend server.'
        );
      }
      this.previousBackendConnected = true;
    } catch {
      const wasConnected = this.backendConnected();
      this.backendConnected.set(false);
      
      // Show toast only on first disconnect
      if (wasConnected && this.previousBackendConnected === true) {
        this.toastService.error(
          'Backend Disconnected',
          'Lost connection to the IoT backend server. Retrying...',
          10000
        );
      }
      this.previousBackendConnected = false;
    }
  }
  
  private checkConnectionStatus(): void {
    const currentWsConnected = this.wsConnected();
    
    // WebSocket connection changed
    if (currentWsConnected !== this.previousWsConnected) {
      if (currentWsConnected) {
        // WebSocket connected
        this.toastService.success(
          'WebSocket Connected',
          'Real-time updates are now active.'
        );
      } else {
        // WebSocket disconnected
        this.toastService.error(
          'WebSocket Disconnected',
          'Real-time updates are unavailable. Attempting to reconnect...',
          10000
        );
      }
      this.previousWsConnected = currentWsConnected;
    }
  }
}
