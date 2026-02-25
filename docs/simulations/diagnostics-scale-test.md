# Scale Test

## Overview

The Scale Test page lets you record and analyze backend performance under simulated load. You start a recording, let the simulator push MQTT messages for a set number of towers, then stop to capture a snapshot of throughput, latency, and error metrics. Multiple test runs are saved so you can compare results across different tower counts and configurations.

**Route:** `/digital-twin/diagnostics/scale-test`  
**Breadcrumb:** Hydroponic Farm > Digital Twin > Diagnostics > Scale Test

## What It Shows

### Recording Controls
- **Tower Count** — A configurable input (default: 250) specifying how many towers the simulator is emulating.
- **Start Recording** — Begins capturing diagnostics snapshots from the WebSocket stream.
- **Stop Recording** — Ends the capture, packages all collected snapshots into a `ScaleTestResult`, and displays the analysis.
- **Reset Counters** — Resets backend cumulative counters (`POST /api/diagnostics/reset`) for a clean baseline before starting.

### Test Results List
Each completed test run shows:
- Tower count
- Duration of the recording
- Timestamp of when it was captured

Click a result to view its detailed charts.

### Detail Charts (for the selected result)

| Chart | Description |
|-------|-------------|
| **Throughput** | Messages/sec over the recording period (total, tower, reservoir) |
| **Latency** | Handler, MongoDB, Twin Upsert, and WS Broadcast latency over time |
| **Errors** | Error counts over the recording period (mongo write errors, processing errors) |

## How to Use

### Step-by-step workflow

1. **Navigate** to the page via sidebar: **Digital Twin > Diagnostics > Scale Test**.
2. **Set the tower count** to match the number of simulated towers you plan to run.
3. **(Optional) Click "Reset Counters"** to zero the backend metrics for a clean baseline.
4. **Start the simulator** (separate process) with the matching tower count. The simulator publishes MQTT messages that flow through the backend pipeline.
5. **Click "Start Recording"** — the page begins collecting every `diagnostics_update` WebSocket snapshot.
6. **Wait** for the desired test duration (e.g., 30 seconds, 1 minute).
7. **Click "Stop Recording"** — the recording ends and a new test result appears in the list, automatically selected for viewing.
8. **Inspect the charts** to analyze throughput, latency, and errors during the test window.
9. **Repeat** with different tower counts to compare scaling behavior.

### Comparing Results
- All test results are kept in memory during the session.
- Click any previous result in the list to switch the charts to that run.
- Delete individual results by clicking the trash icon.

## When to Use This Page

- **Before a demo** — Verify the system handles the expected number of towers without errors.
- **Capacity planning** — Find the tower count at which latency spikes or errors appear.
- **Regression testing** — After backend changes, re-run scale tests to confirm no performance degradation.
- **Thesis benchmarking** — Capture throughput/latency data for different tower counts to include in your thesis write-up.

## Prerequisites

- The **backend** must be running (port 8000).
- The **WebSocket** connection must be active (diagnostics snapshots arrive via WS).
- The **MQTT simulator** must be available to generate load. Start it separately with the matching tower count.
- **MongoDB** and **Mosquitto** must be running.

## Data Flow

```
Simulator (N towers)  -->  MQTT (Mosquitto)  -->  Backend (MQTT Handler)
                                                        |
                                                        v
                                                  Diagnostics metrics
                                                        |
                                                        v
                                                  WebSocket (diagnostics_update)
                                                        |
                                                        v
                                                  DiagnosticsService (frontend)
                                                        |
                                                  [recording buffer]
                                                        |
                                                        v
                                                  ScaleTestResult
                                                        |
                                                        v
                                                  This component (ECharts)
```

## Technical Details

- **Component:** `DiagnosticsScaleTestComponent` (standalone, lazy-loaded)
- **Service:** `DiagnosticsService` — root-scoped, manages recording state
  - `startRecording()` — begins buffering incoming `diagnostics_update` snapshots
  - `stopRecording(towerCount)` — stops buffering, packages snapshots into a `ScaleTestResult`
  - `resetCounters()` — calls `POST /api/diagnostics/reset`
- **Model:** `ScaleTestResult` from `diagnostics.model.ts` — contains the tower count, duration, timestamp, and the array of `SystemMetricsSnapshot` captured during the recording
- **Charts:** ECharts via `ngx-echarts` — throughput, latency, and error charts built from the snapshot array
- **State:** Test results are held in memory (signals) and lost on page reload
