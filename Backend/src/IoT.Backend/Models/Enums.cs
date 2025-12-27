namespace IoT.Backend.Models;

/// <summary>
/// Status mode for nodes and coordinators
/// </summary>
public enum StatusMode
{
    Operational,
    Pairing,
    Ota,
    Error
}

/// <summary>
/// Target type for OTA updates
/// </summary>
public enum TargetType
{
    Node,
    Coordinator
}

/// <summary>
/// OTA job status
/// </summary>
public enum OtaStatus
{
    Pending,
    InProgress,
    Success,
    Rollback,
    Failed
}
