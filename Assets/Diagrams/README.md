# Architecture Diagrams

## Generating PlantUML Diagrams

### Option 1: Online Renderer
1. Go to http://www.plantuml.com/plantuml/uml/
2. Copy the contents of `system-architecture.puml`
3. Paste and view the generated diagram
4. Download as PNG or SVG

### Option 2: VS Code Extension
1. Install "PlantUML" extension in VS Code
2. Open `system-architecture.puml`
3. Press `Alt+D` to preview
4. Right-click and select "Export Current Diagram"

### Option 3: Command Line (requires Java and Graphviz)
```bash
# Install PlantUML
# On macOS: brew install plantuml
# On Ubuntu: sudo apt-get install plantuml
# On Windows: download from https://plantuml.com/download

# Generate PNG
plantuml system-architecture.puml

# Generate SVG (better for documents)
plantuml -tsvg system-architecture.puml
```

### Option 4: Docker
```bash
docker run -v $(pwd):/data plantuml/plantuml system-architecture.puml
```

## Files

- `system-architecture.puml` - Main system architecture diagram (source)
- `system-architecture.png` - Generated architecture diagram (image)

## Architecture Overview

The diagram illustrates:

1. **User Interface Layer**
   - Angular dashboard for farmers

2. **Azure Cloud Layer**
   - ASP.NET Core Backend API
   - Azure Digital Twins (virtual farm simulation)
   - MongoDB time-series database
   - MQTT broker (Mosquitto)
   - Machine Learning engine

3. **Edge Layer**
   - ESP32-S3 coordinators
   - Reservoir controllers
   - Local intelligence and gateway functions

4. **Device Layer**
   - ESP32-C3 tower nodes
   - Wireless mesh network (ESP-NOW)

5. **Sensors & Actuators**
   - Reservoir sensors (pH, EC, TDS, temperature, water level)
   - Tower sensors (air temp, humidity, light)
   - Actuators (pumps, grow lights, dosing pumps)

## Communication Protocols

- **REST API / WebSocket:** Frontend ↔ Backend
- **MQTT:** Backend ↔ Coordinators (over WiFi)
- **ESP-NOW:** Coordinators ↔ Tower Nodes (wireless mesh)
