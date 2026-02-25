import { Component, Input, inject, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import {
  lucideSearch,
  lucideChevronRight,
  lucidePanelLeft,
  lucideSun,
  lucideMoon,
  lucideMonitor,
  lucideActivity,
  lucideWifi,
  lucideWifiOff,
  lucideRefreshCw,
  lucideRadioTower,
  lucideX,
  lucideCheck,
  lucideAlertTriangle
} from '@ng-icons/lucide';

import { ThemeService, Theme } from '../../../core/services/theme.service';
import { WebSocketService, IoTDataService, ApiService } from '../../../core/services';
import { ToastService } from '../../../core/services/toast.service';
import { NotificationService } from '../../../core/services/notification.service';
import { NotificationListenerService } from '../../../core/services/notification-listener.service';
import { SidebarService } from '../../../core/services/sidebar.service';
import { WSCoordinatorRegistrationPayload, ApproveCoordinatorRequest } from '../../../core/models';
import { HlmBadgeDirective } from '../../ui/badge';
import { HlmButtonDirective } from '../../ui/button';
import { HlmIconDirective } from '../../ui/icon';
import {
  HlmDialogComponent,
  HlmDialogHeaderDirective,
  HlmDialogTitleDirective,
  HlmDialogDescriptionDirective,
  HlmDialogFooterDirective,
  HlmDialogCloseDirective
} from '../../ui/dialog';
import { HlmInputDirective } from '../../ui/input';
import { HlmLabelDirective } from '../../ui/label';
import { NotificationsDropdownComponent } from '../../ui/notifications-dropdown/notifications-dropdown.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    NgIcon, 
    HlmBadgeDirective, 
    HlmButtonDirective, 
    HlmIconDirective,
    HlmDialogComponent,
    HlmDialogHeaderDirective,
    HlmDialogTitleDirective,
    HlmDialogDescriptionDirective,
    HlmDialogFooterDirective,
    HlmDialogCloseDirective,
    HlmInputDirective,
    HlmLabelDirective,
    NotificationsDropdownComponent
  ],
  providers: [
    provideIcons({
      lucideSearch,
      lucideChevronRight,
      lucidePanelLeft,
      lucideSun,
      lucideMoon,
      lucideMonitor,
      lucideActivity,
      lucideWifi,
      lucideWifiOff,
      lucideRefreshCw,
      lucideRadioTower,
      lucideX,
      lucideCheck,
      lucideAlertTriangle
    })
  ],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent implements OnDestroy {
  @Input() breadcrumbs: { label: string; route?: string }[] = [
    { label: 'Hydroponic Farm' },
    { label: 'Overview' }
  ];

  searchQuery = '';
  private searchSubject = new Subject<string>();
  private subscriptions: Subscription[] = [];
  
  readonly themeService = inject(ThemeService);
  readonly wsService = inject(WebSocketService);
  readonly dataService = inject(IoTDataService);
  readonly apiService = inject(ApiService);
  readonly toastService = inject(ToastService);
  readonly notificationService = inject(NotificationService);
  readonly notificationListener = inject(NotificationListenerService);
  readonly sidebarService = inject(SidebarService);


  readonly wsConnected = this.wsService.connected;

  // Coordinator registration state
  readonly pendingCoordinatorRegistrations = signal<WSCoordinatorRegistrationPayload[]>([]);
  readonly showRegistrationDialog = signal<boolean>(false);
  readonly selectedRegistration = signal<WSCoordinatorRegistrationPayload | null>(null);

  // Registration form fields
  readonly regFormName = signal<string>('');
  readonly regFormDescription = signal<string>('');
  readonly regFormColor = signal<string>('#3b82f6');
  readonly regFormLocation = signal<string>('');
  readonly regFormFarmId = signal<string>('');
  readonly regFormTags = signal<string>('');
  readonly isSubmitting = signal<boolean>(false);
  
  constructor() {
    // Setup debounced search
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(query => {
      this.performSearch(query);
    });

    // Register this component as the dialog opener for coordinator registration.
    // The NotificationListenerService fires the urgent toast; its "Register" button
    // calls back into openRegistrationDialog() via this handler.
    this.notificationListener.setRegistrationHandler((reg) => {
      this.openRegistrationDialog(reg);
    });

    // Still need to track pending registrations for the banner display
    this.subscriptions.push(
      this.wsService.coordinatorRegistration$.subscribe(registration => {
        this.pendingCoordinatorRegistrations.update(regs => {
          if (regs.some(r => r.coordId === registration.coordId)) return regs;
          return [...regs, registration];
        });
        // Urgent toast + notification center are handled by NotificationListenerService
      })
    );

    // Remove from pending when a coordinator is approved
    this.subscriptions.push(
      this.wsService.coordinatorRegistered$.subscribe(registered => {
        this.pendingCoordinatorRegistrations.update(regs =>
          regs.filter(r => r.coordId !== registered.coordId)
        );

        // Close dialog if it was for this coordinator
        if (this.selectedRegistration()?.coordId === registered.coordId) {
          this.closeRegistrationDialog();
        }
        // Success toast is handled by NotificationListenerService
      })
    );
  }

  ngOnDestroy(): void {
    this.notificationListener.clearRegistrationHandler();
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }
  
  onSearchInput(value: string) {
    this.searchSubject.next(value);
  }
  
  private performSearch(query: string) {
    // TODO: Implement actual search functionality
    if (query.length > 0) {
      console.log('Searching for:', query);
      // Future: Navigate to search results or filter current view
    }
  }

  get themeIcon(): string {
    const theme = this.themeService.theme();
    if (theme === 'system') return 'lucideMonitor';
    return this.themeService.isDark() ? 'lucideMoon' : 'lucideSun';
  }

  get themeTooltip(): string {
    const theme = this.themeService.theme();
    if (theme === 'system') return 'System theme';
    return this.themeService.isDark() ? 'Dark mode' : 'Light mode';
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  cycleTheme() {
    this.themeService.cycleTheme();
  }

  refreshData() {
    this.dataService.loadDashboardData();
  }

  toggleSidebar() {
    this.sidebarService.toggle();
  }

  // ============================================================================
  // Coordinator Registration Methods
  // ============================================================================

  openRegistrationDialog(registration: WSCoordinatorRegistrationPayload): void {
    this.selectedRegistration.set(registration);
    this.regFormName.set(`Coordinator ${registration.coordId.slice(-4).toUpperCase()}`);
    this.regFormDescription.set('');
    this.regFormColor.set('#3b82f6');
    this.regFormLocation.set('');
    this.regFormFarmId.set('');
    this.regFormTags.set('');
    this.showRegistrationDialog.set(true);
  }

  closeRegistrationDialog(): void {
    this.showRegistrationDialog.set(false);
    this.selectedRegistration.set(null);
    this.isSubmitting.set(false);
  }

  approveCoordinator(): void {
    const reg = this.selectedRegistration();
    if (!reg) return;

    const name = this.regFormName().trim();
    const farmId = this.regFormFarmId().trim();

    if (!name) {
      this.toastService.warning('Validation Error', 'Please enter a name for the coordinator.');
      return;
    }
    if (!farmId) {
      this.toastService.warning('Validation Error', 'Please enter a Farm ID.');
      return;
    }

    this.isSubmitting.set(true);

    const request: ApproveCoordinatorRequest = {
      coordId: reg.coordId,
      farmId: farmId,
      name: name,
      description: this.regFormDescription().trim() || undefined,
      color: this.regFormColor() || undefined,
      location: this.regFormLocation().trim() || undefined,
      tags: this.regFormTags().trim()
        ? this.regFormTags().split(',').map(t => t.trim()).filter(t => t.length > 0)
        : undefined
    };

    this.apiService.approveCoordinatorRegistration(request).subscribe({
      next: () => {
        // Remove from pending list (WS event will also do this, but let's be proactive)
        this.pendingCoordinatorRegistrations.update(regs =>
          regs.filter(r => r.coordId !== reg.coordId)
        );
        this.closeRegistrationDialog();
        this.toastService.success(
          'Coordinator Approved',
          `${request.name} (${request.coordId}) registration has been submitted.`
        );
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toastService.error(
          'Registration Failed',
          `Could not approve coordinator: ${err.message || 'Unknown error'}`
        );
      }
    });
  }

  rejectCoordinator(coordId: string): void {
    this.apiService.rejectCoordinatorRegistration(coordId).subscribe({
      next: () => {
        this.pendingCoordinatorRegistrations.update(regs =>
          regs.filter(r => r.coordId !== coordId)
        );
        // Close dialog if open for this coordinator
        if (this.selectedRegistration()?.coordId === coordId) {
          this.closeRegistrationDialog();
        }
        this.toastService.info(
          'Coordinator Rejected',
          `Coordinator ${coordId} registration was rejected.`
        );
      },
      error: (err) => {
        this.toastService.error(
          'Rejection Failed',
          `Could not reject coordinator: ${err.message || 'Unknown error'}`
        );
      }
    });
  }

  formatRssi(rssi: number): string {
    if (rssi >= -50) return 'Excellent';
    if (rssi >= -60) return 'Good';
    if (rssi >= -70) return 'Fair';
    return 'Weak';
  }

  formatBytes(bytes: number): string {
    if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${bytes} B`;
  }


}
