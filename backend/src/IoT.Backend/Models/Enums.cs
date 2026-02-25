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
    Coordinator,
    Tower
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
    Failed,
    Cancelled,
    RollingBack
}

/// <summary>
/// OTA rollout strategy
/// </summary>
public enum RolloutStrategy
{
    /// <summary>
    /// Update all devices immediately
    /// </summary>
    Immediate,
    
    /// <summary>
    /// Update devices in batches with delays
    /// </summary>
    Staged,
    
    /// <summary>
    /// Update a small percentage first, then rollout if successful
    /// </summary>
    Canary
}

/// <summary>
/// Individual device OTA status
/// </summary>
public enum DeviceOtaStatus
{
    Pending,
    Queued,
    Downloading,
    Flashing,
    Verifying,
    Rebooting,
    Success,
    Failed,
    RolledBack,
    Skipped
}

/// <summary>
/// Crop types for hydroponic growing
/// </summary>
public enum CropType
{
    Unknown,
    
    // Leafy Greens
    Lettuce,
    Spinach,
    Kale,
    Arugula,
    SwissChard,
    BokChoy,
    
    // Herbs
    Basil,
    Mint,
    Cilantro,
    Parsley,
    Dill,
    Chives,
    Oregano,
    Thyme,
    Rosemary,
    
    // Fruiting Plants
    Tomato,
    Pepper,
    Cucumber,
    Strawberry,
    
    // Microgreens
    MicrogreenMix,
    Sunflower,
    Pea,
    Radish,
    
    // Other
    Other
}

/// <summary>
/// Method used to measure plant height
/// </summary>
public enum MeasurementMethod
{
    /// <summary>
    /// Manual measurement by user
    /// </summary>
    Manual,
    
    /// <summary>
    /// Automated measurement via sensor (e.g., ToF, ultrasonic)
    /// </summary>
    Sensor,
    
    /// <summary>
    /// Computer vision based measurement
    /// </summary>
    Vision,
    
    /// <summary>
    /// AI/ML estimated measurement
    /// </summary>
    Estimated
}
