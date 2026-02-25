# Remove Mock Data & Redesign System Status Panel

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove all mock data from the frontend so it relies entirely on real backend data (populated by simulations), fix the CORS preflight bug, align the frontend HealthStatus model with the backend, and redesign the sidebar system status panel to show Backend, MongoDB, MQTT Broker, ML Pipeline, Simulator status plus Coordinator/Tower counts. Move the WebSocket indicator to a subtle header icon that grays out the entire status panel when offline.

**Architecture:** The frontend currently falls back to hardcoded mock data when the backend is unavailable. We remove this entirely — on backend failure, pages show error/empty states. The sidebar system status panel is redesigned with 5 service rows + 2 device count rows. The backend health endpoint already returns `database` and `mqtt_connected` fields; we add a new `getMlHealth()` API call for the ML pipeline. Simulator detection is inferred from coordinator/tower data freshness (auto-discovered farms). The WebSocket connection indicator moves from the sidebar to a subtle pulsing dot in the header; when WS is disconnected, the entire status panel grays out.

**Tech Stack:** Angular 19 (signals, standalone components), ASP.NET Core 8, Lucide icons, SCSS

---

## Task 1: Fix CORS Preflight Bug (Backend)

**Files:**
- Modified (already done): `backend/src/IoT.Backend/Middleware/ApiKeyMiddleware.cs`
- Modified (already done): `backend/src/IoT.Backend/Program.cs`

The CORS preflight fix was already applied in the previous session:
1. `ApiKeyMiddleware` now skips `OPTIONS` requests
2. `UseCors()` now runs before `ApiKeyMiddleware` in the pipeline

**Step 1: Verify the fix compiles**

Run: `dotnet build` in `backend/src/IoT.Backend/`
Expected: 0 errors, 2 pre-existing warnings

**Step 2: Rebuild backend Docker container**

Run: `docker compose up -d --build backend`
Expected: Container rebuilds and starts healthy

**Step 3: Test CORS preflight**

```bash
curl -v -X OPTIONS \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: X-API-Key" \
  http://localhost:8000/api/farms
```
Expected: HTTP 204 with `Access-Control-Allow-Origin: http://localhost:4200` header (not 401)

**Step 4: Test actual API call with CORS**

```bash
curl -s -H "Origin: http://localhost:4200" -H "X-API-Key: hydro-thesis-2026" http://localhost:8000/api/farms
```
Expected: 200 with JSON response + `Access-Control-Allow-Origin` header

---

## Task 2: Align Frontend HealthStatus Model with Backend

**Files:**
- Modify: `frontend/src/app/core/models/common.model.ts` (lines 39-51)
- Modify: `frontend/src/app/core/services/api.service.ts` (line 103-105)

The backend returns a flat `{ status, mqtt_connected, mqtt, database, coordinator, timestamp, message }` but the frontend expects `{ status, service, version, uptime, checks: { database, mqtt }, timestamp }`.

**Step 1: Update the HealthStatus interface**

In `common.model.ts`, replace the `HealthStatus` interface with:

```typescript
export interface HealthStatus {
  status: string;
  mqtt_connected: boolean;
  mqtt: boolean;
  database: boolean;
  coordinator: boolean;
  timestamp: Date;
  message?: string;
}
```

Remove the unused `ServiceHealth` type if it's only used by `HealthStatus`.

**Step 2: Add MlHealthStatus interface**

Add below `HealthStatus`:

```typescript
export interface MlHealthStatus {
  status: string;
  version: string;
  mongodb_connected: boolean;
  mqtt_connected: boolean;
  models_loaded: string[];
  uptime_seconds: number;
}
```

**Step 3: Add getMlHealth() to ApiService**

In `api.service.ts`, add:

```typescript
getMlHealth(): Observable<MlHealthStatus> {
  return this.get<MlHealthStatus>('/api/ml/health');
}
```

**Step 4: Update any code consuming the old HealthStatus shape**

Search for `healthStatus()?.checks` or `healthStatus()?.service` — these need to be updated to use the flat structure. The sidebar's `checkBackendHealth()` only uses success/failure of the call, so it doesn't need changes. The `IoTDataService.checkHealth()` stores the result in `healthStatus` signal — consumers of that signal need updating.

---

## Task 3: Remove MockDataService and All Mock Fallbacks

**Files:**
- Delete: `frontend/src/app/core/services/mock-data.service.ts`
- Modify: `frontend/src/app/core/services/iot-data.service.ts` — remove all mock fallback logic
- Modify: `frontend/src/app/core/services/alert.service.ts` — remove `MOCK_ALERTS` and mock fallback
- Modify: `frontend/src/app/core/services/ota.service.ts` — remove `MOCK_*` constants and mock fallback
- Modify: `frontend/src/app/core/services/index.ts` — remove mock-data export
- Modify: 10 page components — remove `usingMockData` signal references

### Step 1: Remove MockDataService

Delete `mock-data.service.ts`. Remove its export from `core/services/index.ts`.

### Step 2: Rewrite IoTDataService.loadDashboardData()

Remove the try/catch mock fallback. The new version:

```typescript
async loadDashboardData(): Promise<void> {
  this.isLoading.set(true);
  this.error.set(null);

  try {
    await this.checkHealth();

    await Promise.all([
      this.loadCoordinators().catch(err => {
        console.warn('Failed to load coordinators:', err);
        this.coordinators.set([]);
      }),
      this.loadNodes().catch(err => {
        console.warn('Failed to load nodes:', err);
        this.nodes.set([]);
      }),
      this.loadAlerts().catch(err => {
        console.warn('Failed to load alerts:', err);
        this.alerts.set([]);
      }),
    ]);
  } catch (err) {
    console.error('Backend unavailable:', err);
    this.error.set('Backend unavailable. Please check if the server is running.');
  } finally {
    this.isLoading.set(false);
  }
}
```

### Step 3: Remove `usingMockData` signal from IoTDataService

Delete the `usingMockData = signal(false)` declaration and all references to it (in `loadDashboardData`, `loadMockData`, `startAutoRefresh`, `loadZones`).

Delete the `loadMockData()` method entirely.

Remove the `MockDataService` import and injection.

### Step 4: Simplify startAutoRefresh()

Remove all mock-aware branching. The auto-refresh should just call real API endpoints:

```typescript
startAutoRefresh(): void {
  this.refreshInterval$.pipe(
    takeUntil(this.destroy$),
    switchMap(() => this.api.getCoordinators()),
    tap(data => {
      if (Array.isArray(data)) {
        this.coordinators.set(data);
      }
    }),
    switchMap(() => this.api.getNodes()),
    tap(data => this.nodes.set(data)),
    switchMap(() => this.api.getAlerts({ page: 1, pageSize: 50 })),
    tap(data => {
      if (Array.isArray(data)) {
        this.alerts.set(data);
      } else {
        this.alerts.set(data.items);
      }
    }),
    catchError(err => {
      console.error('Auto-refresh failed:', err);
      return of(null);
    })
  ).subscribe();
}
```

### Step 5: Remove mock data from AlertService

In `alert.service.ts`:
- Delete the `MOCK_ALERTS` constant (lines ~16-177)
- Remove the `usingMockData` signal
- In `loadAlerts()`: remove the fallback to `MOCK_ALERTS` — on error, set `this.alerts.set([])` and `this.error.set('Failed to load alerts')`
- In mutation methods (`acknowledgeAlert`, `resolveAlert`, etc.): remove the `if (this.usingMockData())` branches — always call the real API

### Step 6: Remove mock data from OtaService

In `ota.service.ts`:
- Delete `MOCK_FIRMWARE_VERSIONS`, `MOCK_DEVICE_STATUS`, `MOCK_OTA_JOBS` constants
- Remove the `usingMockData` signal
- In `loadAllData()`: remove mock fallback — on error, set empty arrays + error signal
- In `startUpdate()` and `cancelJob()`: remove mock-mode branches
- Delete `simulateOtaProgress()` method

### Step 7: Remove usingMockData from page components

In each of these 10 files, remove the `usingMockData` readonly signal and any template references:
1. `pages/farm-overview/farm-overview.component.ts`
2. `pages/node-detail/node-detail.component.ts`
3. `pages/coordinator-detail/coordinator-detail.component.ts`
4. `pages/towers-list/towers-list.component.ts`
5. `pages/reservoirs-list/reservoirs-list.component.ts`
6. `pages/farms-list/farms-list.component.ts`
7. `pages/alerts-dashboard/alerts-dashboard.component.ts`
8. `pages/alerts/alerts.component.ts`
9. `pages/ota-dashboard/ota-dashboard.component.ts`
10. `components/layout/header/header.component.ts` + `.html` (remove the mock data badge)

Also search `.html` templates for `usingMockData` and remove those template bindings.

---

## Task 4: Redesign System Status Panel in Sidebar

**Files:**
- Modify: `frontend/src/app/components/layout/sidebar/sidebar.component.ts`
- Modify: `frontend/src/app/components/layout/sidebar/sidebar.component.html`
- Modify: `frontend/src/app/components/layout/sidebar/sidebar.component.scss`
- Modify: `frontend/src/app/core/services/api.service.ts` (add getMlHealth)

### Step 1: Add new status signals to sidebar component

In `sidebar.component.ts`, add these signals:

```typescript
// Service status signals
readonly backendConnected = signal<boolean>(false);
readonly mongodbConnected = signal<boolean>(false);
readonly mqttConnected = signal<boolean>(false);
readonly mlPipelineHealthy = signal<boolean>(false);
readonly simulatorActive = signal<boolean>(false);

// Device counts (existing, keep as-is)
readonly onlineCoordinators = this.dataService.onlineCoordinatorCount;
readonly totalCoordinators = this.dataService.totalCoordinatorCount;
readonly onlineNodes = this.dataService.onlineNodeCount;
readonly totalNodes = this.dataService.totalNodeCount;

// WebSocket connection state (moved to header, but still used for gray-out)
readonly wsConnected = this.wsService.connected;
```

### Step 2: Update checkBackendHealth() to extract MongoDB, MQTT, Coordinator status

```typescript
private async checkBackendHealth(): Promise<void> {
  try {
    await this.dataService.checkHealth();
    const health = this.dataService.healthStatus();

    this.backendConnected.set(true);
    this.mongodbConnected.set(health?.database ?? false);
    this.mqttConnected.set(health?.mqtt_connected ?? false);
    // Simulator is active if any coordinator has heartbeated recently
    this.simulatorActive.set(health?.coordinator ?? false);
  } catch {
    this.backendConnected.set(false);
    this.mongodbConnected.set(false);
    this.mqttConnected.set(false);
    this.simulatorActive.set(false);
  }
}
```

### Step 3: Add ML health check

```typescript
private async checkMlHealth(): Promise<void> {
  try {
    const health = await firstValueFrom(this.api.getMlHealth());
    this.mlPipelineHealthy.set(health.status === 'healthy');
  } catch {
    this.mlPipelineHealthy.set(false);
  }
}
```

Call it in `ngOnInit()` alongside `checkBackendHealth()`, on the same 30s interval.

### Step 4: Rewrite system status HTML

Replace the system status section in `sidebar.component.html` (lines 196-256) with:

```html
<div class="system-status" [class.ws-offline]="!wsConnected()">
  @if (!collapsed()) {
    <span class="status-section-title">System Status</span>
  }

  <!-- Backend -->
  <div class="status-item" [title]="collapsed() ? ('Backend: ' + (backendConnected() ? 'Online' : 'Offline')) : ''">
    <div class="status-indicator" [class.online]="backendConnected()" [class.offline]="!backendConnected()">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideServer" size="14" />
    @if (!collapsed()) {
      <span class="status-label">Backend</span>
      <span class="status-value" [class.online]="backendConnected()">
        {{ backendConnected() ? 'Online' : 'Offline' }}
      </span>
    }
  </div>

  <!-- MongoDB -->
  <div class="status-item" [title]="collapsed() ? ('MongoDB: ' + (mongodbConnected() ? 'Connected' : 'Offline')) : ''">
    <div class="status-indicator" [class.online]="mongodbConnected()" [class.offline]="!mongodbConnected()">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideDatabase" size="14" />
    @if (!collapsed()) {
      <span class="status-label">MongoDB</span>
      <span class="status-value" [class.online]="mongodbConnected()">
        {{ mongodbConnected() ? 'Connected' : 'Offline' }}
      </span>
    }
  </div>

  <!-- MQTT Broker -->
  <div class="status-item" [title]="collapsed() ? ('MQTT: ' + (mqttConnected() ? 'Connected' : 'Offline')) : ''">
    <div class="status-indicator" [class.online]="mqttConnected()" [class.offline]="!mqttConnected()">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideRadio" size="14" />
    @if (!collapsed()) {
      <span class="status-label">MQTT Broker</span>
      <span class="status-value" [class.online]="mqttConnected()">
        {{ mqttConnected() ? 'Connected' : 'Offline' }}
      </span>
    }
  </div>

  <!-- ML Pipeline -->
  <div class="status-item" [title]="collapsed() ? ('ML: ' + (mlPipelineHealthy() ? 'Healthy' : 'Offline')) : ''">
    <div class="status-indicator" [class.online]="mlPipelineHealthy()" [class.offline]="!mlPipelineHealthy()">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideBrain" size="14" />
    @if (!collapsed()) {
      <span class="status-label">ML Pipeline</span>
      <span class="status-value" [class.online]="mlPipelineHealthy()">
        {{ mlPipelineHealthy() ? 'Healthy' : 'Offline' }}
      </span>
    }
  </div>

  <!-- Simulator -->
  <div class="status-item" [title]="collapsed() ? ('Simulator: ' + (simulatorActive() ? 'Active' : 'Inactive')) : ''">
    <div class="status-indicator" [class.online]="simulatorActive()" [class.offline]="!simulatorActive()">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideFlaskConical" size="14" />
    @if (!collapsed()) {
      <span class="status-label">Simulator</span>
      <span class="status-value" [class.online]="simulatorActive()">
        {{ simulatorActive() ? 'Active' : 'Inactive' }}
      </span>
    }
  </div>

  <!-- Separator -->
  @if (!collapsed()) {
    <div class="status-separator"></div>
  }

  <!-- Coordinators -->
  <div class="status-item" [title]="collapsed() ? ('Coordinators: ' + onlineCoordinators() + '/' + totalCoordinators()) : ''">
    <div class="status-indicator" [class.online]="onlineCoordinators() > 0" [class.offline]="onlineCoordinators() === 0">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideCpu" size="14" />
    @if (!collapsed()) {
      <span class="status-label">Coordinators</span>
      <span class="status-value" [class.online]="onlineCoordinators() > 0">
        {{ onlineCoordinators() }}/{{ totalCoordinators() }}
      </span>
    }
  </div>

  <!-- Towers -->
  <div class="status-item" [title]="collapsed() ? ('Towers: ' + onlineNodes() + '/' + totalNodes()) : ''">
    <div class="status-indicator" [class.online]="onlineNodes() > 0" [class.offline]="onlineNodes() === 0">
      <span class="status-dot"></span>
    </div>
    <ng-icon name="lucideLightbulb" size="14" />
    @if (!collapsed()) {
      <span class="status-label">Towers</span>
      <span class="status-value" [class.online]="onlineNodes() > 0">
        {{ onlineNodes() }}/{{ totalNodes() }}
      </span>
    }
  </div>
</div>
```

### Step 5: Add SCSS for gray-out when WS offline + separator

```scss
// When WebSocket is offline, gray out entire status panel
.system-status.ws-offline {
  opacity: 0.4;
  pointer-events: none;

  .status-dot {
    background-color: oklch(0.55 0 0) !important;
    opacity: 0.3 !important;
  }

  .status-value {
    color: oklch(0.55 0 0) !important;
  }
}

.status-separator {
  height: 1px;
  background-color: var(--sidebar-border);
  margin: 4px 8px;
}
```

### Step 6: Add new Lucide icons

In sidebar component `provideIcons()`, add:
- `lucideRadio` (for MQTT broker)
- `lucideFlaskConical` (for Simulator)

Import them from `@ng-icons/lucide`.

---

## Task 5: Move WebSocket Indicator to Header

**Files:**
- Modify: `frontend/src/app/components/layout/header/header.component.ts`
- Modify: `frontend/src/app/components/layout/header/header.component.html`
- Modify: `frontend/src/app/components/layout/header/header.component.scss`

### Step 1: Replace the current "Online/Offline" badge with a subtle WS dot

In header template, replace the status badges section:

```html
<!-- Status Badges -->
<div class="status-badges">
  <!-- Subtle WebSocket indicator -->
  <div class="ws-indicator" [class.connected]="wsConnected()" [title]="wsConnected() ? 'Real-time connection active' : 'Real-time connection lost'">
    <span class="ws-dot"></span>
  </div>
  <button hlmBtn variant="outline" size="sm" (click)="refreshData()" class="refresh-btn">
    <ng-icon hlmIcon name="lucideRefreshCw" size="sm" />
  </button>
</div>
```

Remove the `usingMockData` badge and the `app-status-badge` for connection status. Remove the Test Toast button (or keep it if desired for development).

### Step 2: Add WS indicator styles

```scss
.ws-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  cursor: default;

  .ws-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background-color: oklch(0.55 0 0);
    opacity: 0.4;
    transition: all 0.3s ease;
  }

  &.connected .ws-dot {
    background-color: oklch(0.7 0.15 145); // Subtle green
    opacity: 0.8;
    box-shadow: 0 0 4px oklch(0.7 0.15 145 / 0.3);
    animation: ws-pulse 3s ease-in-out infinite;
  }
}

@keyframes ws-pulse {
  0%, 100% { opacity: 0.8; }
  50% { opacity: 0.5; }
}
```

### Step 3: Remove usingMockData from header component

In `header.component.ts`, remove:
- `readonly usingMockData = this.dataService.usingMockData;`
- The `StatusBadgeComponent` import (if only used for mock data badge)

---

## Task 6: Enable All Real API Calls in loadDashboardData

**Files:**
- Modify: `frontend/src/app/core/services/iot-data.service.ts`

The current `loadDashboardData()` only loads coordinators and sets empty arrays for everything else. We need to actually call the real endpoints.

### Step 1: Uncomment and wire real API calls

The backend has these working endpoints:
- `GET /api/coordinators` → coordinators list
- `GET /api/nodes` → towers/nodes list (NodesController wraps TowerRepository)
- `GET /api/alerts` → paginated alerts
- `GET /api/farms` → farms list
- `GET /api/zones` → zones list

Wire all of them in `loadDashboardData()`:

```typescript
await Promise.all([
  this.loadCoordinators().catch(err => {
    console.warn('Failed to load coordinators:', err);
    this.coordinators.set([]);
  }),
  this.loadNodes().catch(err => {
    console.warn('Failed to load nodes:', err);
    this.nodes.set([]);
  }),
  this.loadAlerts().catch(err => {
    console.warn('Failed to load alerts:', err);
    this.alerts.set([]);
  }),
  this.loadSites().catch(err => {
    console.warn('Failed to load farms:', err);
    this.sites.set([]);
  }),
  this.loadZones().catch(err => {
    console.warn('Failed to load zones:', err);
    this.zones.set([]);
  }),
]);
```

### Step 2: Remove the hardcoded empty array assignments

Remove these lines from `loadDashboardData()`:
```typescript
// Set empty arrays for unimplemented endpoints
this.sites.set([]);
this.nodes.set([]);
this.zones.set([]);
this.alerts.set([]);
```

### Step 3: Fix loadZones() mock fallback

The `loadZones()` method has its own mock fallback. Remove it — on error, just log and set empty array.

---

## Task 7: Build Verification & Docker Test

**Step 1: Build frontend**

```bash
cd Frontend && npx ng build --configuration production
```
Expected: 0 errors

**Step 2: Build backend**

```bash
cd backend/src/IoT.Backend && dotnet build
```
Expected: 0 errors

**Step 3: Rebuild and restart Docker stack**

```bash
docker compose up -d --build
```
Wait for all 5 containers to be healthy.

**Step 4: Test in browser**

Open `http://localhost:4200` and verify:
1. No console errors about mock data
2. System status panel shows all 5 service rows + 2 device counts
3. Backend, MongoDB, MQTT show correct status from health endpoint
4. ML Pipeline status shows (healthy if ml-api container is running)
5. WebSocket indicator is a subtle dot in the header
6. When WS is connected, dot is green pulsing
7. Dashboard shows real data from backend (farms, coordinators, towers from previous simulation runs)

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: remove mock data, redesign system status panel, fix CORS preflight"
```
