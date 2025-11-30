namespace Application.DTOs;

/// <summary>
/// DTO for listing CRM records
/// </summary>
public record CrmListItemDto(
    int Id,
    string CrmId,
    string? AnalysisMethod,
    string? Type,
    bool IsOurOreas,
    Dictionary<string, decimal> Elements
);

/// <summary>
/// DTO for CRM diff calculation result (comparing project data with CRM values)
/// </summary>
public record CrmDiffResultDto(
    string SolutionLabel,
    string CrmId,
    string AnalysisMethod,
    List<ElementDiffDto> Differences
);

/// <summary>
/// Individual element difference
/// </summary>
public record ElementDiffDto(
    string Element,
    decimal?  ProjectValue,
    decimal? CrmValue,
    decimal? DiffPercent,
    bool IsInRange
);

/// <summary>
/// Request for calculating CRM differences
/// </summary>
public record CrmDiffRequest(
    Guid ProjectId,
    decimal MinDiffPercent = -12m,
    decimal MaxDiffPercent = 12m,
    List<string>? CrmPatterns = null  // e.g., ["258", "252", "906"]
);

/// <summary>
/// Request for adding/updating CRM data
/// </summary>
public record CrmUpsertRequest(
    string CrmId,
    string?  AnalysisMethod,
    string? Type,
    Dictionary<string, decimal> Elements,
    bool IsOurOreas = false
);