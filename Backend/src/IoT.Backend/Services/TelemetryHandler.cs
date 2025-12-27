using System.Text.Json;
using IoT.Backend.Models;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Hosted service that subscribes to MQTT telemetry topics
/// and persists data to MongoDB.
/// </summary>
public class TelemetryHandler : BackgroundService
{
    private readonly IMqttService _mqtt;
    private readonly IRepository _repository;
    private readonly ILogger<TelemetryHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TelemetryHandler(IMqttService mqtt, IRepository repository, ILogger<TelemetryHandler> logger)
    {
        _mqtt = mqtt;
        _repository = repository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for MQTT to be connected
        while (!_mqtt.IsConnected && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("TelemetryHandler subscribing to MQTT topics");

        // Subscribe to coordinator telemetry
        await _mqtt.SubscribeAsync("site/+/coord/+/telemetry", HandleCoordinatorTelemetry, ct: stoppingToken);
        
        // Subscribe to node telemetry (relayed by coordinator)
        await _mqtt.SubscribeAsync("site/+/node/+/telemetry", HandleNodeTelemetry, ct: stoppingToken);
        
        // Subscribe to mmWave data
        await _mqtt.SubscribeAsync("site/+/coord/+/mmwave", HandleMmwaveData, ct: stoppingToken);
        
        // Subscribe to status updates
        await _mqtt.SubscribeAsync("site/+/coord/+/status", HandleCoordinatorStatus, ct: stoppingToken);
        await _mqtt.SubscribeAsync("site/+/node/+/status", HandleNodeStatus, ct: stoppingToken);

        _logger.LogInformation("TelemetryHandler subscribed to all telemetry topics");

        // Keep running until stopped
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCoordinatorTelemetry(string topic, byte[] payload)
    {
        try
        {
            // Parse topic: site/{siteId}/coord/{coordId}/telemetry
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            var coordId = parts[3];

            var telemetry = JsonSerializer.Deserialize<CoordinatorTelemetry>(payload, _jsonOptions);
            if (telemetry == null) return;

            var coordinator = new Coordinator
            {
                Id = coordId,
                CoordId = coordId,
                SiteId = siteId,
                FwVersion = telemetry.FwVersion ?? "",
                NodesOnline = telemetry.NodesOnline,
                WifiRssi = telemetry.WifiRssi,
                MmwaveEventRate = telemetry.MmwaveEventRate,
                LightLux = telemetry.LightLux,
                TempC = telemetry.TempC,
                LastSeen = DateTime.UtcNow
            };

            await _repository.UpsertCoordinatorAsync(coordinator);
            _logger.LogDebug("Updated coordinator {CoordId} telemetry", coordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling coordinator telemetry from {Topic}", topic);
        }
    }

    private async Task HandleNodeTelemetry(string topic, byte[] payload)
    {
        try
        {
            // Parse topic: site/{siteId}/node/{nodeId}/telemetry
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            var nodeId = parts[3];

            var telemetry = JsonSerializer.Deserialize<NodeTelemetry>(payload, _jsonOptions);
            if (telemetry == null) return;

            var node = new Node
            {
                Id = $"{siteId}/{telemetry.CoordinatorId}/{nodeId}",
                LightId = nodeId,
                SiteId = siteId,
                CoordinatorId = telemetry.CoordinatorId ?? "",
                StatusMode = telemetry.StatusMode ?? "operational",
                AvgR = telemetry.AvgR,
                AvgG = telemetry.AvgG,
                AvgB = telemetry.AvgB,
                AvgW = telemetry.AvgW,
                TempC = telemetry.TempC,
                VbatMv = telemetry.VbatMv,
                FwVersion = telemetry.FwVersion ?? "",
                LastSeen = DateTime.UtcNow
            };

            await _repository.UpsertNodeAsync(node);
            _logger.LogDebug("Updated node {NodeId} telemetry", nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling node telemetry from {Topic}", topic);
        }
    }

    private async Task HandleMmwaveData(string topic, byte[] payload)
    {
        try
        {
            // Parse topic: site/{siteId}/coord/{coordId}/mmwave
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            var coordId = parts[3];

            var data = JsonSerializer.Deserialize<MmwavePayload>(payload, _jsonOptions);
            if (data == null) return;

            var frame = new MmwaveFrame
            {
                SiteId = siteId,
                CoordinatorId = coordId,
                SensorId = data.SensorId ?? coordId,
                Presence = data.Presence,
                Confidence = data.Confidence,
                Targets = data.Targets?.Select(t => new MmwaveTarget
                {
                    Id = t.Id,
                    DistanceMm = t.DistanceMm,
                    SpeedCmS = t.SpeedCmS,
                    ResolutionMm = t.ResolutionMm,
                    PositionXMm = t.PositionXMm,
                    PositionYMm = t.PositionYMm,
                    VelocityXMps = t.VelocityXMps,
                    VelocityYMps = t.VelocityYMps
                }).ToList() ?? new List<MmwaveTarget>(),
                Timestamp = DateTime.UtcNow
            };

            await _repository.InsertMmwaveFrameAsync(frame);
            _logger.LogDebug("Inserted mmWave frame for coordinator {CoordId}", coordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mmWave data from {Topic}", topic);
        }
    }

    private async Task HandleCoordinatorStatus(string topic, byte[] payload)
    {
        try
        {
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            var coordId = parts[3];

            var status = JsonSerializer.Deserialize<StatusUpdate>(payload, _jsonOptions);
            if (status == null) return;

            var existing = await _repository.GetCoordinatorBySiteAndIdAsync(siteId, coordId);
            if (existing != null)
            {
                existing.LastSeen = DateTime.UtcNow;
                await _repository.UpsertCoordinatorAsync(existing);
            }

            _logger.LogDebug("Updated coordinator {CoordId} status: {Status}", coordId, status.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling coordinator status from {Topic}", topic);
        }
    }

    private Task HandleNodeStatus(string topic, byte[] payload)
    {
        // Node status updates are handled via telemetry
        _logger.LogDebug("Received node status on {Topic}", topic);
        return Task.CompletedTask;
    }

    #region DTOs for deserialization

    private class CoordinatorTelemetry
    {
        public string? FwVersion { get; set; }
        public int NodesOnline { get; set; }
        public int WifiRssi { get; set; }
        public float MmwaveEventRate { get; set; }
        public float LightLux { get; set; }
        public float TempC { get; set; }
    }

    private class NodeTelemetry
    {
        public string? CoordinatorId { get; set; }
        public string? StatusMode { get; set; }
        public int AvgR { get; set; }
        public int AvgG { get; set; }
        public int AvgB { get; set; }
        public int AvgW { get; set; }
        public float TempC { get; set; }
        public int VbatMv { get; set; }
        public string? FwVersion { get; set; }
    }

    private class MmwavePayload
    {
        public string? SensorId { get; set; }
        public bool Presence { get; set; }
        public float Confidence { get; set; }
        public List<MmwaveTargetDto>? Targets { get; set; }
    }

    private class MmwaveTargetDto
    {
        public int Id { get; set; }
        public int DistanceMm { get; set; }
        public int SpeedCmS { get; set; }
        public int ResolutionMm { get; set; }
        public int PositionXMm { get; set; }
        public int PositionYMm { get; set; }
        public float VelocityXMps { get; set; }
        public float VelocityYMps { get; set; }
    }

    private class StatusUpdate
    {
        public string? Status { get; set; }
        public string? Mode { get; set; }
    }

    #endregion
}
