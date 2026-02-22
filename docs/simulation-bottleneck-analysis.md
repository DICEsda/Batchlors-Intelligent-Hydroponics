# Simulation-Driven Bottleneck Analysis for IoT Hydroponics

## 1. Introduction

This document describes the methodology and architecture for identifying performance bottlenecks in the Intelligent Hydroponics IoT platform through controlled simulation and quantitative instrumentation. Rather than relying on qualitative assessment ("it feels slow"), this approach produces measurable, reproducible evidence of where the system's processing pipeline saturates under load.

The central question is: **given N hydroponic towers publishing telemetry at interval T, at what point does the system fail to process messages in real time, and which component is responsible?**

## 2. Motivation

### 2.1 Why Simulation

The system is designed for real ESP32 hardware (coordinators and tower nodes) communicating over MQTT. However, physical hardware introduces variables that make controlled performance testing impractical:

- Hardware availability: a full test requires hundreds of ESP32 devices.
- Environmental noise: WiFi interference, power fluctuations, and sensor drift are uncontrollable.
- Reproducibility: running the same test twice with physical devices yields different timing characteristics.

A software simulator eliminates these variables. It publishes MQTT messages on the exact same topics, with the exact same JSON payload structure, that the firmware would use. The downstream pipeline (MQTT broker, backend, MongoDB, WebSocket, frontend) cannot distinguish simulated telemetry from real telemetry. This allows controlled, reproducible load testing.

### 2.2 Why Bottleneck Analysis Matters

An IoT system's value depends on its ability to process sensor data in near-real-time. If the backend falls behind the ingestion rate, several failure modes emerge:

| Failure Mode | Consequence |
|---|---|
| **Message backlog** | MQTT broker queues grow unbounded; memory exhaustion or message drops follow. |
| **Stale digital twins** | The twin's reported state diverges from reality; control decisions are based on outdated data. |
| **Delayed anomaly detection** | A pH crisis or water emergency is detected minutes after it occurs, reducing response time. |
| **Dashboard lag** | The frontend displays old data; operators lose trust in the system. |

Identifying the specific component responsible for a throughput ceiling allows targeted optimization rather than speculative infrastructure changes (e.g., adding more MQTT brokers when the real bottleneck is MongoDB write latency).

## 3. System Architecture Under Test

The message processing pipeline is linear:

```
Simulator ──MQTT──> Mosquitto ──sub──> TelemetryHandler ──> MongoDB (time-series)
                                            |
                                            +──> TwinService (twin upsert)
                                            |
                                            +──> WsBroadcaster (WebSocket push)
                                            |
                                            +──> TwinChangeChannel (ADT/Ditto sync)
```

Each MQTT message triggers four downstream operations, three of which involve I/O:

1. **MongoDB time-series insert** (`InsertTowerTelemetryAsync` / `InsertReservoirTelemetryAsync`): persists the raw telemetry reading for historical analysis and ML training.
2. **MongoDB twin upsert** (`ProcessTowerTelemetryAsync` / `ProcessCoordinatorTelemetryAsync`): updates the digital twin's reported state, checks desired-vs-reported delta, updates metadata timestamps.
3. **WebSocket broadcast** (`BroadcastTowerTelemetryAsync`): serializes the telemetry update and sends it to every connected WebSocket client.
4. **Twin change event** (fire-and-forget into a bounded `Channel<TwinChangeEvent>`): triggers downstream sync to an external digital twin service (Azure DT or Eclipse Ditto).

The total per-message cost is approximately `T_deser + T_mongo_insert + T_twin_upsert + T_ws_broadcast + T_channel_write`.

### 3.1 Theoretical Throughput Ceiling

If the average per-message processing time is `P` milliseconds, the maximum sustainable throughput is:

```
max_msg_per_second = 1000 / P
```

For example, if `P = 5ms` (a reasonable estimate for two MongoDB operations plus a WebSocket broadcast), the ceiling is 200 messages/second. At 5-second telemetry intervals:

| Towers | Messages/tick | Messages/second | Fits within 200 msg/s ceiling? |
|---|---|---|---|
| 50 | 55 | 11 | Yes |
| 250 | 275 | 55 | Yes |
| 500 | 550 | 110 | Yes |
| 1000 | 1100 | 220 | **No -- backlog grows** |

The simulator's scale test scenario is designed to find this inflection point empirically.

## 4. Simulator Design

### 4.1 Topology

The simulator generates a deterministic farm hierarchy:

```
Default:  5 farms x 5 coordinators x 10 towers = 250 towers, 25 reservoirs
Scale:    10 farms x 10 coordinators x 10 towers = 1000 towers, 100 reservoirs
```

Each coordinator has one reservoir and up to 10 towers. Towers are assigned random crops from the system's crop catalog (lettuce, basil, tomato, etc.), each with different optimal pH/EC ranges and nutrient consumption rates.

### 4.2 Physics Engine

Sensor values are not random noise. They follow physically realistic models:

| Sensor | Model | Parameters |
|---|---|---|
| Air temperature | Sinusoidal day/night cycle | base=22C, amplitude=4C, peak at 14:00 |
| Humidity | Inverse correlation with temperature | base=70%, -1.5% per degree above 22C |
| Light (lux) | Scheduled grow light with ramp-up/down | 16h on (30,000 lux), 8h off, 30-min ramps |
| pH | Linear drift (acid accumulation) | -0.015 pH/hour (natural root exudate acidification) |
| EC (mS/cm) | Depletion proportional to crop demand | crop-specific rates: lettuce 0.02, tomato 0.05 mS/cm/h |
| Water level | Depletion proportional to tower count | 0.04-0.08 %/hour per tower depending on crop |
| Water temperature | Exponential approach to air temp | lag factor 0.15/hour (thermal mass of water) |
| Plant height | Sigmoid (logistic) growth curve | crop-specific max height and harvest days |

Gaussian noise (crop-appropriate standard deviations) is added to every reading. This ensures the ML pipeline receives realistic-looking data with appropriate variance, not obviously synthetic constant values.

### 4.3 Scenario Catalog

Eleven scenarios test different aspects of the system:

| # | Scenario | What it tests | Key observable |
|---|---|---|---|
| 1 | Steady state | Baseline "everything works" | All metrics stable |
| 2 | pH drift crisis | Anomaly detection, drift prediction | pH drops from 6.0 to 4.5 over 6h |
| 3 | Nutrient depletion | Consumption prediction | EC drops from 1.5 to 0.7 over 12h |
| 4 | Heat stress | Environmental anomaly detection | Temp spikes to 38C, humidity drops |
| 5 | Water emergency | Threshold alerting | Water level drops to 15%, pump stops |
| 6 | Tower pairing | Dynamic device onboarding | Towers join one by one during runtime |
| 7 | Crop conflict | ML clustering / compatibility | Incompatible crops share a reservoir |
| 8 | Growth cycle | Growth prediction accuracy | 30 days simulated in 30 minutes |
| 9 | Reconnection | Stale detection, offline recovery | 50% of coordinators disconnect for 5 min |
| 10 | Full demo | All-in-one demonstration | Cycles through scenarios 1-9 in 15 min |
| 11 | Scale test | **Throughput ceiling / bottleneck ID** | 1000 towers, max message rate |

Scenarios 1-9 validate functional correctness. Scenario 11 is specifically designed for performance analysis.

### 4.4 Bootstrap Protocol

The simulator follows the backend's registration protocol:

1. Create farms via `POST /api/v1/farms` (REST).
2. Announce coordinators via `coordinator/{mac}/announce` (MQTT).
3. Approve coordinators via `POST /api/coordinator-registration/register/approve` (REST).
4. Begin publishing telemetry on `farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry` and `farm/{farmId}/coord/{coordId}/reservoir/telemetry` (MQTT).

The backend's `TelemetryHandler` checks coordinator registration status before processing any message. Without the bootstrap, all telemetry is silently dropped.

## 5. Instrumentation Architecture

### 5.1 What Is Measured

Instrumentation is applied at four points in the message processing pipeline:

```
HandleTowerTelemetry(topic, payload)
{
    sw_total.Start()
    
    Deserialize(payload)
    
    sw_mongo.Start()
    InsertTowerTelemetryAsync(record)        // MongoDB time-series write
    sw_mongo.Stop()  --> diagnostics.RecordMongoWrite(elapsed)
    
    sw_twin.Start()
    ProcessTowerTelemetryAsync(id, state)    // MongoDB twin upsert
    sw_twin.Stop()   --> diagnostics.RecordTwinUpsert(elapsed)
    
    sw_ws.Start()
    BroadcastTowerTelemetryAsync(payload)    // WebSocket push
    sw_ws.Stop()     --> diagnostics.RecordWsBroadcast(elapsed)
    
    sw_total.Stop()  --> diagnostics.RecordTowerMessage(elapsed)
}
```

The `DiagnosticsService` singleton collects these measurements using lock-free `Interlocked` atomic operations to avoid introducing measurement overhead that would distort results.

### 5.2 Metrics Collected

**Counters** (monotonically increasing):

| Counter | Description |
|---|---|
| `tower_messages_total` | Tower telemetry messages processed |
| `reservoir_messages_total` | Reservoir telemetry messages processed |
| `mongodb_writes_total` | Successful MongoDB write operations |
| `mongodb_write_errors` | Failed MongoDB write operations |
| `websocket_broadcasts_total` | WebSocket messages sent |
| `processing_errors_total` | Uncaught exceptions in telemetry handlers |

**Latency distributions** (rolling window of last 1000 samples):

| Latency Bucket | What it measures |
|---|---|
| `handler_total_ms` | End-to-end processing time per message |
| `mongodb_timeseries_ms` | Time-series collection insert |
| `mongodb_twin_upsert_ms` | Twin document upsert (find + update) |
| `websocket_broadcast_ms` | Serialize + send to all connected clients |

From the rolling window, the following statistics are derived: **average, p50, p95, p99, max**.

**Gauges** (point-in-time values):

| Gauge | Description |
|---|---|
| `messages_per_second` | Computed from counter delta over 1-second window |
| `websocket_clients` | Currently connected WebSocket clients |
| `uptime_seconds` | Backend process uptime |

### 5.3 Data Pipeline

```
DiagnosticsService (singleton, in-memory)
    |
    +-- Every 1 second: snapshot counters + latency stats into circular buffer
    |   (last 3600 snapshots = 1 hour of history)
    |
    +-- Every 2 seconds: push latest snapshot via WebSocket
    |   message type: "diagnostics_update"
    |
    +-- On HTTP request: serve current snapshot or history range
        GET /api/diagnostics
        GET /api/diagnostics/history?minutes=30
```

The circular buffer enables the frontend to render time-series charts of system metrics without requiring a separate time-series database for infrastructure metrics.

### 5.4 Telemetry History

Sensor data is persisted to MongoDB time-series collections (`tower_telemetry`, `reservoir_telemetry`) and queried via:

```
GET /api/telemetry/reservoir/history?coordId={id}&minutes=60
GET /api/telemetry/tower/history?towerId={id}&minutes=60
```

A TTL index with a 7-day expiry ensures unbounded growth does not occur. At 250 towers with 5-second intervals, this produces approximately 4.2 million documents per day, occupying roughly 1-2 GB of storage.

## 6. Visualization

### 6.1 System Metrics Dashboard (`/diagnostics/system`)

The system metrics page provides real-time visibility into the processing pipeline:

- **Gauge charts**: messages/second, average latency, MongoDB writes/second, WebSocket clients.
- **Pipeline flow diagram**: visual representation of the message path (Simulator -> MQTT -> Backend -> MongoDB / WebSocket -> Frontend) with per-node throughput and color-coded health status.
- **Throughput chart** (time-series): messages received per second vs. messages processed per second. A divergence between these lines indicates a growing backlog.
- **Latency breakdown chart** (stacked area): per-component latency over time, showing which component dominates processing cost.

### 6.2 Sensor Trends Dashboard (`/diagnostics/sensors`)

The sensor trends page visualizes the physical data produced by the simulator:

- pH, EC, water level, and water temperature plotted over time for each reservoir.
- Air temperature and humidity plotted over time for towers (averaged per coordinator).
- Farm and coordinator selectors for drilling into specific devices.
- Time range controls: 15 minutes, 1 hour, 6 hours, 24 hours.

This page serves dual purpose: it validates that the simulator produces realistic data (e.g., visible day/night temperature cycles, sigmoid growth curves) and demonstrates the system's ability to store and query historical telemetry.

### 6.3 Scale Test Results Dashboard (`/diagnostics/scale-test`)

The scale test page displays results from throughput stress tests:

- **Throughput over time**: published vs. processed message rate across the test duration.
- **Latency over time**: average and p99 processing latency, showing when the system starts to struggle.
- **Per-component breakdown**: stacked area showing MongoDB, twin upsert, and WebSocket contributions.
- **Error timeline**: spike chart showing when and how many errors occurred.

## 7. Expected Findings

Based on the system architecture, the following bottleneck hierarchy is predicted (from most to least likely):

### 7.1 MongoDB Write Contention (most likely)

Each message triggers two MongoDB operations: a time-series insert and a twin document upsert. The twin upsert involves a `find` (by device ID) followed by an `update` (merge reported state, update timestamps). At high throughput, the `find` phase may become slow if indexes are suboptimal or the working set exceeds available RAM.

**Evidence**: `mongodb_twin_upsert_ms` p99 increases disproportionately to `mongodb_timeseries_ms`.

**Mitigation**: Ensure proper indexing on `tower_id` and `coord_id` fields. Consider write batching (collect N messages, write in one `InsertMany`). Reduce write concern to `w:0` for time-series data (acceptable data loss for analytics).

### 7.2 WebSocket Broadcast Fan-out

The `WsBroadcaster` iterates over all connected clients and sends each message sequentially. With `K` clients and 1000 messages/second, this is `1000 * K` send operations per second. If even one client is slow (e.g., a browser tab under memory pressure), it blocks the entire broadcast loop.

**Evidence**: `websocket_broadcast_ms` increases with client count.

**Mitigation**: Implement broadcast throttling (buffer updates, flush every 500ms). Send in parallel using `Task.WhenAll`. Implement backpressure (drop updates for slow clients).

### 7.3 MQTT Broker Queue Depth

Mosquitto's default `max_queued_messages` is 1000. If the backend's MQTT client falls behind, messages are queued in the broker. When the queue fills, messages are dropped.

**Evidence**: `$SYS/broker/messages/stored` increases; simulator publishes more messages than the backend processes.

**Mitigation**: Increase `max_queued_messages`. Increase the backend's MQTT client thread pool. Consider QoS 0 for telemetry (fire-and-forget).

### 7.4 Serialization Overhead (unlikely)

JSON deserialization via `System.Text.Json` is highly optimized. At ~200 bytes per message, deserialization should take <0.1ms.

**Evidence**: `handler_total_ms` is high but `mongodb_timeseries_ms + mongodb_twin_upsert_ms + websocket_broadcast_ms` account for less than 50% of it.

**Mitigation**: Switch to source-generated `JsonSerializerContext` for zero-allocation deserialization.

## 8. Methodology for Thesis

### 8.1 Test Protocol

1. Start the full Docker Compose stack (MongoDB, Mosquitto, Backend, Frontend, ML).
2. Run the simulator with increasing tower counts: 50, 100, 250, 500, 1000.
3. For each run, record:
   - Simulator-side: messages published, publish errors, tick times.
   - Backend-side: messages processed, per-component latency (avg, p95, p99, max), error counts.
   - MongoDB: slow query count (>100ms), connection pool utilization.
   - Mosquitto: messages received/sent, queue depth, heap memory.
4. Hold each load level for 60 seconds minimum to reach steady state.
5. Record all metrics from the `/api/diagnostics/history` endpoint for offline analysis.

### 8.2 Analysis Framework

For each load level, compute:

- **Throughput efficiency**: `messages_processed / messages_published` (should be ~1.0 when keeping up).
- **Latency budget**: percentage of total processing time attributable to each component.
- **Saturation point**: the tower count at which throughput efficiency drops below 0.95 (5% message loss).
- **Bottleneck identification**: the component whose latency p99 increases fastest as load increases.

### 8.3 Presentation

Results should be presented as:

1. **Throughput vs. tower count graph**: X-axis = tower count, Y-axis = messages/second (published line vs. processed line). The point where lines diverge is the saturation point.
2. **Latency breakdown at saturation**: stacked bar chart showing per-component contribution at the saturation point.
3. **Latency over time at 1000 towers**: time-series chart showing how latency evolves during a sustained load test.
4. **Before/after optimization**: if a bottleneck is identified and mitigated (e.g., adding an index), show the throughput improvement.

## 9. Conclusion

Simulation-driven bottleneck analysis transforms a qualitative question ("can the system handle many devices?") into a quantitative one ("the system sustains 550 messages/second before MongoDB twin upsert latency exceeds 50ms p99, at which point a 12% message backlog accumulates"). This methodology produces actionable engineering insights and publishable performance data suitable for a thesis evaluation.

The key architectural decisions that enable this analysis are:
- A deterministic simulator that faithfully reproduces the firmware's MQTT payload format.
- Per-operation latency instrumentation using `Stopwatch` with lock-free aggregation.
- A real-time metrics pipeline from backend to frontend via WebSocket.
- Time-series visualization using Apache ECharts for both system metrics and sensor data.
