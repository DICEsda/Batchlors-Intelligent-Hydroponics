# Frontend Polish & Production Hardening — Design

**Date:** 2026-02-24  
**Status:** Approved  
**Approach:** Phased — Phase A (thesis-grade) first, Phase B (production) later

## Context

The Intelligent Hydroponics system has a complete Angular 19 frontend (18 pages, signal-based state, 3-layer notification system, full pairing workflow, ECharts diagnostics) and an ASP.NET Core 8 backend with clean architecture but zero security controls. The frontend needs visual polish (alerts dashboard, real-time charts, 3D visualization) and the backend needs hardening (auth, validation, error handling) to be thesis-ready and eventually production-deployable.

## Phase A — Thesis Grade

### A1. Alerts Dashboard

**Problem:** The `AlertService` has full CRUD, filtering, and statistics, but no dedicated page displays alerts. The `/logs` route shows coordinator logs, not alert cards. Alerts are only visible as ephemeral toasts.

**Solution:** New page at `/alerts-dashboard`:

- **Alert cards** — severity badge (critical/warning/info), source device ID, metric name, threshold vs actual value, timestamp, status (active/acknowledged/resolved)
- **Real-time** — WS `alert` events push new cards to the top via existing `alertSubject`
- **Filters** — severity, status, source coordinator, date range
- **Actions** — acknowledge (`POST /api/alerts/{id}/acknowledge`), resolve (`PUT /api/alerts/{id}`)
- **Summary bar** — count by severity, active count, resolution rate
- **Stack:** Spartan UI cards/badges/tables/tabs, existing `AlertService` for state

### A2. Real-time Telemetry Charts

**Problem:** Only the diagnostics-sensors page has charts. Coordinator and tower detail pages show raw numbers. The farm overview has no visual telemetry.

**Solution:** Add ECharts sparklines/mini-charts to three pages:

- **Coordinator detail** — pH, EC, water level, water temperature. Data from WS `reservoir_telemetry` + `GET /api/telemetry/coordinator/{id}/history`
- **Tower detail** — ambient temp, humidity, light, battery voltage. Data from WS `tower_telemetry` + history API
- **Farm overview** — "System Health" section with aggregated mini-charts (1-hour rolling window across all coordinators)

All charts: ECharts via `ngx-echarts`, 1-hour rolling window, auto-append on WS events, responsive sizing, dark-theme compatible.

### A3. 3D Tower Rack View

**Problem:** The digital twin page is data-only cards. BabylonJS and Three.js are installed but unused. No spatial visualization of the farm.

**Solution:** Replace the digital twin page content with a Three.js 3D scene:

- **Scene:** Isometric view of a coordinator's tower rack
- **Towers:** Vertical cylinders arranged in a grid, color-coded by health (green=healthy, amber=warning, red=critical, gray=offline)
- **Labels:** CSS2DRenderer floating text above each tower (ID + key metric)
- **Interaction:** Click tower to select → side panel with full telemetry and alert status
- **Real-time:** Colors and labels update via WS `tower_telemetry` and `alert` events
- **Camera:** OrbitControls for rotate/zoom/pan
- **Coordinator selector:** Dropdown to switch coordinators (each with its own rack layout)
- **Tech:** Three.js, OrbitControls, CSS2DRenderer (no heavy 3D models — procedural geometry only)

### A4. API Key Authentication

**Problem:** Zero authentication on all endpoints. Anyone who can reach port 8000 can read all data, send MQTT commands, upload firmware.

**Solution:** Simple API key middleware:

- Middleware checks `X-API-Key` header against configured value
- **Exempt:** `/health`, `/ws/*`, Swagger UI
- **Config:** `ApiSecurity:ApiKey` in appsettings, override via `API_KEY` env var
- **Frontend:** HTTP interceptor adds key to all outbound requests
- **Response:** 401 ProblemDetails on missing/invalid key
- Sufficient for thesis demo; replaced by JWT in Phase B

### A5. Input Validation

**Problem:** ~20 request DTOs with zero validation attributes. No FluentValidation, no data annotations, no `ModelState.IsValid` checks. Empty/garbage payloads silently accepted.

**Solution:** FluentValidation framework:

- Add `FluentValidation.AspNetCore` NuGet package
- Create validators for all request DTOs in `Models/Requests/`
- Wire via `AddFluentValidationAutoValidation()` in `Program.cs`
- Key rules: string length limits, numeric ranges (pH 0-14, temp -40..80, EC 0..10, water level 0-100%), required fields, ID format patterns (`farmId/coordId/towerId`)
- 400 responses with structured field-level errors

### A6. Global Error Handler

**Problem:** No `UseExceptionHandler()`, no ProblemDetails, no correlation IDs. Unhandled exceptions return generic 500.

**Solution:**

- `app.UseExceptionHandler()` with custom ProblemDetails factory
- Correlation ID (`X-Correlation-Id`) in all responses
- Serilog structured logging of all exceptions with correlation ID
- Development: full stack trace in ProblemDetails. Production: generic message only

### A7. MongoDB Missing Indexes

**Problem:** Hot-path queries on `coordinators`, `towers`, and telemetry collections lack compound indexes.

**Solution:** Add indexes at repository initialization:

| Collection | Index |
|---|---|
| `coordinators` | `{ farm_id: 1, coord_id: 1 }` |
| `towers` | `{ farm_id: 1, coord_id: 1, tower_id: 1 }` |
| `tower_telemetry` | `{ farm_id: 1, coord_id: 1, tower_id: 1, timestamp: -1 }` |
| `reservoir_telemetry` | `{ farm_id: 1, coord_id: 1, timestamp: -1 }` |

## Phase B — Production Grade (planned, not yet implemented)

| ID | Item | Description |
|---|---|---|
| B1 | JWT + RBAC | Replace API key with JWT bearer tokens, role claims (admin/viewer), `[Authorize(Roles)]` on write endpoints |
| B2 | HTTPS | Kestrel TLS with configurable certificate, HSTS middleware |
| B3 | MQTT TLS + ACL | TLS listener on 8883, ACL file restricting topic access per user/role |
| B4 | Rate limiting | `AddRateLimiter()` — fixed window per IP, stricter on write endpoints |
| B5 | Secrets management | Remove hardcoded defaults, `.env.example` template, `dotnet user-secrets` for dev |
| B6 | Docker hardening | Resource limits (memory/CPU), internal-only ports, `security_opt: no-new-privileges` |
| B7 | Login page | Angular login form, JWT storage, auth guard on routes |
| B8 | Role-based UI | Hide admin actions for viewer role |
| B9 | Dependency cleanup | Remove unused BabylonJS, Chart.js, D3 deps. Clean `_archived/` folder |

## Decision Log

| Decision | Rationale |
|---|---|
| Three.js over BabylonJS | Lighter, sufficient for procedural geometry, reduces bundle size |
| API key over JWT for Phase A | Simpler to implement and demonstrate; JWT deferred to Phase B |
| FluentValidation over Data Annotations | More expressive rules, better separation of concerns, standard in ASP.NET |
| ECharts for all charts | Already in use on diagnostics-sensors; consistent, well-documented Angular binding |
| CSS2DRenderer for 3D labels | Avoids WebGL text rendering complexity; uses DOM elements styled with Tailwind |
| Phase A/B split | Thesis evaluation needs polished demo + security awareness, not battle-tested hardening |
