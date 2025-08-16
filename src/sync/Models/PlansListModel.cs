using System.Text.Json.Serialization;

namespace sync.Models;

internal record PlansListModel
{
    [JsonPropertyName("data")] public required PlanListData Data { get; init; }
    [JsonPropertyName("meta")] public required MetaData Meta { get; init; }
}

internal record PlanListData
{
    [JsonPropertyName("plans")] public required List<Plan> Plans { get; init; }
}

internal record Plan
{
    [JsonPropertyName("planId")] public required string PlanId { get; init; }
}

internal record MetaData
{
    [JsonPropertyName("totalPages")] public required int TotalPages { get; init; }
}
