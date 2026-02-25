# Logging Strategy: Intelligent Hydroponics IoT System

## Overview

This document describes the logging architecture across all system layers, enabling comprehensive monitoring, debugging, and operational insights.

---

## Logging Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Log Aggregation                          │
│              (Azure Monitor / Application Insights)          │
└──────────────────────────────┬──────────────────────────────┘
                               │
                    ┌──────────┴──────────┐
                    │                     │
         ┌──────────▼──────────┐  ┌──────▼──────────┐
         │   Backend Logs      │  │  Frontend Logs  │
         │   (ASP.NET Core)    │  │   (Angular)     │
         └──────────┬──────────┘  └─────────────────┘
                    │
         ┌──────────┼──────────┐
         │          │          │
    ┌────▼───┐ ┌────▼───┐ ┌───▼────┐
    │MongoDB │ │  MQTT  │ │ Digital│
    │  Logs  │ │  Logs  │ │  Twins │
    └────────┘ └────┬───┘ └────────┘
                    │
         ┌──────────┴──────────┐
         │                     │
    ┌────▼───────┐      ┌──────▼──────┐
    │Coordinator │      │Tower Nodes  │
    │   Logs     │      │    Logs     │
    │ (ESP32-S3) │      │ (ESP32-C3)  │
    └────────────┘      └─────────────┘
```

---

## Layer 1: Device Layer (ESP32 Firmware)

### Coordinator Logs (ESP32-S3)

**Location:** Serial output, stored in circular buffer, sent to backend via MQTT

**Log Format:**
```
[TIMESTAMP] [LEVEL] [COMPONENT] [CoordID] Message [Context]
```

**Example:**
```
[1707564123] [INFO] [MQTT] [COORD-001] Connected to broker [broker.local:1883]
[1707564125] [WARN] [SENSOR] [COORD-001] pH sensor reading unstable [value=6.89, drift=0.3]
[1707564130] [ERROR] [ESP-NOW] [COORD-001] Failed to send to tower [tower_id=T005, retries=3]
```

**What Gets Logged:**

| Component | Events Logged | Log Level |
|-----------|---------------|-----------|
| **Boot** | Device startup, firmware version, reset reason | INFO |
| **WiFi** | Connection attempts, RSSI, IP assignment | INFO, WARN |
| **MQTT** | Connect/disconnect, topic subscriptions, publish failures | INFO, ERROR |
| **ESP-NOW** | Pairing, send/receive, failures, latency | INFO, WARN, ERROR |
| **Sensors** | Readings, calibration, drift detection, failures | DEBUG, WARN, ERROR |
| **Pumps** | Start/stop commands, duration, failures | INFO, ERROR |
| **Memory** | Heap usage, stack overflow warnings | WARN, ERROR |
| **Errors** | Exception traces, crash dumps, watchdog resets | CRITICAL |

**Code Example (C++):**
```cpp
// Coordinator logging implementation
enum LogLevel { DEBUG, INFO, WARN, ERROR, CRITICAL };

class Logger {
private:
    String coordId;
    CircularBuffer<String, 100> logBuffer;
    
public:
    void log(LogLevel level, const char* component, const char* message, JsonObject context = {}) {
        String timestamp = String(millis());
        String levelStr = getLevelString(level);
        
        // Format: [TIMESTAMP] [LEVEL] [COMPONENT] [CoordID] Message [Context]
        String logEntry = "[" + timestamp + "] [" + levelStr + "] [" + 
                         String(component) + "] [" + coordId + "] " + 
                         String(message);
        
        if (!context.isNull()) {
            logEntry += " [" + context.as<String>() + "]";
        }
        
        // Output to serial
        Serial.println(logEntry);
        
        // Store in circular buffer
        logBuffer.push(logEntry);
        
        // Send critical/error logs immediately to backend
        if (level >= ERROR) {
            sendLogToBackend(logEntry);
        }
    }
    
    void sendLogToBackend(String logEntry) {
        String topic = "farm/" + farmId + "/coord/" + coordId + "/logs";
        mqttClient.publish(topic.c_str(), logEntry.c_str());
    }
};

// Usage examples
logger.log(INFO, "BOOT", "Coordinator started", {{"version", "v1.2.0"}});
logger.log(WARN, "SENSOR", "pH drift detected", {{"value", ph}, {"target", 6.0}});
logger.log(ERROR, "PUMP", "Pump activation failed", {{"pump_id", 1}, {"reason", "timeout"}});
```

**Log Retention:**
- Serial buffer: 100 most recent entries (circular buffer)
- MQTT transmission: Errors and warnings sent immediately
- Debug logs: On-demand via serial connection

---

### Tower Node Logs (ESP32-C3)

**Location:** Serial output (limited), sent to coordinator via ESP-NOW

**Log Format:**
```
[TIMESTAMP] [LEVEL] [TowerID] Message [Context]
```

**Example:**
```
[1707564150] [INFO] [T005] Sensor reading complete [temp=23.5C, humidity=65%, light=450lux]
[1707564155] [WARN] [T005] Battery low [voltage=3.2V, percentage=15%]
[1707564160] [ERROR] [T005] Failed to send telemetry [retries=3, last_error=TIMEOUT]
```

**What Gets Logged:**

| Component | Events Logged | Log Level |
|-----------|---------------|-----------|
| **Boot** | Wake from sleep, reset reason | INFO |
| **ESP-NOW** | Send/receive status, pairing | INFO, ERROR |
| **Sensors** | Reading success/failure, out-of-range values | DEBUG, WARN |
| **Actuators** | Pump on/off, light on/off | INFO |
| **Battery** | Voltage level, low battery warnings | WARN |
| **Sleep** | Sleep/wake cycles, duration | DEBUG |
| **Errors** | Sensor failures, communication timeouts | ERROR |

**Code Example (C++):**
```cpp
// Tower node minimal logging (memory-constrained)
class TowerLogger {
private:
    String towerId;
    
public:
    void log(LogLevel level, const char* message, JsonObject context = {}) {
        if (level < INFO) return; // Skip DEBUG on battery-powered nodes
        
        String logEntry = "[" + String(millis()) + "] [" + 
                         getLevelString(level) + "] [" + towerId + "] " + 
                         String(message);
        
        if (!context.isNull()) {
            logEntry += " [" + context.as<String>() + "]";
        }
        
        Serial.println(logEntry);
        
        // Send to coordinator if critical
        if (level >= ERROR) {
            sendLogToCoordinator(logEntry);
        }
    }
};

// Usage
logger.log(INFO, "Sensor reading complete", {{"temp", temp}, {"humidity", humidity}});
logger.log(WARN, "Battery low", {{"voltage", batteryVoltage}});
```

**Log Retention:**
- Minimal buffering (memory-constrained)
- Critical logs forwarded to coordinator
- Debug logs available via serial during development

---

## Layer 2: Communication Layer (MQTT Broker)

### MQTT Broker Logs (Mosquitto)

**Location:** `/var/log/mosquitto/mosquitto.log` (containerized or host)

**Log Format:** Mosquitto standard format

**Example:**
```
1707564200: New connection from 192.168.1.100 on port 1883.
1707564201: New client connected from 192.168.1.100 as COORD-001 (c1, k60).
1707564205: Client COORD-001 has exceeded timeout, disconnecting.
1707564210: Socket error on client COORD-001, disconnecting.
```

**What Gets Logged:**

| Event Type | Details | Log Level |
|------------|---------|-----------|
| **Connections** | Client connects/disconnects, IP addresses | INFO |
| **Authentication** | Login success/failure, username | WARN, ERROR |
| **Subscriptions** | Topic subscriptions, QoS levels | INFO |
| **Messages** | Publish events (optional, can be verbose) | DEBUG |
| **Errors** | Connection timeouts, malformed packets | ERROR |
| **Performance** | Message rate, queue depth | INFO |

**Configuration (mosquitto.conf):**
```conf
# Enable logging
log_dest file /var/log/mosquitto/mosquitto.log
log_dest stdout
log_type error
log_type warning
log_type notice
log_type information

# Log connections and messages
connection_messages true
log_timestamp true
log_timestamp_format %Y-%m-%dT%H:%M:%S

# Optional: Log all messages (verbose!)
# log_type all
```

---

## Layer 3: Backend Layer (ASP.NET Core)

### Backend API Logs

**Location:** 
- Console output (stdout)
- File: `/logs/backend/app-{Date}.log`
- Azure Application Insights (production)

**Log Format:** Structured JSON logging

**Example:**
```json
{
  "timestamp": "2026-02-09T14:30:25.123Z",
  "level": "Information",
  "category": "API.Controllers.CoordinatorController",
  "message": "Received telemetry from coordinator",
  "traceId": "0HN7M1E4Q8D2A",
  "spanId": "7B8F3C2A1D6E",
  "farmId": "farm-001",
  "coordId": "COORD-001",
  "context": {
    "pH": 6.2,
    "ec": 1.8,
    "temp": 22.5,
    "requestPath": "/api/coordinators/farm-001/COORD-001/reservoir",
    "method": "POST",
    "statusCode": 200,
    "duration": 45
  }
}
```

**What Gets Logged:**

| Component | Events Logged | Log Level |
|-----------|---------------|-----------|
| **HTTP Requests** | All API calls, path, method, status, duration | INFO |
| **MQTT Events** | Messages received/sent, connection status | INFO |
| **Database** | Queries, write operations, connection pool | DEBUG, WARN |
| **Digital Twins** | Sync events, simulation updates | INFO |
| **ML** | Model predictions, training events | INFO |
| **Validation** | Input validation failures | WARN |
| **Auth** | Login attempts, token validation | INFO, WARN |
| **Errors** | Exceptions, stack traces, failed operations | ERROR |
| **Performance** | Slow queries, high memory usage | WARN |

**Code Example (C#):**
```csharp
// Startup.cs - Configure structured logging
public void ConfigureServices(IServiceCollection services)
{
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
        logging.AddFile("logs/backend/app-{Date}.log");
        
        // Production: Add Application Insights
        logging.AddApplicationInsights();
    });
}

// CoordinatorController.cs - Usage examples
public class CoordinatorController : ControllerBase
{
    private readonly ILogger<CoordinatorController> _logger;
    
    public CoordinatorController(ILogger<CoordinatorController> logger)
    {
        _logger = logger;
    }
    
    [HttpPost("reservoir")]
    public async Task<IActionResult> ReceiveReservoirTelemetry(
        string farmId, 
        string coordId, 
        [FromBody] ReservoirTelemetry telemetry)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["FarmId"] = farmId,
            ["CoordId"] = coordId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        });
        
        _logger.LogInformation(
            "Received telemetry from coordinator {CoordId}: pH={pH}, EC={ec}, Temp={temp}",
            coordId, telemetry.pH, telemetry.ec, telemetry.waterTemp
        );
        
        try
        {
            await _telemetryService.StoreTelemetry(farmId, coordId, telemetry);
            
            // Check for anomalies
            if (telemetry.pH < 5.5 || telemetry.pH > 6.5)
            {
                _logger.LogWarning(
                    "pH out of range for {CoordId}: {pH} (target: 5.5-6.5)",
                    coordId, telemetry.pH
                );
                await _alertService.SendAlert(farmId, coordId, "pH out of range");
            }
            
            _logger.LogDebug(
                "Telemetry stored successfully for {CoordId}",
                coordId
            );
            
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process telemetry from {CoordId}",
                coordId
            );
            return StatusCode(500, "Internal server error");
        }
    }
}

// MqttService.cs - MQTT logging
public class MqttService
{
    private readonly ILogger<MqttService> _logger;
    
    public async Task OnMessageReceived(MqttApplicationMessage message)
    {
        _logger.LogInformation(
            "MQTT message received: topic={Topic}, size={Size}bytes, qos={QoS}",
            message.Topic,
            message.Payload.Length,
            message.QualityOfServiceLevel
        );
        
        try
        {
            var payload = Encoding.UTF8.GetString(message.Payload);
            _logger.LogDebug("Message payload: {Payload}", payload);
            
            await ProcessMessage(message.Topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing MQTT message from topic {Topic}",
                message.Topic
            );
        }
    }
}
```

**Log Configuration (appsettings.json):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "System": "Warning"
    },
    "Console": {
      "IncludeScopes": true,
      "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff "
    },
    "File": {
      "Path": "logs/backend/app-{Date}.log",
      "MinLevel": "Information",
      "OutputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {SourceContext} {Message}{NewLine}{Exception}"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

---

## Layer 4: Database Layer (MongoDB)

### MongoDB Logs

**Location:** `/var/log/mongodb/mongod.log`

**What Gets Logged:**

| Event Type | Details | Log Level |
|------------|---------|-----------|
| **Queries** | Slow queries (>100ms), full table scans | WARN |
| **Connections** | New connections, connection pool exhaustion | INFO, WARN |
| **Writes** | Insert/update/delete operations | DEBUG |
| **Indexes** | Index usage, missing indexes | INFO |
| **Replication** | Replica set status, sync lag | INFO, WARN |
| **Errors** | Query failures, disk space issues | ERROR |

**Configuration (mongod.conf):**
```yaml
systemLog:
  destination: file
  path: /var/log/mongodb/mongod.log
  logAppend: true
  verbosity: 0  # 0=INFO, 1=DEBUG, 2=VERBOSE
  component:
    query:
      verbosity: 1  # Log slow queries
    storage:
      verbosity: 0

# Enable profiling for slow queries
operationProfiling:
  mode: slowOp
  slowOpThresholdMs: 100  # Log queries >100ms
```

**Backend Integration:**
```csharp
// Log slow MongoDB queries
public class TelemetryRepository
{
    private readonly ILogger<TelemetryRepository> _logger;
    private readonly IMongoCollection<ReservoirTelemetry> _collection;
    
    public async Task<List<ReservoirTelemetry>> GetHistoricalData(
        string farmId, 
        string coordId, 
        DateTime from, 
        DateTime to)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug(
            "Querying reservoir telemetry: farmId={FarmId}, coordId={CoordId}, from={From}, to={To}",
            farmId, coordId, from, to
        );
        
        try
        {
            var filter = Builders<ReservoirTelemetry>.Filter.And(
                Builders<ReservoirTelemetry>.Filter.Eq(t => t.FarmId, farmId),
                Builders<ReservoirTelemetry>.Filter.Eq(t => t.CoordId, coordId),
                Builders<ReservoirTelemetry>.Filter.Gte(t => t.Timestamp, from),
                Builders<ReservoirTelemetry>.Filter.Lte(t => t.Timestamp, to)
            );
            
            var result = await _collection.Find(filter).ToListAsync();
            
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 100)
            {
                _logger.LogWarning(
                    "Slow query detected: {Duration}ms for {RecordCount} records",
                    stopwatch.ElapsedMilliseconds,
                    result.Count
                );
            }
            else
            {
                _logger.LogDebug(
                    "Query completed in {Duration}ms, returned {RecordCount} records",
                    stopwatch.ElapsedMilliseconds,
                    result.Count
                );
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Database query failed for farmId={FarmId}, coordId={CoordId}",
                farmId, coordId
            );
            throw;
        }
    }
}
```

---

## Layer 5: Frontend Layer (Angular)

### Frontend Logs

**Location:** 
- Browser console (development)
- Backend logging endpoint (production)
- Azure Application Insights (production)

**Log Format:** Structured with trace correlation

**Example (Console):**
```
[2026-02-09T14:30:25.123Z] [INFO] [DashboardComponent] Loaded farm data (farmId: farm-001, towers: 12, coordCount: 2)
[2026-02-09T14:30:30.456Z] [WARN] [WebSocketService] Connection lost, attempting reconnect (retries: 1/5)
[2026-02-09T14:30:35.789Z] [ERROR] [ApiService] HTTP request failed (url: /api/towers, status: 500, error: Internal Server Error)
```

**What Gets Logged:**

| Component | Events Logged | Log Level |
|-----------|---------------|-----------|
| **Navigation** | Route changes, page loads | INFO |
| **API Calls** | HTTP requests, responses, errors, duration | INFO, ERROR |
| **WebSocket** | Connection status, message received/sent | INFO, WARN |
| **User Actions** | Button clicks, form submissions | DEBUG |
| **Data Loading** | Component initialization, data fetch | INFO |
| **Errors** | JavaScript errors, API failures, validation | ERROR |
| **Performance** | Slow page loads, memory warnings | WARN |

**Code Example (TypeScript):**
```typescript
// logger.service.ts
export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3
}

@Injectable({
  providedIn: 'root'
})
export class LoggerService {
  private logLevel: LogLevel = LogLevel.INFO;
  
  constructor(private http: HttpClient) {}
  
  private log(level: LogLevel, component: string, message: string, context?: any) {
    if (level < this.logLevel) return;
    
    const timestamp = new Date().toISOString();
    const levelStr = LogLevel[level];
    
    const logEntry = {
      timestamp,
      level: levelStr,
      component,
      message,
      context,
      url: window.location.href,
      userAgent: navigator.userAgent
    };
    
    // Console output (always in development)
    const consoleMsg = `[${timestamp}] [${levelStr}] [${component}] ${message}`;
    switch (level) {
      case LogLevel.DEBUG:
        console.debug(consoleMsg, context);
        break;
      case LogLevel.INFO:
        console.info(consoleMsg, context);
        break;
      case LogLevel.WARN:
        console.warn(consoleMsg, context);
        break;
      case LogLevel.ERROR:
        console.error(consoleMsg, context);
        break;
    }
    
    // Send errors to backend
    if (level >= LogLevel.ERROR) {
      this.sendLogToBackend(logEntry);
    }
  }
  
  private sendLogToBackend(logEntry: any) {
    this.http.post('/api/logs/frontend', logEntry).subscribe({
      error: (err) => console.error('Failed to send log to backend', err)
    });
  }
  
  debug(component: string, message: string, context?: any) {
    this.log(LogLevel.DEBUG, component, message, context);
  }
  
  info(component: string, message: string, context?: any) {
    this.log(LogLevel.INFO, component, message, context);
  }
  
  warn(component: string, message: string, context?: any) {
    this.log(LogLevel.WARN, component, message, context);
  }
  
  error(component: string, message: string, context?: any) {
    this.log(LogLevel.ERROR, component, message, context);
  }
}

// Usage in components
@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  constructor(
    private logger: LoggerService,
    private farmService: FarmService
  ) {}
  
  ngOnInit() {
    this.logger.info('DashboardComponent', 'Component initialized');
    
    this.farmService.getFarms().subscribe({
      next: (farms) => {
        this.logger.info(
          'DashboardComponent', 
          'Loaded farm data', 
          { farmCount: farms.length, farms: farms.map(f => f.id) }
        );
      },
      error: (error) => {
        this.logger.error(
          'DashboardComponent',
          'Failed to load farms',
          { error: error.message, status: error.status }
        );
      }
    });
  }
  
  onPumpControl(coordId: string, action: 'start' | 'stop') {
    this.logger.info(
      'DashboardComponent',
      `User action: ${action} pump`,
      { coordId, timestamp: Date.now() }
    );
    
    // ... rest of implementation
  }
}

// api.service.ts - HTTP interceptor for logging
@Injectable()
export class LoggingInterceptor implements HttpInterceptor {
  constructor(private logger: LoggerService) {}
  
  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const started = Date.now();
    
    this.logger.debug(
      'HTTP',
      `Request: ${req.method} ${req.url}`,
      { headers: req.headers, body: req.body }
    );
    
    return next.handle(req).pipe(
      tap({
        next: (event) => {
          if (event instanceof HttpResponse) {
            const elapsed = Date.now() - started;
            this.logger.info(
              'HTTP',
              `Response: ${req.method} ${req.url} (${event.status})`,
              { status: event.status, duration: elapsed }
            );
          }
        },
        error: (error: HttpErrorResponse) => {
          const elapsed = Date.now() - started;
          this.logger.error(
            'HTTP',
            `Request failed: ${req.method} ${req.url}`,
            { 
              status: error.status, 
              statusText: error.statusText,
              error: error.error,
              duration: elapsed
            }
          );
        }
      })
    );
  }
}

// Global error handler
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  constructor(private logger: LoggerService) {}
  
  handleError(error: Error) {
    this.logger.error(
      'GlobalErrorHandler',
      'Unhandled exception',
      {
        message: error.message,
        stack: error.stack,
        type: error.constructor.name
      }
    );
    
    // Also log to console
    console.error('Unhandled error:', error);
  }
}
```

---

## Layer 6: Azure Digital Twins

### Digital Twins Logs

**Location:** Azure Monitor / Application Insights

**What Gets Logged:**

| Event Type | Details | Log Level |
|------------|---------|-----------|
| **Twin Updates** | Property changes, relationships | INFO |
| **Queries** | Digital twin queries, duration | DEBUG |
| **Events** | Telemetry ingestion, event routing | INFO |
| **Simulation** | Scenario runs, state changes | INFO |
| **Sync** | Physical-to-digital sync events | INFO |
| **Errors** | API failures, validation errors | ERROR |

**Code Example (C#):**
```csharp
public class DigitalTwinService
{
    private readonly ILogger<DigitalTwinService> _logger;
    private readonly DigitalTwinsClient _dtClient;
    
    public async Task UpdateTwinProperty(
        string twinId, 
        string property, 
        object value)
    {
        _logger.LogInformation(
            "Updating digital twin property: twinId={TwinId}, property={Property}, value={Value}",
            twinId, property, value
        );
        
        try
        {
            var updateTwinData = new JsonPatchDocument();
            updateTwinData.AppendReplace($"/{property}", value);
            
            await _dtClient.UpdateDigitalTwinAsync(twinId, updateTwinData);
            
            _logger.LogInformation(
                "Digital twin updated successfully: twinId={TwinId}",
                twinId
            );
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to update digital twin: twinId={TwinId}, property={Property}, statusCode={StatusCode}",
                twinId, property, ex.Status
            );
            throw;
        }
    }
}
```

---

## Log Correlation Strategy

### Distributed Tracing

To correlate logs across all layers, use **Trace IDs**:

```
User Request → TraceID: 0HN7M1E4Q8D2A
  ├─ Frontend: [0HN7M1E4Q8D2A] User clicked "Start Pump"
  ├─ Backend:  [0HN7M1E4Q8D2A] POST /api/coordinators/farm-001/COORD-001/reservoir/pump
  ├─ MQTT:     [0HN7M1E4Q8D2A] Published to farm/farm-001/coord/COORD-001/reservoir/cmd
  ├─ Coordinator: [0HN7M1E4Q8D2A] Pump started successfully
  └─ Backend:  [0HN7M1E4Q8D2A] Response 200 OK
```

**Implementation:**

1. **Frontend generates TraceID** on user action
2. **Backend receives TraceID** via HTTP header `X-Trace-Id`
3. **Backend propagates TraceID** to MQTT messages
4. **Coordinator extracts TraceID** from MQTT payload
5. **All layers log with same TraceID**

**Code Example:**
```typescript
// Frontend: Add trace ID to HTTP requests
export class TraceIdInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<any>, next: HttpHandler) {
    const traceId = this.generateTraceId();
    const clonedReq = req.clone({
      headers: req.headers.set('X-Trace-Id', traceId)
    });
    return next.handle(clonedReq);
  }
  
  private generateTraceId(): string {
    return Math.random().toString(36).substring(2, 15);
  }
}
```

```csharp
// Backend: Extract and use trace ID
public class TraceIdMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var traceId = context.Request.Headers["X-Trace-Id"].FirstOrDefault() 
                     ?? Guid.NewGuid().ToString("N");
        
        using (LogContext.PushProperty("TraceId", traceId))
        {
            context.Response.Headers.Add("X-Trace-Id", traceId);
            await next(context);
        }
    }
}
```

---

## Log Aggregation & Monitoring

### Recommended Tools

1. **Development:** Console logs + local log files
2. **Production:** Azure Application Insights + Azure Monitor

### Azure Application Insights Setup

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});
```

### Log Queries (Kusto/KQL)

```kql
// Find all errors in the last hour
traces
| where timestamp > ago(1h)
| where severityLevel >= 3  // Error or higher
| project timestamp, message, customDimensions, severityLevel
| order by timestamp desc

// Find logs for a specific trace ID
traces
| where customDimensions.TraceId == "0HN7M1E4Q8D2A"
| project timestamp, message, severityLevel
| order by timestamp asc

// Find slow API requests
requests
| where duration > 1000  // >1 second
| project timestamp, name, duration, resultCode
| order by duration desc

// Count errors by component
traces
| where severityLevel >= 3
| summarize ErrorCount = count() by tostring(customDimensions.Component)
| order by ErrorCount desc
```

---

## Log Retention Policy

| Layer | Development | Production | Reason |
|-------|-------------|------------|--------|
| **Device Logs** | 1 hour (buffer) | Errors only → Backend | Memory-constrained devices |
| **MQTT Logs** | 7 days | 30 days | Debugging connection issues |
| **Backend Logs** | 7 days | 90 days | Compliance, auditing |
| **MongoDB Logs** | 7 days | 30 days | Performance analysis |
| **Frontend Logs** | Session only | Errors → Backend | Privacy, minimal storage |
| **Azure Insights** | 30 days | 90 days | Long-term analysis |

---

## Best Practices

### ✅ Do's

1. **Use structured logging** (JSON) for easy parsing
2. **Include context** (farmId, coordId, towerId) in every log
3. **Log before and after critical operations** (database writes, pump controls)
4. **Use appropriate log levels** (don't log everything as ERROR)
5. **Sanitize sensitive data** (don't log passwords, API keys)
6. **Add trace IDs** for distributed tracing
7. **Log performance metrics** (duration, latency)
8. **Include stack traces** for errors

### ❌ Don'ts

1. **Don't log personally identifiable information (PII)**
2. **Don't log on every loop iteration** (creates noise)
3. **Don't use string concatenation** (use structured logging)
4. **Don't ignore log levels** in production
5. **Don't log raw binary data** (encode as base64 if needed)
6. **Don't assume logs are free** (storage costs)

---

## Monitoring Alerts

Set up alerts for critical conditions:

```yaml
Alerts:
  - Name: High Error Rate
    Condition: ErrorCount > 100 in 5 minutes
    Action: Email, SMS
    
  - Name: Device Offline
    Condition: No telemetry from coordinator for 10 minutes
    Action: Email, push notification
    
  - Name: Slow API Response
    Condition: Average response time > 1000ms
    Action: Email
    
  - Name: Database Connection Failure
    Condition: MongoDB connection errors
    Action: Email, SMS, PagerDuty
    
  - Name: pH Out of Range
    Condition: pH < 5.5 OR pH > 6.5 for 5 minutes
    Action: SMS, push notification
```

---

## Summary

This logging strategy provides:

✅ **Comprehensive coverage** across all system layers  
✅ **Trace correlation** for debugging distributed systems  
✅ **Appropriate log levels** for filtering  
✅ **Structured data** for analysis and alerting  
✅ **Production-ready** monitoring with Azure  
✅ **Privacy-conscious** (no PII logging)  
✅ **Cost-effective** retention policies  

This enables effective monitoring, debugging, and operational insights for the intelligent hydroponics system.
