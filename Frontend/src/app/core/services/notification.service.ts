import { Injectable, signal } from '@angular/core';

export interface Notification {
  id: string;
  type: 'info' | 'success' | 'warning' | 'error' | 'discovery';
  title: string;
  message: string;
  timestamp: Date;
  read: boolean;
  actionLabel?: string;
  actionCallback?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notifications = signal<Notification[]>([]);
  private idCounter = 0;

  readonly allNotifications = this.notifications.asReadonly();
  
  readonly unreadCount = () => {
    return this.notifications().filter(n => !n.read).length;
  };

  addNotification(
    type: Notification['type'],
    title: string,
    message: string,
    actionLabel?: string,
    actionCallback?: () => void
  ): void {
    const notification: Notification = {
      id: `notif-${++this.idCounter}-${Date.now()}`,
      type,
      title,
      message,
      timestamp: new Date(),
      read: false,
      actionLabel,
      actionCallback
    };

    this.notifications.update(notifications => [notification, ...notifications]);

    // Keep only last 50 notifications
    if (this.notifications().length > 50) {
      this.notifications.update(notifications => notifications.slice(0, 50));
    }
  }

  markAsRead(id: string): void {
    this.notifications.update(notifications =>
      notifications.map(n =>
        n.id === id ? { ...n, read: true } : n
      )
    );
  }

  markAllAsRead(): void {
    this.notifications.update(notifications =>
      notifications.map(n => ({ ...n, read: true }))
    );
  }

  deleteNotification(id: string): void {
    this.notifications.update(notifications =>
      notifications.filter(n => n.id !== id)
    );
  }

  clearAll(): void {
    this.notifications.set([]);
  }

  // Convenience methods
  info(title: string, message: string, actionLabel?: string, actionCallback?: () => void): void {
    this.addNotification('info', title, message, actionLabel, actionCallback);
  }

  success(title: string, message: string, actionLabel?: string, actionCallback?: () => void): void {
    this.addNotification('success', title, message, actionLabel, actionCallback);
  }

  warning(title: string, message: string, actionLabel?: string, actionCallback?: () => void): void {
    this.addNotification('warning', title, message, actionLabel, actionCallback);
  }

  error(title: string, message: string, actionLabel?: string, actionCallback?: () => void): void {
    this.addNotification('error', title, message, actionLabel, actionCallback);
  }

  discovery(title: string, message: string, actionLabel?: string, actionCallback?: () => void): void {
    this.addNotification('discovery', title, message, actionLabel, actionCallback);
  }
}
