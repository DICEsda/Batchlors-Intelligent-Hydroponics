# Sensor Telemetry Diagnostics

## Overview

The Sensor Telemetry page lets you query and visualize historical sensor data stored in MongoDB. It displays time-series charts for both reservoir sensors (pH, EC, water level, water temperature) and tower sensors (ambient temperature, humidity, light level). Use this page to inspect raw sensor data, validate firmware readings, and spot anomalies over time.

**Route:** `/digital-twin/diagnostics/sensors`  
**Breadcrumb:** Hydroponic Farm > Digital Twin > Diagnostics > Sensors

## What It Shows

### Reservoir Charts (requires Coordinator ID)

| Chart | Unit | Optimal Range |
|-------|------|---------------|
| **pH Level** | pH | 5.5 - 6.5 (highlighted band) |
| **EC (Electrical Conductivity)** | mS/cm | 1.0 - 2.5 (highlighted band) |
| **Water Level** | % | 0 - 100 |
| **Water Temperature** | Celsius | -- |

### Tower Charts (requires Tower ID)

| Chart | Unit |
|-------|------|
| **Ambient Temperature** | Celsius |
| **Humidity** | % (0-100) |
| **Light Level** | lux |

Each chart shows a smooth time-series line with gradient fill, tooltips on hover, and auto-scaled axes.

## How to Use

1. **Navigate** to the page via sidebar: **Digital Twin > Diagnostics > Sensors**.
2. **Fill in the filter panel:**
   - **Farm ID** — Your farm identifier (defaults to `farm-001`).
   - **Coordinator ID** — Required. Enter the coordinator you want to inspect (e.g., `coord-001`).
   - **Tower ID** — Optional. Enter a tower ID to also load tower sensor charts.
   - **Time Range** — Select how far back to query: 15 min, 30 min, 1 hour, 6 hours, or 24 hours.
3. **Click "Load Data"** to fetch telemetry from the backend.
4. Charts render automatically once data arrives.
5. **Change the time range** or IDs and click "Load Data" again to refresh.

### Tips
- If you only enter a Coordinator ID (no Tower ID), only reservoir charts appear.
- The data point count badge shows how many readings were returned.
- The "Last updated" timestamp in the header shows when the last fetch completed.
- If no data is found, an empty state message tells you which ID and time range returned nothing.

## Error Handling

- If the backend returns an error, a red error banner appears with a **Retry** button.
- Reservoir and tower errors are shown separately so you can see which query failed.

## Prerequisites

- The **backend** must be running (port 8000).
- **MongoDB** must be running (port 27017) with telemetry data stored.
- Real MQTT telemetry must have been flowing (from firmware or simulator) to have historical data.
- You need to know valid Coordinator IDs and Tower IDs from your farm.

## Data Flow

```
ESP32 (sensors)  -->  MQTT  -->  Backend (MQTT Handler)  -->  MongoDB
                                                                  |
                                                                  v
                                                          REST API query
                                                                  |
                                                                  v
                                                     TelemetryHistoryService
                                                                  |
                                                                  v
                                                     This component (ECharts)
```

## Technical Details

- **Component:** `DiagnosticsSensorsComponent` (standalone, lazy-loaded)
- **Service:** `TelemetryHistoryService` — fetches historical telemetry via REST
  - `GET /api/telemetry/reservoir?coordId=...&farmId=...&minutes=...`
  - `GET /api/telemetry/tower?towerId=...&farmId=...&coordId=...&minutes=...`
- **Charts:** ECharts via `ngx-echarts`
- **Reactive effects:** Changing the Coordinator ID or Tower ID signal automatically triggers a re-fetch
- **Models:** `ReservoirTelemetry` and `TowerTelemetry` from `telemetry.model.ts`
