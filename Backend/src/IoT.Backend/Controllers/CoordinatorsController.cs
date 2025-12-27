using IoT.Backend.Models;
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
    private readonly IRepository _repository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<CoordinatorsController> _logger;

    public CoordinatorsController(IRepository repository, IMqttService mqtt, ILogger<CoordinatorsController> logger)
    {
        _repository = repository;
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get a coordinator by site and coordinator ID.
    /// </summary>
    [HttpGet("{siteId}/{coordId}")]
    public async Task<ActionResult<Coordinator>> GetCoordinator(string siteId, string coordId, CancellationToken ct)
    {
        var coordinator = await _repository.GetCoordinatorBySiteAndIdAsync(siteId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound();
        }
        return Ok(coordinator);
    }

    /// <summary>
    /// Get coordinator with all its nodes.
    /// </summary>
    [HttpGet("{siteId}/{coordId}/nodes")]
    public async Task<ActionResult<CoordinatorWithNodes>> GetCoordinatorWithNodes(string siteId, string coordId, CancellationToken ct)
    {
        var coordinator = await _repository.GetCoordinatorBySiteAndIdAsync(siteId, coordId, ct);
        if (coordinator == null)
        {
            return NotFound();
        }

        var nodes = await _repository.GetNodesByCoordinatorAsync(siteId, coordId, ct);

        return Ok(new CoordinatorWithNodes
        {
            Coordinator = coordinator,
            Nodes = nodes.ToList()
        });
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
    /// Broadcast command to all nodes via coordinator.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/broadcast")]
    public async Task<IActionResult> BroadcastToNodes(string siteId, string coordId, [FromBody] NodeCommand command, CancellationToken ct)
    {
        var topic = $"site/{siteId}/coord/{coordId}/broadcast";
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Broadcast command {Command} via coordinator {CoordId}", command.Cmd, coordId);
        return Accepted();
    }
}

public class CoordinatorWithNodes
{
    public Coordinator Coordinator { get; set; } = null!;
    public List<Node> Nodes { get; set; } = new();
}

public class CoordinatorCommand
{
    public string Cmd { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}

public class PairRequest
{
    public int DurationSeconds { get; set; } = 60;
}
