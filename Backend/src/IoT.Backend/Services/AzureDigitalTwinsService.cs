using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using IoT.Backend.Models;
using Microsoft.Extensions.Options;

namespace IoT.Backend.Services;

/// <summary>
/// Azure Digital Twins service implementation using Azure.DigitalTwins.Core SDK.
/// Supports both DefaultAzureCredential and Service Principal authentication.
/// 
/// DI Registration in Program.cs:
/// <code>
/// // Azure Digital Twins
/// builder.Services.Configure&lt;AzureDigitalTwinsConfig&gt;(
///     builder.Configuration.GetSection(AzureDigitalTwinsConfig.Section));
/// builder.Services.AddSingleton&lt;IAzureDigitalTwinsService, AzureDigitalTwinsService&gt;();
/// builder.Services.AddSingleton&lt;AdtTwinMapper&gt;();
/// </code>
/// 
/// Required NuGet packages:
/// - Azure.DigitalTwins.Core
/// - Azure.Identity
/// 
/// Configuration in appsettings.json:
/// <code>
/// {
///   "AzureDigitalTwins": {
///     "InstanceUrl": "https://your-instance.api.wus2.digitaltwins.azure.net",
///     "TenantId": "your-tenant-id",        // Optional - for service principal
///     "ClientId": "your-client-id",        // Optional - for service principal
///     "ClientSecret": "your-secret",       // Optional - for service principal
///     "TimeoutSeconds": 30,
///     "EnableVerboseLogging": false
///   }
/// }
/// </code>
/// </summary>
public class AzureDigitalTwinsService : IAzureDigitalTwinsService
{
    private readonly AzureDigitalTwinsConfig _config;
    private readonly ILogger<AzureDigitalTwinsService> _logger;
    private readonly DigitalTwinsClient? _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AzureDigitalTwinsService(
        IOptions<AzureDigitalTwinsConfig> config,
        ILogger<AzureDigitalTwinsService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        if (_config.IsConfigured)
        {
            try
            {
                var credential = CreateCredential();
                var clientOptions = new DigitalTwinsClientOptions
                {
                    Retry = 
                    {
                        MaxRetries = 3,
                        Delay = TimeSpan.FromSeconds(1),
                        MaxDelay = TimeSpan.FromSeconds(10),
                        Mode = RetryMode.Exponential
                    }
                };

                _client = new DigitalTwinsClient(new Uri(_config.InstanceUrl!), credential, clientOptions);
                _logger.LogInformation("Azure Digital Twins client initialized for instance: {InstanceUrl}", _config.InstanceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Digital Twins client");
                _client = null;
            }
        }
        else
        {
            _logger.LogInformation("Azure Digital Twins is not configured - ADT operations will be no-ops");
        }
    }

    /// <summary>
    /// Creates the appropriate Azure credential based on configuration.
    /// </summary>
    private TokenCredential CreateCredential()
    {
        if (_config.UseServicePrincipal)
        {
            _logger.LogDebug("Using Service Principal authentication for Azure Digital Twins");
            return new ClientSecretCredential(
                _config.TenantId,
                _config.ClientId,
                _config.ClientSecret);
        }

        _logger.LogDebug("Using DefaultAzureCredential for Azure Digital Twins");
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true
        });
    }

    /// <inheritdoc />
    public async Task<bool> IsConfiguredAsync()
    {
        if (_client == null)
        {
            return false;
        }

        try
        {
            // Simple query to verify connectivity
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            await _client.QueryAsync<BasicDigitalTwin>("SELECT TOP 1 * FROM digitaltwins", cts.Token).GetAsyncEnumerator().MoveNextAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Digital Twins connectivity check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UpsertTwinAsync(string twinId, BasicDigitalTwin twin, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(UpsertTwinAsync), twinId);
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            await _client.CreateOrReplaceDigitalTwinAsync(twinId, twin, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Upserted twin {TwinId} with model {ModelId}", twinId, twin.Metadata.ModelId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Twin already exists with same ETag - this is fine for upsert scenarios
            _logger.LogDebug("Twin {TwinId} already exists with same version, skipping upsert", twinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert twin {TwinId}", twinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateTwinPropertyAsync(string twinId, JsonPatchDocument patch, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(UpdateTwinPropertyAsync), twinId);
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            await _client.UpdateDigitalTwinAsync(twinId, patch, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Updated properties for twin {TwinId}", twinId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Twin {TwinId} not found for property update", twinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update properties for twin {TwinId}", twinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendTelemetryAsync(string twinId, object telemetry, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(SendTelemetryAsync), twinId);
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            var telemetryJson = JsonSerializer.Serialize(telemetry, _jsonOptions);
            var messageId = Guid.NewGuid().ToString();
            
            await _client.PublishTelemetryAsync(twinId, messageId, telemetryJson, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Sent telemetry to twin {TwinId}: {Telemetry}", twinId, telemetryJson);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Twin {TwinId} not found for telemetry", twinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send telemetry to twin {TwinId}", twinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationshipAsync(string sourceTwinId, string targetTwinId, string relationshipName, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(CreateRelationshipAsync), $"{sourceTwinId} -> {targetTwinId}");
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            var relationshipId = $"{sourceTwinId}-{relationshipName}-{targetTwinId}";
            
            var relationship = new BasicRelationship
            {
                Id = relationshipId,
                SourceId = sourceTwinId,
                TargetId = targetTwinId,
                Name = relationshipName
            };

            await _client.CreateOrReplaceRelationshipAsync(sourceTwinId, relationshipId, relationship, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Created relationship {RelationshipName}: {SourceId} -> {TargetId}", 
                    relationshipName, sourceTwinId, targetTwinId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Relationship already exists - this is fine
            _logger.LogDebug("Relationship {RelationshipName} from {SourceId} to {TargetId} already exists", 
                relationshipName, sourceTwinId, targetTwinId);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Source or target twin not found for relationship: {SourceId} -> {TargetId}", 
                sourceTwinId, targetTwinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relationship {RelationshipName}: {SourceId} -> {TargetId}", 
                relationshipName, sourceTwinId, targetTwinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetTwinAsync<T>(string twinId, CancellationToken ct = default) where T : class
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(GetTwinAsync), twinId);
            return null;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            var response = await _client.GetDigitalTwinAsync<T>(twinId, cts.Token);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Twin {TwinId} not found", twinId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get twin {TwinId}", twinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationshipAsync(string sourceTwinId, string relationshipId, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(DeleteRelationshipAsync), $"{sourceTwinId}/{relationshipId}");
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            await _client.DeleteRelationshipAsync(sourceTwinId, relationshipId, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Deleted relationship {RelationshipId} from twin {TwinId}", relationshipId, sourceTwinId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Relationship {RelationshipId} not found on twin {TwinId}", relationshipId, sourceTwinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete relationship {RelationshipId} from twin {TwinId}", relationshipId, sourceTwinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteTwinAsync(string twinId, CancellationToken ct = default)
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(DeleteTwinAsync), twinId);
            return;
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            
            // First, delete all relationships (both incoming and outgoing)
            await foreach (var relationship in _client.GetRelationshipsAsync<BasicRelationship>(twinId, cancellationToken: cts.Token))
            {
                await _client.DeleteRelationshipAsync(twinId, relationship.Id, cancellationToken: cts.Token);
            }

            await foreach (var relationship in _client.GetIncomingRelationshipsAsync(twinId, cts.Token))
            {
                await _client.DeleteRelationshipAsync(relationship.SourceId, relationship.RelationshipId, cancellationToken: cts.Token);
            }

            // Now delete the twin
            await _client.DeleteDigitalTwinAsync(twinId, cancellationToken: cts.Token);
            
            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Deleted twin {TwinId} and all relationships", twinId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Twin {TwinId} not found for deletion", twinId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete twin {TwinId}", twinId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryTwinsAsync<T>(string query, CancellationToken ct = default) where T : class
    {
        if (_client == null)
        {
            LogNotConfigured(nameof(QueryTwinsAsync), query);
            return Array.Empty<T>();
        }

        try
        {
            using var cts = CreateTimeoutCts(ct);
            var results = new List<T>();
            
            await foreach (var twin in _client.QueryAsync<T>(query, cts.Token))
            {
                results.Add(twin);
            }

            if (_config.EnableVerboseLogging)
            {
                _logger.LogDebug("Query returned {Count} results: {Query}", results.Count, query);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query: {Query}", query);
            throw;
        }
    }

    private void LogNotConfigured(string operation, string context)
    {
        if (_config.EnableVerboseLogging)
        {
            _logger.LogDebug("ADT {Operation} skipped (not configured): {Context}", operation, context);
        }
    }

    private CancellationTokenSource CreateTimeoutCts(CancellationToken ct)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(ct);
    }
}
