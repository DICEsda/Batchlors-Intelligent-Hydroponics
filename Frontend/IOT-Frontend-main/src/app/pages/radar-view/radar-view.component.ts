import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideRadar, lucideSettings, lucidePlay, lucidePause } from '@ng-icons/lucide';

@Component({
  selector: 'app-radar-view',
  standalone: true,
  imports: [CommonModule, NgIcon],
  providers: [
    provideIcons({
      lucideRadar,
      lucideSettings,
      lucidePlay,
      lucidePause
    })
  ],
  template: `
    <div class="radar-page">
      <!-- Page Header -->
      <div class="page-header">
        <div class="header-content">
          <div class="header-title">
            <ng-icon name="lucideRadar" size="28" />
            <h1>Radar View</h1>
          </div>
          <p class="header-description">
            Real-time presence detection and motion tracking visualization
          </p>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" (click)="toggleRadar()">
            <ng-icon [name]="radarActive() ? 'lucidePause' : 'lucidePlay'" size="18" />
            {{ radarActive() ? 'Pause' : 'Start' }}
          </button>
          <button class="btn btn-secondary">
            <ng-icon name="lucideSettings" size="18" />
            Settings
          </button>
        </div>
      </div>

      <!-- Radar Display -->
      <div class="radar-container">
        <div class="radar-display">
          <div class="radar-grid">
            <!-- Radar circles -->
            <div class="radar-circle circle-1"></div>
            <div class="radar-circle circle-2"></div>
            <div class="radar-circle circle-3"></div>
            <div class="radar-circle circle-4"></div>
            
            <!-- Center dot -->
            <div class="radar-center"></div>
            
            <!-- Radar sweep -->
            @if (radarActive()) {
              <div class="radar-sweep"></div>
            }
            
            <!-- Detection zones -->
            <div class="detection-zones">
              @for (zone of detectionZones(); track zone.id) {
                <div 
                  class="detection-zone"
                  [style.left.%]="zone.x"
                  [style.top.%]="zone.y"
                  [class.active]="zone.detected"
                >
                  <span class="zone-label">{{ zone.label }}</span>
                </div>
              }
            </div>
          </div>
        </div>
        
        <!-- Status Panel -->
        <div class="status-panel">
          <h3>Detection Status</h3>
          <div class="status-list">
            <div class="status-item">
              <span class="status-label">Active Targets:</span>
              <span class="status-value">{{ activeTargets() }}</span>
            </div>
            <div class="status-item">
              <span class="status-label">Last Detection:</span>
              <span class="status-value">{{ lastDetection() || 'None' }}</span>
            </div>
            <div class="status-item">
              <span class="status-label">Scan Rate:</span>
              <span class="status-value">10 Hz</span>
            </div>
            <div class="status-item">
              <span class="status-label">Range:</span>
              <span class="status-value">5.0 m</span>
            </div>
          </div>
          
          <h3>Recent Activity</h3>
          <div class="activity-log">
            @for (event of activityLog(); track event.timestamp) {
              <div class="activity-item">
                <span class="activity-time">{{ event.time }}</span>
                <span class="activity-message">{{ event.message }}</span>
              </div>
            } @empty {
              <p class="no-activity">No recent activity</p>
            }
          </div>
        </div>
      </div>
      
      <!-- Info Banner -->
      <div class="info-banner">
        <ng-icon name="lucideRadar" size="20" />
        <p>
          This radar view integrates with the HLK-LD2450 presence detection sensor. 
          Connect the sensor to your coordinator to enable real-time tracking.
        </p>
      </div>
    </div>
  `,
  styles: [`
    .radar-page {
      padding: 1.5rem;
      max-width: 1400px;
      margin: 0 auto;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
      flex-wrap: wrap;
      gap: 1rem;
    }

    .header-title {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      
      h1 {
        font-size: 1.75rem;
        font-weight: 600;
        color: var(--text-primary, #fff);
        margin: 0;
      }
    }

    .header-description {
      color: var(--text-secondary, #888);
      margin: 0.5rem 0 0 0;
    }

    .header-actions {
      display: flex;
      gap: 0.75rem;
    }

    .btn {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 1rem;
      border-radius: 0.5rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      border: none;
      
      &.btn-secondary {
        background: var(--bg-secondary, #1f1f1f);
        color: var(--text-primary, #fff);
        border: 1px solid var(--border-color, #333);
        
        &:hover {
          background: var(--bg-tertiary, #2a2a2a);
        }
      }
    }

    .radar-container {
      display: grid;
      grid-template-columns: 1fr 320px;
      gap: 1.5rem;
      margin-bottom: 1.5rem;
      
      @media (max-width: 900px) {
        grid-template-columns: 1fr;
      }
    }

    .radar-display {
      background: var(--card-bg, #1a1a1a);
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #333);
      padding: 2rem;
      aspect-ratio: 1;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .radar-grid {
      position: relative;
      width: 100%;
      max-width: 400px;
      aspect-ratio: 1;
    }

    .radar-circle {
      position: absolute;
      border: 1px solid rgba(59, 130, 246, 0.3);
      border-radius: 50%;
      
      &.circle-1 {
        width: 25%;
        height: 25%;
        top: 37.5%;
        left: 37.5%;
      }
      
      &.circle-2 {
        width: 50%;
        height: 50%;
        top: 25%;
        left: 25%;
      }
      
      &.circle-3 {
        width: 75%;
        height: 75%;
        top: 12.5%;
        left: 12.5%;
      }
      
      &.circle-4 {
        width: 100%;
        height: 100%;
        top: 0;
        left: 0;
      }
    }

    .radar-center {
      position: absolute;
      width: 12px;
      height: 12px;
      background: #3b82f6;
      border-radius: 50%;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      box-shadow: 0 0 10px rgba(59, 130, 246, 0.5);
    }

    .radar-sweep {
      position: absolute;
      width: 50%;
      height: 2px;
      background: linear-gradient(90deg, #3b82f6, transparent);
      top: 50%;
      left: 50%;
      transform-origin: left center;
      animation: sweep 3s linear infinite;
    }

    @keyframes sweep {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }

    .detection-zones {
      position: absolute;
      inset: 0;
    }

    .detection-zone {
      position: absolute;
      width: 20px;
      height: 20px;
      background: rgba(59, 130, 246, 0.3);
      border: 2px solid #3b82f6;
      border-radius: 50%;
      transform: translate(-50%, -50%);
      transition: all 0.3s;
      
      &.active {
        background: rgba(34, 197, 94, 0.5);
        border-color: #22c55e;
        box-shadow: 0 0 15px rgba(34, 197, 94, 0.5);
      }
      
      .zone-label {
        position: absolute;
        top: 100%;
        left: 50%;
        transform: translateX(-50%);
        font-size: 0.625rem;
        color: var(--text-secondary, #888);
        white-space: nowrap;
        margin-top: 4px;
      }
    }

    .status-panel {
      background: var(--card-bg, #1a1a1a);
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #333);
      padding: 1.25rem;
      
      h3 {
        font-size: 0.875rem;
        font-weight: 600;
        color: var(--text-primary, #fff);
        margin: 0 0 1rem 0;
        padding-bottom: 0.5rem;
        border-bottom: 1px solid var(--border-color, #333);
        
        &:not(:first-child) {
          margin-top: 1.5rem;
        }
      }
    }

    .status-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .status-item {
      display: flex;
      justify-content: space-between;
      
      .status-label {
        color: var(--text-secondary, #888);
        font-size: 0.875rem;
      }
      
      .status-value {
        color: var(--text-primary, #fff);
        font-weight: 500;
        font-size: 0.875rem;
      }
    }

    .activity-log {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-height: 200px;
      overflow-y: auto;
    }

    .activity-item {
      display: flex;
      gap: 0.75rem;
      font-size: 0.75rem;
      padding: 0.5rem;
      background: var(--bg-secondary, #1f1f1f);
      border-radius: 0.375rem;
      
      .activity-time {
        color: var(--text-secondary, #888);
        flex-shrink: 0;
      }
      
      .activity-message {
        color: var(--text-primary, #fff);
      }
    }

    .no-activity {
      color: var(--text-secondary, #888);
      font-size: 0.875rem;
      text-align: center;
      padding: 1rem;
    }

    .info-banner {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1rem;
      background: rgba(59, 130, 246, 0.1);
      border: 1px solid rgba(59, 130, 246, 0.3);
      border-radius: 0.5rem;
      color: #60a5fa;
      
      p {
        margin: 0;
        font-size: 0.875rem;
      }
    }
  `]
})
export class RadarViewComponent {
  readonly radarActive = signal(true);
  readonly activeTargets = signal(0);
  readonly lastDetection = signal<string | null>(null);
  
  readonly detectionZones = signal([
    { id: 1, label: 'Zone A', x: 30, y: 30, detected: false },
    { id: 2, label: 'Zone B', x: 70, y: 40, detected: false },
    { id: 3, label: 'Zone C', x: 50, y: 70, detected: false }
  ]);
  
  readonly activityLog = signal<{ timestamp: number; time: string; message: string }[]>([]);

  toggleRadar(): void {
    this.radarActive.update(active => !active);
  }
}
