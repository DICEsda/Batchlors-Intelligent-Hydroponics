using System.Security.Cryptography;
using IoT.Backend.Models;
using IoT.Backend.Models.Requests;
using IoT.Backend.Repositories;
using IoT.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoT.Backend.Controllers;

/// <summary>
/// Controller for OTA (Over-The-Air) firmware update management.
/// Handles creation, monitoring, and cancellation of OTA update jobs.
/// Also manages firmware version catalog for available updates.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OtaController : ControllerBase
{
    private readonly IOtaJobRepository _otaJobRepository;
    private readonly IFirmwareRepository _firmwareRepository;
    private readonly IMqttService _mqtt;
    private readonly IWsBroadcaster _broadcaster;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OtaController> _logger;

    // Maximum firmware file size: 16MB (typical ESP32 firmware is 1-4MB)
    private const long MaxFirmwareFileSize = 16 * 1024 * 1024;

    public OtaController(
        IOtaJobRepository otaJobRepository,
        IFirmwareRepository firmwareRepository,
        IMqttService mqtt,
        IWsBroadcaster broadcaster,
        IWebHostEnvironment environment,
        ILogger<OtaController> logger)
    {
        _otaJobRepository = otaJobRepository;
        _firmwareRepository = firmwareRepository;
        _mqtt = mqtt;
        _broadcaster = broadcaster;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Lists all OTA jobs, optionally filtered by farm.
    /// </summary>
    /// <param name="farm_id">Optional farm ID filter</param>
    /// <param name="limit">Maximum number of jobs to return (default 50)</param>
    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<OtaJob>>> GetJobs(
        [FromQuery] string? farm_id = null,
        [FromQuery] int limit = 50)
    {
        var jobs = await _otaJobRepository.GetAllAsync(farm_id, limit);
        return Ok(jobs);
    }

    /// <summary>
    /// Gets a specific OTA job by ID.
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    public async Task<ActionResult<OtaJob>> GetJob(string jobId)
    {
        var job = await _otaJobRepository.GetByIdAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        return Ok(job);
    }

    /// <summary>
    /// Starts a new OTA update job.
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<OtaJob>> StartOtaJob([FromBody] StartOtaRequest request)
    {
        if (string.IsNullOrEmpty(request.FarmId))
        {
            return BadRequest(new { error = "farm_id is required" });
        }

        if (string.IsNullOrEmpty(request.CoordId))
        {
            return BadRequest(new { error = "coord_id is required" });
        }

        if (string.IsNullOrEmpty(request.TargetVersion))
        {
            return BadRequest(new { error = "target_version is required" });
        }

        // Validate target type
        var validTypes = new[] { "tower", "coordinator" };
        var targetType = request.TargetType?.ToLowerInvariant() ?? "tower";
        
        if (!validTypes.Contains(targetType))
        {
            return BadRequest(new { error = $"Invalid target_type. Must be one of: {string.Join(", ", validTypes)}" });
        }

        // Validate rollout strategy if provided
        var validStrategies = new[] { "immediate", "staged", "canary" };
        var rolloutStrategy = request.RolloutStrategy?.ToLowerInvariant() ?? "immediate";
        
        if (!validStrategies.Contains(rolloutStrategy))
        {
            return BadRequest(new { error = $"Invalid rollout_strategy. Must be one of: {string.Join(", ", validStrategies)}" });
        }

        // Resolve firmware URL and checksum from FirmwareId if provided
        string? firmwareUrl = request.FirmwareUrl;
        string? firmwareChecksum = request.FirmwareChecksum;

        if (!string.IsNullOrEmpty(request.FirmwareId))
        {
            var firmware = await _firmwareRepository.GetByIdAsync(request.FirmwareId);
            if (firmware == null)
            {
                return BadRequest(new { error = $"Firmware version not found: {request.FirmwareId}" });
            }

            // Use firmware record values if not overridden in request
            firmwareUrl ??= firmware.DownloadUrl;
            firmwareChecksum ??= firmware.Checksum;
        }

        var job = new OtaJob
        {
            Id = Guid.NewGuid().ToString("N"),
            FarmId = request.FarmId,
            CoordId = request.CoordId,
            TargetType = targetType,
            TargetId = request.TargetId,
            TargetVersion = request.TargetVersion,
            FirmwareId = request.FirmwareId,
            FirmwareUrl = firmwareUrl,
            FirmwareChecksum = firmwareChecksum,
            Status = "pending",
            Progress = 0,
            DevicesTotal = 0,
            DevicesUpdated = 0,
            DevicesFailed = 0,
            DevicesPending = 0,
            RolloutStrategy = rolloutStrategy,
            RolloutPercentage = request.RolloutPercentage ?? 100,
            RolloutBatchSize = request.RolloutBatchSize ?? 0,
            RolloutDelaySeconds = request.RolloutDelaySeconds ?? 0,
            FailureThreshold = request.FailureThreshold ?? 20,
            AutoRollback = request.AutoRollback ?? true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _otaJobRepository.CreateAsync(job);
        _logger.LogInformation("Created OTA job {JobId} for {TargetType} to version {Version} on farm {FarmId}",
            job.Id, job.TargetType, job.TargetVersion, job.FarmId);

        // Send MQTT command to start OTA (new topic format: farm/{farmId}/coord/{coordId}/ota/start)
        var cmdTopic = MqttTopics.OtaStart(request.FarmId, request.CoordId);
        var cmdPayload = new
        {
            job_id = job.Id,
            target_type = job.TargetType,
            target_id = job.TargetId,
            target_version = job.TargetVersion,
            firmware_url = job.FirmwareUrl,
            firmware_checksum = job.FirmwareChecksum,
            rollout_strategy = job.RolloutStrategy,
            rollout_percentage = job.RolloutPercentage,
            rollout_batch_size = job.RolloutBatchSize,
            rollout_delay_seconds = job.RolloutDelaySeconds,
            failure_threshold = job.FailureThreshold,
            auto_rollback = job.AutoRollback
        };

        await _mqtt.PublishJsonAsync(cmdTopic, cmdPayload);

        // Broadcast OTA status to WebSocket clients
        await _broadcaster.BroadcastOtaStatusAsync(job.Id, "pending");

        return CreatedAtAction(nameof(GetJob), new { jobId = job.Id }, job);
    }

    /// <summary>
    /// Cancels an OTA job.
    /// </summary>
    [HttpPost("jobs/{jobId}/cancel")]
    public async Task<ActionResult> CancelJob(string jobId)
    {
        var job = await _otaJobRepository.GetByIdAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        if (job.Status is "completed" or "failed" or "cancelled")
        {
            return BadRequest(new { error = $"Cannot cancel job in {job.Status} state" });
        }

        // Update status
        await _otaJobRepository.UpdateStatusAsync(jobId, "cancelled");
        _logger.LogInformation("Cancelled OTA job {JobId}", jobId);

        // Send cancel command via MQTT (new topic format: farm/{farmId}/coord/{coordId}/ota/cancel)
        var cmdTopic = MqttTopics.OtaCancel(job.FarmId, job.CoordId);
        await _mqtt.PublishJsonAsync(cmdTopic, new { job_id = jobId });

        // Broadcast status update
        await _broadcaster.BroadcastOtaStatusAsync(jobId, "cancelled");

        return Ok(new { message = "Job cancelled", job_id = jobId });
    }

    /// <summary>
    /// Updates OTA job progress (called by coordinator via MQTT or internal service).
    /// </summary>
    [HttpPut("jobs/{jobId}/progress")]
    public async Task<ActionResult> UpdateProgress(string jobId, [FromBody] UpdateProgressRequest request)
    {
        var job = await _otaJobRepository.GetByIdAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        // Update job fields
        if (request.Status != null)
            job.Status = request.Status;
        if (request.Progress.HasValue)
            job.Progress = request.Progress.Value;
        if (request.DevicesTotal.HasValue)
            job.DevicesTotal = request.DevicesTotal.Value;
        if (request.DevicesUpdated.HasValue)
            job.DevicesUpdated = request.DevicesUpdated.Value;
        if (request.DevicesFailed.HasValue)
            job.DevicesFailed = request.DevicesFailed.Value;
        if (request.ErrorMessage != null)
            job.ErrorMessage = request.ErrorMessage;

        job.UpdatedAt = DateTime.UtcNow;

        // Set completed time for terminal states
        if (job.Status is "completed" or "failed" or "cancelled")
        {
            job.CompletedAt = DateTime.UtcNow;
        }

        await _otaJobRepository.UpdateAsync(job);

        // Broadcast progress update
        await _broadcaster.BroadcastOtaStatusAsync(jobId, job.Status);

        return Ok(job);
    }

    /// <summary>
    /// Gets the status of the last OTA job for a device.
    /// </summary>
    [HttpGet("status/{deviceType}/{deviceId}")]
    public async Task<ActionResult> GetDeviceOtaStatus(string deviceType, string deviceId)
    {
        // Map terminology
        var targetType = deviceType.ToLowerInvariant();
        if (targetType == "tower") targetType = "node";
        if (targetType == "reservoir") targetType = "coordinator";

        // Get recent jobs and find the last one for this device
        var jobs = await _otaJobRepository.GetAllAsync(limit: 100);
        var deviceJob = jobs.FirstOrDefault(j => 
            j.TargetType == targetType && 
            (j.TargetId == deviceId || string.IsNullOrEmpty(j.TargetId)));

        if (deviceJob == null)
        {
            return Ok(new { status = "none", message = "No OTA jobs found for this device" });
        }

        return Ok(new
        {
            job_id = deviceJob.Id,
            status = deviceJob.Status,
            target_version = deviceJob.TargetVersion,
            progress = deviceJob.Progress,
            updated_at = deviceJob.UpdatedAt
        });
    }

    #region Firmware Version Management

    /// <summary>
    /// Uploads a firmware binary file and creates a FirmwareVersion record.
    /// </summary>
    /// <param name="file">The .bin firmware file to upload</param>
    /// <param name="version">Semantic version string (e.g., "1.0.0")</param>
    /// <param name="deviceType">Device type: "coordinator" or "tower"</param>
    /// <param name="changelog">Optional changelog/release notes</param>
    /// <param name="isStable">Whether this is a stable release (default: true)</param>
    /// <param name="minVersion">Minimum firmware version required to upgrade</param>
    /// <returns>The created FirmwareVersion record</returns>
    [HttpPost("firmware/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFirmwareFileSize)]
    public async Task<ActionResult<FirmwareVersion>> UploadFirmware(
        IFormFile file,
        [FromForm] string version,
        [FromForm] string deviceType,
        [FromForm] string? changelog = null,
        [FromForm] bool isStable = true,
        [FromForm] string? minVersion = null)
    {
        // Validate required parameters
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (string.IsNullOrEmpty(version))
        {
            return BadRequest(new { error = "version is required" });
        }

        // Validate file extension
        var fileName = file.FileName.ToLowerInvariant();
        if (!fileName.EndsWith(".bin"))
        {
            return BadRequest(new { error = "Invalid file type. Only .bin files are allowed" });
        }

        // Validate file size
        if (file.Length > MaxFirmwareFileSize)
        {
            return BadRequest(new { error = $"File size exceeds maximum allowed size of {MaxFirmwareFileSize / (1024 * 1024)}MB" });
        }

        // Validate device type
        var validTypes = new[] { "coordinator", "tower" };
        var normalizedDeviceType = deviceType?.ToLowerInvariant() ?? "coordinator";
        
        if (!validTypes.Contains(normalizedDeviceType))
        {
            return BadRequest(new { error = $"Invalid device_type. Must be one of: {string.Join(", ", validTypes)}" });
        }

        // Check if version already exists for this device type
        var existing = await _firmwareRepository.GetByVersionAndTypeAsync(version, normalizedDeviceType);
        if (existing != null)
        {
            return Conflict(new { error = $"Firmware version {version} already exists for {normalizedDeviceType}" });
        }

        // Create firmware directory if it doesn't exist
        var firmwareDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), 
            "firmware", normalizedDeviceType);
        Directory.CreateDirectory(firmwareDir);

        // Generate unique filename: {version}_{timestamp}.bin
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeVersion = version.Replace(".", "_").Replace("-", "_");
        var storedFileName = $"{safeVersion}_{timestamp}.bin";
        var filePath = Path.Combine(firmwareDir, storedFileName);

        // Calculate SHA256 checksum and save file
        string checksum;
        try
        {
            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);
            
            await file.CopyToAsync(cryptoStream);
            cryptoStream.FlushFinalBlock();
            
            checksum = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save firmware file");
            return StatusCode(500, new { error = "Failed to save firmware file" });
        }

        // Build download URL (relative path that can be served by static files middleware)
        var downloadUrl = $"/firmware/{normalizedDeviceType}/{storedFileName}";

        // Create FirmwareVersion record
        var firmware = new FirmwareVersion
        {
            Id = Guid.NewGuid().ToString("N"),
            Version = version,
            DeviceType = normalizedDeviceType,
            Changelog = changelog,
            ReleaseDate = DateTime.UtcNow,
            DownloadUrl = downloadUrl,
            Checksum = checksum,
            FileSizeBytes = file.Length,
            IsStable = isStable,
            MinVersion = minVersion,
            CreatedAt = DateTime.UtcNow
        };

        await _firmwareRepository.CreateAsync(firmware);
        
        _logger.LogInformation(
            "Uploaded firmware {Version} for {DeviceType}: {FileName} ({Size} bytes, checksum: {Checksum})",
            version, normalizedDeviceType, storedFileName, file.Length, checksum);

        return CreatedAtAction(nameof(GetFirmwareVersion), new { id = firmware.Id }, firmware);
    }

    /// <summary>
    /// Lists all available firmware versions.
    /// </summary>
    /// <param name="device_type">Filter by device type (coordinator/tower)</param>
    /// <param name="stable_only">If true, only return stable releases</param>
    /// <param name="limit">Maximum number of results (default 50)</param>
    [HttpGet("firmware")]
    public async Task<ActionResult<IReadOnlyList<FirmwareVersion>>> GetFirmwareVersions(
        [FromQuery] string? device_type = null,
        [FromQuery] bool? stable_only = null,
        [FromQuery] int limit = 50)
    {
        var versions = await _firmwareRepository.GetAllAsync(device_type, stable_only, limit);
        return Ok(versions);
    }

    /// <summary>
    /// Gets a specific firmware version by ID.
    /// </summary>
    [HttpGet("firmware/{id}")]
    public async Task<ActionResult<FirmwareVersion>> GetFirmwareVersion(string id)
    {
        var firmware = await _firmwareRepository.GetByIdAsync(id);
        if (firmware == null)
        {
            return NotFound(new { error = "Firmware version not found" });
        }

        return Ok(firmware);
    }

    /// <summary>
    /// Gets the latest firmware version for a device type.
    /// </summary>
    /// <param name="deviceType">Device type (coordinator/tower)</param>
    /// <param name="stable_only">If true, only consider stable releases (default true)</param>
    [HttpGet("firmware/latest/{deviceType}")]
    public async Task<ActionResult<FirmwareVersion>> GetLatestFirmware(
        string deviceType,
        [FromQuery] bool stable_only = true)
    {
        var validTypes = new[] { "coordinator", "tower" };
        var normalizedType = deviceType.ToLowerInvariant();
        
        if (!validTypes.Contains(normalizedType))
        {
            return BadRequest(new { error = $"Invalid device_type. Must be one of: {string.Join(", ", validTypes)}" });
        }

        var firmware = await _firmwareRepository.GetLatestAsync(normalizedType, stable_only);
        if (firmware == null)
        {
            return NotFound(new { error = $"No firmware found for device type: {deviceType}" });
        }

        return Ok(firmware);
    }

    /// <summary>
    /// Creates a new firmware version record.
    /// </summary>
    [HttpPost("firmware")]
    public async Task<ActionResult<FirmwareVersion>> CreateFirmwareVersion([FromBody] CreateFirmwareRequest request)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.Version))
        {
            return BadRequest(new { error = "version is required" });
        }

        if (string.IsNullOrEmpty(request.DownloadUrl))
        {
            return BadRequest(new { error = "download_url is required" });
        }

        // Validate device type
        var validTypes = new[] { "coordinator", "tower" };
        var deviceType = request.DeviceType?.ToLowerInvariant() ?? "coordinator";
        
        if (!validTypes.Contains(deviceType))
        {
            return BadRequest(new { error = $"Invalid device_type. Must be one of: {string.Join(", ", validTypes)}" });
        }

        // Check if version already exists for this device type
        var existing = await _firmwareRepository.GetByVersionAndTypeAsync(request.Version, deviceType);
        if (existing != null)
        {
            return Conflict(new { error = $"Firmware version {request.Version} already exists for {deviceType}" });
        }

        var firmware = new FirmwareVersion
        {
            Id = Guid.NewGuid().ToString("N"),
            Version = request.Version,
            DeviceType = deviceType,
            Changelog = request.Changelog,
            ReleaseDate = request.ReleaseDate ?? DateTime.UtcNow,
            DownloadUrl = request.DownloadUrl,
            Checksum = request.Checksum ?? string.Empty,
            FileSizeBytes = request.FileSizeBytes ?? 0,
            IsStable = request.IsStable,
            MinVersion = request.MinVersion,
            CreatedAt = DateTime.UtcNow
        };

        await _firmwareRepository.CreateAsync(firmware);
        _logger.LogInformation("Created firmware version {Version} for {DeviceType}", 
            firmware.Version, firmware.DeviceType);

        return CreatedAtAction(nameof(GetFirmwareVersion), new { id = firmware.Id }, firmware);
    }

    /// <summary>
    /// Deletes a firmware version and its associated binary file.
    /// </summary>
    [HttpDelete("firmware/{id}")]
    public async Task<ActionResult> DeleteFirmwareVersion(string id)
    {
        var firmware = await _firmwareRepository.GetByIdAsync(id);
        if (firmware == null)
        {
            return NotFound(new { error = "Firmware version not found" });
        }

        // Delete the physical file if it exists and is a local file
        if (!string.IsNullOrEmpty(firmware.DownloadUrl) && firmware.DownloadUrl.StartsWith("/firmware/"))
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var filePath = Path.Combine(webRoot, firmware.DownloadUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("Deleted firmware file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete firmware file: {FilePath}", filePath);
                    // Continue with database deletion even if file deletion fails
                }
            }
        }

        await _firmwareRepository.DeleteAsync(id);
        _logger.LogInformation("Deleted firmware version {Id} ({Version} for {DeviceType})", 
            id, firmware.Version, firmware.DeviceType);

        return Ok(new { message = "Firmware version deleted", id });
    }

    #endregion
}
