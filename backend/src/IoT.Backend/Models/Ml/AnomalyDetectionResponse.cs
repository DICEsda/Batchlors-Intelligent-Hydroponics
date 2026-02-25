using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response model containing anomaly detection results.
/// </summary>
public class AnomalyDetectionResponse
{
    /// <summary>
    /// Tower identifier (if provided in request).
    /// </summary>
    [JsonPropertyName("tower_id")]
    public string? TowerId { get; set; }

    /// <summary>
    /// Coordinator identifier (if provided in request).
    /// </summary>
    [JsonPropertyName("coord_id")]
    public string? CoordId { get; set; }

    /// <summary>
    /// Whether any anomaly was detected.
    /// </summary>
    [JsonPropertyName("is_anomalous")]
    public bool IsAnomalous { get; set; }

    /// <summary>
    /// Overall anomaly score (0-1).
    /// </summary>
    [JsonPropertyName("anomaly_score")]
    public double AnomalyScore { get; set; }

    /// <summary>
    /// List of detected anomalies.
    /// </summary>
    [JsonPropertyName("anomalies")]
    public List<AnomalyResult> Anomalies { get; set; } = new();

    /// <summary>
    /// Name of the model used for detection.
    /// </summary>
    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the model used for detection.
    /// </summary>
    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the detection was performed (ISO format).
    /// </summary>
    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;
}

/// <summary>
/// Single anomaly detection result.
/// </summary>
public class AnomalyResult
{
    /// <summary>
    /// Feature name that may be anomalous.
    /// </summary>
    [JsonPropertyName("feature")]
    public string Feature { get; set; } = string.Empty;

    /// <summary>
    /// Current value of the feature.
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    /// <summary>
    /// Expected minimum value.
    /// </summary>
    [JsonPropertyName("expected_min")]
    public double ExpectedMin { get; set; }

    /// <summary>
    /// Expected maximum value.
    /// </summary>
    [JsonPropertyName("expected_max")]
    public double ExpectedMax { get; set; }

    /// <summary>
    /// Severity level: low, medium, high, critical.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the anomaly.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
