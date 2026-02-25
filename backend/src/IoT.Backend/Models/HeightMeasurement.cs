using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IoT.Backend.Models;

/// <summary>
/// Represents a plant height measurement for growth tracking.
/// Used to track plant growth over time in hydroponic tower slots.
/// </summary>
public class HeightMeasurement
{
    /// <summary>
    /// Unique identifier for this measurement
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Tower where the measurement was taken
    /// </summary>
    [BsonElement("towerId")]
    public string TowerId { get; set; } = string.Empty;

    /// <summary>
    /// Farm this measurement belongs to
    /// </summary>
    [BsonElement("farmId")]
    public string FarmId { get; set; } = string.Empty;

    /// <summary>
    /// Coordinator managing the tower
    /// </summary>
    [BsonElement("coordId")]
    public string CoordId { get; set; } = string.Empty;

    /// <summary>
    /// When the measurement was taken (UTC)
    /// </summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Measured height in centimeters
    /// </summary>
    [BsonElement("heightCm")]
    public double HeightCm { get; set; }

    /// <summary>
    /// Slot index in the tower (0-5 for a 6-slot tower)
    /// </summary>
    [BsonElement("slotIndex")]
    public int SlotIndex { get; set; }

    /// <summary>
    /// Type of crop being grown in this slot
    /// </summary>
    [BsonElement("cropType")]
    public CropType CropType { get; set; } = CropType.Unknown;

    /// <summary>
    /// Method used to obtain this measurement
    /// </summary>
    [BsonElement("method")]
    public MeasurementMethod Method { get; set; } = MeasurementMethod.Manual;

    /// <summary>
    /// Optional notes about this measurement
    /// </summary>
    [BsonElement("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Planting date for this slot (for growth rate calculations)
    /// </summary>
    [BsonElement("plantedDate")]
    public DateTime? PlantedDate { get; set; }

    /// <summary>
    /// Calculated days since planting (if PlantedDate is set)
    /// </summary>
    [BsonIgnore]
    public int? DaysSincePlanting => PlantedDate.HasValue 
        ? (int)(Timestamp - PlantedDate.Value).TotalDays 
        : null;
}
