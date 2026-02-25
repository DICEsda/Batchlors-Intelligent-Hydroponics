# Digital Twin

## Overview

The Digital Twin page provides a real-time virtual representation of your entire hydroponic farm. It displays the live state of every coordinator and its connected towers, including sensor readings, connection status, sync state, and ML-driven predictions — all sourced from the backend's twin service.

**Route:** `/digital-twin`  
**Breadcrumb:** Hydroponic Farm > Digital Twin

## What It Shows

### Coordinator Twins
Each registered coordinator appears as a collapsible card showing:

| Field | Description |
|-------|-------------|
| **Name / ID** | Coordinator display name and unique ID |
| **Connection Status** | Online/Offline badge (green = connected) |
| **Sync Status** | In Sync, Pending, Stale, Conflict, or Offline |
| **pH** | Current nutrient solution pH level |
| **EC** | Electrical conductivity in mS/cm |
| **Water Temperature** | Reservoir water temperature in Celsius |
| **Water Level** | Reservoir water level as a percentage |
| **Tower Count** | Number of towers connected to this coordinator |

### Tower Twins (nested under each Coordinator)
Expand a coordinator to see its towers, each displaying:

| Field | Description |
|-------|-------------|
| **Air Temperature** | Ambient temperature in Celsius |
| **Humidity** | Relative humidity percentage |
| **Light Level** | Light intensity in lux |
| **Battery Voltage** | Battery level in millivolts |
| **Health Score** | ML-predicted plant health (0-100%) |
| **Crop Type** | What's growing in this tower |
| **Days to Harvest** | ML-predicted remaining days until harvest |

## How to Use

1. **Navigate** to `/digital-twin` via the sidebar under **Projects > Digital Twin**.
2. The page automatically resolves your farm ID and loads all twins on init.
3. **Click a coordinator row** to expand/collapse its detail view and tower list.
4. **Click "Refresh"** (top-right) to re-fetch the latest twin state from the backend.
5. Twin state updates arrive in real-time via WebSocket (`digital_twin_update` messages), so the page stays current without manual refresh.

## Prerequisites

- The **backend** must be running and healthy (port 8000).
- At least one coordinator must be registered with the farm.
- The **WebSocket** connection must be active for real-time updates.
- The **ML API** (port 8001) must be running for health scores and harvest predictions to appear.

## Data Flow

```
ESP32 Firmware  -->  MQTT (Mosquitto)  -->  Backend (MQTT Handler)
                                               |
                                               v
                                         MongoDB (persist)
                                               |
                                               v
                                         Twin Service (upsert)
                                               |
                                               v
                                         WebSocket broadcast  -->  Frontend (this page)
```

## Technical Details

- **Component:** `DigitalTwinComponent` (standalone, lazy-loaded)
- **Service:** `TwinService` — loads farm twins via `GET /api/twins/farm/:farmId`
- **WebSocket event:** `digital_twin_update` — pushes incremental state changes
- **Farm ID resolution:** Tries `IoTDataService.sites()` first, then `loadDashboardData()`, then falls back to `GET /api/farms`
