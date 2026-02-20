using Azure;
using Azure.DigitalTwins.Core;

namespace IoT.Backend.Services;

/// <summary>
/// Service interface for Azure Digital Twins integration.
/// Provides methods to manage digital twins in Azure Digital Twins service.
/// </summary>
public interface IAzureDigitalTwinsService
{
    /// <summary>
    /// Creates or updates a digital twin in Azure Digital Twins.
    /// </summary>
    /// <param name="twinId">The unique identifier for the twin</param>
    /// <param name="twin">The digital twin data including model ID and properties</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task UpsertTwinAsync(string twinId, BasicDigitalTwin twin, CancellationToken ct = default);

    /// <summary>
    /// Updates specific properties of a digital twin using JSON Patch.
    /// </summary>
    /// <param name="twinId">The unique identifier for the twin</param>
    /// <param name="patch">JSON Patch document containing the property updates</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task UpdateTwinPropertyAsync(string twinId, JsonPatchDocument patch, CancellationToken ct = default);

    /// <summary>
    /// Sends telemetry data to a digital twin.
    /// </summary>
    /// <param name="twinId">The unique identifier for the twin</param>
    /// <param name="telemetry">The telemetry data object (will be serialized to JSON)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SendTelemetryAsync(string twinId, object telemetry, CancellationToken ct = default);

    /// <summary>
    /// Creates a relationship between two digital twins.
    /// </summary>
    /// <param name="sourceTwinId">The source twin ID (relationship owner)</param>
    /// <param name="targetTwinId">The target twin ID (relationship target)</param>
    /// <param name="relationshipName">The name of the relationship (must match DTDL definition)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task CreateRelationshipAsync(string sourceTwinId, string targetTwinId, string relationshipName, CancellationToken ct = default);

    /// <summary>
    /// Gets a digital twin by its ID.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the twin into</typeparam>
    /// <param name="twinId">The unique identifier for the twin</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The twin data or null if not found</returns>
    Task<T?> GetTwinAsync<T>(string twinId, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Checks whether Azure Digital Twins is configured and available.
    /// </summary>
    /// <returns>True if ADT is configured and the service can connect</returns>
    Task<bool> IsConfiguredAsync();

    /// <summary>
    /// Deletes a relationship between two digital twins.
    /// </summary>
    /// <param name="sourceTwinId">The source twin ID (relationship owner)</param>
    /// <param name="relationshipId">The unique relationship ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteRelationshipAsync(string sourceTwinId, string relationshipId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a digital twin.
    /// </summary>
    /// <param name="twinId">The unique identifier for the twin</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteTwinAsync(string twinId, CancellationToken ct = default);

    /// <summary>
    /// Queries digital twins using the ADT query language.
    /// </summary>
    /// <typeparam name="T">The type to deserialize results into</typeparam>
    /// <param name="query">The ADT query string</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of query results</returns>
    Task<IReadOnlyList<T>> QueryTwinsAsync<T>(string query, CancellationToken ct = default) where T : class;
}
