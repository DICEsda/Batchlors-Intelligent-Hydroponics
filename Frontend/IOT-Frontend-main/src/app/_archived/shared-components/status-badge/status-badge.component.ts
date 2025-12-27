import { Component, computed, input } from '@angular/core';
import { hlm } from '../../ui/utils';
import { HlmBadgeDirective, badgeVariants } from '../../ui/badge';

export type DeviceStatus = 'online' | 'offline' | 'pairing' | 'error' | 'warning' | 'unknown';

/**
 * Status Badge Component for IoT devices
 * Displays a status indicator with appropriate color and optional pulse animation
 */
@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [HlmBadgeDirective],
  template: `
    <span 
      hlmBadge 
      [variant]="badgeVariant()" 
      [class]="_computedClass()"
    >
      <span class="status-dot" [class]="dotClass()"></span>
      {{ displayText() }}
    </span>
  `,
  styles: [`
    :host {
      display: inline-flex;
    }
    
    .status-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      margin-right: 6px;
      flex-shrink: 0;
    }
    
    .dot-online {
      background-color: #22c55e;
      animation: pulse-green 2s infinite;
    }
    
    .dot-offline {
      background-color: #ef4444;
    }
    
    .dot-pairing {
      background-color: #f59e0b;
      animation: pulse-amber 1.5s infinite;
    }
    
    .dot-error {
      background-color: #ef4444;
      animation: pulse-red 1s infinite;
    }
    
    .dot-warning {
      background-color: #f59e0b;
    }
    
    .dot-unknown {
      background-color: #6b7280;
    }
    
    @keyframes pulse-green {
      0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(34, 197, 94, 0.7); }
      50% { opacity: 0.8; box-shadow: 0 0 0 4px rgba(34, 197, 94, 0); }
    }
    
    @keyframes pulse-amber {
      0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(245, 158, 11, 0.7); }
      50% { opacity: 0.8; box-shadow: 0 0 0 4px rgba(245, 158, 11, 0); }
    }
    
    @keyframes pulse-red {
      0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(239, 68, 68, 0.7); }
      50% { opacity: 0.8; box-shadow: 0 0 0 4px rgba(239, 68, 68, 0); }
    }
  `],
})
export class StatusBadgeComponent {
  public readonly status = input<DeviceStatus>('unknown');
  public readonly showDot = input<boolean>(true);
  public readonly size = input<'sm' | 'default' | 'lg'>('default');
  public readonly userClass = input<string>('', { alias: 'class' });

  protected badgeVariant = computed(() => {
    const statusMap: Record<DeviceStatus, 'online' | 'offline' | 'pairing' | 'error' | 'warning' | 'default'> = {
      online: 'online',
      offline: 'offline',
      pairing: 'pairing',
      error: 'error',
      warning: 'warning',
      unknown: 'default',
    };
    return statusMap[this.status()];
  });

  protected dotClass = computed(() => {
    return `dot-${this.status()}`;
  });

  protected displayText = computed(() => {
    const textMap: Record<DeviceStatus, string> = {
      online: 'Online',
      offline: 'Offline',
      pairing: 'Pairing',
      error: 'Error',
      warning: 'Warning',
      unknown: 'Unknown',
    };
    return textMap[this.status()];
  });

  protected _computedClass = computed(() => {
    const sizeClasses: Record<string, string> = {
      sm: 'text-xs py-0.5',
      default: '',
      lg: 'text-sm py-1.5 px-3',
    };
    return hlm(sizeClasses[this.size()], this.userClass());
  });
}
