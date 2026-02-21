# How to Render PlantUML Diagrams

## Quick Start (Easiest Method)

### Online Rendering (No Installation Required)

1. **Open the PlantUML Online Server:**
   - Go to: http://www.plantuml.com/plantuml/uml/

2. **Copy the diagram code:**
   - Open `system-architecture-simple.puml` in any text editor
   - Copy all the content (Ctrl+A, Ctrl+C)

3. **Paste and render:**
   - Paste the code into the text box on the website
   - The diagram will automatically appear below
   - Click on the diagram to download as PNG or SVG

## Alternative Methods

### Method 1: VS Code (Recommended for Development)

1. **Install VS Code extension:**
   ```
   Name: PlantUML
   Publisher: jebbs
   ```

2. **Preview the diagram:**
   - Open `system-architecture-simple.puml` in VS Code
   - Press `Alt+D` to open preview panel
   - The diagram will render automatically

3. **Export as image:**
   - Right-click in the preview
   - Select "Export Current Diagram"
   - Choose PNG, SVG, or PDF format

### Method 2: Command Line (For Automation)

**Requirements:**
- Java Runtime Environment (JRE)
- Graphviz (for rendering)
- PlantUML JAR file

**On Windows:**
```powershell
# Install via Chocolatey
choco install plantuml

# Or download PlantUML.jar from https://plantuml.com/download
# Then run:
java -jar plantuml.jar system-architecture-simple.puml
```

**On macOS:**
```bash
# Install via Homebrew
brew install plantuml

# Render diagram
plantuml system-architecture-simple.puml
```

**On Linux:**
```bash
# Install via apt
sudo apt-get install plantuml

# Render diagram
plantuml system-architecture-simple.puml
```

### Method 3: Docker (No Local Installation)

```bash
# Navigate to diagrams directory
cd "C:\Users\yahya\Desktop\Batchlors-Intelligent-Hydroponics\Assets\Diagrams"

# Render using Docker
docker run --rm -v ${PWD}:/data plantuml/plantuml system-architecture-simple.puml

# This will create system-architecture-simple.png in the same directory
```

### Method 4: Online PlantUML Editor with GitHub

1. Go to: https://www.planttext.com/
2. Paste your PlantUML code
3. Click "Refresh" to render
4. Download as PNG or get shareable link

## Which Diagram to Use?

### `system-architecture-simple.puml` (Recommended)
- **Best for:** Thesis document, presentations
- **Pros:** Clean, renders reliably, no external dependencies
- **Cons:** Uses generic shapes instead of Azure icons

### `system-architecture.puml` (Advanced)
- **Best for:** Technical documentation, Azure-focused presentations
- **Pros:** Uses official Azure icons, very professional
- **Cons:** Requires Azure PlantUML library, slower rendering

## Troubleshooting

### Diagram doesn't render
- **Check syntax:** Make sure all `@startuml` has matching `@enduml`
- **Try simplified version:** Use `system-architecture-simple.puml` first
- **Online renderer:** Always works - use as fallback

### Missing Azure icons
- The `system-architecture.puml` needs internet connection to fetch Azure icons
- Use `system-architecture-simple.puml` for offline work

### Slow rendering
- Complex diagrams take longer
- Online renderers may be faster than local for first-time rendering
- Docker method is consistently fast

## For Your Thesis

**Recommended workflow:**

1. **Render the diagram:**
   ```bash
   # Use online renderer or VS Code extension
   # Save as: system-architecture.png
   ```

2. **Insert into proposal:**
   ```markdown
   ![System Architecture](Assets/Diagrams/system-architecture.png)
   ```

3. **Keep source available:**
   - Include the `.puml` file in your thesis repository
   - Allows advisor to view/modify if needed
   - Shows technical proficiency

## Additional Resources

- PlantUML Documentation: https://plantuml.com/
- PlantUML Gallery: https://real-world-plantuml.com/
- Azure Icons Library: https://github.com/plantuml-stdlib/Azure-PlantUML
- Live Editor: https://www.planttext.com/

## Quick Reference

**Generate PNG:**
```bash
plantuml system-architecture-simple.puml
```

**Generate SVG (vector, better quality):**
```bash
plantuml -tsvg system-architecture-simple.puml
```

**Generate all diagrams in folder:**
```bash
plantuml *.puml
```

---

**Note:** Once rendered, the PNG/SVG image can be embedded directly in your thesis document, PowerPoint presentations, or any other medium. The PlantUML source (.puml) file should be kept for version control and future modifications.
