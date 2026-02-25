# System Diagnostics

## Overview

The System Diagnostics page provides real-time visibility into the backend message processing pipeline. It shows live throughput, latency, error counts, and WebSocket client metrics — updated every 2 seconds via WebSocket. Use this page to monitor backend health and identify performance bottlenecks.

**Route:** `/digital-twin/diagnostics/system`  
**Breadcrumb:** Hydroponic Farm > Digital Twin > Diagnostics > System

## What It Shows

### Stat Cards (top row)

| Card | Description |
|------|-------------|
| **Messages/sec** | Current message throughput (total, tower, reservoir breakdown) |
| **Total Messages** | Cumulative message count since last reset (tower + reservoir) |
| **Avg Latency** | Average end-to-end handler pipeline latency in milliseconds |
| **P95 Latency** | 95th percentile handler latency (with P99 shown below) |
| **WS Clients** | Number of active WebSocket connections to the backend |
| **Errors** | Total errors (MongoDB write errors + processing errors) |

### Time-Series Charts

1. **Throughput Chart** — Messages processed per second over time, with separate lines for Total, Tower, and Reservoir message rates.
2. **Latency Breakdown Chart** — Average per-component latency over time, broken down into: Handler (total), MongoDB write, Twin Upsert, and WebSocket Broadcast.

### Pipeline Breakdown Table

A detailed table showing per-component latency from the latest snapshot:

| Column | Description |
|--------|-------------|
| **Component** | Handler (total), MongoDB Write, Twin Upsert, WS Broadcast |
| **Avg (ms)** | Average latency |
| **Max (ms)** | Maximum latency observed |
| **P95 (ms)** | 95th percentile latency |
| **P99 (ms)** | 99th percentile latency |

## How to Use

1. **Navigate** to the page via sidebar: **Digital Twin > Diagnostics > System**.
2. The page connects to WebSocket automatically and begins receiving `diagnostics_update` snapshots every 2 seconds.
3. **Watch the stat cards** for current throughput and latency at a glance.
4. **Watch the charts** as they build up a time-series history showing trends.
5. **Click "Reset Counters"** (top-right) to zero out all cumulative counters on the backend. This calls `POST /api/diagnostics/reset`. The next WebSocket snapshot will reflect the reset.

### Connection Status Badge
- **Live** (green) — WebSocket connected, data flowing
- **Connecting** (yellow) — Attempting to connect
- **Disconnected** (red) — No WebSocket connection; data is stale

## When to Use This Page

- **During development** — Verify that MQTT messages flow through the pipeline correctly.
- **Load testing** — Monitor throughput and latency while the simulator is running.
- **Debugging** — Identify which pipeline stage (MongoDB, Twin, WS) is the bottleneck.
- **Health checks** — Confirm zero errors and acceptable latency before demos.

## Prerequisites

- The **backend** must be running (port 8000).
- The **WebSocket** connection must be active.
- MQTT messages must be flowing (from real firmware or the simulator) to see non-zero metrics.

## Data Flow

```
Backend Pipeline Metrics  -->  DiagnosticsService (backend)
                                      |
                                      v
                               WebSocket (diagnostics_update every 2s)
                                      |
                                      v
                               DiagnosticsService (frontend, root-scoped)
                                      |
                                      v
                               This component (signals + ECharts)
```

## Technical Details

- **Component:** `DiagnosticsSystemComponent` (standalone, lazy-loaded)
- **Service:** `DiagnosticsService` — root-scoped, subscribes to `diagnostics_update` WebSocket messages
- **REST endpoints used:**
  - `POST /api/diagnostics/reset` — reset cumulative counters
- **Charts:** ECharts via `ngx-echarts`
- **History buffer:** The service maintains a rolling window of snapshots for chart rendering
