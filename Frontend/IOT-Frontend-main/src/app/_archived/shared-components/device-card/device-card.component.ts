import { Component, computed, input, output } from '@angular/core';
import { hlm } from '../../ui/utils';
import { 
  HlmCardComponent, 
  HlmCardHeaderComponent, 
  HlmCardTitleComponent, 
  HlmCardContentComponent 
} from '../../ui/card';
import { StatusBadgeComponent, DeviceStatus } from '../status-badge';
import { Node } from '../../../core/models/api.models';

/**
 * Device Card Component for displaying node/device information
 * Shows device status, battery, sensor data, and actions
 */
@Component({
  selector: 'app-device-card',
  standalone: true,
  imports: [
    HlmCardComponent,
    HlmCardHeaderComponent,
    HlmCardTitleComponent,
    HlmCardContentComponent,
    StatusBadgeComponent,
  ],
  template: `
    <div hlmCard [class]="_computedClass()">
      <!-- Header -->
      <div hlmCardHeader class="pb-2">
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0 flex-1">
            <h3 hlmCardTitle class="text-base font-semibold truncate">
              {{ deviceName() }}
            </h3>
            <p class="text-xs text-muted-foreground mt-0.5">
              Zone: {{ zoneName() }}
            </p>
          </div>
          <app-status-badge 
            [status]="deviceStatus()" 
            size="sm"
          />
        </div>
      </div>
      
      <!-- Content -->
      <div hlmCardContent class="space-y-3">
        <!-- Battery Indicator -->
        <div class="space-y-1">
          <div class="flex items-center justify-between text-xs">
            <span class="text-muted-foreground">Battery</span>
            <span [class]="batteryTextClass()">
              {{ batteryDisplay() }}
            </span>
          </div>
          <div class="h-2 w-full rounded-full bg-muted overflow-hidden">
            <div 
              class="h-full transition-all duration-300"
              [class]="batteryBarClass()"
              [style.width.%]="batteryPercent() ?? 0"
            ></div>
          </div>
        </div>
        
        <!-- Metrics Grid -->
        <div class="grid grid-cols-2 gap-3 pt-2">
          <div class="space-y-0.5">
            <span class="text-xs text-muted-foreground">Brightness</span>
            <p class="text-sm font-medium">{{ brightnessDisplay() }}</p>
          </div>
          <div class="space-y-0.5">
            <span class="text-xs text-muted-foreground">Last Seen</span>
            <p class="text-sm font-medium">{{ lastSeenDisplay() }}</p>
          </div>
        </div>
        
        <!-- Sensor Data -->
        @if (showSensors()) {
          <div class="border-t border-border pt-3 mt-3">
            <h4 class="text-xs font-medium text-muted-foreground mb-2">Sensors</h4>
            <div class="grid grid-cols-2 gap-2">
              @for (sensor of sensorReadings(); track sensor.label) {
                <div class="flex flex-col">
                  <span class="text-xs text-muted-foreground">{{ sensor.label }}</span>
                  <span class="text-sm" [class]="sensor.value ? 'font-medium' : 'text-muted-foreground'">
                    {{ sensor.value ?? 'No data' }}
                  </span>
                </div>
              }
            </div>
          </div>
        }
        
        <!-- Actions Slot -->
        <ng-content select="[slot=actions]"></ng-content>
      </div>
    </div>
  `,
})
export class DeviceCardComponent {
  public readonly node = input.required<Node>();
  public readonly showSensors = input<boolean>(true);
  public readonly userClass = input<string>('', { alias: 'class' });

  public readonly cardClick = output<Node>();

  // Computed properties
  protected deviceName = computed(() => 
    this.node().name || this.node().node_id || 'Unknown Device'
  );

  protected zoneName = computed(() => 
    this.node().zone_id || 'Unassigned'
  );

  protected deviceStatus = computed((): DeviceStatus => {
    const status = this.node().status;
    if (status === 'online' || status === 'offline' || status === 'pairing' || status === 'error') {
      return status;
    }
    return 'unknown';
  });

  protected batteryPercent = computed(() => {
    const battery = this.node().battery_percent;
    if (typeof battery === 'number') {
      return Math.max(0, Math.min(100, battery));
    }
    return null;
  });

  protected batteryDisplay = computed(() => {
    const percent = this.batteryPercent();
    return percent !== null ? `${percent}%` : 'No data';
  });

  protected batteryBarClass = computed(() => {
    const percent = this.batteryPercent();
    if (percent === null) return 'bg-muted-foreground/30';
    if (percent > 70) return 'bg-green-500';
    if (percent > 30) return 'bg-amber-500';
    return 'bg-destructive';
  });

  protected batteryTextClass = computed(() => {
    const percent = this.batteryPercent();
    if (percent === null) return 'text-muted-foreground';
    if (percent > 70) return 'text-green-500 font-medium';
    if (percent > 30) return 'text-amber-500 font-medium';
    return 'text-destructive font-medium';
  });

  protected brightnessDisplay = computed(() => {
    const brightness = this.node().brightness;
    return brightness !== undefined ? `${brightness}%` : 'No data';
  });

  protected lastSeenDisplay = computed(() => {
    const lastSeen = this.node().last_seen;
    if (!lastSeen) return 'No data';
    
    const date = typeof lastSeen === 'string' ? new Date(lastSeen) : lastSeen;
    if (isNaN(date.getTime())) return 'No data';

    const diffMinutes = Math.floor((Date.now() - date.getTime()) / 60000);
    if (diffMinutes < 1) return 'Just now';
    if (diffMinutes === 1) return '1 min ago';
    if (diffMinutes < 60) return `${diffMinutes} mins ago`;

    const hours = Math.floor(diffMinutes / 60);
    if (hours === 1) return '1 hour ago';
    if (hours < 24) return `${hours}h ago`;

    const days = Math.floor(hours / 24);
    return days === 1 ? '1 day ago' : `${days}d ago`;
  });

  protected sensorReadings = computed(() => {
    const node = this.node();
    return [
      {
        label: 'Temperature',
        value: typeof node.temperature === 'number' 
          ? `${node.temperature.toFixed(1)}°C` 
          : null,
      },
      {
        label: 'Battery',
        value: typeof node.battery_voltage === 'number' 
          ? `${node.battery_voltage.toFixed(2)}V` 
          : null,
      },
    ];
  });

  protected _computedClass = computed(() => {
    const statusClass = this.deviceStatus() === 'online' 
      ? 'border-green-500/20' 
      : this.deviceStatus() === 'error' 
        ? 'border-destructive/20' 
        : '';
    return hlm('transition-all hover:shadow-md', statusClass, this.userClass());
  });
}
