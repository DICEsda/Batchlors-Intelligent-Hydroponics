# Phase A Implementation Plan — Frontend Polish & Production Hardening

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the Intelligent Hydroponics system thesis-ready with a polished alerts dashboard, real-time telemetry charts, 3D tower visualization, and basic backend security (API key auth, input validation, error handling, indexes).

**Architecture:** Phase A adds 3 frontend features (alerts page, charts on detail pages, Three.js 3D view) and 4 backend hardening items (API key middleware, FluentValidation, global error handler, MongoDB indexes). Backend tasks are independent. Frontend tasks are independent. The API key interceptor bridges both domains.

**Tech Stack:** Angular 19 (signals, standalone components, ECharts, Three.js), ASP.NET Core 8 (FluentValidation, ProblemDetails), MongoDB indexes, Spartan UI (Helm), TailwindCSS.

**Design doc:** `docs/plans/2026-02-24-frontend-polish-and-production-hardening-design.md`

---

## Parallelism Map

```
Backend (parallel batch):          Frontend (parallel batch):
  Task 1: Global Error Handler       Task 5: Alerts Dashboard
  Task 2: API Key Auth (backend)     Task 6: Real-time Telemetry Charts
  Task 3: MongoDB Indexes            Task 7: 3D Tower Rack View
  Task 4: Input Validation
          │
          └─── Task 8: API Key Interceptor (frontend) ───┘
```

Tasks 1-4 are backend-only and independent of each other.
Tasks 5-7 are frontend-only and independent of each other.
Task 8 bridges backend Task 2 to the frontend (adds API key header to HTTP calls).

Tasks 1-7 can all run in parallel. Task 8 depends on Task 2.

---

## Task 1: Global Error Handler

**Domain:** Backend
**Files:**
- Create: `Backend/src/IoT.Backend/Middleware/ErrorHandlingMiddleware.cs`
- Modify: `Backend/src/IoT.Backend/Program.cs` (lines 154-170)

### Step 1: Create the error handling middleware

Create `Backend/src/IoT.Backend/Middleware/ErrorHandlingMiddleware.cs`:

```csharp
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace IoT.Backend.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}, Path={Path}",
                correlationId, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title = "An unexpected error occurred",
                status = 500,
                detail = _env.IsDevelopment() ? ex.ToString() : "An internal server error occurred.",
                instance = context.Request.Path.Value,
                correlationId
            };

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            await context.Response.WriteAsync(json);
        }
    }
}
```

### Step 2: Wire into Program.cs

In `Program.cs`, add after `var app = builder.Build();` (line 154) and before Swagger/CORS:

```csharp
// --- Add after line 154: var app = builder.Build(); ---
app.UseMiddleware<IoT.Backend.Middleware.ErrorHandlingMiddleware>();
```

This must be the **first middleware** in the pipeline so it catches everything.

### Step 3: Verify

Run: `dotnet build` in `Backend/src/IoT.Backend/`
Expected: Build succeeds with no errors.

### Step 4: Commit

```
feat(backend): add global error handling middleware with correlation IDs

ProblemDetails responses with correlation ID on all unhandled exceptions.
Development mode includes stack trace; production returns generic message.
```

---

## Task 2: API Key Authentication (Backend)

**Domain:** Backend
**Files:**
- Create: `Backend/src/IoT.Backend/Middleware/ApiKeyMiddleware.cs`
- Modify: `Backend/src/IoT.Backend/Program.cs` (add middleware + config)
- Modify: `Backend/src/IoT.Backend/appsettings.json` (add ApiSecurity section)
- Modify: `docker-compose.yml` (add API_KEY env var)
- Modify: `docker-compose.simulation.yml` (add API_KEY env var)

### Step 1: Create the API key middleware

Create `Backend/src/IoT.Backend/Middleware/ApiKeyMiddleware.cs`:

```csharp
using System.Net;
using System.Text.Json;

namespace IoT.Backend.Middleware;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly string? _apiKey;

    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/alive",
        "/ws",
        "/swagger"
    };

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger, IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _apiKey = config["ApiSecurity:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip if no API key is configured (backward compat / dev mode)
        if (string.IsNullOrEmpty(_apiKey))
        {
            await _next(context);
            return;
        }

        // Exempt paths
        var path = context.Request.Path.Value ?? "";
        if (ExemptPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Check header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            providedKey.ToString() != _apiKey)
        {
            _logger.LogWarning("Unauthorized API request. Path={Path}, IP={IP}",
                path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                title = "Unauthorized",
                status = 401,
                detail = "Missing or invalid API key. Provide a valid key via the X-API-Key header.",
                instance = path
            };

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }
}
```

### Step 2: Add config section to appsettings.json

Add to `appsettings.json` at root level:

```json
"ApiSecurity": {
    "ApiKey": null
}
```

When `null`, auth is disabled (dev mode). Set via env var `ApiSecurity__ApiKey` or `API_KEY` override in docker-compose.

### Step 3: Wire into Program.cs

In `Program.cs`, add **after** the ErrorHandlingMiddleware (from Task 1) and **before** `UseSerilogRequestLogging()`:

```csharp
app.UseMiddleware<IoT.Backend.Middleware.ApiKeyMiddleware>();
```

### Step 4: Update docker-compose files

In `docker-compose.yml`, add to the backend service environment:

```yaml
ApiSecurity__ApiKey: ${API_KEY:-hydro-thesis-2026}
```

In `docker-compose.simulation.yml`, add the same to the backend service environment.

### Step 5: Verify

Run: `dotnet build` in `Backend/src/IoT.Backend/`
Expected: Build succeeds.

### Step 6: Commit

```
feat(backend): add API key authentication middleware

X-API-Key header required on all /api/* endpoints when configured.
Health checks, WebSocket, and Swagger endpoints are exempt.
Disabled by default (null key) for dev; enabled via env var in Docker.
```

---

## Task 3: MongoDB Missing Indexes

**Domain:** Backend
**Files:**
- Modify: `Backend/src/IoT.Backend/Data/MongoRepository.cs` (add indexes in `EnsureIndexes` method)

### Step 1: Identify existing index code

The existing indexes are created in `MongoRepository.cs` around lines 45-65. Add the missing compound indexes in the same pattern.

### Step 2: Add compound indexes

Add these indexes after the existing TTL indexes:

```csharp
// Coordinator compound index
var coordCollection = _database.GetCollection<BsonDocument>("coordinators");
await coordCollection.Indexes.CreateOneAsync(
    new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys
            .Ascending("farm_id")
            .Ascending("coord_id"),
        new CreateIndexOptions { Name = "farm_coord_idx", Background = true }));

// Tower compound index
var towerCollection = _database.GetCollection<BsonDocument>("towers");
await towerCollection.Indexes.CreateOneAsync(
    new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys
            .Ascending("farm_id")
            .Ascending("coord_id")
            .Ascending("tower_id"),
        new CreateIndexOptions { Name = "farm_coord_tower_idx", Background = true }));

// Tower telemetry compound index (for history queries)
var towerTelCollection = _database.GetCollection<BsonDocument>("tower_telemetry");
await towerTelCollection.Indexes.CreateOneAsync(
    new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys
            .Ascending("farm_id")
            .Ascending("coord_id")
            .Ascending("tower_id")
            .Descending("timestamp"),
        new CreateIndexOptions { Name = "farm_coord_tower_ts_idx", Background = true }));

// Reservoir telemetry compound index (for history queries)
var resTelCollection = _database.GetCollection<BsonDocument>("reservoir_telemetry");
await resTelCollection.Indexes.CreateOneAsync(
    new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys
            .Ascending("farm_id")
            .Ascending("coord_id")
            .Descending("timestamp"),
        new CreateIndexOptions { Name = "farm_coord_ts_idx", Background = true }));
```

### Step 3: Verify

Run: `dotnet build` in `Backend/src/IoT.Backend/`
Expected: Build succeeds.

### Step 4: Commit

```
feat(backend): add missing MongoDB compound indexes for hot-path queries

Adds indexes on coordinators (farm+coord), towers (farm+coord+tower),
and telemetry collections (farm+coord+tower+timestamp desc) to optimize
the most frequent query patterns.
```

---

## Task 4: Input Validation (FluentValidation)

**Domain:** Backend
**Files:**
- Modify: `Backend/src/IoT.Backend/IoT.Backend.csproj` (add NuGet package)
- Create: `Backend/src/IoT.Backend/Validators/` directory with validator classes
- Modify: `Backend/src/IoT.Backend/Program.cs` (wire FluentValidation)

### Step 1: Add FluentValidation NuGet package

Run in `Backend/src/IoT.Backend/`:

```bash
dotnet add package FluentValidation.AspNetCore --version 11.3.0
```

### Step 2: Create validators

Create `Backend/src/IoT.Backend/Validators/PairingValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class StartPairingRequestValidator : AbstractValidator<StartPairingRequest>
{
    public StartPairingRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DurationSeconds).InclusiveBetween(10, 300);
    }
}

public class StopPairingRequestValidator : AbstractValidator<StopPairingRequest>
{
    public StopPairingRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
    }
}

public class ApproveTowerRequestValidator : AbstractValidator<ApproveTowerRequest>
{
    public ApproveTowerRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TowerId).NotEmpty().MaximumLength(100);
    }
}

public class RejectTowerRequestValidator : AbstractValidator<RejectTowerRequest>
{
    public RejectTowerRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TowerId).NotEmpty().MaximumLength(100);
    }
}

public class ForgetDeviceRequestValidator : AbstractValidator<ForgetDeviceRequest>
{
    public ForgetDeviceRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TowerId).NotEmpty().MaximumLength(100);
    }
}
```

Create `Backend/src/IoT.Backend/Validators/TowerValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class UpdateNameRequestValidator : AbstractValidator<UpdateNameRequest>
{
    public UpdateNameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class SetLightRequestValidator : AbstractValidator<SetLightRequest>
{
    public SetLightRequestValidator()
    {
        RuleFor(x => x.Brightness).InclusiveBetween(0, 100).When(x => x.Brightness.HasValue);
    }
}

public class SetPumpRequestValidator : AbstractValidator<SetPumpRequest>
{
    public SetPumpRequestValidator()
    {
        RuleFor(x => x.DurationSeconds).GreaterThan(0).When(x => x.DurationSeconds.HasValue);
    }
}

public class RecordHeightRequestValidator : AbstractValidator<RecordHeightRequest>
{
    public RecordHeightRequestValidator()
    {
        RuleFor(x => x.SlotIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HeightCm).GreaterThan(0).LessThanOrEqualTo(300);
        RuleFor(x => x.Method).IsInEnum();
    }
}

public class SetCropRequestValidator : AbstractValidator<SetCropRequest>
{
    public SetCropRequestValidator()
    {
        RuleFor(x => x.CropType).IsInEnum();
    }
}

public class LedPreviewRequestValidator : AbstractValidator<LedPreviewRequest>
{
    public LedPreviewRequestValidator()
    {
        RuleFor(x => x.Brightness).InclusiveBetween(0, 100);
        RuleFor(x => x.Duration).InclusiveBetween(1, 60);
    }
}
```

Create `Backend/src/IoT.Backend/Validators/ReservoirValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class DosingRequestValidator : AbstractValidator<DosingRequest>
{
    public DosingRequestValidator()
    {
        RuleFor(x => x.PhUpMl).GreaterThanOrEqualTo(0).When(x => x.PhUpMl.HasValue);
        RuleFor(x => x.PhDownMl).GreaterThanOrEqualTo(0).When(x => x.PhDownMl.HasValue);
        RuleFor(x => x.NutrientAMl).GreaterThanOrEqualTo(0).When(x => x.NutrientAMl.HasValue);
        RuleFor(x => x.NutrientBMl).GreaterThanOrEqualTo(0).When(x => x.NutrientBMl.HasValue);
        RuleFor(x => x.NutrientCMl).GreaterThanOrEqualTo(0).When(x => x.NutrientCMl.HasValue);
        // At least one dosing amount must be specified
        RuleFor(x => x).Must(x =>
            x.PhUpMl.HasValue || x.PhDownMl.HasValue ||
            x.NutrientAMl.HasValue || x.NutrientBMl.HasValue || x.NutrientCMl.HasValue)
            .WithMessage("At least one dosing amount must be specified.");
    }
}

public class ReservoirPumpRequestValidator : AbstractValidator<ReservoirPumpRequest>
{
    public ReservoirPumpRequestValidator()
    {
        RuleFor(x => x.DurationSeconds).GreaterThan(0).When(x => x.DurationSeconds.HasValue);
    }
}

public class ReservoirTargetsRequestValidator : AbstractValidator<ReservoirTargetsRequest>
{
    public ReservoirTargetsRequestValidator()
    {
        RuleFor(x => x.PhMin).InclusiveBetween(0, 14).When(x => x.PhMin.HasValue);
        RuleFor(x => x.PhMax).InclusiveBetween(0, 14).When(x => x.PhMax.HasValue);
        RuleFor(x => x.EcMin).GreaterThanOrEqualTo(0).When(x => x.EcMin.HasValue);
        RuleFor(x => x.EcMax).GreaterThanOrEqualTo(0).When(x => x.EcMax.HasValue);
        RuleFor(x => x.WaterTempMinC).InclusiveBetween(-10, 50).When(x => x.WaterTempMinC.HasValue);
        RuleFor(x => x.WaterTempMaxC).InclusiveBetween(-10, 50).When(x => x.WaterTempMaxC.HasValue);
        // Min must be less than max when both are provided
        RuleFor(x => x).Must(x => !x.PhMin.HasValue || !x.PhMax.HasValue || x.PhMin < x.PhMax)
            .WithMessage("PhMin must be less than PhMax.");
        RuleFor(x => x).Must(x => !x.EcMin.HasValue || !x.EcMax.HasValue || x.EcMin < x.EcMax)
            .WithMessage("EcMin must be less than EcMax.");
        RuleFor(x => x).Must(x => !x.WaterTempMinC.HasValue || !x.WaterTempMaxC.HasValue || x.WaterTempMinC < x.WaterTempMaxC)
            .WithMessage("WaterTempMinC must be less than WaterTempMaxC.");
    }
}
```

Create `Backend/src/IoT.Backend/Validators/OtaValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class StartOtaRequestValidator : AbstractValidator<StartOtaRequest>
{
    public StartOtaRequestValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoordId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TargetVersion).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RolloutPercentage).InclusiveBetween(0, 100).When(x => x.RolloutPercentage.HasValue);
        RuleFor(x => x.FailureThreshold).InclusiveBetween(0, 100).When(x => x.FailureThreshold.HasValue);
        RuleFor(x => x.MaxConcurrent).GreaterThan(0).When(x => x.MaxConcurrent.HasValue);
    }
}

public class CreateFirmwareRequestValidator : AbstractValidator<CreateFirmwareRequest>
{
    public CreateFirmwareRequestValidator()
    {
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DownloadUrl).NotEmpty().MaximumLength(500);
    }
}
```

Create `Backend/src/IoT.Backend/Validators/ZoneValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class CreateZoneRequestValidator : AbstractValidator<CreateZoneRequest>
{
    public CreateZoneRequestValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CoordinatorId).NotEmpty().MaximumLength(100);
    }
}
```

Create `Backend/src/IoT.Backend/Validators/CoordinatorValidators.cs`:

```csharp
using FluentValidation;
using IoT.Backend.Models.Requests;

namespace IoT.Backend.Validators;

public class WifiConfigRequestValidator : AbstractValidator<WifiConfigRequest>
{
    public WifiConfigRequestValidator()
    {
        RuleFor(x => x.Ssid).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(64);
    }
}
```

### Step 3: Wire FluentValidation into Program.cs

In `Program.cs`, after `builder.Services.AddControllers(...)` (line 49), add:

```csharp
using FluentValidation;
using FluentValidation.AspNetCore;

// After AddControllers:
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

### Step 4: Verify

Run: `dotnet build` in `Backend/src/IoT.Backend/`
Expected: Build succeeds.

### Step 5: Commit

```
feat(backend): add FluentValidation input validation for all request DTOs

27 request DTOs now validated: string length limits, numeric ranges
(pH 0-14, brightness 0-100, etc.), required fields, enum checks.
Invalid requests return 400 with structured field-level errors.
```

---

## Task 5: Alerts Dashboard

**Domain:** Frontend
**Files:**
- Create: `Frontend/IOT-Frontend-main/src/app/pages/alerts-dashboard/alerts-dashboard.component.ts`
- Create: `Frontend/IOT-Frontend-main/src/app/pages/alerts-dashboard/alerts-dashboard.component.html`
- Create: `Frontend/IOT-Frontend-main/src/app/pages/alerts-dashboard/alerts-dashboard.component.scss`
- Modify: `Frontend/IOT-Frontend-main/src/app/app.routes.ts` (add route, before wildcard)

### Step 1: Create the alerts dashboard component

The component should:
- Be standalone, use `AlertService` for all state (it already has `filteredAlerts`, `alertStats`, filter signals, actions)
- Inject `AlertService`, `WebSocketService`, `ToastService`
- Display a **summary bar** at top: total active (red badge), acknowledged (amber), resolved (green), resolution rate %
- Display **filter controls**: severity dropdown (all/critical/warning/info), status dropdown (all/active/acknowledged/resolved), search input
- Display **alert cards** in a scrollable list, each card showing:
  - Severity icon + badge (critical=red, warning=amber, info=blue)
  - Alert message / metric name
  - Source device ID (coordinator or tower)
  - Threshold vs actual value
  - Timestamp (relative, e.g. "5m ago")
  - Status badge
  - Action buttons: Acknowledge (if active), Resolve (if acknowledged), Dismiss
- Sort by: timestamp descending (newest first), with critical alerts pinned to top
- Real-time: new alerts from `AlertService.addAlert()` (triggered by WS) appear at top with subtle animation

Use existing Spartan UI components: `HlmCard`, `HlmBadge`, `HlmButton`, `HlmIcon`, `HlmInput`, `HlmSelect`, `HlmTabs`.

### Step 2: Add the route

In `app.routes.ts`, add before the wildcard redirect (before line 251):

```typescript
{
    path: 'alerts-dashboard',
    loadComponent: () => import('./pages/alerts-dashboard/alerts-dashboard.component')
        .then(m => m.AlertsDashboardComponent),
    title: 'Alerts Dashboard',
},
```

### Step 3: Add sidebar navigation

In the sidebar component, add "Alerts Dashboard" entry with a `bell-ring` Lucide icon, linking to `/alerts-dashboard`.

### Step 4: Verify

Run: `npx ng build` in `Frontend/IOT-Frontend-main/`
Expected: Build succeeds.

### Step 5: Commit

```
feat(frontend): add alerts dashboard page with real-time updates

Dedicated alerts page with severity/status filtering, alert cards,
acknowledge/resolve actions, and summary statistics. Integrates with
existing AlertService and WebSocket alert events.
```

---

## Task 6: Real-time Telemetry Charts

**Domain:** Frontend
**Files:**
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/telemetry-chart/telemetry-chart.component.ts` (reusable ECharts wrapper)
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/telemetry-chart/telemetry-chart.component.html`
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/telemetry-chart/telemetry-chart.component.scss`
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/coordinator-detail/coordinator-detail.component.ts` (add chart imports + data)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/coordinator-detail/coordinator-detail.component.html` (add chart section)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/node-detail/node-detail.component.ts` (add chart imports + data)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/node-detail/node-detail.component.html` (add chart section)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/farm-overview/farm-overview.component.ts` (add chart section)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/farm-overview/farm-overview.component.html` (add system health charts)

### Step 1: Create reusable telemetry chart component

A standalone component wrapping `ngx-echarts` with these inputs:
- `@Input() title: string` — chart title (e.g., "pH Level")
- `@Input() unit: string` — unit label (e.g., "pH", "°C", "%")
- `@Input() data: { time: Date; value: number }[]` — time series data
- `@Input() color: string` — line color (CSS variable or hex)
- `@Input() minY?: number` — optional Y-axis min
- `@Input() maxY?: number` — optional Y-axis max
- `@Input() thresholdLow?: number` — optional low threshold line
- `@Input() thresholdHigh?: number` — optional high threshold line
- `@Input() height: string = '200px'` — chart height

Internal behavior:
- Rolling 1-hour window (auto-prune old data)
- Dark theme compatible (read CSS variables for colors)
- Responsive width
- Area fill under line with gradient
- Threshold lines as dashed horizontal markLines
- Tooltip showing exact value + time on hover
- Smooth animation on new data points

Method:
- `appendData(point: { time: Date; value: number })` — add a single point and trigger chart update

### Step 2: Add charts to coordinator detail

In `coordinator-detail.component.ts`:
- Import `TelemetryChartComponent` and `ApiService`
- On init, call `apiService.getCoordinatorHistory(coordId, '1h')` to load initial data
- Subscribe to `wsService.reservoirTelemetry$` to append new points
- Maintain signal arrays for: `phData`, `ecData`, `waterLevelData`, `waterTempData`

In the template, add a "Telemetry" section with 4 charts in a 2x2 grid:
- pH (range 0-14, thresholds from alert config: low=5.5, high=7.5)
- EC (range 0-5, thresholds: low=0.8, high=3.0)
- Water Level (range 0-100%)
- Water Temperature (range 10-40°C)

### Step 3: Add charts to tower detail

In `node-detail.component.ts`:
- Import `TelemetryChartComponent`
- On init, call `apiService.getNodeHistory(nodeId, '1h')` for initial data
- Subscribe to `wsService.towerTelemetry$` (filter by nodeId) to append
- Maintain: `tempData`, `humidityData`, `lightData`, `batteryData`

Template: 4 charts in 2x2 grid:
- Temperature (range -10..50°C, thresholds: high=35)
- Humidity (range 0-100%)
- Light Level (range 0-100%)
- Battery Voltage (range 2500-4200 mV)

### Step 4: Add system health charts to farm overview

In `farm-overview.component.ts`:
- Add a "System Health" section with 2-3 mini sparklines
- Subscribe to `wsService.reservoirTelemetry$` to aggregate across coordinators
- Show: avg pH, avg EC, avg water level across all coordinators

Template: 3 small sparklines in a row, compact height (120px).

### Step 5: Verify

Run: `npx ng build` in `Frontend/IOT-Frontend-main/`
Expected: Build succeeds.

### Step 6: Commit

```
feat(frontend): add real-time telemetry charts to detail pages and overview

Reusable ECharts TelemetryChartComponent with rolling 1-hour window,
threshold lines, and dark theme support. Added to coordinator detail
(pH/EC/water level/temp), tower detail (temp/humidity/light/battery),
and farm overview (system health sparklines).
```

---

## Task 7: 3D Tower Rack View

**Domain:** Frontend
**Files:**
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/tower-rack-3d/tower-rack-3d.component.ts`
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/tower-rack-3d/tower-rack-3d.component.html`
- Create: `Frontend/IOT-Frontend-main/src/app/components/ui/tower-rack-3d/tower-rack-3d.component.scss`
- Create: `Frontend/IOT-Frontend-main/src/app/services/three-scene.service.ts` (optional — or inline in component)
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/digital-twin/digital-twin.component.ts`
- Modify: `Frontend/IOT-Frontend-main/src/app/pages/digital-twin/digital-twin.component.html`

### Step 1: Create the 3D tower rack component

Standalone component with Three.js scene:

**Inputs:**
- `@Input() towers: TowerTwin[]` — tower data from TwinService
- `@Input() alerts: Alert[]` — active alerts for color-coding
- `@Output() towerSelected = new EventEmitter<string>()` — emits tower ID on click

**Scene setup:**
- `THREE.WebGLRenderer` with alpha + antialias, attached to a canvas element
- `THREE.PerspectiveCamera` with isometric-like angle (azimuth 45°, elevation 30°)
- `THREE.AmbientLight` (soft white) + `THREE.DirectionalLight` (subtle shadow)
- Ground plane: flat dark rectangle
- `OrbitControls` from `three/addons/controls/OrbitControls.js` for rotate/zoom/pan

**Tower geometry:**
- Each tower: `THREE.CylinderGeometry(radius=0.3, height=2.5, segments=16)` with `THREE.MeshStandardMaterial`
- Arranged in a grid: `Math.ceil(sqrt(n))` columns, rows as needed, spacing 1.5 units
- Color by health status:
  - Green (`#22c55e`): online, no active alerts
  - Amber (`#f59e0b`): has warning-level alerts
  - Red (`#ef4444`): has critical alerts or offline with error
  - Gray (`#6b7280`): offline / no data
- Small cylinder cap on top (lighter shade) for visual depth

**Labels (CSS2DRenderer):**
- Import `CSS2DRenderer`, `CSS2DObject` from `three/addons/renderers/CSS2DRenderer.js`
- Each tower gets a floating HTML label above it: tower ID (truncated) + key metric value
- Styled with Tailwind classes (text-xs, bg-card, rounded, px-1)

**Interaction:**
- `THREE.Raycaster` on click → detect which tower mesh was hit
- Emit `towerSelected` with the tower ID
- Highlight selected tower with emissive glow or outline

**Animation loop:**
- `requestAnimationFrame` render loop
- Update label positions each frame
- Smooth color transitions when health changes (lerp material color)

**Lifecycle:**
- `ngAfterViewInit`: create scene, renderer, camera, controls
- `ngOnChanges`: rebuild towers when `towers` input changes
- `ngOnDestroy`: dispose renderer, geometries, materials, remove event listeners

### Step 2: Integrate into digital twin page

Modify `digital-twin.component.ts`:
- Import `TowerRack3dComponent`
- Add a coordinator selector dropdown (from `coordTwins`)
- When a coordinator is selected, filter `towerTwins` for that coordinator → pass to `<app-tower-rack-3d>`
- Add a side panel that shows selected tower detail when `towerSelected` fires
- Keep the existing card-based view as a fallback/alternative tab ("3D View" | "Card View")

### Step 3: Handle real-time updates

- Subscribe to `wsService.towerTelemetry$` and `wsService.alerts$`
- Update tower health states → component detects changes via `ngOnChanges` and updates colors/labels

### Step 4: Responsive + dark theme

- Renderer resizes with container (`ResizeObserver`)
- Background color reads from CSS `--background` variable
- Label styling uses CSS variables for colors

### Step 5: Verify

Run: `npx ng build` in `Frontend/IOT-Frontend-main/`
Expected: Build succeeds. No Three.js import errors.

### Step 6: Commit

```
feat(frontend): add 3D tower rack visualization on digital twin page

Three.js scene with procedural tower cylinders, health-based color
coding, CSS2D labels, OrbitControls, and click-to-select interaction.
Integrated into digital twin page with coordinator selector and side
panel detail view. Real-time updates via WebSocket.
```

---

## Task 8: API Key Frontend Interceptor

**Domain:** Frontend
**Depends on:** Task 2 (backend API key middleware must exist)
**Files:**
- Create: `Frontend/IOT-Frontend-main/src/app/core/api-key.interceptor.ts`
- Modify: `Frontend/IOT-Frontend-main/src/app/app.config.ts` (register interceptor)
- Modify: `Frontend/IOT-Frontend-main/src/app/core/services/environment.service.ts` (add apiKey config)

### Step 1: Create the API key interceptor

Create `Frontend/IOT-Frontend-main/src/app/core/api-key.interceptor.ts`:

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { EnvironmentService } from './services/environment.service';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
    const env = inject(EnvironmentService);
    const apiKey = env.apiKey;

    if (apiKey && req.url.startsWith(env.apiUrl)) {
        const cloned = req.clone({
            setHeaders: { 'X-API-Key': apiKey }
        });
        return next(cloned);
    }

    return next(req);
};
```

### Step 2: Add apiKey to EnvironmentService

In `environment.service.ts`, add:

```typescript
get apiKey(): string {
    return (window as any).__env?.apiKey ?? 'hydro-thesis-2026';
}
```

The default matches the docker-compose default. For production, override via runtime config.

### Step 3: Register the interceptor

In `app.config.ts`, add `apiKeyInterceptor` to the `provideHttpClient(withInterceptors([...]))` chain, alongside the existing `snakeCaseInterceptor`.

### Step 4: Verify

Run: `npx ng build` in `Frontend/IOT-Frontend-main/`
Expected: Build succeeds.

### Step 5: Commit

```
feat(frontend): add API key HTTP interceptor

All API requests now include X-API-Key header. Key is configurable
via runtime environment; defaults to match docker-compose default.
```

---

## Verification Checklist

After all 8 tasks are complete:

1. **Backend builds:** `dotnet build` in `Backend/src/IoT.Backend/` — no errors
2. **Frontend builds:** `npx ng build` in `Frontend/IOT-Frontend-main/` — no errors
3. **Docker compose up:** `docker-compose up --build` — all services healthy
4. **API key enforcement:** `curl http://localhost:8000/api/farms` returns 401; `curl -H "X-API-Key: hydro-thesis-2026" http://localhost:8000/api/farms` returns 200
5. **Validation:** `curl -X POST -H "X-API-Key: hydro-thesis-2026" -H "Content-Type: application/json" -d '{"farm_id":"","coord_id":""}' http://localhost:8000/api/pairing/start` returns 400 with field errors
6. **Error handling:** Intentional server error returns ProblemDetails JSON with correlation ID
7. **Alerts dashboard:** Navigate to `/alerts-dashboard` — page loads, shows alerts (or mock data)
8. **Charts:** Navigate to coordinator/tower detail — ECharts render with data
9. **3D view:** Navigate to `/digital-twin` — Three.js canvas renders towers
10. **Simulation tests still pass:** `pytest tools/simulator/tests/ -v` — 30/30 pass (with API key header added to test fixtures)

---

## Post-Task: Update simulation test fixtures

The simulation test suite (`tools/simulator/tests/conftest.py`) needs updating to send the `X-API-Key` header. Update `ApiTestClient` to include `headers={"X-API-Key": "hydro-thesis-2026"}` in all requests. This ensures the existing 30 tests continue to pass with auth enabled.
