# Backend Migration Analysis: Go to .NET

**Document Version:** 1.0  
**Date:** January 2, 2026  
**Author:** Backend Agent  
**Status:** Analysis Complete

---

## Executive Summary

This document provides a comprehensive analysis comparing the existing Go backend implementation with the .NET backend for the IoT Smart Tile System. The goal is to consolidate all backend functionality into the .NET implementation, eliminating the need for dual backend maintenance.

### Key Findings

| Metric | Value |
|--------|-------|
| Total Go Endpoints | ~35 |
| Total .NET Endpoints | ~25 |
| Missing Features in .NET | **12 major features** |
| New Controllers Required | 3-4 |
| New Services Required | 2-3 |
| Estimated Migration Effort | Medium (2-3 weeks) |

### Migration Recommendation

The .NET backend has a **solid foundation** with core CRUD operations, MQTT integration, and WebSocket support. However, several critical features for real-time operations and device control are missing. **Priority should be given to Light Control and WebSocket Broadcasting** as these are essential for the frontend's real-time functionality.

---

## 1. Architecture Comparison

### 1.1 Technology Stack

| Component | Go Backend | .NET Backend |
|-----------|------------|--------------|
| **Framework** | Gorilla Mux | ASP.NET Core 8.0 |
| **Port** | 8080 | 8000 |
| **MQTT Client** | Eclipse Paho | MQTTnet |
| **WebSocket** | Gorilla WebSocket | System.Net.WebSockets |
| **Database** | MongoDB (mongo-driver) | MongoDB.Driver |
| **DI Container** | Uber Fx | Built-in ASP.NET DI |
| **Logging** | Zap | Serilog (configured) |
| **API Docs** | None | Swagger/OpenAPI |

### 1.2 Project Structure

**Go Backend:**
```
backend/internal/
├── config/           # Configuration loading
├── db/               # MongoDB connection
├── googlehome/       # Google Home integration
├── http/             # All HTTP handlers
│   ├── handlers.go
│   ├── coordinator_handlers.go
│   ├── zone_handlers.go
│   ├── customize_handlers.go
│   ├── settings_handlers.go
│   ├── ws_broadcast.go
│   └── radar_handlers.go
├── repository/       # Data access layer
└── types/            # Domain models
```

**.NET Backend:**
```
backend/src/IoT.Backend/
├── Controllers/      # HTTP endpoints (7 controllers)
├── Models/           # Domain models (8 files)
├── Repositories/     # Data access (interface + impl)
├── Services/         # Business logic (3 services)
├── WebSockets/       # WS-MQTT bridge
└── Program.cs        # Startup configuration
```

---

## 2. Feature Comparison Matrix

### 2.1 Site Management

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Get All Sites | ✅ | ✅ | Equivalent |
| Get Site by ID | ✅ | ✅ | Equivalent |
| Create Site | ✅ | ✅ | Equivalent |
| Update Site | ✅ | ✅ | Equivalent |
| Delete Site | ✅ | ✅ | Equivalent |
| Get Site Settings | ✅ | ✅ | Equivalent |
| Update Site Settings | ✅ | ✅ | .NET missing Google Home fields |

### 2.2 Coordinator Management

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Get Coordinator | ✅ | ✅ | Equivalent |
| Get Coordinator with Nodes | ✅ | ✅ | Equivalent |
| Send Command | ✅ | ✅ | Equivalent |
| Start Discovery | ✅ | ✅ | Equivalent |
| Start Pairing | ✅ | ✅ | Go has duration param |
| Broadcast Message | ✅ | ✅ | Equivalent |
| **Restart Coordinator** | ✅ | ❌ | **MISSING** |
| **Update WiFi Config** | ✅ | ❌ | **MISSING** |

### 2.3 Node Management

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Get All Nodes | ✅ | ✅ | Equivalent |
| Get Node by ID | ✅ | ✅ | Equivalent |
| Update Node Zone | ✅ | ✅ | Equivalent |
| Update Node Name | ✅ | ✅ | Equivalent |
| Send Node Command | ✅ | ✅ | Equivalent |
| Delete Node | ✅ | ✅ | Equivalent |
| **Test Color** | ✅ | ❌ | **MISSING** |
| **Turn Off** | ✅ | ❌ | **MISSING** |
| **Set Brightness** | ✅ | ❌ | **MISSING** |

### 2.4 Zone Management

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Get Zones by Site | ✅ | ✅ | Equivalent |
| Get Zone by ID | ✅ | ✅ | Equivalent |
| Create Zone | ✅ | ✅ | Go auto-assigns nodes |
| Update Zone | ✅ | ✅ | Equivalent |
| Delete Zone | ✅ | ✅ | Equivalent |
| **Flash Green on Change** | ✅ | ❌ | **MISSING** - MQTT feedback |
| **Auto-assign Nodes** | ✅ | ❌ | **MISSING** |

### 2.5 Light Control

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| **Light Control Endpoint** | ✅ | ❌ | **MISSING** - `/api/v1/node/light/control` |
| **Test Color (R,G,B,W)** | ✅ | ❌ | **MISSING** |
| **Turn Off Node** | ✅ | ❌ | **MISSING** |
| **Set Brightness** | ✅ | ❌ | **MISSING** |

### 2.6 Customization API

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| **Get Default Config** | ✅ | ❌ | **MISSING** - Returns coordinator/node defaults |
| **Update Coordinator Config** | ✅ | ❌ | **MISSING** - MQTT `update_config` |
| **Update Light Sensor Config** | ✅ | ❌ | **MISSING** |
| **Update LED Config** | ✅ | ❌ | **MISSING** |
| **Reset to Defaults** | ✅ | ❌ | **MISSING** - MQTT `reset_config` |
| **LED Preview** | ✅ | ❌ | **MISSING** - Temporary preview |

### 2.7 Radar/mmWave

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Get Radar Data (JSON) | ✅ | ✅ | Equivalent |
| Get Radar Stats | ❌ | ✅ | .NET has additional stats |
| **Get Radar Image (PNG)** | ✅ | ❌ | **MISSING** - Server-side rendering |
| **Radar Cache** | ✅ | ❌ | **MISSING** - In-memory latest frames |

### 2.8 OTA Updates

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| **Start OTA** | ✅ | ❌ | **MISSING** - POST endpoint |
| **Get OTA Status** | ✅ | ❌ | **MISSING** - GET endpoint |
| OTA Repository Methods | ✅ | ✅ | .NET has repo, no controller |

### 2.9 WebSocket

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| WebSocket Connection | ✅ | ✅ | Both support |
| Subscribe to Topics | ✅ | ✅ | Equivalent |
| Publish Messages | ✅ | ✅ | Equivalent |
| **Auto-broadcast Telemetry** | ✅ | ❌ | **MISSING** - Push to ALL clients |
| **WSBroadcaster Service** | ✅ | ❌ | **MISSING** |

### 2.10 Telemetry

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| MQTT → MongoDB Ingestion | ✅ | ✅ | Equivalent |
| Get Telemetry by Coordinator | ❌ | ✅ | .NET has more endpoints |
| Get Telemetry by Node | ❌ | ✅ | .NET has more endpoints |
| **Real-time Push to Frontend** | ✅ | ❌ | **MISSING** - Via WSBroadcaster |

### 2.11 Health & Settings

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| Health Check | ✅ | ✅ | .NET more detailed |
| Readiness Check | ❌ | ✅ | .NET only |
| Get Settings | ✅ | ✅ | Equivalent |
| Update Settings | ✅ | ✅ | Go populates env vars |
| **Google Home Fields** | ✅ | ❌ | **MISSING** in Settings model |

### 2.12 Google Home Integration

| Feature | Go | .NET | Notes |
|---------|:--:|:----:|-------|
| **Auth Endpoint** | ✅ | ❌ | **MISSING** |
| **Callback Endpoint** | ✅ | ❌ | **MISSING** |
| **Disconnect Endpoint** | ✅ | ❌ | **MISSING** |
| **Google Home Service** | ✅ | ❌ | **MISSING** |

---

## 3. Detailed Missing Features

### 3.1 Light Control API (HIGH PRIORITY)

**Go Implementation:** `coordinator_handlers.go`

```go
// Endpoints:
POST /api/v1/coordinator/{coordId}/node/{nodeId}/test-color
POST /api/v1/coordinator/{coordId}/node/{nodeId}/off
POST /api/v1/coordinator/{coordId}/node/{nodeId}/brightness
POST /api/v1/node/light/control
```

**Required .NET Implementation:**

```csharp
// Controllers/LightController.cs
[ApiController]
[Route("api/v1")]
public class LightController : ControllerBase
{
    [HttpPost("coordinator/{coordId}/node/{nodeId}/test-color")]
    public async Task<IActionResult> TestColor(string coordId, string nodeId, [FromBody] ColorRequest request)
    
    [HttpPost("coordinator/{coordId}/node/{nodeId}/off")]
    public async Task<IActionResult> TurnOff(string coordId, string nodeId)
    
    [HttpPost("coordinator/{coordId}/node/{nodeId}/brightness")]
    public async Task<IActionResult> SetBrightness(string coordId, string nodeId, [FromBody] BrightnessRequest request)
    
    [HttpPost("node/light/control")]
    public async Task<IActionResult> LightControl([FromBody] LightControlRequest request)
}
```

**MQTT Commands Sent:**
- `test_color` with payload `{"r": 255, "g": 0, "b": 0, "w": 0}`
- `off` with empty payload
- `brightness` with payload `{"value": 128}`

---

### 3.2 WebSocket Broadcaster (HIGH PRIORITY)

**Go Implementation:** `ws_broadcast.go`

The Go backend has a `WSBroadcaster` that:
1. Maintains a map of all connected WebSocket clients
2. Automatically broadcasts telemetry to ALL clients when received
3. Supports message types: `node_telemetry`, `coord_telemetry`

**Current .NET Implementation:**
- Client must explicitly subscribe to topics
- No automatic broadcasting to all clients

**Required .NET Implementation:**

```csharp
// Services/IWsBroadcaster.cs
public interface IWsBroadcaster
{
    void AddClient(string clientId, WebSocket socket);
    void RemoveClient(string clientId);
    Task BroadcastNodeTelemetry(NodeTelemetry telemetry);
    Task BroadcastCoordinatorTelemetry(CoordinatorTelemetry telemetry);
    Task BroadcastToAll(string messageType, object payload);
}

// Services/WsBroadcaster.cs
public class WsBroadcaster : IWsBroadcaster
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients;
    // Implementation...
}
```

**Integration Point:**
- `TelemetryHandler.cs` should call `IWsBroadcaster.BroadcastNodeTelemetry()` when processing MQTT messages

---

### 3.3 Customization API (MEDIUM PRIORITY)

**Go Implementation:** `customize_handlers.go`

```go
// Endpoints:
GET  /api/v1/customize/config?type={coordinator|node}
PUT  /api/v1/customize/coordinator/{coordId}/config
PUT  /api/v1/customize/coordinator/{coordId}/light
PUT  /api/v1/customize/coordinator/{coordId}/led
POST /api/v1/customize/coordinator/{coordId}/reset
POST /api/v1/customize/coordinator/{coordId}/led/preview
```

**Required .NET Implementation:**

```csharp
// Controllers/CustomizeController.cs
[ApiController]
[Route("api/v1/customize")]
public class CustomizeController : ControllerBase
{
    [HttpGet("config")]
    public IActionResult GetDefaultConfig([FromQuery] string type)
    
    [HttpPut("coordinator/{coordId}/config")]
    public async Task<IActionResult> UpdateCoordinatorConfig(string coordId, [FromBody] CoordinatorConfig config)
    
    [HttpPut("coordinator/{coordId}/light")]
    public async Task<IActionResult> UpdateLightConfig(string coordId, [FromBody] LightSensorConfig config)
    
    [HttpPut("coordinator/{coordId}/led")]
    public async Task<IActionResult> UpdateLedConfig(string coordId, [FromBody] LedConfig config)
    
    [HttpPost("coordinator/{coordId}/reset")]
    public async Task<IActionResult> ResetToDefaults(string coordId)
    
    [HttpPost("coordinator/{coordId}/led/preview")]
    public async Task<IActionResult> LedPreview(string coordId, [FromBody] LedPreviewRequest request)
}
```

**Default Config Structure (from Go):**
```json
{
  "coordinator": {
    "reportInterval": 5000,
    "lightSensor": {
      "enabled": true,
      "threshold": 500,
      "hysteresis": 50
    },
    "led": {
      "brightness": 255,
      "colorTemp": 4000
    }
  }
}
```

---

### 3.4 Coordinator Commands (MEDIUM PRIORITY)

**Go Implementation:** `coordinator_handlers.go`

**Missing Endpoints:**
```
POST /api/v1/coordinator/{coordId}/restart
POST /api/v1/coordinator/{coordId}/wifi
```

**Required Addition to CoordinatorsController.cs:**

```csharp
[HttpPost("{coordId}/restart")]
public async Task<IActionResult> RestartCoordinator(string coordId)
{
    var topic = $"site/{siteId}/coord/{coordId}/cmd";
    await _mqttService.PublishAsync(topic, new { command = "restart" });
    return Ok(new { message = "Restart command sent" });
}

[HttpPost("{coordId}/wifi")]
public async Task<IActionResult> UpdateWiFi(string coordId, [FromBody] WifiConfigRequest request)
{
    var topic = $"site/{siteId}/coord/{coordId}/cmd";
    await _mqttService.PublishAsync(topic, new { 
        command = "update_wifi",
        ssid = request.Ssid,
        password = request.Password
    });
    return Ok(new { message = "WiFi config sent" });
}
```

---

### 3.5 Zone Enhancements (MEDIUM PRIORITY)

**Go Implementation:** `zone_handlers.go`

**Missing Features:**
1. Auto-assign all nodes to zone on creation
2. Send `flash_green` MQTT command on zone changes

**Required Modifications to ZonesController.cs:**

```csharp
[HttpPost]
public async Task<IActionResult> CreateZone(string siteId, [FromBody] Zone zone)
{
    // Existing creation logic...
    
    // Auto-assign all unassigned nodes
    var nodes = await _repository.GetAllAsync<Node>();
    var unassignedNodes = nodes.Where(n => string.IsNullOrEmpty(n.ZoneId));
    foreach (var node in unassignedNodes)
    {
        node.ZoneId = zone.Id;
        await _repository.UpdateAsync(node);
    }
    
    // Flash green feedback
    if (!string.IsNullOrEmpty(zone.CoordinatorId))
    {
        var topic = $"site/{siteId}/coord/{zone.CoordinatorId}/cmd";
        await _mqttService.PublishAsync(topic, new { command = "flash_green" });
    }
    
    return CreatedAtAction(...);
}
```

---

### 3.6 OTA Controller (LOW PRIORITY)

**Go Implementation:** `handlers.go` (stub endpoints)

**Required .NET Implementation:**

```csharp
// Controllers/OtaController.cs
[ApiController]
[Route("api/v1/ota")]
public class OtaController : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartOta([FromBody] OtaStartRequest request)
    
    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetOtaStatus(string jobId)
    
    [HttpGet("jobs")]
    public async Task<IActionResult> GetAllJobs()
}
```

---

### 3.7 Radar PNG Rendering (LOW PRIORITY)

**Go Implementation:** `radar_renderer.go`

The Go backend can render radar data as a PNG image server-side.

**Required .NET Implementation:**

```csharp
// Services/RadarRenderer.cs
public class RadarRenderer
{
    public byte[] RenderToPng(List<MmwaveFrame> frames, int width = 800, int height = 600)
    {
        // Use SkiaSharp or System.Drawing to render
    }
}

// In RadarController.cs
[HttpGet("{coordId}/image")]
[Produces("image/png")]
public async Task<IActionResult> GetRadarImage(string coordId)
```

**Note:** The current architecture prefers JSON-only from backend with frontend rendering. This may be optional.

---

### 3.8 Radar Cache (LOW PRIORITY)

**Go Implementation:** `radar_handlers.go`

```go
type RadarCache struct {
    mu     sync.RWMutex
    frames map[string]*MmwaveFrame // coordId -> latest frame
}
```

**Required .NET Implementation:**

```csharp
// Services/RadarCache.cs
public class RadarCache : IRadarCache
{
    private readonly ConcurrentDictionary<string, MmwaveFrame> _latestFrames;
    
    public void UpdateFrame(string coordId, MmwaveFrame frame);
    public MmwaveFrame? GetLatestFrame(string coordId);
    public IEnumerable<MmwaveFrame> GetAllLatestFrames();
}
```

---

### 3.9 Google Home Integration (OPTIONAL)

**Go Implementation:** `backend/internal/googlehome/`

This is a complete module with:
- OAuth2 flow handlers
- Google Home fulfillment service
- Device sync and execute handlers

**Assessment:** This is a significant feature that may or may not be required for the Bachelor's project. Recommend discussing with stakeholders before implementing.

---

## 4. Model Updates Required

### 4.1 Settings Model

**Current .NET Model:** `Models/Settings.cs`
```csharp
public class Settings
{
    public string Id { get; set; }
    public string SiteId { get; set; }
    // ... existing fields
}
```

**Required Additions:**
```csharp
public class Settings
{
    // ... existing fields
    
    // Google Home Integration
    public bool GoogleHomeEnabled { get; set; }
    public string? GoogleHomeProjectId { get; set; }
    public string? GoogleHomeAccessToken { get; set; }
    public string? GoogleHomeRefreshToken { get; set; }
    public DateTime? GoogleHomeTokenExpiry { get; set; }
}
```

### 4.2 New Request/Response Models

```csharp
// Models/Requests/ColorRequest.cs
public record ColorRequest(byte R, byte G, byte B, byte W);

// Models/Requests/BrightnessRequest.cs
public record BrightnessRequest(int Value);

// Models/Requests/WifiConfigRequest.cs
public record WifiConfigRequest(string Ssid, string Password);

// Models/Requests/LightControlRequest.cs
public record LightControlRequest(string NodeId, string Action, object? Params);

// Models/Requests/LedPreviewRequest.cs
public record LedPreviewRequest(byte R, byte G, byte B, byte W, int Duration);

// Models/CoordinatorConfig.cs
public class CoordinatorConfig
{
    public int ReportInterval { get; set; }
    public LightSensorConfig LightSensor { get; set; }
    public LedConfig Led { get; set; }
}

// Models/LightSensorConfig.cs
public class LightSensorConfig
{
    public bool Enabled { get; set; }
    public int Threshold { get; set; }
    public int Hysteresis { get; set; }
}

// Models/LedConfig.cs
public class LedConfig
{
    public int Brightness { get; set; }
    public int ColorTemp { get; set; }
}
```

---

## 5. Migration Plan

### Phase 1: Core Device Control (Week 1)
**Priority: HIGH**

| Task | Effort | Dependencies |
|------|--------|--------------|
| Create `LightController.cs` | 4 hours | None |
| Create `IWsBroadcaster` + `WsBroadcaster` | 6 hours | None |
| Integrate WsBroadcaster with TelemetryHandler | 2 hours | WsBroadcaster |
| Add restart/wifi to CoordinatorsController | 2 hours | None |
| Add new request models | 1 hour | None |
| Unit tests | 4 hours | All above |

**Total: ~19 hours**

### Phase 2: Enhanced Features (Week 2)
**Priority: MEDIUM**

| Task | Effort | Dependencies |
|------|--------|--------------|
| Create `CustomizeController.cs` | 6 hours | None |
| Create default config builder | 2 hours | None |
| Enhance ZonesController (flash_green, auto-assign) | 3 hours | None |
| Create `OtaController.cs` | 4 hours | None |
| Update Settings model | 1 hour | None |
| Unit tests | 4 hours | All above |

**Total: ~20 hours**

### Phase 3: Optional Features (Week 3)
**Priority: LOW**

| Task | Effort | Dependencies |
|------|--------|--------------|
| Create `RadarCache` service | 3 hours | None |
| Add radar PNG rendering (SkiaSharp) | 6 hours | RadarCache |
| Google Home integration | 16 hours | Settings model |
| Integration tests | 8 hours | All above |

**Total: ~33 hours**

---

## 6. Files to Create/Modify

### 6.1 New Files

| File | Purpose |
|------|---------|
| `Controllers/LightController.cs` | Light control endpoints |
| `Controllers/CustomizeController.cs` | Device configuration endpoints |
| `Controllers/OtaController.cs` | OTA update endpoints |
| `Services/IWsBroadcaster.cs` | WebSocket broadcaster interface |
| `Services/WsBroadcaster.cs` | WebSocket broadcaster implementation |
| `Services/IRadarCache.cs` | Radar cache interface |
| `Services/RadarCache.cs` | Radar cache implementation |
| `Models/Requests/ColorRequest.cs` | Color request DTO |
| `Models/Requests/BrightnessRequest.cs` | Brightness request DTO |
| `Models/Requests/WifiConfigRequest.cs` | WiFi config DTO |
| `Models/Requests/LightControlRequest.cs` | Light control DTO |
| `Models/Requests/LedPreviewRequest.cs` | LED preview DTO |
| `Models/CoordinatorConfig.cs` | Coordinator config model |
| `Models/LightSensorConfig.cs` | Light sensor config model |
| `Models/LedConfig.cs` | LED config model |

### 6.2 Modified Files

| File | Changes |
|------|---------|
| `Controllers/CoordinatorsController.cs` | Add restart, wifi endpoints |
| `Controllers/ZonesController.cs` | Add flash_green, auto-assign logic |
| `Models/Settings.cs` | Add Google Home fields |
| `Services/TelemetryHandler.cs` | Integrate WsBroadcaster |
| `WebSockets/MqttBridgeHandler.cs` | Register with WsBroadcaster |
| `Program.cs` | Register new services |

---

## 7. Testing Strategy

### 7.1 Unit Tests

```
Tests/
├── Controllers/
│   ├── LightControllerTests.cs
│   ├── CustomizeControllerTests.cs
│   └── OtaControllerTests.cs
├── Services/
│   ├── WsBroadcasterTests.cs
│   └── RadarCacheTests.cs
└── Integration/
    └── WebSocketBroadcastTests.cs
```

### 7.2 Integration Tests

- Test MQTT → WsBroadcaster → Frontend flow
- Test light control commands reach MQTT broker
- Test customization commands update devices

### 7.3 Manual Testing with Postman

Update `api-collection.json` with new endpoints for:
- Light control
- Customization
- OTA

---

## 8. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| WebSocket breaking changes | HIGH | Maintain backward compatibility with existing message format |
| MQTT topic changes | MEDIUM | Coordinate with `@firmware` agent |
| Frontend integration | MEDIUM | Coordinate with `@frontend` agent |
| Google Home complexity | LOW | Defer to Phase 3 or post-MVP |

---

## 9. Recommendations

1. **Start with Phase 1** - Light control and WebSocket broadcasting are the most impactful features for user experience.

2. **Coordinate with Frontend** - Before implementing WsBroadcaster, confirm the expected message format with `@frontend` agent.

3. **Defer Google Home** - This is a complex feature that may not be required for the Bachelor's project submission.

4. **Keep Go Backend Running** - During migration, run both backends to ensure no functionality is lost.

5. **Update API Documentation** - Add Swagger annotations to all new endpoints.

---

## 10. Conclusion

The .NET backend provides a solid foundation with approximately **70%** of the required functionality already implemented. The missing **30%** consists primarily of:

1. Real-time features (WebSocket broadcasting)
2. Device control endpoints (light control, customization)
3. Enhanced zone management

With an estimated **3 weeks of development effort**, the .NET backend can achieve full feature parity with the Go backend, enabling the team to maintain a single, unified backend codebase.

---

## Appendix A: MQTT Topic Reference

| Topic Pattern | Direction | Purpose |
|---------------|-----------|---------|
| `site/{siteId}/coord/{coordId}/cmd` | Publish | Commands to coordinator |
| `site/{siteId}/node/{nodeId}/cmd` | Publish | Commands to node |
| `site/{siteId}/coord/{coordId}/telemetry` | Subscribe | Coordinator telemetry |
| `site/{siteId}/node/{nodeId}/telemetry` | Subscribe | Node telemetry |
| `site/{siteId}/coord/{coordId}/mmwave` | Subscribe | Radar data |
| `site/{siteId}/coord/{coordId}/status` | Subscribe | Coordinator status |

## Appendix B: Command Reference

| Command | Payload | Target |
|---------|---------|--------|
| `test_color` | `{"r":255,"g":0,"b":0,"w":0}` | Node |
| `off` | `{}` | Node |
| `brightness` | `{"value":128}` | Node |
| `restart` | `{}` | Coordinator |
| `update_wifi` | `{"ssid":"...","password":"..."}` | Coordinator |
| `update_config` | `{...config object...}` | Coordinator |
| `reset_config` | `{}` | Coordinator |
| `led_preview` | `{"r":255,"g":0,"b":0,"w":0,"duration":5000}` | Coordinator |
| `flash_green` | `{}` | Coordinator |
| `discover` | `{}` | Coordinator |
| `pair` | `{"duration":30}` | Coordinator |
