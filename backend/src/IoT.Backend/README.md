# IoT Hydroponics Backend API

ASP.NET Core 8 REST API server for the IoT Hydroponics System. Provides device management, telemetry ingestion, digital twin state management, and OTA firmware updates.

## Quick Start

```bash
# Run with Docker
cd backend
docker-compose up -d

# Or run locally
cd backend/src/IoT.Backend
dotnet run
```

**Base URL:** `http://localhost:5000/api`

---

## API Reference

### Health Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Full health check (DB, MQTT, Coordinator status) |
| GET | `/health/ready` | Kubernetes readiness probe |
| GET | `/health/live` | Kubernetes liveness probe |

#### GET /health

Returns system health status including MongoDB, MQTT broker, and coordinator connectivity.

**Response:**
```json
{
  "status": "healthy",
  "mqttConnected": true,
  "mqtt": true,
  "database": true,
  "coordinator": true,
  "timestamp": "2026-01-05T12:00:00Z"
}
```

---

### Coordinators Controller

**Base:** `/api/coordinators`

Manages coordinator devices (reservoir controllers).

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{siteId}/{coordId}` | Get coordinator by ID |
| GET | `/farm/{farmId}` | List all coordinators in a farm |
| GET | `/{farmId}/{coordId}/reservoir` | Get reservoir state |
| POST | `/{farmId}/{coordId}/reservoir/pump` | Control reservoir pump |
| POST | `/{farmId}/{coordId}/reservoir/dosing` | Trigger nutrient/pH dosing |
| PUT | `/{farmId}/{coordId}/reservoir/targets` | Update reservoir setpoints |
| POST | `/{siteId}/{coordId}/command` | Send generic MQTT command |
| POST | `/{siteId}/{coordId}/discover` | Trigger tower discovery |
| POST | `/{siteId}/{coordId}/pair` | Enter pairing mode |
| POST | `/{siteId}/{coordId}/restart` | Restart coordinator |
| POST | `/{siteId}/{coordId}/wifi` | Update WiFi configuration |
| POST | `/{siteId}/{coordId}/pairing/approve` | Approve pending node pairing |

#### GET /api/coordinators/{siteId}/{coordId}

**Response:**
```json
{
  "_id": "coord-001",
  "site_id": "farm-001",
  "name": "Main Reservoir Controller",
  "firmware_version": "1.2.0",
  "ip_address": "192.168.1.100",
  "status": "online",
  "last_seen": "2026-01-05T12:00:00Z",
  "reservoir": {
    "ph_setpoint": 6.0,
    "ec_setpoint": 1.8,
    "temperature_setpoint": 22.0,
    "level_min_percent": 20,
    "level_max_percent": 90
  }
}
```

#### POST /api/coordinators/{farmId}/{coordId}/reservoir/pump

Control the main reservoir pump.

**Request:**
```json
{
  "action": "on",
  "duration_seconds": 300
}
```

#### POST /api/coordinators/{farmId}/{coordId}/reservoir/dosing

Trigger nutrient or pH dosing.

**Request:**
```json
{
  "type": "nutrient_a",
  "amount_ml": 50
}
```

Valid types: `nutrient_a`, `nutrient_b`, `ph_up`, `ph_down`

#### PUT /api/coordinators/{farmId}/{coordId}/reservoir/targets

Update reservoir setpoints.

**Request:**
```json
{
  "ph_setpoint": 6.2,
  "ec_setpoint": 2.0,
  "temperature_setpoint": 23.0
}
```

---

### Towers Controller

**Base:** `/api/towers`

Manages tower devices (hydroponic grow towers).

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/farm/{farmId}` | List all towers in a farm |
| GET | `/farm/{farmId}/coord/{coordId}` | List towers by coordinator |
| GET | `/{farmId}/{coordId}/{towerId}` | Get tower by ID |
| PUT | `/{farmId}/{coordId}/{towerId}` | Create or update tower |
| PATCH | `/{farmId}/{coordId}/{towerId}/name` | Update tower name |
| DELETE | `/{farmId}/{coordId}/{towerId}` | Delete tower |
| POST | `/{farmId}/{coordId}/{towerId}/command` | Send command to tower |
| POST | `/{farmId}/{coordId}/{towerId}/light` | Control grow light |
| POST | `/{farmId}/{coordId}/{towerId}/pump` | Control tower pump |
| GET | `/{farmId}/{coordId}/{towerId}/telemetry` | Get telemetry history |
| GET | `/{farmId}/{coordId}/{towerId}/telemetry/latest` | Get latest telemetry |
| GET | `/{farmId}/{coordId}/{towerId}/height` | Get height measurements |
| POST | `/{farmId}/{coordId}/{towerId}/height` | Record height measurement |
| POST | `/{farmId}/{coordId}/{towerId}/crop` | Set crop information |

#### GET /api/towers/{farmId}/{coordId}/{towerId}

**Response:**
```json
{
  "_id": "tower-001",
  "farm_id": "farm-001",
  "coord_id": "coord-001",
  "name": "Tower A1",
  "slot_count": 36,
  "status": "online",
  "last_seen": "2026-01-05T12:00:00Z",
  "capabilities": {
    "has_pump": true,
    "has_light": true,
    "has_temp_sensor": true,
    "has_humidity_sensor": true,
    "has_height_sensor": false
  },
  "crop": {
    "type": "lettuce",
    "planted_at": "2026-01-01T00:00:00Z",
    "expected_harvest": "2026-02-01T00:00:00Z"
  }
}
```

#### POST /api/towers/{farmId}/{coordId}/{towerId}/light

Control the tower grow light.

**Request:**
```json
{
  "action": "on",
  "brightness": 80,
  "duration_minutes": 480
}
```

#### POST /api/towers/{farmId}/{coordId}/{towerId}/height

Record a plant height measurement.

**Request:**
```json
{
  "slot_index": 5,
  "height_cm": 12.5,
  "method": "manual",
  "crop_type": "lettuce",
  "notes": "Looking healthy"
}
```

---

### Telemetry Controller

**Base:** `/api/telemetry`

Query historical and real-time telemetry data.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/coordinator/{siteId}/{coordId}` | Legacy coordinator telemetry |
| GET | `/reservoir/{farmId}/{coordId}` | Reservoir telemetry history |
| GET | `/reservoir/{farmId}/{coordId}/latest` | Latest reservoir reading |
| GET | `/reservoir/{farmId}/{coordId}/daily` | Daily reservoir averages |
| GET | `/tower/{farmId}/{coordId}/{towerId}` | Tower telemetry history |
| GET | `/tower/{farmId}/{coordId}/{towerId}/latest` | Latest tower reading |
| GET | `/tower/{farmId}/{coordId}/latest` | Latest readings for all towers |
| GET | `/tower/{farmId}/{coordId}/{towerId}/daily` | Daily tower averages |
| GET | `/height/{farmId}/{towerId}` | Height measurement history |
| GET | `/height/{farmId}/{towerId}/latest` | Latest height per slot |

#### Query Parameters

All history endpoints support:
- `start` - ISO 8601 start datetime (default: 24 hours ago)
- `end` - ISO 8601 end datetime (default: now)
- `limit` - Max records to return (default: 1000)

**Example:**
```
GET /api/telemetry/reservoir/farm-001/coord-001?start=2026-01-01T00:00:00Z&limit=500
```

#### Reservoir Telemetry Response

```json
{
  "data": [
    {
      "timestamp": "2026-01-05T12:00:00Z",
      "ph": 6.1,
      "ec": 1.85,
      "tds": 925,
      "water_temp": 22.3,
      "water_level_percent": 75,
      "pump_running": false
    }
  ],
  "count": 1,
  "farm_id": "farm-001",
  "coord_id": "coord-001"
}
```

#### Tower Telemetry Response

```json
{
  "data": [
    {
      "timestamp": "2026-01-05T12:00:00Z",
      "temperature": 24.5,
      "humidity": 65.2,
      "light_level": 800,
      "pump_running": false,
      "light_on": true
    }
  ],
  "count": 1,
  "farm_id": "farm-001",
  "tower_id": "tower-001"
}
```

---

### Digital Twins Controller

**Base:** `/api/twins`

Manage desired/reported state synchronization for devices.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/towers/{towerId}` | Get tower twin |
| GET | `/towers` | Get tower twins (query: farmId, coordId) |
| PUT | `/towers/{towerId}/desired` | Set tower desired state |
| GET | `/towers/{towerId}/delta` | Get tower state delta |
| POST | `/towers/{towerId}/sync/success` | Mark tower sync success |
| GET | `/coordinators/{coordId}` | Get coordinator twin |
| GET | `/coordinators` | Get coordinator twins (query: farmId) |
| PUT | `/coordinators/{coordId}/desired` | Set coordinator desired state |
| GET | `/coordinators/{coordId}/delta` | Get coordinator state delta |
| POST | `/coordinators/{coordId}/sync/success` | Mark coordinator sync success |
| GET | `/farms/{farmId}` | Get all twins for a farm |

#### Tower Desired State

```json
{
  "light_on": true,
  "light_brightness": 80,
  "pump_on": false,
  "pump_interval_minutes": 30,
  "pump_duration_seconds": 60
}
```

#### Delta Response

```json
{
  "tower_id": "tower-001",
  "sync_status": "pending",
  "is_in_sync": false,
  "delta": {
    "light_brightness": 80
  }
}
```

---

### Pairing Controller

**Base:** `/api/pairing`

Manage device pairing workflow.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/start` | Start pairing session |
| POST | `/stop` | Stop pairing session |
| GET | `/session/{farmId}/{coordId}` | Get active session |
| GET | `/requests/{farmId}/{coordId}` | Get pending pairing requests |
| POST | `/approve` | Approve pairing request |
| POST | `/reject` | Reject pairing request |
| POST | `/forget` | Forget (unpair) a device |

#### Start Pairing Session

**Request:**
```json
{
  "farm_id": "farm-001",
  "coord_id": "coord-001",
  "duration_seconds": 120
}
```

**Response:**
```json
{
  "session_id": "sess-abc123",
  "farm_id": "farm-001",
  "coord_id": "coord-001",
  "status": "active",
  "expires_at": "2026-01-05T12:02:00Z",
  "pending_requests": []
}
```

#### Approve Pairing

**Request:**
```json
{
  "farm_id": "farm-001",
  "coord_id": "coord-001",
  "tower_id": "tower-new-001"
}
```

---

### Zones Controller

**Base:** `/api/zones`

Manage logical zones grouping coordinators.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/site/{siteId}` | List zones by site |
| GET | `/{zoneId}` | Get zone by ID |
| POST | `/` | Create zone |
| PUT | `/{zoneId}` | Update zone |
| DELETE | `/{zoneId}` | Delete zone |

#### Create Zone

**Request:**
```json
{
  "name": "Greenhouse A",
  "site_id": "farm-001",
  "coordinator_id": "coord-001",
  "description": "Main growing area",
  "color": "#00FF00"
}
```

---

### OTA Controller

**Base:** `/api/ota`

Manage over-the-air firmware updates.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/jobs` | List OTA jobs (query: site_id, limit) |
| GET | `/jobs/{jobId}` | Get OTA job by ID |
| POST | `/start` | Start new OTA job |
| POST | `/jobs/{jobId}/cancel` | Cancel OTA job |
| PUT | `/jobs/{jobId}/progress` | Update job progress |
| GET | `/status/{deviceType}/{deviceId}` | Get device OTA status |

#### Start OTA Job

**Request:**
```json
{
  "site_id": "farm-001",
  "target_type": "tower",
  "target_id": "tower-001",
  "target_version": "1.3.0",
  "firmware_url": "https://firmware.example.com/tower-1.3.0.bin"
}
```

Valid target types: `node`, `tower`, `coordinator`, `reservoir`

**Response:**
```json
{
  "_id": "job-abc123",
  "site_id": "farm-001",
  "target_type": "node",
  "target_id": "tower-001",
  "target_version": "1.3.0",
  "status": "pending",
  "progress": 0,
  "devices_total": 0,
  "devices_updated": 0,
  "devices_failed": 0,
  "created_at": "2026-01-05T12:00:00Z"
}
```

#### OTA Job Statuses

- `pending` - Job created, waiting to start
- `in_progress` - Update in progress
- `completed` - All devices updated successfully
- `failed` - Update failed
- `cancelled` - Job cancelled by user

---

## MQTT Topics

The backend subscribes to and publishes on the following MQTT topics:

### Telemetry (Subscribe)

```
farm/{farmId}/coord/{coordId}/reservoir/telemetry    # Reservoir sensor data
farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry  # Tower sensor data
```

### Commands (Publish)

```
farm/{farmId}/coord/{coordId}/reservoir/cmd    # Reservoir commands
farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd  # Tower commands
site/{siteId}/coord/{coordId}/cmd              # Legacy coordinator commands
site/{siteId}/coord/{coordId}/broadcast        # Broadcast to all towers
site/{siteId}/ota/start                        # OTA start command
site/{siteId}/ota/cancel                       # OTA cancel command
```

### Command Payload Examples

**Pump Control:**
```json
{
  "cmd": "pump",
  "action": "on",
  "duration": 300
}
```

**Light Control:**
```json
{
  "cmd": "light",
  "action": "on",
  "brightness": 80
}
```

**Dosing:**
```json
{
  "cmd": "dose",
  "type": "ph_down",
  "amount_ml": 10
}
```

---

## WebSocket Events

Connect to `/ws` for real-time updates.

### Event Types

- `telemetry` - New telemetry data received
- `device_status` - Device online/offline status change
- `pairing_request` - New tower pairing request
- `ota_status` - OTA job status update

### Event Format

```json
{
  "type": "telemetry",
  "payload": {
    "device_type": "tower",
    "device_id": "tower-001",
    "data": { ... }
  },
  "timestamp": "2026-01-05T12:00:00Z"
}
```

---

## Error Responses

All errors follow a consistent format:

```json
{
  "error": "Description of the error",
  "details": "Additional context (optional)"
}
```

### HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 202 | Accepted (async operation started) |
| 400 | Bad Request - Invalid input |
| 404 | Not Found - Resource doesn't exist |
| 409 | Conflict - Resource already exists |
| 500 | Internal Server Error |
| 503 | Service Unavailable - Dependency down |

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MONGODB_URI` | MongoDB connection string | `mongodb://localhost:27017` |
| `MONGODB_DATABASE` | Database name | `iot_hydroponics` |
| `MQTT_HOST` | MQTT broker host | `localhost` |
| `MQTT_PORT` | MQTT broker port | `1883` |
| `MQTT_USER` | MQTT username | (none) |
| `MQTT_PASSWORD` | MQTT password | (none) |
| `ASPNETCORE_URLS` | Server URLs | `http://+:5000` |

### appsettings.json

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "iot_hydroponics"
  },
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "ClientId": "iot-backend",
    "Username": "",
    "Password": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Data Models

### Coordinator

```json
{
  "_id": "string",
  "site_id": "string",
  "name": "string",
  "firmware_version": "string",
  "ip_address": "string",
  "mac_address": "string",
  "status": "online|offline|error",
  "last_seen": "datetime",
  "reservoir": {
    "ph_setpoint": "number",
    "ec_setpoint": "number",
    "temperature_setpoint": "number",
    "level_min_percent": "number",
    "level_max_percent": "number"
  }
}
```

### Tower

```json
{
  "_id": "string",
  "farm_id": "string",
  "coord_id": "string",
  "name": "string",
  "slot_count": "number",
  "firmware_version": "string",
  "mac_address": "string",
  "status": "online|offline|error|pairing",
  "last_seen": "datetime",
  "capabilities": {
    "has_pump": "boolean",
    "has_light": "boolean",
    "has_temp_sensor": "boolean",
    "has_humidity_sensor": "boolean",
    "has_height_sensor": "boolean"
  },
  "crop": {
    "type": "string",
    "variety": "string",
    "planted_at": "datetime",
    "expected_harvest": "datetime"
  }
}
```

### OtaJob

```json
{
  "_id": "string",
  "site_id": "string",
  "target_type": "node|coordinator",
  "target_id": "string|null",
  "target_version": "string",
  "firmware_url": "string",
  "status": "pending|in_progress|completed|failed|cancelled",
  "progress": "number (0-100)",
  "error_message": "string|null",
  "devices_total": "number",
  "devices_updated": "number",
  "devices_failed": "number",
  "created_at": "datetime",
  "updated_at": "datetime",
  "completed_at": "datetime|null"
}
```

---

## Development

### Prerequisites

- .NET 8 SDK
- MongoDB 6.0+
- MQTT Broker (Mosquitto recommended)

### Running Tests

```bash
cd backend/src/IoT.Backend
dotnet test
```

### Building Docker Image

```bash
cd backend/src/IoT.Backend
docker build -t iot-backend:latest .
```

### API Documentation

Swagger UI is available in development mode at:
```
http://localhost:5000/swagger
```
