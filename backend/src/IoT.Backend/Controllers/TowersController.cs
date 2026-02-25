using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing hydroponic towers.
/// </summary>
[ApiController]
[Route("api/towers")]
public class TowersController : ControllerBase
{
    private readonly ITowerRepository _towerRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<TowersController> _logger;

    public TowersController(
        ITowerRepository towerRepository,
        ITelemetryRepository telemetryRepository,
        IMqttService mqtt,
        ILogger<TowersController> logger)
    {
        _towerRepository = towerRepository;
        _telemetryRepository = telemetryRepository;
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get all towers for a farm.
    /// </summary>
    [HttpGet("farm/{farmId}")]
    public async Task<ActionResult<IReadOnlyList<Tower>>> GetTowersByFarm(string farmId, CancellationToken ct)
    {
        var towers = await _towerRepository.GetByFarmAsync(farmId, ct);
        return Ok(towers);
    }

    /// <summary>
    /// Get all towers for a coordinator.
    /// </summary>
    [HttpGet("farm/{farmId}/coord/{coordId}")]
    public async Task<ActionResult<IReadOnlyList<Tower>>> GetTowersByCoordinator(
        string farmId, 
        string coordId, 
        CancellationToken ct)
    {
        var towers = await _towerRepository.GetByCoordinatorAsync(farmId, coordId, ct);
        return Ok(towers);
    }

    /// <summary>
    /// Get a specific tower by ID.
    /// </summary>
    [HttpGet("{farmId}/{coordId}/{towerId}")]
    public async Task<ActionResult<Tower>> GetTower(
        string farmId, 
        string coordId, 
        string towerId, 
        CancellationToken ct)
    {
        var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, towerId, ct);
        if (tower == null)
        {
            return NotFound();
        }
        return Ok(tower);
    }

    /// <summary>
    /// Create or update a tower.
    /// </summary>
    [HttpPut("{farmId}/{coordId}/{towerId}")]
    public async Task<ActionResult<Tower>> UpsertTower(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] UpsertTowerRequest request,
        CancellationToken ct)
    {
        var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, towerId, ct) 
            ?? new Tower
            {
                Id = $"{farmId}/{coordId}/{towerId}",
                TowerId = towerId,
                CoordId = coordId,
                FarmId = farmId,
                CreatedAt = DateTime.UtcNow
            };

        // Update fields from request
        if (request.Name != null)
            tower.Name = request.Name;
        if (request.CropType.HasValue)
            tower.CropType = request.CropType.Value;
        if (request.PlantingDate.HasValue)
            tower.PlantingDate = request.PlantingDate.Value;
        
        tower.UpdatedAt = DateTime.UtcNow;

        await _towerRepository.UpsertAsync(tower, ct);
        _logger.LogInformation("Upserted tower {TowerId} for coordinator {CoordId}", towerId, coordId);
        
        return Ok(tower);
    }

    /// <summary>
    /// Update tower name.
    /// </summary>
    [HttpPatch("{farmId}/{coordId}/{towerId}/name")]
    public async Task<IActionResult> UpdateTowerName(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] UpdateNameRequest request,
        CancellationToken ct)
    {
        await _towerRepository.UpdateNameAsync(farmId, coordId, towerId, request.Name, ct);
        _logger.LogInformation("Updated name for tower {TowerId} to {Name}", towerId, request.Name);
        return Ok(new { status = "success", message = "Tower name updated" });
    }

    /// <summary>
    /// Delete a tower.
    /// </summary>
    [HttpDelete("{farmId}/{coordId}/{towerId}")]
    public async Task<IActionResult> DeleteTower(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct)
    {
        await _towerRepository.DeleteAsync(farmId, coordId, towerId, ct);
        _logger.LogInformation("Deleted tower {TowerId} from coordinator {CoordId}", towerId, coordId);
        return NoContent();
    }

    /// <summary>
    /// Send command to a tower via MQTT (through coordinator).
    /// </summary>
    [HttpPost("{farmId}/{coordId}/{towerId}/command")]
    public async Task<IActionResult> SendCommand(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] TowerCommand command,
        CancellationToken ct)
    {
        var topic = MqttTopics.TowerCmd(farmId, coordId, towerId);
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Sent command {Command} to tower {TowerId}", command.Cmd, towerId);
        return Accepted();
    }

    /// <summary>
    /// Set tower grow light state.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/{towerId}/light")]
    public async Task<IActionResult> SetLight(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] SetLightRequest request,
        CancellationToken ct)
    {
        var topic = MqttTopics.TowerCmd(farmId, coordId, towerId);
        var command = new TowerCommand
        {
            Cmd = "set_light",
            Params = new Dictionary<string, object>
            {
                ["on"] = request.On,
                ["brightness"] = request.Brightness ?? 255
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Set light for tower {TowerId}: on={On}, brightness={Brightness}", 
            towerId, request.On, request.Brightness);
        return Accepted();
    }

    /// <summary>
    /// Set tower pump state.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/{towerId}/pump")]
    public async Task<IActionResult> SetPump(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] SetPumpRequest request,
        CancellationToken ct)
    {
        var topic = MqttTopics.TowerCmd(farmId, coordId, towerId);
        var command = new TowerCommand
        {
            Cmd = "set_pump",
            Params = new Dictionary<string, object>
            {
                ["on"] = request.On
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Set pump for tower {TowerId}: on={On}", towerId, request.On);
        return Accepted();
    }

    /// <summary>
    /// Get tower telemetry history.
    /// </summary>
    [HttpGet("{farmId}/{coordId}/{towerId}/telemetry")]
    public async Task<ActionResult<IReadOnlyList<TowerTelemetry>>> GetTelemetry(
        string farmId,
        string coordId,
        string towerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
        var toDate = to ?? DateTime.UtcNow;
        
        var telemetry = await _telemetryRepository.GetTowerTelemetryAsync(
            farmId, coordId, towerId, fromDate, toDate, limit, ct);
        return Ok(telemetry);
    }

    /// <summary>
    /// Get latest tower telemetry.
    /// </summary>
    [HttpGet("{farmId}/{coordId}/{towerId}/telemetry/latest")]
    public async Task<ActionResult<TowerTelemetry>> GetLatestTelemetry(
        string farmId,
        string coordId,
        string towerId,
        CancellationToken ct)
    {
        var telemetry = await _telemetryRepository.GetLatestTowerTelemetryAsync(farmId, coordId, towerId, ct);
        if (telemetry == null)
        {
            return NotFound();
        }
        return Ok(telemetry);
    }

    /// <summary>
    /// Get height measurements for a tower.
    /// </summary>
    [HttpGet("{farmId}/{coordId}/{towerId}/height")]
    public async Task<ActionResult<IReadOnlyList<HeightMeasurement>>> GetHeightMeasurements(
        string farmId,
        string coordId,
        string towerId,
        [FromQuery] int? slotIndex,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var measurements = await _telemetryRepository.GetHeightMeasurementsAsync(
            farmId, towerId, slotIndex, from, to, limit, ct);
        return Ok(measurements);
    }

    /// <summary>
    /// Record a height measurement.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/{towerId}/height")]
    public async Task<IActionResult> RecordHeightMeasurement(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] RecordHeightRequest request,
        CancellationToken ct)
    {
        var measurement = new HeightMeasurement
        {
            FarmId = farmId,
            CoordId = coordId,
            TowerId = towerId,
            SlotIndex = request.SlotIndex,
            HeightCm = request.HeightCm,
            Method = request.Method,
            Notes = request.Notes,
            Timestamp = DateTime.UtcNow
        };

        await _telemetryRepository.InsertHeightMeasurementAsync(measurement, ct);
        
        // Also update the tower's last height
        var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, towerId, ct);
        if (tower != null)
        {
            tower.LastHeightCm = request.HeightCm;
            tower.LastHeightAt = DateTime.UtcNow;
            tower.UpdatedAt = DateTime.UtcNow;
            await _towerRepository.UpsertAsync(tower, ct);
        }

        _logger.LogInformation("Recorded height measurement for tower {TowerId}, slot {SlotIndex}: {Height}cm", 
            towerId, request.SlotIndex, request.HeightCm);
        return Created($"/api/towers/{farmId}/{coordId}/{towerId}/height", measurement);
    }

    /// <summary>
    /// Set crop information for a tower.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/{towerId}/crop")]
    public async Task<IActionResult> SetCrop(
        string farmId,
        string coordId,
        string towerId,
        [FromBody] SetCropRequest request,
        CancellationToken ct)
    {
        var tower = await _towerRepository.GetByFarmCoordAndIdAsync(farmId, coordId, towerId, ct);
        if (tower == null)
        {
            return NotFound();
        }

        tower.CropType = request.CropType;
        tower.PlantingDate = request.PlantingDate ?? DateTime.UtcNow;
        tower.ExpectedHarvestDate = request.ExpectedHarvestDate;
        tower.LastHeightCm = null; // Reset height tracking for new crop
        tower.LastHeightAt = null;
        tower.PredictedHeightCm = null;
        tower.UpdatedAt = DateTime.UtcNow;

        await _towerRepository.UpsertAsync(tower, ct);
        _logger.LogInformation("Set crop for tower {TowerId}: {CropType}, planted {PlantingDate}", 
            towerId, request.CropType, tower.PlantingDate);
        
        return Ok(tower);
    }
}
