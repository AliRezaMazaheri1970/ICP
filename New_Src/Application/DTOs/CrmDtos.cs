namespace Application.DTOs;

/// <summary>
/// Represents a CRM list item data transfer object.
/// </summary>
/// <param name="Id">The unique, internal identifier of the CRM.</param>
/// <param name="CrmId">The official CRM identifier (e.g. OREAS #).</param>
/// <param name="AnalysisMethod">The analysis method used.</param>
/// <param name="Type">The type category of the CRM.</param>
/// <param name="IsOurOreas">Indicates if this is a frequently used CRM.</param>
/// <param name="Elements">The dictionary of element concentrations.</param>
public record CrmListItemDto(
    int Id,
    string CrmId,
    string? AnalysisMethod,
    string? Type,
    bool IsOurOreas,
    Dictionary<string, decimal> Elements
);

/// <summary>
/// Represents the result of a comparison between project data and a CRM.
/// </summary>
/// <param name="SolutionLabel">The sample solution label.</param>
/// <param name="CrmId">The ID of the CRM being compared against.</param>
/// <param name="AnalysisMethod">The analysis method of the CRM.</param>
/// <param name="Differences">List of differences per element.</param>
public record CrmDiffResultDto(
    string SolutionLabel,
    string CrmId,
    string AnalysisMethod,
    List<ElementDiffDto> Differences
);

/// <summary>
/// Represents the difference calculation for a single element.
/// </summary>
/// <param name="Element">The element symbol.</param>
/// <param name="ProjectValue">The value measured in the project.</param>
/// <param name="CrmValue">The certified value from the CRM.</param>
/// <param name="DiffPercent">The difference percentage.</param>
/// <param name="IsInRange">Indicates if the difference is within acceptable limits.</param>
public record ElementDiffDto(
    string Element,
    decimal? ProjectValue,
    decimal? CrmValue,
    decimal? DiffPercent,
    bool IsInRange
);

/// <summary>
/// Represents a request to calculate CRM differences.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="MinDiffPercent">The lower bound for acceptable difference percentage.</param>
/// <param name="MaxDiffPercent">The upper bound for acceptable difference percentage.</param>
/// <param name="CrmPatterns">Optional list of patterns to match CRM IDs.</param>
public record CrmDiffRequest(
    Guid ProjectId,
    decimal MinDiffPercent = -12m,
    decimal MaxDiffPercent = 12m,
    List<string>? CrmPatterns = null
);

/// <summary>
/// Represents a request to create or update a CRM record.
/// </summary>
/// <param name="CrmId">The official CRM identifier.</param>
/// <param name="AnalysisMethod">The analysis method.</param>
/// <param name="Type">The CRM category.</param>
/// <param name="Elements">The element concentrations.</param>
/// <param name="IsOurOreas">Flag for frequently used CRMs.</param>
public record CrmUpsertRequest(
    string CrmId,
    string? AnalysisMethod,
    string? Type,
    Dictionary<string, decimal> Elements,
    bool IsOurOreas = false
);

/// <summary>
/// Represents a generic paginated result.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <param name="Items">The list of items on the current page.</param>
/// <param name="TotalCount">The total number of items available.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The size of the page.</param>
public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);