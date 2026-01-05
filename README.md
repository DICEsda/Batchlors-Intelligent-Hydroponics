# IoT Intelligent Hydroponics System

A distributed IoT platform for monitoring and controlling hydroponic farming systems. The architecture consists of ESP32 firmware for tower nodes and coordinators, an ASP.NET Core backend API, and an Angular frontend dashboard.

## System Architecture

```
                                    ┌─────────────────┐
                                    │  Angular        │
                                    │  Dashboard      │
                                    │  (Port 4200)    │
                                    └────────┬────────┘
                                             │ REST/WebSocket
                                    ┌────────▼────────┐
                                    │  ASP.NET Core   │
                                    │  Backend API    │
                                    │  (Port 8000)    │
                                    └────────┬────────┘
                              ┌──────────────┼──────────────┐
                              │              │              │
                     ┌────────▼────┐   ┌─────▼─────┐  ┌─────▼─────┐
                     │  Mosquitto  │   │  MongoDB  │  │  MongoDB  │
                     │  MQTT Broker│   │  Database │  │  Telemetry│
                     │  (Port 1883)│   │           │  │  (TimeSer)│
                     └──────┬──────┘   └───────────┘  └───────────┘
                            │ MQTT
           ┌────────────────┼────────────────┐
           │                │                │
    ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐
    │ Coordinator │  │ Coordinator │  │ Coordinator │
    │   (ESP32)   │  │   (ESP32)   │  │   (ESP32)   │
    │ + Reservoir │  │ + Reservoir │  │ + Reservoir │
    └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
           │ ESP-NOW        │ ESP-NOW        │ ESP-NOW
    ┌──────┴──────┐  ┌──────┴──────┐  ┌──────┴──────┐
    │ Tower Nodes │  │ Tower Nodes │  │ Tower Nodes │
    │ (ESP32-C3)  │  │ (ESP32-C3)  │  │ (ESP32-C3)  │
    └─────────────┘  └─────────────┘  └─────────────┘
```

## Project Structure

```
├── Backend/                    # ASP.NET Core 8 Backend API
│   ├── src/IoT.Backend/        # Main API project
│   │   ├── Controllers/        # REST API endpoints
│   │   ├── Models/             # Domain models and DTOs
│   │   ├── Repositories/       # MongoDB data access
│   │   ├── Services/           # Business logic & MQTT
│   │   └── WebSockets/         # Real-time communication
│   └── config/                 # MQTT broker configuration
│
├── Firmware/                   # ESP32 Firmware (PlatformIO)
│   ├── coordinator/            # ESP32-S3 coordinator firmware
│   │   └── src/
│   │       ├── comm/           # MQTT & ESP-NOW communication
│   │       ├── sensors/        # Reservoir sensor drivers
│   │       ├── actuators/      # Pump & dosing control
│   │       └── pairing/        # Tower pairing protocol
│   ├── node/                   # ESP32-C3 tower node firmware
│   │   └── src/
│   │       ├── sensor/         # DHT22, light sensors
│   │       ├── actuators/      # Pump & grow light control
│   │       └── pairing/        # Coordinator pairing
│   └── shared/                 # Shared ESP-NOW message types
│
├── Frontend/                   # Angular 18 Dashboard
│   └── IOT-Frontend-main/
│       └── src/app/
│
├── Assets/                     # Documentation & diagrams
│   ├── Diagrams/               # PlantUML architecture diagrams
│   └── docs/                   # Technical documentation
│
└── docker-compose.yml          # Full stack deployment
```

## Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 8 SDK (for local development)
- Node.js 20+ (for frontend development)
- PlatformIO (for firmware development)

### Running with Docker

```bash
# Start all services (MongoDB, MQTT, Backend, Frontend)
docker-compose up -d

# View logs
docker-compose logs -f backend

# Check health
curl http://localhost:8000/health/live
```

### Service Ports

| Service    | Port  | Description                      |
|------------|-------|----------------------------------|
| Backend    | 8000  | ASP.NET Core REST API            |
| Frontend   | 4200  | Angular Dashboard                |
| MongoDB    | 27017 | Database                         |
| MQTT       | 1883  | Mosquitto broker                 |
| MQTT WS    | 9001  | MQTT over WebSocket              |

### Environment Variables

```bash
# MongoDB
MONGO_ROOT_USER=admin
MONGO_ROOT_PASSWORD=admin123
MONGO_DB=iot_smarttile

# MQTT
MQTT_USERNAME=user1
MQTT_PASSWORD=user1

# API
API_URL=http://backend:8000
WS_URL=ws://backend:8000/ws
```

## API Documentation

See [Backend/README.md](Backend/src/IoT.Backend/README.md) for detailed API documentation.

### Key Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/coordinators/farm/{farmId}` | GET | List coordinators by farm |
| `/api/coordinators/{farmId}/{coordId}/reservoir` | GET | Get reservoir state |
| `/api/coordinators/{farmId}/{coordId}/reservoir/pump` | POST | Control reservoir pump |
| `/api/towers/farm/{farmId}` | GET | List towers by farm |
| `/api/towers/{farmId}/{coordId}/{towerId}` | GET | Get tower details |
| `/api/towers/{farmId}/{coordId}/{towerId}/light` | POST | Control grow light |
| `/api/telemetry/reservoir/{farmId}/{coordId}` | GET | Reservoir history |
| `/api/telemetry/tower/{farmId}/{coordId}/{towerId}` | GET | Tower history |
| `/health/live` | GET | Liveness probe |
| `/health/ready` | GET | Readiness probe |

## Data Models

### Coordinator (Reservoir Controller)

The coordinator is an ESP32-S3 that manages the hydroponic reservoir and communicates with tower nodes.

```json
{
  "coord_id": "coord-001",
  "farm_id": "farm-001",
  "name": "Main Reservoir",
  "ph": 6.2,
  "ec_ms_cm": 1.5,
  "tds_ppm": 750,
  "water_temp_c": 20.5,
  "water_level_pct": 85,
  "main_pump_on": true,
  "towers_online": 4,
  "setpoints": {
    "ph_target": 6.0,
    "ph_tolerance": 0.3,
    "ec_target": 1.5,
    "ec_tolerance": 0.2
  }
}
```

### Tower (Growing Tower Node)

Tower nodes are ESP32-C3 devices that monitor individual growing towers.

```json
{
  "tower_id": "tower-001",
  "coord_id": "coord-001",
  "farm_id": "farm-001",
  "name": "Tower A",
  "air_temp_c": 24.5,
  "humidity_pct": 65,
  "light_lux": 12000,
  "pump_on": false,
  "light_on": true,
  "light_brightness": 200,
  "crop_type": "Lettuce",
  "planting_date": "2026-01-01T00:00:00Z",
  "last_height_cm": 15.5
}
```

## MQTT Topic Structure

### Hydroponic Topics

```
# Reservoir (Coordinator)
farm/{farmId}/coord/{coordId}/reservoir/telemetry    # Sensor data (pub)
farm/{farmId}/coord/{coordId}/reservoir/cmd          # Commands (sub)

# Tower
farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry  # Sensor data (pub)
farm/{farmId}/coord/{coordId}/tower/{towerId}/cmd        # Commands (sub)

# System
farm/{farmId}/coord/{coordId}/status                 # Online/offline
farm/{farmId}/coord/{coordId}/ota                    # Firmware updates
```

### Command Examples

```json
// Reservoir pump control
{ "cmd": "pump", "params": { "on": true, "duration_s": 300 } }

// Nutrient dosing
{ "cmd": "dose", "params": { "nutrient_a_ml": 10, "ph_down_ml": 5 } }

// Tower light control
{ "cmd": "set_light", "params": { "on": true, "brightness": 200 } }

// Tower pump control
{ "cmd": "set_pump", "params": { "on": true } }
```

## Development

### Backend Development

```bash
cd Backend/src/IoT.Backend

# Restore dependencies
dotnet restore

# Run locally (requires MongoDB & MQTT running)
dotnet run

# Build Docker image
docker build -t iot-backend .
```

### Firmware Development

```bash
cd Firmware/coordinator  # or Firmware/node

# Build
pio run

# Upload to device
pio run --target upload

# Monitor serial
pio device monitor
```

### Frontend Development

```bash
cd Frontend/IOT-Frontend-main

# Install dependencies
npm install

# Development server
npm start

# Build for production
npm run build
```

## Crop Types Supported

| Category | Crops |
|----------|-------|
| Leafy Greens | Lettuce, Spinach, Kale, Arugula, Swiss Chard, Bok Choy |
| Herbs | Basil, Mint, Cilantro, Parsley, Dill, Oregano, Thyme, Rosemary |
| Fruiting | Tomato, Pepper, Cucumber, Strawberry |
| Microgreens | Mixed, Sunflower, Pea, Radish |

## Technology Stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8, C# 12 |
| Database | MongoDB 7.0 |
| Message Broker | Mosquitto MQTT 2.0 |
| Frontend | Angular 18, Tailwind CSS |
| Firmware | ESP-IDF, PlatformIO, C++ |
| Protocol | ESP-NOW (mesh), MQTT (cloud) |
| Containerization | Docker, Docker Compose |

## License

This project is part of a Bachelor's thesis on Intelligent Hydroponics Systems.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes with tests
4. Submit a pull request

## Acknowledgments

- ESP-IDF and PlatformIO communities
- ASP.NET Core team
- MongoDB and Mosquitto projects
