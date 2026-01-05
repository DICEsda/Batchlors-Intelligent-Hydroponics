using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing coordinators.
/// </summary>
[ApiController]
[Route("api/coordinators")]
public class CoordinatorsController : ControllerBase
{
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<CoordinatorsController> _logger;

    public CoordinatorsController(ICoordinatorRepository coordinatorRepository, IMqttService mqtt, ILogger<CoordinatorsController> logger)
    {
        _coordinatorRepository = coordinatorRepository;
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get a coordinator by site and coordinator ID.
    /// </summary>
    [HttpGet("{siteId}/{coordId}")]
    public async Task<ActionResult<Coordinator>> GetCoordinator(string siteId, string coordId, CancellationToken ct)
    {
        var coordinator = await _coordinatorRepository.GetBySiteAndIdAsync(siteId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound();
        }
        return Ok(coordinator);
    }

    /// <summary>
    /// Send command to a coordinator via MQTT.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/command")]
    public async Task<IActionResult> SendCommand(string siteId, string coordId, [FromBody] CoordinatorCommand command, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Sent command {Command} to coordinator {CoordId}", command.Cmd, coordId);
        return Accepted();
    }

    /// <summary>
    /// Trigger node discovery on coordinator.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/discover")]
    public async Task<IActionResult> DiscoverNodes(string siteId, string coordId, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        var command = new CoordinatorCommand { Cmd = "discover" };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Triggered discovery on coordinator {CoordId}", coordId);
        return Accepted();
    }

    /// <summary>
    /// Put coordinator into pairing mode.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/pair")]
    public async Task<IActionResult> PairMode(string siteId, string coordId, [FromBody] PairRequest? request, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        var command = new CoordinatorCommand
        {
            Cmd = "pair",
            Params = new Dictionary<string, object>
            {
                ["duration_s"] = request?.DurationSeconds ?? 60
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Coordinator {CoordId} entering pairing mode", coordId);
        return Accepted();
    }

    /// <summary>
    /// Broadcast command to all towers via coordinator.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/broadcast")]
    public async Task<IActionResult> BroadcastToTowers(string siteId, string coordId, [FromBody] TowerCommand command, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/broadcast";
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Broadcast command {Command} via coordinator {CoordId}", command.Cmd, coordId);
        return Accepted();
    }

    /// <summary>
    /// Restart the coordinator device.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/restart")]
    public async Task<IActionResult> RestartCoordinator(string siteId, string coordId, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        var command = new CoordinatorCommand { Cmd = "restart" };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Restart command sent to coordinator {CoordId}", coordId);
        return Ok(new { status = "success", message = "Restart command sent" });
    }

    /// <summary>
    /// Approve a tower that is requesting to pair with the coordinator.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/pairing/approve")]
    public async Task<IActionResult> ApproveTowerPairing(
        string siteId,
        string coordId,
        [FromBody] ApprovePairingRequest request,
        CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        var command = new CoordinatorCommand
        {
            Cmd = "approve_pairing",
            Params = new Dictionary<string, object>
            {
                ["tower_id"] = request.TowerId
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Approved pairing for tower {TowerId} on coordinator {CoordId}", request.TowerId, coordId);
        return Ok(new { status = "success", message = "Pairing approved" });
    }

    /// <summary>
    /// Update coordinator WiFi configuration.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/wifi")]
    public async Task<IActionResult> UpdateWifi(
        string siteId,
        string coordId,
        [FromBody] WifiConfigRequest request,
        CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/cmd";
        var command = new CoordinatorCommand
        {
            Cmd = "wifi_config",
            Params = new Dictionary<string, object>
            {
                ["ssid"] = request.Ssid,
                ["password"] = request.Password
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("WiFi config update sent to coordinator {CoordId}", coordId);
        return Ok(new { status = "success", message = "WiFi configuration update sent" });
    }

    // ============================================================================
    // Hydroponic Farm Endpoints (new routing: /api/coordinators/{farmId}/{coordId})
    // ============================================================================

    /// <summary>
    /// Get reservoir state from coordinator twin.
    /// Returns the coordinator with all reservoir sensor data (pH, EC, water level, pump states).
    /// </summary>
    [HttpGet("{farmId}/{coordId}/reservoir")]
    public async Task<ActionResult<ReservoirStateResponse>> GetReservoirState(
        string farmId,
        string coordId,
        CancellationToken ct)
    {
        // Try farm-based lookup first, fall back to site-based
        var coordinator = await _coordinatorRepository.GetBySiteAndIdAsync(farmId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound(new { error = "Coordinator not found" });
        }

        var response = new ReservoirStateResponse
        {
            CoordId = coordinator.CoordId,
            FarmId = coordinator.FarmId ?? coordinator.SiteId,
            Name = coordinator.Name,
            Ph = coordinator.Ph,
            EcMsCm = coordinator.EcMsCm,
            TdsPpm = coordinator.TdsPpm,
            WaterTempC = coordinator.WaterTempC,
            WaterLevelPct = coordinator.WaterLevelPct,
            WaterLevelCm = coordinator.WaterLevelCm,
            LowWaterAlert = coordinator.LowWaterAlert,
            MainPumpOn = coordinator.MainPumpOn,
            LastPumpChangeAt = coordinator.LastPumpChangeAt,
            DosingPumpPhOn = coordinator.DosingPumpPhOn,
            DosingPumpNutrientOn = coordinator.DosingPumpNutrientOn,
            Setpoints = coordinator.Setpoints,
            LastSeen = coordinator.LastSeen,
            StatusMode = coordinator.StatusMode
        };

        return Ok(response);
    }

    /// <summary>
    /// Control main reservoir pump.
    /// Sends MQTT command to coordinator to turn pump on/off.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/reservoir/pump")]
    public async Task<IActionResult> ControlReservoirPump(
        string farmId,
        string coordId,
        [FromBody] ReservoirPumpRequest request,
        CancellationToken ct)
    {
        var topic = $"farm/{farmId}/coord/{coordId}/reservoir/cmd";
        var command = new CoordinatorCommand
        {
            Cmd = "pump",
            Params = new Dictionary<string, object>
            {
                ["on"] = request.On
            }
        };
        if (request.DurationSeconds.HasValue)
        {
            command.Params["duration_s"] = request.DurationSeconds.Value;
        }

        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Reservoir pump command (on={On}) sent to {FarmId}/{CoordId}", 
            request.On, farmId, coordId);

        return Accepted(new { 
            status = "accepted", 
            message = $"Pump {(request.On ? "on" : "off")} command sent",
            farmId,
            coordId
        });
    }

    /// <summary>
    /// Trigger nutrient/pH dosing.
    /// Sends MQTT command to coordinator to dose specified amounts.
    /// </summary>
    [HttpPost("{farmId}/{coordId}/reservoir/dosing")]
    public async Task<IActionResult> TriggerDosing(
        string farmId,
        string coordId,
        [FromBody] DosingRequest request,
        CancellationToken ct)
    {
        var topic = $"farm/{farmId}/coord/{coordId}/reservoir/cmd";
        var dosingParams = new Dictionary<string, object>();

        if (request.NutrientAMl.HasValue)
            dosingParams["nutrient_a_ml"] = request.NutrientAMl.Value;
        if (request.NutrientBMl.HasValue)
            dosingParams["nutrient_b_ml"] = request.NutrientBMl.Value;
        if (request.PhUpMl.HasValue)
            dosingParams["ph_up_ml"] = request.PhUpMl.Value;
        if (request.PhDownMl.HasValue)
            dosingParams["ph_down_ml"] = request.PhDownMl.Value;

        if (dosingParams.Count == 0)
        {
            return BadRequest(new { error = "At least one dosing amount must be specified" });
        }

        var command = new CoordinatorCommand
        {
            Cmd = "dose",
            Params = dosingParams
        };

        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Dosing command sent to {FarmId}/{CoordId}: {@Params}", 
            farmId, coordId, dosingParams);

        return Accepted(new { 
            status = "accepted", 
            message = "Dosing command sent",
            farmId,
            coordId,
            dosing = dosingParams
        });
    }

    /// <summary>
    /// Update reservoir target setpoints.
    /// Updates the coordinator's setpoints for automated pH/EC control.
    /// </summary>
    [HttpPut("{farmId}/{coordId}/reservoir/targets")]
    public async Task<IActionResult> UpdateReservoirTargets(
        string farmId,
        string coordId,
        [FromBody] ReservoirTargetsRequest request,
        CancellationToken ct)
    {
        // Get current coordinator
        var coordinator = await _coordinatorRepository.GetBySiteAndIdAsync(farmId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound(new { error = "Coordinator not found" });
        }

        // Initialize setpoints if null
        coordinator.Setpoints ??= new ReservoirSetpoints();

        // Update only provided values
        if (request.PhMin.HasValue && request.PhMax.HasValue)
        {
            coordinator.Setpoints.PhTarget = (request.PhMin.Value + request.PhMax.Value) / 2;
            coordinator.Setpoints.PhTolerance = (request.PhMax.Value - request.PhMin.Value) / 2;
        }
        if (request.EcMin.HasValue && request.EcMax.HasValue)
        {
            coordinator.Setpoints.EcTarget = (request.EcMin.Value + request.EcMax.Value) / 2;
            coordinator.Setpoints.EcTolerance = (request.EcMax.Value - request.EcMin.Value) / 2;
        }
        if (request.TempMinC.HasValue)
        {
            coordinator.Setpoints.WaterTempTargetC = request.TempMinC.Value;
        }

        // Persist to database
        await _coordinatorRepository.UpsertAsync(coordinator, ct);
        _logger.LogInformation("Reservoir targets updated for {FarmId}/{CoordId}", farmId, coordId);

        // Also send to device via MQTT so it can enforce setpoints locally
        var topic = $"farm/{farmId}/coord/{coordId}/reservoir/cmd";
        var command = new CoordinatorCommand
        {
            Cmd = "set_targets",
            Params = new Dictionary<string, object>
            {
                ["ph_target"] = coordinator.Setpoints.PhTarget,
                ["ph_tolerance"] = coordinator.Setpoints.PhTolerance,
                ["ec_target"] = coordinator.Setpoints.EcTarget,
                ["ec_tolerance"] = coordinator.Setpoints.EcTolerance,
                ["water_temp_target_c"] = coordinator.Setpoints.WaterTempTargetC
            }
        };
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);

        return Ok(new { 
            status = "success", 
            message = "Reservoir targets updated",
            setpoints = coordinator.Setpoints
        });
    }

    /// <summary>
    /// Get all coordinators for a farm.
    /// </summary>
    [HttpGet("farm/{farmId}")]
    public async Task<ActionResult<IEnumerable<Coordinator>>> GetCoordinatorsByFarm(
        string farmId,
        CancellationToken ct)
    {
        var coordinators = await _coordinatorRepository.GetByFarmAsync(farmId, ct);
        return Ok(coordinators);
    }
}

// ============================================================================
// Response DTOs
// ============================================================================

/// <summary>
/// Response DTO for reservoir state query.
/// </summary>
public class ReservoirStateResponse
{
    public string CoordId { get; set; } = string.Empty;
    public string? FarmId { get; set; }
    public string? Name { get; set; }

    // Water Quality Sensors
    public float? Ph { get; set; }
    public float? EcMsCm { get; set; }
    public float? TdsPpm { get; set; }
    public float? WaterTempC { get; set; }

    // Water Level
    public float? WaterLevelPct { get; set; }
    public float? WaterLevelCm { get; set; }
    public bool? LowWaterAlert { get; set; }

    // Actuator States
    public bool? MainPumpOn { get; set; }
    public DateTime? LastPumpChangeAt { get; set; }
    public bool? DosingPumpPhOn { get; set; }
    public bool? DosingPumpNutrientOn { get; set; }

    // Configuration
    public ReservoirSetpoints? Setpoints { get; set; }

    // Status
    public DateTime LastSeen { get; set; }
    public string? StatusMode { get; set; }
}
