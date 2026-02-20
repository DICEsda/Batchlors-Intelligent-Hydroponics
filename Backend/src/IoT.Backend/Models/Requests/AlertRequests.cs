namespace IoT.Backend.Models.Requests;

/// <summary>
/// Request to acknowledge an alert.
/// </summary>
public class AcknowledgeAlertRequest
{
    public string? AcknowledgedBy { get; set; }
}

/// <summary>
/// Request to resolve an alert.
/// </summary>
public class ResolveAlertRequest
{
    public string? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
}
