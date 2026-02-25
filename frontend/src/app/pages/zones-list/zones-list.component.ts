import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideGrid3x3,
  lucideLightbulb,
  lucidePalette,
  lucidePlus,
  lucideRefreshCw,
  lucideTrash2,
  lucidePencil
} from '@ng-icons/lucide';
import { IoTDataService } from '../../core/services/iot-data.service';
import { Zone } from '../../core/models';

@Component({
  selector: 'app-zones-list',
  standalone: true,
  imports: [CommonModule, RouterLink, NgIcon],
  providers: [
    provideIcons({
      lucideGrid3x3,
      lucideLightbulb,
      lucidePalette,
      lucidePlus,
      lucideRefreshCw,
      lucideTrash2,
      lucidePencil
    })
  ],
  template: `
    <div class="zones-page">
      <!-- Page Header -->
      <div class="page-header">
        <div class="header-content">
          <div class="header-title">
            <ng-icon name="lucideGrid3x3" size="28" />
            <h1>Lighting Zones</h1>
          </div>
          <p class="header-description">
            Manage lighting zones to group and control multiple nodes together
          </p>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" (click)="loadZones()">
            <ng-icon name="lucideRefreshCw" size="18" />
            Refresh
          </button>
          <button class="btn btn-primary" (click)="createZone()">
            <ng-icon name="lucidePlus" size="18" />
            Create Zone
          </button>
        </div>
      </div>

      <!-- Loading State -->
      @if (loading()) {
        <div class="loading-container">
          <div class="spinner"></div>
          <p>Loading zones...</p>
        </div>
      }

      <!-- Zones Grid -->
      @if (!loading()) {
        <div class="zones-grid">
          @for (zone of zones(); track zone._id) {
            <div class="zone-card" [style.border-color]="zone.color || '#3b82f6'">
              <div class="zone-header">
                <div class="zone-color" [style.background-color]="zone.color || '#3b82f6'">
                  <ng-icon name="lucidePalette" size="20" />
                </div>
                <div class="zone-info">
                  <h3>{{ zone.name }}</h3>
                  <span class="zone-id">ID: {{ zone._id }}</span>
                </div>
                <div class="zone-actions">
                  <button class="icon-btn" title="Edit Zone">
                    <ng-icon name="lucidePencil" size="16" />
                  </button>
                  <button class="icon-btn danger" title="Delete Zone">
                    <ng-icon name="lucideTrash2" size="16" />
                  </button>
                </div>
              </div>
              
              <div class="zone-body">
                @if (zone.description) {
                  <p class="zone-description">{{ zone.description }}</p>
                }
                
                <div class="zone-stats">
                  <div class="stat">
                    <ng-icon name="lucideLightbulb" size="16" />
                    <span>{{ zone.node_ids?.length || 0 }} Nodes</span>
                  </div>
                  @if (zone.brightness !== undefined) {
                    <div class="stat">
                      <span class="brightness">Brightness: {{ zone.brightness }}%</span>
                    </div>
                  }
                </div>
                
                @if (zone.node_ids && zone.node_ids.length > 0) {
                  <div class="node-chips">
                    @for (nodeId of zone.node_ids.slice(0, 5); track nodeId) {
                      <a [routerLink]="['/nodes', nodeId]" class="node-chip">
                        {{ nodeId.slice(-6) }}
                      </a>
                    }
                    @if (zone.node_ids.length > 5) {
                      <span class="more-chip">+{{ zone.node_ids.length - 5 }} more</span>
                    }
                  </div>
                }
              </div>
              
              <div class="zone-footer">
                <button class="btn btn-sm btn-outline" (click)="setZoneColor(zone._id)">
                  <ng-icon name="lucidePalette" size="14" />
                  Set Color
                </button>
                <button class="btn btn-sm btn-primary">
                  Control
                </button>
              </div>
            </div>
          } @empty {
            <div class="empty-state">
              <ng-icon name="lucideGrid3x3" size="64" />
              <h3>No Zones Created</h3>
              <p>Create your first lighting zone to group and control multiple nodes together.</p>
              <button class="btn btn-primary" (click)="createZone()">
                <ng-icon name="lucidePlus" size="18" />
                Create Zone
              </button>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .zones-page {
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
      
      &.btn-primary {
        background: var(--accent-color, #3b82f6);
        color: white;
        
        &:hover {
          background: var(--accent-hover, #2563eb);
        }
      }
      
      &.btn-secondary {
        background: var(--bg-secondary, #1f1f1f);
        color: var(--text-primary, #fff);
        border: 1px solid var(--border-color, #333);
        
        &:hover {
          background: var(--bg-tertiary, #2a2a2a);
        }
      }
      
      &.btn-outline {
        background: transparent;
        color: var(--text-primary, #fff);
        border: 1px solid var(--border-color, #333);
        
        &:hover {
          background: var(--bg-secondary, #1f1f1f);
        }
      }
      
      &.btn-sm {
        padding: 0.375rem 0.75rem;
        font-size: 0.875rem;
      }
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 4rem;
      color: var(--text-secondary, #888);
      
      .spinner {
        width: 40px;
        height: 40px;
        border: 3px solid var(--border-color, #333);
        border-top-color: var(--accent-color, #3b82f6);
        border-radius: 50%;
        animation: spin 1s linear infinite;
      }
      
      p {
        margin-top: 1rem;
      }
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .zones-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 1.5rem;
    }

    .zone-card {
      background: var(--card-bg, #1a1a1a);
      border-radius: 0.75rem;
      border: 2px solid;
      overflow: hidden;
    }

    .zone-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1rem;
      border-bottom: 1px solid var(--border-color, #333);
    }

    .zone-color {
      width: 40px;
      height: 40px;
      border-radius: 0.5rem;
      display: flex;
      align-items: center;
      justify-content: center;
      color: white;
    }

    .zone-info {
      flex: 1;
      
      h3 {
        font-size: 1rem;
        font-weight: 600;
        color: var(--text-primary, #fff);
        margin: 0;
      }
      
      .zone-id {
        font-size: 0.75rem;
        color: var(--text-secondary, #888);
      }
    }

    .zone-actions {
      display: flex;
      gap: 0.25rem;
    }

    .icon-btn {
      width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 0.375rem;
      background: transparent;
      border: none;
      color: var(--text-secondary, #888);
      cursor: pointer;
      
      &:hover {
        background: var(--bg-secondary, #1f1f1f);
        color: var(--text-primary, #fff);
      }
      
      &.danger:hover {
        background: rgba(239, 68, 68, 0.1);
        color: #ef4444;
      }
    }

    .zone-body {
      padding: 1rem;
    }

    .zone-description {
      color: var(--text-secondary, #888);
      font-size: 0.875rem;
      margin: 0 0 1rem 0;
    }

    .zone-stats {
      display: flex;
      gap: 1rem;
      margin-bottom: 1rem;
      
      .stat {
        display: flex;
        align-items: center;
        gap: 0.375rem;
        font-size: 0.875rem;
        color: var(--text-secondary, #888);
      }
    }

    .node-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }

    .node-chip {
      padding: 0.25rem 0.5rem;
      background: var(--bg-secondary, #1f1f1f);
      border-radius: 0.25rem;
      font-size: 0.75rem;
      color: var(--text-secondary, #888);
      text-decoration: none;
      
      &:hover {
        background: var(--accent-color, #3b82f6);
        color: white;
      }
    }

    .more-chip {
      padding: 0.25rem 0.5rem;
      font-size: 0.75rem;
      color: var(--text-secondary, #888);
    }

    .zone-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      background: var(--bg-secondary, #1f1f1f);
      border-top: 1px solid var(--border-color, #333);
    }

    .empty-state {
      grid-column: 1 / -1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 4rem 2rem;
      background: var(--card-bg, #1a1a1a);
      border-radius: 0.75rem;
      border: 2px dashed var(--border-color, #333);
      text-align: center;
      color: var(--text-secondary, #888);
      
      h3 {
        font-size: 1.25rem;
        color: var(--text-primary, #fff);
        margin: 1rem 0 0.5rem 0;
      }
      
      p {
        margin: 0 0 1.5rem 0;
        max-width: 400px;
      }
    }
  `]
})
export class ZonesListComponent implements OnInit {
  private readonly dataService = inject(IoTDataService);

  readonly loading = signal(false);
  readonly zones = signal<Zone[]>([]);

  async ngOnInit(): Promise<void> {
    await this.loadZones();
  }

  async loadZones(): Promise<void> {
    this.loading.set(true);
    try {
      await this.dataService.loadZones();
      this.zones.set(this.dataService.zones());
    } finally {
      this.loading.set(false);
    }
  }

  createZone(): void {
    // TODO: Open create zone dialog
    console.log('Create zone clicked');
  }

  setZoneColor(zoneId: string): void {
    // TODO: Open color picker dialog
    console.log('Set color for zone:', zoneId);
  }
}
