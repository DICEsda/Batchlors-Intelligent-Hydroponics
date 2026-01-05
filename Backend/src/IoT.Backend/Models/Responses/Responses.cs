using IoT.Backend.Models.DigitalTwin;

namespace IoT.Backend.Models.Responses;

// ============================================================================
// Customize Responses
// ============================================================================

/// <summary>
/// Device configuration schema response.
/// </summary>
public class DeviceConfigSchema
{
    public Dictionary<string, object>? Device { get; set; }
    public Dictionary<string, object>? Radar { get; set; }
    public Dictionary<string, object>? Light { get; set; }
    public Dictionary<string, object>? Led { get; set; }
}

// ============================================================================
// Twin Responses
// ============================================================================

/// <summary>
/// Response containing tower state delta information.
/// </summary>
public class TowerDeltaResponse
{
    public string TowerId { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public bool IsInSync { get; set; }
    public TowerDesiredState? Delta { get; set; }
}

/// <summary>
/// Response containing coordinator state delta information.
/// </summary>
public class CoordinatorDeltaResponse
{
    public string CoordId { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public bool IsInSync { get; set; }
    public CoordinatorDesiredState? Delta { get; set; }
}

/// <summary>
/// Response containing all twins for a farm.
/// </summary>
public class FarmTwinsResponse
{
    public string FarmId { get; set; } = string.Empty;
    public IReadOnlyList<CoordinatorTwin> Coordinators { get; set; } = new List<CoordinatorTwin>();
    public IReadOnlyList<TowerTwin> Towers { get; set; } = new List<TowerTwin>();
}
