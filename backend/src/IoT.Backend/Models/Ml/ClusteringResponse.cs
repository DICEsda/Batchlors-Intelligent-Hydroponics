using System.Text.Json.Serialization;

namespace IoT.Backend.Models.Ml;

/// <summary>
/// Response from the ML service for crop compatibility clustering.
/// </summary>
public class CompatibilityMatrixResponse
{
    [JsonPropertyName("crops")]
    public List<string> Crops { get; set; } = new();

    [JsonPropertyName("matrix")]
    public List<List<double>> Matrix { get; set; } = new();
}

/// <summary>
/// Cluster assignments and recommended setpoints.
/// </summary>
public class ClustersResponse
{
    [JsonPropertyName("clusters")]
    public Dictionary<string, List<string>> Clusters { get; set; } = new();

    [JsonPropertyName("setpoints")]
    public Dictionary<string, Dictionary<string, double>> Setpoints { get; set; } = new();
}

/// <summary>
/// Request body for reservoir grouping recommendation.
/// </summary>
public class ClusterRecommendationRequest
{
    [JsonPropertyName("crops")]
    public List<string> Crops { get; set; } = new();
}

/// <summary>
/// Reservoir grouping recommendation result.
/// </summary>
public class ClusterRecommendationResponse
{
    [JsonPropertyName("input_crops")]
    public List<string> InputCrops { get; set; } = new();

    [JsonPropertyName("n_reservoirs_needed")]
    public int NReservoirsNeeded { get; set; }

    [JsonPropertyName("recommendations")]
    public List<ClusterGroup> Recommendations { get; set; } = new();
}

/// <summary>
/// A single reservoir-sharing group recommendation.
/// </summary>
public class ClusterGroup
{
    [JsonPropertyName("reservoir_group")]
    public int ReservoirGroup { get; set; }

    [JsonPropertyName("crops")]
    public List<string> Crops { get; set; } = new();

    [JsonPropertyName("average_compatibility")]
    public double AverageCompatibility { get; set; }

    [JsonPropertyName("recommended_setpoints")]
    public Dictionary<string, double> RecommendedSetpoints { get; set; } = new();

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Pairwise compatibility score between two crops.
/// </summary>
public class PairwiseScoreResponse
{
    [JsonPropertyName("crop_a")]
    public string CropA { get; set; } = string.Empty;

    [JsonPropertyName("crop_b")]
    public string CropB { get; set; } = string.Empty;

    [JsonPropertyName("compatibility_score")]
    public double CompatibilityScore { get; set; }

    [JsonPropertyName("same_cluster")]
    public bool SameCluster { get; set; }
}
