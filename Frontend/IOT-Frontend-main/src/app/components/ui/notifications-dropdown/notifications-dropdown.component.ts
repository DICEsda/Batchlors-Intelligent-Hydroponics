import { Component, inject, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBell,
  lucideCheck,
  lucideCheckCheck,
  lucideX,
  lucideTrash2,
  lucideAlertCircle,
  lucideCheckCircle2,
  lucideInfo,
  lucideAlertTriangle,
  lucideSearch
} from '@ng-icons/lucide';
import { NotificationService, Notification } from '../../../core/services/notification.service';
import { HlmButtonDirective } from '../button';
import { HlmBadgeDirective } from '../badge';
import { HlmIconDirective } from '../icon';

@Component({
  selector: 'app-notifications-dropdown',
  standalone: true,
  imports: [CommonModule, NgIcon, HlmButtonDirective, HlmBadgeDirective, HlmIconDirective],
  providers: [
    provideIcons({
      lucideBell,
      lucideCheck,
      lucideCheckCheck,
      lucideX,
      lucideTrash2,
      lucideAlertCircle,
      lucideCheckCircle2,
      lucideInfo,
      lucideAlertTriangle,
      lucideSearch
    })
  ],
  template: `
    <div class="notifications-container">
      <button 
        hlmBtn
        variant="ghost"
        size="icon"
        class="notifications-trigger"
        (click)="toggleDropdown()"
        [attr.aria-expanded]="isOpen()"
        aria-label="Notifications"
      >
        <ng-icon hlmIcon name="lucideBell" size="sm" />
        @if (notificationService.unreadCount() > 0) {
          <span class="notification-badge">
            {{ notificationService.unreadCount() > 9 ? '9+' : notificationService.unreadCount() }}
          </span>
        }
      </button>

      @if (isOpen()) {
        <div class="notifications-dropdown" (click)="$event.stopPropagation()">
          <div class="dropdown-header">
            <h3 class="dropdown-title">Notifications</h3>
            @if (notificationService.unreadCount() > 0) {
              <button 
                hlmBtn
                variant="ghost"
                size="sm"
                (click)="markAllAsRead()"
                class="mark-all-read"
              >
                <ng-icon hlmIcon name="lucideCheckCheck" size="xs" />
                Mark all read
              </button>
            }
          </div>

          <div class="notifications-list">
            @if (notificationService.allNotifications().length === 0) {
              <div class="empty-state">
                <ng-icon name="lucideBell" size="32" class="empty-icon" />
                <p class="empty-text">No notifications</p>
                <p class="empty-subtext">You're all caught up!</p>
              </div>
            } @else {
              @for (notification of notificationService.allNotifications(); track notification.id) {
                <div 
                  class="notification-item"
                  [class.unread]="!notification.read"
                  [class]="'type-' + notification.type"
                  (click)="markAsRead(notification.id)"
                >
                  <div class="notification-icon">
                    <ng-icon [name]="getIconForType(notification.type)" size="16" />
                  </div>
                  
                  <div class="notification-content">
                    <div class="notification-header">
                      <h4 class="notification-title">{{ notification.title }}</h4>
                      <span class="notification-time">{{ formatTimestamp(notification.timestamp) }}</span>
                    </div>
                    <p class="notification-message">{{ notification.message }}</p>
                    
                    @if (notification.actionLabel && notification.actionCallback) {
                      <button 
                        hlmBtn
                        variant="outline"
                        size="sm"
                        class="notification-action"
                        (click)="handleAction(notification); $event.stopPropagation()"
                      >
                        {{ notification.actionLabel }}
                      </button>
                    }
                  </div>

                  <button 
                    hlmBtn
                    variant="ghost"
                    size="icon"
                    class="notification-delete"
                    (click)="deleteNotification(notification.id); $event.stopPropagation()"
                    aria-label="Delete notification"
                  >
                    <ng-icon hlmIcon name="lucideX" size="xs" />
                  </button>
                </div>
              }
            }
          </div>

          @if (notificationService.allNotifications().length > 0) {
            <div class="dropdown-footer">
              <button 
                hlmBtn
                variant="ghost"
                size="sm"
                (click)="clearAll()"
                class="clear-all"
              >
                <ng-icon hlmIcon name="lucideTrash2" size="xs" />
                Clear all
              </button>
              <button 
                hlmBtn
                variant="ghost"
                size="sm"
                (click)="viewAll()"
                class="view-all"
              >
                View all notifications
              </button>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .notifications-container {
      position: relative;
    }

    .notifications-trigger {
      position: relative;
    }

    .notification-badge {
      position: absolute;
      top: -4px;
      right: -4px;
      background: var(--destructive);
      color: var(--destructive-foreground);
      font-size: 0.625rem;
      font-weight: 600;
      line-height: 1;
      padding: 0.125rem 0.375rem;
      border-radius: 9999px;
      min-width: 18px;
      text-align: center;
      animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
    }

    .notifications-dropdown {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: 380px;
      max-height: 500px;
      background: var(--popover);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      box-shadow: 0 10px 40px oklch(0 0 0 / 0.15);
      display: flex;
      flex-direction: column;
      z-index: 50;
      animation: slideInFromTop 0.2s var(--ease-out-expo);
    }

    @keyframes slideInFromTop {
      from {
        opacity: 0;
        transform: translateY(-8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .dropdown-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--spacing-md) var(--spacing-lg);
      border-bottom: 1px solid var(--border);
    }

    .dropdown-title {
      font-size: var(--text-base);
      font-weight: 600;
      margin: 0;
      color: var(--foreground);
    }

    .mark-all-read {
      gap: 0.375rem;
      color: var(--muted-foreground);
      
      &:hover {
        color: var(--foreground);
      }
    }

    .notifications-list {
      flex: 1;
      overflow-y: auto;
      min-height: 200px;
      max-height: 400px;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-3xl) var(--spacing-xl);
      text-align: center;
    }

    .empty-icon {
      color: var(--muted-foreground);
      margin-bottom: var(--spacing-md);
    }

    .empty-text {
      font-size: var(--text-base);
      font-weight: 500;
      color: var(--foreground);
      margin: 0 0 var(--spacing-xs);
    }

    .empty-subtext {
      font-size: var(--text-sm);
      color: var(--muted-foreground);
      margin: 0;
    }

    .notification-item {
      display: flex;
      gap: var(--spacing-md);
      padding: var(--spacing-md) var(--spacing-lg);
      border-bottom: 1px solid var(--border);
      transition: background-color var(--duration-fast) ease;
      cursor: pointer;

      &:hover {
        background: var(--accent);
      }

      &:last-child {
        border-bottom: none;
      }

      &.unread {
        background: oklch(from var(--primary) l c h / 0.05);
        
        &::before {
          content: '';
          position: absolute;
          left: 0;
          top: 0;
          bottom: 0;
          width: 3px;
          background: var(--primary);
        }
      }
    }

    .notification-icon {
      flex-shrink: 0;
      width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      
      .type-info & {
        background: oklch(from var(--primary) l c h / 0.15);
        color: var(--primary);
      }
      
      .type-success & {
        background: oklch(from var(--status-online) l c h / 0.15);
        color: var(--status-online);
      }
      
      .type-warning & {
        background: oklch(from var(--status-warning) l c h / 0.15);
        color: var(--status-warning);
      }
      
      .type-error & {
        background: oklch(from var(--destructive) l c h / 0.15);
        color: var(--destructive);
      }
      
      .type-discovery & {
        background: oklch(from var(--status-pairing) l c h / 0.15);
        color: var(--status-pairing);
      }
    }

    .notification-content {
      flex: 1;
      min-width: 0;
    }

    .notification-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--spacing-sm);
      margin-bottom: var(--spacing-xs);
    }

    .notification-title {
      font-size: var(--text-sm);
      font-weight: 600;
      color: var(--foreground);
      margin: 0;
      line-height: 1.4;
    }

    .notification-time {
      font-size: var(--text-xs);
      color: var(--muted-foreground);
      white-space: nowrap;
      flex-shrink: 0;
    }

    .notification-message {
      font-size: var(--text-sm);
      color: var(--muted-foreground);
      margin: 0 0 var(--spacing-sm);
      line-height: 1.5;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .notification-action {
      margin-top: var(--spacing-xs);
    }

    .notification-delete {
      flex-shrink: 0;
      opacity: 0;
      transition: opacity var(--duration-fast) ease;
      
      .notification-item:hover & {
        opacity: 1;
      }
    }

    .dropdown-footer {
      display: flex;
      gap: var(--spacing-sm);
      padding: var(--spacing-md) var(--spacing-lg);
      border-top: 1px solid var(--border);
    }

    .clear-all,
    .view-all {
      gap: 0.375rem;
      font-size: var(--text-sm);
    }

    @keyframes pulse {
      0%, 100% {
        opacity: 1;
        transform: scale(1);
      }
      50% {
        opacity: 0.8;
        transform: scale(1.05);
      }
    }
  `]
})
export class NotificationsDropdownComponent {
  readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);
  
  isOpen = signal(false);

  toggleDropdown(): void {
    this.isOpen.update(v => !v);
  }

  @HostListener('document:click')
  closeDropdown(): void {
    this.isOpen.set(false);
  }

  markAsRead(id: string): void {
    this.notificationService.markAsRead(id);
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead();
  }

  deleteNotification(id: string): void {
    this.notificationService.deleteNotification(id);
  }

  clearAll(): void {
    this.notificationService.clearAll();
    this.isOpen.set(false);
  }

  viewAll(): void {
    this.router.navigate(['/alerts']);
    this.isOpen.set(false);
  }

  handleAction(notification: Notification): void {
    if (notification.actionCallback) {
      notification.actionCallback();
      this.isOpen.set(false);
    }
  }

  getIconForType(type: Notification['type']): string {
    const icons: Record<Notification['type'], string> = {
      info: 'lucideInfo',
      success: 'lucideCheckCircle2',
      warning: 'lucideAlertTriangle',
      error: 'lucideAlertCircle',
      discovery: 'lucideSearch'
    };
    return icons[type];
  }

  formatTimestamp(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (seconds < 60) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    if (days < 7) return `${days}d ago`;
    
    return date.toLocaleDateString();
  }
}
