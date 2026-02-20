import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideCircle,
  lucideCheckCircle2,
  lucideXCircle,
  lucideAlertCircle,
  lucideLoader2,
  lucideWifi,
  lucideWifiOff,
  lucideAlertTriangle,
} from '@ng-icons/lucide';

export type StatusType = 
  | 'online' 
  | 'offline' 
  | 'error' 
  | 'warning' 
  | 'pairing' 
  | 'success'
  | 'info'
  | 'critical';

export interface StatusConfig {
  label: string;
  icon: string;
  color: string;
  pulse?: boolean;
}

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule, NgIconComponent],
  providers: [
    provideIcons({
      lucideCircle,
      lucideCheckCircle2,
      lucideXCircle,
      lucideAlertCircle,
      lucideLoader2,
      lucideWifi,
      lucideWifiOff,
      lucideAlertTriangle,
    })
  ],
  template: `
    <div class="status-badge" [class]="'status-' + status()" [class.pulse]="config().pulse">
      <ng-icon 
        [name]="config().icon" 
        size="14"
        class="status-icon"
      />
      @if (showLabel()) {
        <span class="status-label">{{ config().label }}</span>
      }
    </div>
  `,
  styles: [`
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.25rem 0.625rem;
      border-radius: 9999px;
      font-size: var(--text-xs);
      font-weight: 500;
      line-height: 1;
      transition: all var(--duration-fast) ease;
      
      &.pulse {
        animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
      }
    }
    
    .status-icon {
      flex-shrink: 0;
    }
    
    .status-label {
      white-space: nowrap;
    }
    
    /* Status variants */
    .status-online {
      background: oklch(from var(--status-online) l c h / 0.15);
      color: var(--status-online);
      border: 1px solid oklch(from var(--status-online) l c h / 0.3);
    }
    
    .status-offline {
      background: oklch(from var(--status-offline) l c h / 0.15);
      color: var(--status-offline);
      border: 1px solid oklch(from var(--status-offline) l c h / 0.3);
    }
    
    .status-error {
      background: oklch(from var(--status-error) l c h / 0.15);
      color: var(--status-error);
      border: 1px solid oklch(from var(--status-error) l c h / 0.3);
    }
    
    .status-warning {
      background: oklch(from var(--status-warning) l c h / 0.15);
      color: var(--status-warning);
      border: 1px solid oklch(from var(--status-warning) l c h / 0.3);
    }
    
    .status-pairing {
      background: oklch(from var(--status-pairing) l c h / 0.15);
      color: var(--status-pairing);
      border: 1px solid oklch(from var(--status-pairing) l c h / 0.3);
    }
    
    .status-success {
      background: oklch(from var(--status-online) l c h / 0.15);
      color: var(--status-online);
      border: 1px solid oklch(from var(--status-online) l c h / 0.3);
    }
    
    .status-info {
      background: oklch(from var(--primary) l c h / 0.15);
      color: var(--primary);
      border: 1px solid oklch(from var(--primary) l c h / 0.3);
    }
    
    .status-critical {
      background: oklch(from var(--destructive) l c h / 0.15);
      color: var(--destructive);
      border: 1px solid oklch(from var(--destructive) l c h / 0.3);
      font-weight: 600;
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
export class StatusBadgeComponent {
  status = input.required<StatusType>();
  showLabel = input<boolean>(true);
  
  private statusConfigs: Record<StatusType, StatusConfig> = {
    online: {
      label: 'Online',
      icon: 'lucideWifi',
      color: 'var(--status-online)',
      pulse: false
    },
    offline: {
      label: 'Offline',
      icon: 'lucideWifiOff',
      color: 'var(--status-offline)',
      pulse: false
    },
    error: {
      label: 'Error',
      icon: 'lucideXCircle',
      color: 'var(--status-error)',
      pulse: false
    },
    warning: {
      label: 'Warning',
      icon: 'lucideAlertCircle',
      color: 'var(--status-warning)',
      pulse: false
    },
    pairing: {
      label: 'Pairing...',
      icon: 'lucideLoader2',
      color: 'var(--status-pairing)',
      pulse: true
    },
    success: {
      label: 'Success',
      icon: 'lucideCheckCircle2',
      color: 'var(--status-online)',
      pulse: false
    },
    info: {
      label: 'Info',
      icon: 'lucideCircle',
      color: 'var(--primary)',
      pulse: false
    },
    critical: {
      label: 'Critical',
      icon: 'lucideAlertTriangle',
      color: 'var(--destructive)',
      pulse: true
    }
  };
  
  config = () => this.statusConfigs[this.status()];
}
