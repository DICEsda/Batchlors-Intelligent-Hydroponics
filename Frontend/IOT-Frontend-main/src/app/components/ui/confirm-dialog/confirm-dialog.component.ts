import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideAlertTriangle, lucideInfo, lucideAlertCircle } from '@ng-icons/lucide';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { HlmButtonDirective } from '../button';
import { HlmIconDirective } from '../icon';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, NgIcon, HlmButtonDirective, HlmIconDirective],
  providers: [
    provideIcons({
      lucideAlertTriangle,
      lucideInfo,
      lucideAlertCircle
    })
  ],
  template: `
    @if (state().isOpen) {
      <div class="confirm-dialog-overlay" (click)="onCancel()" @fadeIn>
        <div class="confirm-dialog" (click)="$event.stopPropagation()" @scaleIn>
          <div class="dialog-icon" [class]="'icon-' + state().type">
            <ng-icon 
              [name]="getIcon()" 
              size="xl"
              hlmIcon
            />
          </div>
          
          <div class="dialog-content">
            <h3 class="dialog-title">{{ state().title }}</h3>
            <p class="dialog-message">{{ state().message }}</p>
          </div>
          
          <div class="dialog-actions">
            <button 
              hlmBtn 
              variant="outline" 
              (click)="onCancel()"
              class="cancel-btn"
            >
              {{ state().cancelText }}
            </button>
            <button 
              hlmBtn 
              [variant]="state().type === 'danger' ? 'destructive' : 'default'"
              (click)="onConfirm()"
              class="confirm-btn"
              autofocus
            >
              {{ state().confirmText }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }
    
    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.95) translateY(10px);
      }
      to {
        opacity: 1;
        transform: scale(1) translateY(0);
      }
    }
    
    .confirm-dialog-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: oklch(0 0 0 / 0.5);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 9999;
      padding: var(--spacing-lg);
      animation: fadeIn var(--duration-normal) var(--ease-out-expo);
    }
    
    .confirm-dialog {
      background: var(--card);
      border: 1px solid var(--border);
      border-radius: calc(var(--radius) * 1.5);
      padding: var(--spacing-2xl);
      max-width: 400px;
      width: 100%;
      box-shadow: 0 20px 25px oklch(0 0 0 / 0.15);
      animation: scaleIn var(--duration-slow) var(--ease-spring);
    }
    
    .dialog-icon {
      width: 48px;
      height: 48px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto var(--spacing-lg);
      
      &.icon-danger {
        background: oklch(from var(--destructive) l c h / 0.1);
        color: var(--destructive);
      }
      
      &.icon-warning {
        background: oklch(0.769 0.188 70.08 / 0.1);
        color: oklch(0.769 0.188 70.08);
      }
      
      &.icon-info {
        background: oklch(from var(--primary) l c h / 0.1);
        color: var(--primary);
      }
    }
    
    .dialog-content {
      text-align: center;
      margin-bottom: var(--spacing-2xl);
    }
    
    .dialog-title {
      font-size: var(--text-xl);
      font-weight: 600;
      color: var(--foreground);
      margin: 0 0 var(--spacing-sm);
      line-height: var(--leading-tight);
    }
    
    .dialog-message {
      font-size: var(--text-sm);
      color: var(--muted-foreground);
      margin: 0;
      line-height: var(--leading-normal);
    }
    
    .dialog-actions {
      display: flex;
      gap: var(--spacing-md);
      justify-content: flex-end;
    }
    
    .cancel-btn,
    .confirm-btn {
      flex: 1;
      min-width: 100px;
    }
    
    /* Mobile adjustments */
    @media (max-width: 640px) {
      .confirm-dialog {
        padding: var(--spacing-xl);
      }
      
      .dialog-actions {
        flex-direction: column-reverse;
        
        .cancel-btn,
        .confirm-btn {
          width: 100%;
        }
      }
    }
  `]
})
export class ConfirmDialogComponent {
  private confirmService = inject(ConfirmDialogService);
  
  readonly state = this.confirmService.state;
  
  getIcon(): string {
    const type = this.state().type;
    if (type === 'danger') return 'lucideAlertTriangle';
    if (type === 'warning') return 'lucideAlertCircle';
    return 'lucideInfo';
  }
  
  onConfirm() {
    this.state().onConfirm?.();
  }
  
  onCancel() {
    this.state().onCancel?.();
  }
}
