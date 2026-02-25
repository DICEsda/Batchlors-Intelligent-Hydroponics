import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideX,
  lucideCheckCircle,
  lucideAlertCircle,
  lucideInfo,
  lucideAlertTriangle,
  lucideRadioTower,
  lucideAlertOctagon
} from '@ng-icons/lucide';

export type ToastType = 'success' | 'error' | 'info' | 'warning' | 'discovery' | 'urgent';

export interface ToastAction {
  label: string;
  callback: () => void;
}

export interface ToastSecondaryAction {
  label: string;
  callback: () => void;
}

export interface Toast {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration?: number;
  dismissible?: boolean;
  action?: ToastAction;
  secondaryAction?: ToastSecondaryAction;
}

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule, NgIcon],
  providers: [
    provideIcons({
      lucideX,
      lucideCheckCircle,
      lucideAlertCircle,
      lucideInfo,
      lucideAlertTriangle,
      lucideRadioTower,
      lucideAlertOctagon
    })
  ],
  animations: [
    trigger('toastAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateX(100%)' }),
        animate('300ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 1, transform: 'translateX(0)' }))
      ]),
      transition(':leave', [
        animate('250ms cubic-bezier(0.4, 0, 1, 1)', style({ 
          opacity: 0, 
          transform: 'translateX(120%)',
          height: 0,
          marginBottom: 0,
          paddingTop: 0,
          paddingBottom: 0
        }))
      ])
    ])
  ],
  template: `
    <div class="toast-container">
      @for (toast of toasts(); track toast.id) {
        <div 
          class="toast" 
          [class]="'toast toast-' + toast.type"
          [@toastAnimation]
        >
          <div class="toast-icon">
            <ng-icon [name]="getIcon(toast.type)" size="20" />
          </div>
          <div class="toast-content">
            <div class="toast-title">{{ toast.title }}</div>
            @if (toast.message) {
              <div class="toast-message">{{ toast.message }}</div>
            }
            @if (toast.action || toast.secondaryAction) {
              <div class="toast-actions">
                @if (toast.action) {
                  <button 
                    class="toast-action-btn" 
                    (click)="handleAction(toast)"
                  >
                    {{ toast.action.label }}
                  </button>
                }
                @if (toast.secondaryAction) {
                  <button 
                    class="toast-secondary-btn" 
                    (click)="handleSecondaryAction(toast)"
                  >
                    {{ toast.secondaryAction.label }}
                  </button>
                }
              </div>
            }
          </div>
          @if (toast.dismissible !== false) {
            <button class="toast-close" (click)="dismiss(toast.id)">
              <ng-icon name="lucideX" size="16" />
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      max-width: 400px;
      pointer-events: none;
    }

    .toast {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 14px 16px;
      margin-bottom: 12px;
      background: var(--card);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      pointer-events: auto;
      overflow: hidden;
    }

    .toast-icon {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      border-radius: 50%;
    }

    .toast-success .toast-icon {
      color: var(--status-online);
    }

    .toast-error .toast-icon {
      color: var(--status-error);
    }

    .toast-warning .toast-icon {
      color: var(--status-warning);
    }

    .toast-info .toast-icon {
      color: var(--status-pairing);
    }

    .toast-discovery .toast-icon {
      color: var(--status-pairing);
    }

    .toast-discovery {
      border-left: 3px solid var(--status-pairing);
    }

    .toast-urgent .toast-icon {
      color: var(--status-online);
    }

    .toast-urgent {
      border-left: 3px solid var(--status-online);
      background: var(--card);
    }

    .toast-content {
      flex: 1;
      min-width: 0;
    }

    .toast-title {
      font-size: 14px;
      font-weight: 500;
      color: var(--foreground);
      line-height: 1.4;
    }

    .toast-message {
      font-size: 13px;
      color: var(--muted-foreground);
      margin-top: 2px;
      line-height: 1.4;
    }

    .toast-close {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      background: transparent;
      border: none;
      border-radius: 4px;
      color: var(--muted-foreground);
      cursor: pointer;
      transition: all 0.15s ease;
      margin: -4px -4px -4px 0;
    }

    .toast-close:hover {
      background: var(--accent);
      color: var(--accent-foreground);
    }

    .toast-actions {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }

    .toast-action-btn {
      padding: 6px 12px;
      font-size: 13px;
      font-weight: 500;
      color: var(--primary-foreground);
      background: var(--primary);
      border: none;
      border-radius: var(--radius);
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .toast-action-btn:hover {
      opacity: 0.9;
      transform: translateY(-1px);
    }

    .toast-action-btn:active {
      transform: translateY(0);
    }

    .toast-secondary-btn {
      padding: 6px 12px;
      font-size: 13px;
      font-weight: 500;
      color: var(--muted-foreground);
      background: transparent;
      border: 1px solid var(--border);
      border-radius: var(--radius);
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .toast-secondary-btn:hover {
      background: var(--secondary);
      color: var(--foreground);
    }

    .toast-urgent .toast-action-btn {
      background: var(--status-online);
      color: white;
    }

    .toast-urgent .toast-secondary-btn {
      border-color: var(--status-online);
      color: var(--status-online);
    }

    .toast-urgent .toast-secondary-btn:hover {
      background: oklch(0.65 0.17 145 / 0.1);
    }
  `]
})
export class ToastContainerComponent {
  readonly toasts = signal<Toast[]>([]);

  getIcon(type: ToastType): string {
    switch (type) {
      case 'success': return 'lucideCheckCircle';
      case 'error': return 'lucideAlertCircle';
      case 'warning': return 'lucideAlertTriangle';
      case 'info': return 'lucideInfo';
      case 'discovery': return 'lucideRadioTower';
      case 'urgent': return 'lucideAlertOctagon';
      default: return 'lucideInfo';
    }
  }

  handleAction(toast: Toast): void {
    if (toast.action) {
      toast.action.callback();
      this.dismiss(toast.id);
    }
  }

  handleSecondaryAction(toast: Toast): void {
    if (toast.secondaryAction) {
      toast.secondaryAction.callback();
      this.dismiss(toast.id);
    }
  }

  addToast(toast: Omit<Toast, 'id'>): string {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const newToast: Toast = { ...toast, id };
    
    this.toasts.update(current => [...current, newToast]);

    // Auto-dismiss after duration (default 5 seconds)
    const duration = toast.duration ?? 5000;
    if (duration > 0) {
      setTimeout(() => this.dismiss(id), duration);
    }

    return id;
  }

  dismiss(id: string): void {
    this.toasts.update(current => current.filter(t => t.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }
}
