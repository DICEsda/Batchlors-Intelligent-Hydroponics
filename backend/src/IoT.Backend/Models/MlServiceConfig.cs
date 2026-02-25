namespace IoT.Backend.Models;

/// <summary>
/// Configuration options for the ML service integration.
/// </summary>
public class MlServiceConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string Section = "MlService";

    /// <summary>
    /// Base URL of the ML FastAPI service.
    /// </summary>
    public string BaseUrl { get; set; } = "http://ml-api:8000";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Indicates whether the ML service is properly configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
