namespace IoT.Backend.Models.Requests;

/// <summary>
/// Request to create a new farm manually.
/// </summary>
public class CreateFarmRequest
{
    public string FarmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request to update an existing farm.
/// </summary>
public class UpdateFarmRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}
