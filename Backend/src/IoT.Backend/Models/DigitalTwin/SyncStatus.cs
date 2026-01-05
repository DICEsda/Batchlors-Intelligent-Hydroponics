namespace IoT.Backend.Models.DigitalTwin;

/// <summary>
/// Represents the synchronization status between desired and reported state
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Desired state matches reported state - device is in sync
    /// </summary>
    InSync,
    
    /// <summary>
    /// Desired state has been set but not yet confirmed by device
    /// </summary>
    Pending,
    
    /// <summary>
    /// Device hasn't reported in longer than expected threshold
    /// </summary>
    Stale,
    
    /// <summary>
    /// Desired and reported state differ after sync timeout - requires investigation
    /// </summary>
    Conflict,
    
    /// <summary>
    /// Device is offline or not responding
    /// </summary>
    Offline
}
