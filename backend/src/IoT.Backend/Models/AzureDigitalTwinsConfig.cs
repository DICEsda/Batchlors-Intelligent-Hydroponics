namespace IoT.Backend.Models;

/// <summary>
/// Configuration options for Azure Digital Twins integration.
/// </summary>
public class AzureDigitalTwinsConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string Section = "AzureDigitalTwins";

    /// <summary>
    /// The Azure Digital Twins instance URL (e.g., https://your-instance.api.wus2.digitaltwins.azure.net)
    /// </summary>
    public string? InstanceUrl { get; set; }

    /// <summary>
    /// Azure Active Directory tenant ID for authentication.
    /// Required when using service principal authentication.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure Active Directory application (client) ID for service principal authentication.
    /// If not provided, DefaultAzureCredential will be used.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure Active Directory client secret for service principal authentication.
    /// Required when ClientId is specified.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Indicates whether Azure Digital Twins is configured and should be used.
    /// Returns true if InstanceUrl is provided.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(InstanceUrl);

    /// <summary>
    /// Indicates whether service principal authentication is configured.
    /// When false, DefaultAzureCredential will be used (supports managed identity, Azure CLI, etc.)
    /// </summary>
    public bool UseServicePrincipal =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// Timeout in seconds for ADT API calls. Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable verbose logging for ADT operations. Defaults to false.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;
}
