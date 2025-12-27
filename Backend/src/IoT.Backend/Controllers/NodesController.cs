using IoT.Backend.Models;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// REST API for managing tile nodes.
/// </summary>
[ApiController]
[Route("api/nodes")]
public class NodesController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly IMqttService _mqtt;
    private readonly ILogger<NodesController> _logger;

    public NodesController(IRepository repository, IMqttService mqtt, ILogger<NodesController> logger)
    {
        _repository = repository;
        _mqtt = mqtt;
        _logger = logger;
    }

    /// <summary>
    /// Get all nodes for a coordinator.
    /// </summary>
    [HttpGet("{siteId}/{coordId}")]
    public async Task<ActionResult<IEnumerable<Node>>> GetNodes(string siteId, string coordId, CancellationToken ct)
    {
        var nodes = await _repository.GetNodesByCoordinatorAsync(siteId, coordId, ct);
        return Ok(nodes);
    }

    /// <summary>
    /// Get a specific node by ID.
    /// </summary>
    [HttpGet("{siteId}/{coordId}/{nodeId}")]
    public async Task<ActionResult<Node>> GetNode(string siteId, string coordId, string nodeId, CancellationToken ct)
    {
        var node = await _repository.GetNodeByIdAsync($"{siteId}/{coordId}/{nodeId}", ct);
        if (node == null)
        {
            return NotFound();
        }
        return Ok(node);
    }

    /// <summary>
    /// Update node zone assignment.
    /// </summary>
    [HttpPut("{siteId}/{coordId}/{nodeId}/zone")]
    public async Task<IActionResult> UpdateNodeZone(string siteId, string coordId, string nodeId, [FromBody] NodeZoneUpdateRequest request, CancellationToken ct)
    {
        await _repository.UpdateNodeZoneAsync(siteId, coordId, nodeId, request.ZoneId, ct);
        _logger.LogInformation("Node {NodeId} assigned to zone {ZoneId}", nodeId, request.ZoneId);
        return NoContent();
    }

    /// <summary>
    /// Update node name.
    /// </summary>
    [HttpPut("{siteId}/{coordId}/{nodeId}/name")]
    public async Task<IActionResult> UpdateNodeName(string siteId, string coordId, string nodeId, [FromBody] UpdateNameRequest request, CancellationToken ct)
    {
        await _repository.UpdateNodeNameAsync(siteId, coordId, nodeId, request.Name, ct);
        _logger.LogInformation("Node {NodeId} renamed to {Name}", nodeId, request.Name);
        return NoContent();
    }

    /// <summary>
    /// Send command to a node via MQTT.
    /// </summary>
    [HttpPost("{siteId}/{coordId}/{nodeId}/command")]
    public async Task<IActionResult> SendCommand(string siteId, string coordId, string nodeId, [FromBody] NodeCommand command, CancellationToken ct)
    {
        var topic = $"site/{siteId}/node/{nodeId}/cmd";
        await _mqtt.PublishJsonAsync(topic, command, ct: ct);
        _logger.LogInformation("Sent command {Command} to node {NodeId}", command.Cmd, nodeId);
        return Accepted();
    }

    /// <summary>
    /// Delete a node.
    /// </summary>
    [HttpDelete("{siteId}/{coordId}/{nodeId}")]
    public async Task<IActionResult> DeleteNode(string siteId, string coordId, string nodeId, CancellationToken ct)
    {
        await _repository.DeleteNodeAsync(siteId, coordId, nodeId, ct);
        _logger.LogInformation("Deleted node {NodeId}", nodeId);
        return NoContent();
    }
}

public class NodeZoneUpdateRequest
{
    public string ZoneId { get; set; } = string.Empty;
}

public class UpdateNameRequest
{
    public string Name { get; set; } = string.Empty;
}

public class NodeCommand
{
    public string Cmd { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}
