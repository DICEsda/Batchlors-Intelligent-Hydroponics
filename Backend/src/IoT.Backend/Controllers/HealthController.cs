using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// Health check endpoint.
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IMqttService _mqtt;
    private readonly IRepository _repository;
    private readonly ICoordinatorRepository _coordinatorRepo;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IMqttService mqtt,
        IRepository repository,
        ICoordinatorRepository coordinatorRepo,
        ILogger<HealthController> logger)
    {
        _mqtt = mqtt;
        _repository = repository;
        _coordinatorRepo = coordinatorRepo;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> GetHealth()
    {
        // Check MongoDB connection
        bool dbHealthy = false;
        try
        {
            dbHealthy = await _repository.CheckConnectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB health check failed");
        }

        // Check if any coordinator is online (has recent heartbeat)
        bool coordinatorOnline = false;
        try
        {
            var count = await _coordinatorRepo.CountOnlineAsync(TimeSpan.FromMinutes(5));
            coordinatorOnline = count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Coordinator health check failed");
        }

        var status = dbHealthy && _mqtt.IsConnected ? "healthy" : "degraded";

        return Ok(new HealthResponse
        {
            Status = status,
            MqttConnected = _mqtt.IsConnected,
            Mqtt = _mqtt.IsConnected,
            Database = dbHealthy,
            Coordinator = coordinatorOnline,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Detailed health check for Kubernetes probes.
    /// </summary>
    [HttpGet("ready")]
    public ActionResult<HealthResponse> GetReadiness()
    {
        if (!_mqtt.IsConnected)
        {
            return StatusCode(503, new HealthResponse
            {
                Status = "unhealthy",
                MqttConnected = false,
                Timestamp = DateTime.UtcNow,
                Message = "MQTT broker not connected"
            });
        }

        return Ok(new HealthResponse
        {
            Status = "ready",
            MqttConnected = true,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Liveness probe - always returns OK if the process is running.
    /// </summary>
    [HttpGet("live")]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse
        {
            Status = "alive",
            Timestamp = DateTime.UtcNow
        });
    }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public bool MqttConnected { get; set; }
    public bool Mqtt { get; set; }
    public bool Database { get; set; }
    public bool Coordinator { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Message { get; set; }
}
