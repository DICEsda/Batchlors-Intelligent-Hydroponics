# Logging Quick Reference

## Overview Table

| Layer | What Gets Logged | Log Level | Output Location | Retention |
|-------|------------------|-----------|-----------------|-----------|
| **Tower Nodes (ESP32-C3)** | Sensor readings, battery status, errors | INFO, ERROR | Serial + Coordinator | Real-time only |
| **Coordinators (ESP32-S3)** | WiFi, MQTT, ESP-NOW, sensors, pumps, errors | INFO, WARN, ERROR, CRITICAL | Serial + MQTT + Circular buffer (100) | 1 hour buffer |
| **MQTT Broker** | Connections, subscriptions, messages, errors | INFO, WARN, ERROR | `/var/log/mosquitto/mosquitto.log` | 30 days |
| **MongoDB** | Queries, connections, slow operations | INFO, WARN | `/var/log/mongodb/mongod.log` | 30 days |
| **Backend (ASP.NET)** | HTTP, MQTT, DB, Digital Twins, ML, errors | DEBUG, INFO, WARN, ERROR | `logs/backend/app-{Date}.log` + Azure Insights | 90 days |
| **Azure Digital Twins** | Twin updates, queries, sync events | INFO, ERROR | Azure Monitor | 90 days |
| **Frontend (Angular)** | User actions, API calls, WebSocket, errors | INFO, ERROR | Browser console + Backend API (errors) | Session only |

---

## Log Format by Layer

### Device Layer (C++)
```cpp
[TIMESTAMP] [LEVEL] [COMPONENT] [DeviceID] Message [Context]
[1707564123] [INFO] [MQTT] [COORD-001] Connected to broker [broker.local:1883]
```

### Backend Layer (C# JSON)
```json
{
  "timestamp": "2026-02-09T14:30:25.123Z",
  "level": "Information",
  "category": "API.Controllers.CoordinatorController",
  "message": "Received telemetry from coordinator",
  "traceId": "0HN7M1E4Q8D2A",
  "farmId": "farm-001",
  "coordId": "COORD-001"
}
```

### Frontend Layer (TypeScript)
```typescript
[2026-02-09T14:30:25.123Z] [INFO] [DashboardComponent] Loaded farm data
```

---

## Common Logging Scenarios

### Scenario 1: Device Sends Telemetry
```
Tower Node   → [INFO] Sensor reading complete [temp=23.5C, humidity=65%]
Coordinator  → [INFO] Received telemetry from tower T005
Coordinator  → [INFO] Publishing to MQTT [topic=farm/farm-001/coord/COORD-001/telemetry]
MQTT Broker  → [INFO] Message received on topic farm/farm-001/coord/COORD-001/telemetry
Backend      → [INFO] Received telemetry from coordinator COORD-001
Backend      → [DEBUG] Storing telemetry in MongoDB
MongoDB      → [DEBUG] Insert operation completed [duration=15ms]
Backend      → [INFO] Telemetry stored successfully
Frontend     → [INFO] Dashboard updated with new telemetry data
```

### Scenario 2: User Controls Pump
```
Frontend     → [INFO] User clicked "Start Pump" [coordId=COORD-001, traceId=ABC123]
Frontend     → [DEBUG] HTTP POST /api/coordinators/farm-001/COORD-001/reservoir/pump
Backend      → [INFO] POST /api/.../pump [traceId=ABC123]
Backend      → [INFO] Publishing MQTT command [topic=farm/.../cmd, traceId=ABC123]
MQTT Broker  → [INFO] Message published to topic farm/.../cmd
Coordinator  → [INFO] Received pump command [action=start, traceId=ABC123]
Coordinator  → [INFO] Pump started successfully [pump_id=1, traceId=ABC123]
Coordinator  → [INFO] Sending confirmation to backend [traceId=ABC123]
Backend      → [INFO] Pump status updated [status=running, traceId=ABC123]
Frontend     → [INFO] Pump status updated on dashboard [traceId=ABC123]
```

### Scenario 3: Sensor Alert (pH Out of Range)
```
Coordinator  → [WARN] pH out of range [value=7.2, target=6.0, threshold=0.5]
Coordinator  → [WARN] Publishing alert to backend
Backend      → [WARN] pH alert received [coordId=COORD-001, value=7.2]
Backend      → [INFO] Creating alert notification
Backend      → [INFO] Sending SMS to farmer [phone=+1234567890]
Backend      → [INFO] Sending push notification to mobile app
Frontend     → [WARN] Alert displayed to user [type=pH_OUT_OF_RANGE]
```

### Scenario 4: Error - Communication Failure
```
Tower Node   → [ERROR] Failed to send telemetry [retries=3, last_error=TIMEOUT]
Tower Node   → [INFO] Forwarding error to coordinator
Coordinator  → [ERROR] Tower T005 communication failure [consecutive_failures=1]
Coordinator  → [WARN] Publishing error log to backend
Backend      → [ERROR] Device communication error [deviceId=T005, coordId=COORD-001]
Backend      → [INFO] Triggering alert [type=DEVICE_OFFLINE]
```

---

## Trace Correlation Example

All logs for a single user action share the same **TraceID**:

```
TraceID: 0HN7M1E4Q8D2A

[Frontend]     [0HN7M1E4Q8D2A] User action: Start Pump
[Backend]      [0HN7M1E4Q8D2A] POST /api/coordinators/.../pump
[Backend]      [0HN7M1E4Q8D2A] Publishing MQTT command
[MQTT]         [0HN7M1E4Q8D2A] Message published
[Coordinator]  [0HN7M1E4Q8D2A] Pump started
[Backend]      [0HN7M1E4Q8D2A] Response 200 OK
[Frontend]     [0HN7M1E4Q8D2A] UI updated
```

Query in Azure Application Insights:
```kql
traces
| where customDimensions.TraceId == "0HN7M1E4Q8D2A"
| order by timestamp asc
```

---

## Key Logging Functions

### ESP32 (C++)
```cpp
logger.log(INFO, "SENSOR", "pH reading", {{"value", 6.2}});
logger.log(ERROR, "PUMP", "Activation failed", {{"pump_id", 1}});
```

### Backend (C#)
```csharp
_logger.LogInformation("Received telemetry from {CoordId}", coordId);
_logger.LogError(ex, "Failed to process telemetry");
```

### Frontend (TypeScript)
```typescript
this.logger.info('DashboardComponent', 'Loaded data', { farmId: 'farm-001' });
this.logger.error('ApiService', 'HTTP request failed', { status: 500 });
```

---

## Monitoring Queries (Azure KQL)

### Find all errors in last hour
```kql
traces
| where timestamp > ago(1h)
| where severityLevel >= 3
| project timestamp, message, customDimensions
```

### Trace a specific coordinator
```kql
traces
| where customDimensions.CoordId == "COORD-001"
| where timestamp > ago(24h)
| order by timestamp desc
```

### Find slow API requests
```kql
requests
| where duration > 1000
| project timestamp, name, duration, resultCode
| order by duration desc
```

### Count errors by component
```kql
traces
| where severityLevel >= 3
| summarize ErrorCount = count() by tostring(customDimensions.Component)
| order by ErrorCount desc
```

---

## Alert Configuration

### Critical Alerts (Immediate Action)
- **Device Offline:** No telemetry for 10 minutes → SMS + Email
- **pH Critical:** pH < 5.0 or > 7.0 → SMS + Push notification
- **Database Down:** Connection failures → PagerDuty + SMS
- **High Error Rate:** >100 errors in 5 minutes → Email

### Warning Alerts (Monitor)
- **Slow Queries:** Average query time > 100ms → Email
- **High Memory:** Backend memory > 80% → Email
- **Battery Low:** Tower node battery < 20% → Push notification

---

## Best Practices Summary

✅ **Use structured logging** (JSON with context)  
✅ **Include trace IDs** for correlation  
✅ **Log at appropriate levels** (INFO for normal, ERROR for failures)  
✅ **Add context** (farmId, coordId, towerId)  
✅ **Sanitize sensitive data** (no passwords/API keys)  
✅ **Log before and after** critical operations  
✅ **Include performance metrics** (duration, latency)  

❌ **Don't log PII** (personal identifiable information)  
❌ **Don't log in tight loops** (creates noise)  
❌ **Don't use string concatenation** (use parameters)  
❌ **Don't log everything as ERROR**  
❌ **Don't ignore log retention costs**  

---

## Troubleshooting with Logs

### Problem: Coordinator not sending telemetry

**Step 1:** Check coordinator logs (serial or MQTT)
```
[ERROR] [MQTT] Failed to publish telemetry [reason=DISCONNECTED]
```

**Step 2:** Check MQTT broker logs
```
Client COORD-001 has exceeded timeout, disconnecting.
```

**Step 3:** Check network connectivity
```
[WARN] [WiFi] Signal strength low [rssi=-75dBm]
```

**Resolution:** Improve WiFi signal or relocate coordinator

---

### Problem: API requests timing out

**Step 1:** Check backend logs
```
[WARN] Slow query detected: 1250ms for 50000 records
```

**Step 2:** Check MongoDB logs
```
Slow query: { find: "telemetry", filter: {...} } 1250ms
```

**Step 3:** Check database indexes
```
[INFO] Index not used: full collection scan performed
```

**Resolution:** Create appropriate database index

---

## Log File Locations

```
Project Root
├── logs/
│   ├── backend/
│   │   └── app-2026-02-09.log
│   ├── mongodb/
│   │   └── mongod.log
│   └── mosquitto/
│       └── mosquitto.log
│
├── Device logs (runtime only):
│   ├── Coordinator: Serial UART output
│   └── Tower nodes: Serial UART output
│
└── Cloud logs:
    └── Azure Application Insights (web portal)
```

---

## Quick Commands

### View backend logs (Docker)
```bash
docker logs intelligent-hydroponics-backend -f
```

### View MQTT logs (Docker)
```bash
docker logs intelligent-hydroponics-mqtt -f
```

### View MongoDB logs (Docker)
```bash
docker logs intelligent-hydroponics-mongodb -f
```

### View coordinator logs (Serial)
```bash
# Linux/Mac
screen /dev/ttyUSB0 115200

# Windows
putty -serial COM3 -sercfg 115200,8,n,1,N
```

### Search backend logs
```bash
grep "ERROR" logs/backend/app-2026-02-09.log
grep "COORD-001" logs/backend/app-2026-02-09.log | tail -20
```

---

## Summary

This logging structure provides:

✅ Comprehensive coverage across all layers  
✅ Distributed tracing with correlation IDs  
✅ Appropriate retention policies  
✅ Production-ready monitoring (Azure)  
✅ Easy troubleshooting and debugging  
✅ Performance insights and optimization  

For detailed implementation, see `Logging-Strategy.md`.
