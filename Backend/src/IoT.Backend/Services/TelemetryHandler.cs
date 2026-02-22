using System.Diagnostics;
using System.Text.Json;
using IoT.Backend.Models;
using IoT.Backend.Models.DigitalTwin;
using IoT.Backend.Repositories;

namespace IoT.Backend.Services;

/// <summary>
/// Hosted service that subscribes to MQTT telemetry topics,
/// persists data to MongoDB, and broadcasts to WebSocket clients.
/// </summary>
public class TelemetryHandler : BackgroundService
{
    private readonly IMqttService _mqtt;
    private readonly ICoordinatorRepository _coordinatorRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IOtaJobRepository _otaJobRepository;
    private readonly IWsBroadcaster _broadcaster;
    private readonly ITwinService _twinService;
    private readonly IPairingService _pairingService;
    private readonly ICoordinatorRegistrationService _registrationService;
    private readonly IDiagnosticsService _diagnostics;
    private readonly ILogger<TelemetryHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TelemetryHandler(
        IMqttService mqtt, 
        ICoordinatorRepository coordinatorRepository,
        ITelemetryRepository telemetryRepository,
        IOtaJobRepository otaJobRepository,
        IWsBroadcaster broadcaster,
        ITwinService twinService,
        IPairingService pairingService,
        ICoordinatorRegistrationService registrationService,
        IDiagnosticsService diagnostics,
        ILogger<TelemetryHandler> logger)
    {
        _mqtt = mqtt;
        _coordinatorRepository = coordinatorRepository;
        _telemetryRepository = telemetryRepository;
        _otaJobRepository = otaJobRepository;
        _broadcaster = broadcaster;
        _twinService = twinService;
        _pairingService = pairingService;
        _registrationService = registrationService;
        _diagnostics = diagnostics;
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

        // Load registered coordinators into cache before processing any messages
        await _registrationService.RefreshCacheAsync(stoppingToken);

        _logger.LogInformation("TelemetryHandler subscribing to MQTT topics");

        // ============================================================================
        // Legacy Smart Tile Topics (site/{siteId}/...)
        // ============================================================================
        
        // Subscribe to coordinator telemetry
        await _mqtt.SubscribeAsync("site/+/coord/+/telemetry", HandleCoordinatorTelemetry, ct: stoppingToken);
        
        // Subscribe to status updates
        await _mqtt.SubscribeAsync("site/+/coord/+/status", HandleCoordinatorStatus, ct: stoppingToken);
        
        // Subscribe to OTA progress updates
        await _mqtt.SubscribeAsync("site/+/ota/progress", HandleOtaProgress, ct: stoppingToken);

        // ============================================================================
        // Hydroponic System Topics (farm/{farmId}/...)
        // ============================================================================
        
        // Subscribe to reservoir telemetry (coordinator with water quality sensors)
        await _mqtt.SubscribeAsync("farm/+/coord/+/reservoir/telemetry", HandleReservoirTelemetry, ct: stoppingToken);
        
        // Subscribe to tower telemetry (DHT22, light sensor, actuator states)
        await _mqtt.SubscribeAsync("farm/+/coord/+/tower/+/telemetry", HandleTowerTelemetry, ct: stoppingToken);
        
        // Subscribe to tower status updates
        await _mqtt.SubscribeAsync("farm/+/coord/+/tower/+/status", HandleTowerStatus, ct: stoppingToken);

        // ============================================================================
        // Pairing Workflow Topics
        // ============================================================================
        
        // Subscribe to tower pairing requests (tower wants to pair with coordinator)
        await _mqtt.SubscribeAsync("farm/+/coord/+/pairing/request", HandlePairingRequest, ct: stoppingToken);
        
        // Subscribe to pairing mode status updates from coordinator
        await _mqtt.SubscribeAsync("farm/+/coord/+/pairing/status", HandlePairingStatus, ct: stoppingToken);
        
        // Subscribe to pairing completion events from coordinator
        await _mqtt.SubscribeAsync("farm/+/coord/+/pairing/complete", HandlePairingComplete, ct: stoppingToken);

        // ============================================================================
        // Coordinator Serial Log Topics (Real-time log streaming)
        // ============================================================================
        
        // Subscribe to coordinator serial logs (debug/diagnostics)
        await _mqtt.SubscribeAsync("farm/+/coord/+/serial", HandleSerialLog, ct: stoppingToken);

        // ============================================================================
        // Connection Status Topics (WiFi/MQTT connection events)
        // ============================================================================
        
        // Subscribe to connection status events (WiFi/MQTT connect/disconnect)
        await _mqtt.SubscribeAsync("farm/+/coord/+/status/connection", HandleConnectionStatus, ct: stoppingToken);

        // ============================================================================
        // Coordinator Registration Topics (coordinator announce/discovery)
        // ============================================================================
        
        // Subscribe to coordinator announce (registration)
        await _mqtt.SubscribeAsync("coordinator/+/announce", HandleCoordinatorAnnounce, ct: stoppingToken);

        _logger.LogInformation("TelemetryHandler subscribed to all telemetry topics");

        // Keep running until stopped
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCoordinatorTelemetry(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: site/{siteId}/coord/{coordId}/telemetry
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            coordId = parts[3];

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
                LightLux = telemetry.LightLux,
                TempC = telemetry.TempC,
                LastSeen = DateTime.UtcNow
            };

            await _coordinatorRepository.UpsertAsync(coordinator);
            _logger.LogDebug("Updated coordinator {CoordId} telemetry", coordId);

            // Broadcast to WebSocket clients (reservoir = coordinator)
            var broadcastPayload = new ReservoirTelemetryPayload
            {
                ReservoirId = coordId,
                SiteId = siteId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LightLux = telemetry.LightLux,
                TempC = telemetry.TempC,
                WifiRssi = telemetry.WifiRssi
            };
            await _broadcaster.BroadcastReservoirTelemetryAsync(broadcastPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling coordinator telemetry from {Topic}", topic);
        }
    }

    private async Task HandleCoordinatorStatus(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var siteId = parts[1];
            coordId = parts[3];

            var status = JsonSerializer.Deserialize<StatusUpdate>(payload, _jsonOptions);
            if (status == null) return;

            var existing = await _coordinatorRepository.GetBySiteAndIdAsync(siteId, coordId);
            if (existing != null)
            {
                existing.LastSeen = DateTime.UtcNow;
                await _coordinatorRepository.UpsertAsync(existing);
            }

            _logger.LogDebug("Updated coordinator {CoordId} status: {Status}", coordId, status.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling coordinator status from {Topic}", topic);
        }
    }

    private async Task HandleOtaProgress(string topic, byte[] payload)
    {
        try
        {
            // Parse topic: site/{siteId}/ota/progress
            var parts = topic.Split('/');
            if (parts.Length < 3) return;

            var siteId = parts[1];

            var progress = JsonSerializer.Deserialize<OtaProgressUpdate>(payload, _jsonOptions);
            if (progress == null || string.IsNullOrEmpty(progress.JobId)) return;

            _logger.LogInformation("OTA progress update for job {JobId}: {Status} ({Progress}%)",
                progress.JobId, progress.Status, progress.Progress);

            // Update job in database
            var job = await _otaJobRepository.GetByIdAsync(progress.JobId);
            if (job != null)
            {
                if (!string.IsNullOrEmpty(progress.Status))
                    job.Status = progress.Status;
                if (progress.Progress.HasValue)
                    job.Progress = progress.Progress.Value;
                if (progress.DevicesTotal.HasValue)
                    job.DevicesTotal = progress.DevicesTotal.Value;
                if (progress.DevicesUpdated.HasValue)
                    job.DevicesUpdated = progress.DevicesUpdated.Value;
                if (progress.DevicesFailed.HasValue)
                    job.DevicesFailed = progress.DevicesFailed.Value;
                if (!string.IsNullOrEmpty(progress.ErrorMessage))
                    job.ErrorMessage = progress.ErrorMessage;

                job.UpdatedAt = DateTime.UtcNow;

                // Set completed time for terminal states
                if (job.Status is "completed" or "failed" or "cancelled")
                {
                    job.CompletedAt = DateTime.UtcNow;
                }

                await _otaJobRepository.UpdateAsync(job);
            }

            // Broadcast to WebSocket clients
            await _broadcaster.BroadcastOtaStatusAsync(progress.JobId, progress.Status ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OTA progress from {Topic}", topic);
        }
    }

    // ============================================================================
    // Hydroponic System Handlers (Digital Twin Integration)
    // ============================================================================

    /// <summary>
    /// Handle reservoir telemetry from coordinator with water quality sensors
    /// Topic: farm/{farmId}/coord/{coordId}/reservoir/telemetry
    /// </summary>
    private async Task HandleReservoirTelemetry(string topic, byte[] payload)
    {
        var swTotal = Stopwatch.StartNew();
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/reservoir/telemetry
            var parts = topic.Split('/');
            if (parts.Length < 5) return;

            var farmId = parts[1];
            coordId = parts[3];

            var telemetry = JsonSerializer.Deserialize<ReservoirTelemetryDto>(payload, _jsonOptions);
            if (telemetry == null) return;

            _logger.LogDebug("Received reservoir telemetry for farm {FarmId} coordinator {CoordId}: pH={Ph}, EC={Ec}", 
                farmId, coordId, telemetry.Ph, telemetry.EcMsCm);

            // 1. Persist to time-series collection for historical analysis
            var reservoirTelemetry = new ReservoirTelemetry
            {
                FarmId = farmId,
                CoordId = coordId,
                Timestamp = DateTime.UtcNow,
                Ph = telemetry.Ph,
                EcMsCm = telemetry.EcMsCm,
                TdsPpm = telemetry.TdsPpm,
                WaterTempC = telemetry.WaterTempC,
                WaterLevelPct = telemetry.WaterLevelPct,
                WaterLevelCm = telemetry.WaterLevelCm,
                MainPumpOn = telemetry.MainPumpOn,
                DosingPumpPhOn = telemetry.DosingPumpPhOn,
                DosingPumpNutrientOn = telemetry.DosingPumpNutrientOn,
                WifiRssi = telemetry.WifiRssi,
                TowersOnline = telemetry.TowersOnline
            };

            var swMongo = Stopwatch.StartNew();
            await _telemetryRepository.InsertReservoirTelemetryAsync(reservoirTelemetry);
            swMongo.Stop();

            // 2. Create reported state from telemetry for Digital Twin
            var reportedState = new CoordinatorReportedState
            {
                FwVersion = telemetry.FwVersion ?? string.Empty,
                TowersOnline = telemetry.TowersOnline,
                WifiRssi = telemetry.WifiRssi,
                StatusMode = telemetry.StatusMode ?? "operational",
                UptimeS = telemetry.UptimeS,
                TempC = telemetry.TempC,
                
                // Reservoir water quality sensors
                Ph = telemetry.Ph,
                EcMsCm = telemetry.EcMsCm,
                TdsPpm = telemetry.TdsPpm,
                WaterTempC = telemetry.WaterTempC,
                
                // Water level
                WaterLevelPct = telemetry.WaterLevelPct,
                WaterLevelCm = telemetry.WaterLevelCm,
                LowWaterAlert = telemetry.LowWaterAlert,
                
                // Actuator states (as reported)
                MainPumpOn = telemetry.MainPumpOn,
                DosingPumpPhOn = telemetry.DosingPumpPhOn,
                DosingPumpNutrientOn = telemetry.DosingPumpNutrientOn
            };

            // 3. Update Digital Twin reported state
            var swTwin = Stopwatch.StartNew();
            await _twinService.ProcessCoordinatorTelemetryAsync(coordId, farmId, reportedState);
            swTwin.Stop();

            swTotal.Stop();
            _diagnostics.RecordReservoirMessage(swTotal.Elapsed, swMongo.Elapsed, swTwin.Elapsed, TimeSpan.Zero);

            _logger.LogDebug("Updated coordinator twin {CoordId} with reservoir telemetry", coordId);
        }
        catch (Exception ex)
        {
            swTotal.Stop();
            _diagnostics.RecordError("processing");
            _logger.LogError(ex, "Error handling reservoir telemetry from {Topic}", topic);
        }
    }

    /// <summary>
    /// Handle tower telemetry (DHT22, light sensor, actuator states)
    /// Topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry
    /// </summary>
    private async Task HandleTowerTelemetry(string topic, byte[] payload)
    {
        var swTotal = Stopwatch.StartNew();
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/telemetry
            var parts = topic.Split('/');
            if (parts.Length < 7) return;

            var farmId = parts[1];
            coordId = parts[3];
            var towerId = parts[5];

            var telemetry = JsonSerializer.Deserialize<TowerTelemetryDto>(payload, _jsonOptions);
            if (telemetry == null) return;

            _logger.LogDebug("Received tower telemetry for tower {TowerId}: temp={Temp}C, humidity={Humidity}%", 
                towerId, telemetry.AirTempC, telemetry.HumidityPct);

            // 1. Persist to time-series collection for historical analysis
            var towerTelemetryRecord = new TowerTelemetry
            {
                FarmId = farmId,
                CoordId = coordId,
                TowerId = towerId,
                Timestamp = DateTime.UtcNow,
                AirTempC = telemetry.AirTempC,
                HumidityPct = telemetry.HumidityPct,
                LightLux = telemetry.LightLux,
                PumpOn = telemetry.PumpOn,
                LightOn = telemetry.LightOn,
                LightBrightness = telemetry.LightBrightness,
                VbatMv = telemetry.VbatMv,
                SignalQuality = telemetry.SignalQuality,
                StatusMode = telemetry.StatusMode
            };

            var swMongo = Stopwatch.StartNew();
            await _telemetryRepository.InsertTowerTelemetryAsync(towerTelemetryRecord);
            swMongo.Stop();

            // 2. Create reported state from telemetry for Digital Twin
            var reportedState = new TowerReportedState
            {
                // Environmental sensors
                AirTempC = telemetry.AirTempC,
                HumidityPct = telemetry.HumidityPct,
                LightLux = telemetry.LightLux,
                
                // Actuator states (as reported)
                PumpOn = telemetry.PumpOn,
                LightOn = telemetry.LightOn,
                LightBrightness = telemetry.LightBrightness,
                
                // System status
                StatusMode = telemetry.StatusMode ?? "operational",
                VbatMv = telemetry.VbatMv,
                FwVersion = telemetry.FwVersion ?? string.Empty,
                UptimeS = telemetry.UptimeS,
                SignalQuality = telemetry.SignalQuality
            };

            // 3. Update Digital Twin reported state
            var swTwin = Stopwatch.StartNew();
            await _twinService.ProcessTowerTelemetryAsync(towerId, coordId, farmId, reportedState);
            swTwin.Stop();

            _logger.LogDebug("Updated tower twin {TowerId} with telemetry", towerId);

            // 4. Broadcast to WebSocket clients for real-time dashboard
            var broadcastPayload = new TowerTelemetryPayload
            {
                TowerId = towerId,
                LightId = towerId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TempC = telemetry.AirTempC,
                VbatMv = telemetry.VbatMv,
                StatusMode = telemetry.StatusMode ?? "operational",
                Light = new TowerLightState
                {
                    On = telemetry.LightOn,
                    Brightness = telemetry.LightBrightness,
                    AvgR = 0,
                    AvgG = 0,
                    AvgB = 0,
                    AvgW = telemetry.LightBrightness
                }
            };

            var swWs = Stopwatch.StartNew();
            await _broadcaster.BroadcastTowerTelemetryAsync(broadcastPayload);
            swWs.Stop();

            swTotal.Stop();
            _diagnostics.RecordTowerMessage(swTotal.Elapsed, swMongo.Elapsed, swTwin.Elapsed, swWs.Elapsed);
        }
        catch (Exception ex)
        {
            swTotal.Stop();
            _diagnostics.RecordError("processing");
            _logger.LogError(ex, "Error handling tower telemetry from {Topic}", topic);
        }
    }

    /// <summary>
    /// Handle tower status updates (online/offline, mode changes)
    /// Topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/status
    /// </summary>
    private async Task HandleTowerStatus(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/tower/{towerId}/status
            var parts = topic.Split('/');
            if (parts.Length < 7) return;

            var farmId = parts[1];
            coordId = parts[3];
            var towerId = parts[5];

            var status = JsonSerializer.Deserialize<TowerStatusDto>(payload, _jsonOptions);
            if (status == null) return;

            _logger.LogDebug("Received tower status for {TowerId}: online={Online}, mode={Mode}", 
                towerId, status.Online, status.StatusMode);

            // If the tower reports a status mode change, update the twin
            if (!string.IsNullOrEmpty(status.StatusMode))
            {
                var reportedState = new TowerReportedState
                {
                    StatusMode = status.StatusMode
                };

            await _twinService.ProcessTowerTelemetryAsync(towerId, coordId, farmId, reportedState);
            }

            // If this is an acknowledgment of a command sync, mark it successful
            if (status.CmdAck == true)
            {
                await _twinService.MarkTowerSyncSuccessAsync(towerId);
                _logger.LogInformation("Tower {TowerId} acknowledged command sync", towerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tower status from {Topic}", topic);
        }
    }

    // ============================================================================
    // Pairing Workflow Handlers
    // ============================================================================

    /// <summary>
    /// Handle tower pairing request from coordinator
    /// Topic: farm/{farmId}/coord/{coordId}/pairing/request
    /// </summary>
    private async Task HandlePairingRequest(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/pairing/request
            var parts = topic.Split('/');
            if (parts.Length < 5) return;

            var farmId = parts[1];
            coordId = parts[3];

            var dto = JsonSerializer.Deserialize<PairingRequestDto>(payload, _jsonOptions);
            if (dto == null || string.IsNullOrEmpty(dto.TowerId)) return;

            _logger.LogInformation(
                "Received pairing request from tower {TowerId} (MAC: {Mac}) on {FarmId}/{CoordId}",
                dto.TowerId, dto.MacAddress, farmId, coordId);

            // Create TowerPairingRequest from DTO
            var request = new TowerPairingRequest
            {
                TowerId = dto.TowerId,
                MacAddress = dto.MacAddress,
                FwVersion = dto.FwVersion,
                Capabilities = dto.Capabilities,
                Rssi = dto.Rssi
            };

            // Process via PairingService
            await _pairingService.ProcessPairingRequestAsync(farmId, coordId, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pairing request from {Topic}", topic);
        }
    }

    /// <summary>
    /// Handle pairing mode status update from coordinator
    /// Topic: farm/{farmId}/coord/{coordId}/pairing/status
    /// </summary>
    private async Task HandlePairingStatus(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/pairing/status
            var parts = topic.Split('/');
            if (parts.Length < 5) return;

            var farmId = parts[1];
            coordId = parts[3];

            var status = JsonSerializer.Deserialize<PairingStatusUpdate>(payload, _jsonOptions);
            if (status == null) return;

            _logger.LogDebug("Received pairing status from {FarmId}/{CoordId}: {Status}",
                farmId, coordId, status.Status);

            // Forward to PairingService
            await _pairingService.HandlePairingStatusAsync(farmId, coordId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pairing status from {Topic}", topic);
        }
    }

    /// <summary>
    /// Handle pairing completion event from coordinator
    /// Topic: farm/{farmId}/coord/{coordId}/pairing/complete
    /// </summary>
    private async Task HandlePairingComplete(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/pairing/complete
            var parts = topic.Split('/');
            if (parts.Length < 5) return;

            var farmId = parts[1];
            coordId = parts[3];

            var completion = JsonSerializer.Deserialize<PairingCompleteEvent>(payload, _jsonOptions);
            if (completion == null) return;

            _logger.LogInformation(
                "Received pairing complete for tower {TowerId} on {FarmId}/{CoordId}: success={Success}",
                completion.TowerId, farmId, coordId, completion.Success);

            // Forward to PairingService
            await _pairingService.HandlePairingCompleteAsync(farmId, coordId, completion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pairing complete from {Topic}", topic);
        }
    }

    // ============================================================================
    // Serial Log Handler (Real-time log streaming)
    // ============================================================================

    /// <summary>
    /// Handle coordinator serial log messages for real-time debugging
    /// Topic: farm/{farmId}/coord/{coordId}/serial
    /// </summary>
    private async Task HandleSerialLog(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/serial
            var parts = topic.Split('/');
            if (parts.Length < 5)
            {
                _logger.LogWarning("Invalid serial log topic format: {Topic}", topic);
                return;
            }

            var farmId = parts[1];
            coordId = parts[3];

            _logger.LogDebug("Received serial log from {FarmId}/{CoordId}", farmId, coordId);

            // Deserialize log payload
            var logDto = JsonSerializer.Deserialize<SerialLogDto>(payload, _jsonOptions);
            if (logDto == null)
            {
                _logger.LogWarning("Failed to deserialize serial log from {Topic}", topic);
                return;
            }

            // Console debug output
            Console.WriteLine($"[SERIAL LOG] {farmId}/{coordId} [{logDto.Level}] {logDto.Message}");
            _logger.LogInformation("Serial log from {CoordId}: [{Level}] {Message}", 
                coordId, logDto.Level ?? "INFO", logDto.Message);

            // Broadcast to WebSocket clients for real-time display
            var wsPayload = new CoordinatorLogPayload
            {
                CoordId = coordId,
                FarmId = farmId,
                Message = logDto.Message,
                Level = logDto.Level ?? "INFO",
                Tag = logDto.Tag,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await _broadcaster.BroadcastCoordinatorLogAsync(wsPayload);
            
            _logger.LogDebug("Broadcasted serial log to WebSocket clients");

            // TODO: Optionally persist to MongoDB for historical analysis
            // await _repository.InsertSerialLogAsync(new SerialLog { ... });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling serial log from {Topic}", topic);
            Console.WriteLine($"[ERROR] Failed to handle serial log: {ex.Message}");
        }
    }

    // ============================================================================
    // Connection Status Handler (WiFi/MQTT lifecycle events)
    // ============================================================================

    /// <summary>
    /// Handle coordinator connection status events (WiFi/MQTT connect/disconnect).
    /// Topic: farm/{farmId}/coord/{coordId}/status/connection
    /// </summary>
    private async Task HandleConnectionStatus(string topic, byte[] payload)
    {
        try
        {
            var coordId = ExtractCoordIdFromTopic(topic);
            if (coordId != null && !await _registrationService.IsRegisteredAsync(coordId))
            {
                await _registrationService.ProcessUnknownCoordinatorAsync(coordId, topic, payload);
                return; // Drop message from unregistered coordinator
            }

            // Parse topic: farm/{farmId}/coord/{coordId}/status/connection
            var parts = topic.Split('/');
            if (parts.Length < 6)
            {
                _logger.LogWarning("Invalid connection status topic format: {Topic}", topic);
                return;
            }

            var farmId = parts[1];
            coordId = parts[3];

            _logger.LogDebug("Received connection status from {FarmId}/{CoordId}", farmId, coordId);

            // Deserialize connection status payload
            var statusDto = JsonSerializer.Deserialize<ConnectionStatusDto>(payload, _jsonOptions);
            if (statusDto == null)
            {
                _logger.LogWarning("Failed to deserialize connection status from {Topic}", topic);
                return;
            }

            // Log connection event for backend diagnostics
            var eventType = statusDto.Event switch
            {
                "wifi_connected" => "WiFi Connected",
                "wifi_disconnected" => "WiFi Disconnected",
                "mqtt_connected" => "MQTT Connected",
                "mqtt_disconnected" => "MQTT Disconnected",
                "wifi_got_ip" => "WiFi Got IP",
                "wifi_lost_ip" => "WiFi Lost IP",
                _ => statusDto.Event
            };

            var reasonMsg = !string.IsNullOrEmpty(statusDto.Reason) ? $" (Reason: {statusDto.Reason})" : "";
            _logger.LogInformation("Connection status from {CoordId}: {Event}{Reason} | WiFi: {WifiStatus} (RSSI: {Rssi}), MQTT: {MqttStatus}",
                coordId, eventType, reasonMsg, 
                statusDto.WifiConnected ? "Connected" : "Disconnected", statusDto.WifiRssi,
                statusDto.MqttConnected ? "Connected" : "Disconnected");

            // Broadcast to WebSocket clients for real-time frontend updates
            var wsPayload = new ConnectionStatusPayload
            {
                Ts = statusDto.Ts,
                CoordId = coordId,
                FarmId = farmId,
                Event = statusDto.Event,
                WifiConnected = statusDto.WifiConnected,
                WifiRssi = statusDto.WifiRssi,
                MqttConnected = statusDto.MqttConnected,
                UptimeMs = statusDto.UptimeMs,
                FreeHeap = statusDto.FreeHeap,
                Reason = statusDto.Reason
            };

            await _broadcaster.BroadcastConnectionStatusAsync(wsPayload);

            _logger.LogDebug("Broadcasted connection status to WebSocket clients");

            // TODO: Optionally persist to MongoDB for historical analysis and uptime tracking
            // await _repository.InsertConnectionEventAsync(new ConnectionEvent { ... });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection status from {Topic}", topic);
        }
    }

    // ============================================================================
    // Coordinator Registration Helpers
    // ============================================================================

    /// <summary>
    /// Extract the coordinator ID from an MQTT topic.
    /// Handles patterns like: farm/{farmId}/coord/{coordId}/...
    ///                    and: site/{siteId}/coord/{coordId}/...
    /// </summary>
    private static string? ExtractCoordIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "coord" && i + 1 < parts.Length)
                return parts[i + 1];
        }
        return null;
    }

    /// <summary>
    /// Handle coordinator announce message for registration workflow.
    /// Topic: coordinator/{mac}/announce
    /// </summary>
    private async Task HandleCoordinatorAnnounce(string topic, byte[] payload)
    {
        try
        {
            // Topic: coordinator/{mac}/announce
            var parts = topic.Split('/');
            if (parts.Length < 3) return;
            var coordId = parts[1];

            var announce = JsonSerializer.Deserialize<CoordinatorAnnounceDto>(payload, _jsonOptions);
            if (announce == null) return;

            _logger.LogInformation("Coordinator announce from {CoordId}: FW={Fw}, IP={Ip}, RSSI={Rssi}",
                coordId, announce.FwVersion, announce.Ip, announce.WifiRssi);

            await _registrationService.HandleCoordinatorAnnounceAsync(coordId, announce);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling coordinator announce from {Topic}", topic);
        }
    }

    #region DTOs for deserialization

    private class CoordinatorTelemetry
    {
        public string? FwVersion { get; set; }
        public int NodesOnline { get; set; }
        public int WifiRssi { get; set; }
        public float LightLux { get; set; }
        public float TempC { get; set; }
    }

    private class StatusUpdate
    {
        public string? Status { get; set; }
        public string? Mode { get; set; }
    }

    private class OtaProgressUpdate
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public int? Progress { get; set; }
        public int? DevicesTotal { get; set; }
        public int? DevicesUpdated { get; set; }
        public int? DevicesFailed { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // ============================================================================
    // Hydroponic System DTOs
    // ============================================================================

    /// <summary>
    /// DTO for reservoir telemetry from coordinator
    /// </summary>
    private class ReservoirTelemetryDto
    {
        // System status
        public string? FwVersion { get; set; }
        public int TowersOnline { get; set; }
        public int WifiRssi { get; set; }
        public string? StatusMode { get; set; }
        public long UptimeS { get; set; }
        public float TempC { get; set; }

        // Water quality sensors
        public float? Ph { get; set; }
        public float? EcMsCm { get; set; }
        public float? TdsPpm { get; set; }
        public float? WaterTempC { get; set; }

        // Water level
        public float? WaterLevelPct { get; set; }
        public float? WaterLevelCm { get; set; }
        public bool? LowWaterAlert { get; set; }

        // Actuator states (as reported)
        public bool? MainPumpOn { get; set; }
        public bool? DosingPumpPhOn { get; set; }
        public bool? DosingPumpNutrientOn { get; set; }
    }

    /// <summary>
    /// DTO for tower telemetry
    /// </summary>
    private class TowerTelemetryDto
    {
        // Environmental sensors
        public float AirTempC { get; set; }
        public float HumidityPct { get; set; }
        public float LightLux { get; set; }

        // Actuator states (as reported)
        public bool PumpOn { get; set; }
        public bool LightOn { get; set; }
        public int LightBrightness { get; set; }

        // System status
        public string? StatusMode { get; set; }
        public int VbatMv { get; set; }
        public string? FwVersion { get; set; }
        public long UptimeS { get; set; }
        public int? SignalQuality { get; set; }
    }

    /// <summary>
    /// DTO for tower status updates
    /// </summary>
    private class TowerStatusDto
    {
        public bool? Online { get; set; }
        public string? StatusMode { get; set; }
        /// <summary>
        /// Command acknowledgment - true if tower has applied the commanded desired state
        /// </summary>
        public bool? CmdAck { get; set; }
    }

    // ============================================================================
    // Pairing Workflow DTOs
    // ============================================================================

    /// <summary>
    /// DTO for incoming tower pairing request from MQTT
    /// Topic: farm/{farmId}/coord/{coordId}/pairing/request
    /// </summary>
    private class PairingRequestDto
    {
        /// <summary>
        /// Tower's unique identifier (typically derived from MAC)
        /// </summary>
        public string TowerId { get; set; } = string.Empty;

        /// <summary>
        /// Tower's MAC address
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Tower's firmware version
        /// </summary>
        public string? FwVersion { get; set; }

        /// <summary>
        /// Tower's hardware capabilities
        /// </summary>
        public Models.TowerCapabilities? Capabilities { get; set; }

        /// <summary>
        /// Signal strength (RSSI) at time of request
        /// </summary>
        public int? Rssi { get; set; }
    }

    /// <summary>
    /// DTO for coordinator serial log messages
    /// Topic: farm/{farmId}/coord/{coordId}/serial
    /// </summary>
    private class SerialLogDto
    {
        /// <summary>
        /// Timestamp in seconds since boot
        /// </summary>
        public int Ts { get; set; }

        /// <summary>
        /// Log message content
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Log level: DEBUG, INFO, WARN, ERROR
        /// </summary>
        public string? Level { get; set; }

        /// <summary>
        /// Optional subsystem tag (e.g., "WiFi", "MQTT", "Sensor")
        /// </summary>
        public string? Tag { get; set; }
    }

    /// <summary>
    /// DTO for coordinator connection status events (WiFi/MQTT lifecycle)
    /// Topic: farm/{farmId}/coord/{coordId}/status/connection
    /// </summary>
    private class ConnectionStatusDto
    {
        /// <summary>
        /// Timestamp in seconds since epoch
        /// </summary>
        public long Ts { get; set; }

        /// <summary>
        /// Coordinator ID
        /// </summary>
        public string CoordId { get; set; } = string.Empty;

        /// <summary>
        /// Farm ID
        /// </summary>
        public string FarmId { get; set; } = string.Empty;

        /// <summary>
        /// Event type: wifi_connected, wifi_disconnected, mqtt_connected, mqtt_disconnected, wifi_got_ip, wifi_lost_ip
        /// </summary>
        public string Event { get; set; } = string.Empty;

        /// <summary>
        /// Current WiFi connection status
        /// </summary>
        public bool WifiConnected { get; set; }

        /// <summary>
        /// WiFi signal strength (RSSI in dBm)
        /// </summary>
        public int WifiRssi { get; set; }

        /// <summary>
        /// Current MQTT broker connection status
        /// </summary>
        public bool MqttConnected { get; set; }

        /// <summary>
        /// Coordinator uptime in milliseconds
        /// </summary>
        public long UptimeMs { get; set; }

        /// <summary>
        /// Free heap memory in bytes
        /// </summary>
        public int FreeHeap { get; set; }

        /// <summary>
        /// Optional disconnect reason (e.g., "Beacon timeout", "Association leave")
        /// </summary>
        public string? Reason { get; set; }
    }

    #endregion
}

