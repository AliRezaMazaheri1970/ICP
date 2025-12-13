namespace Application.DTOs;

/// <summary>
/// Represents a request for an advanced pivot table with support for GCD and repeat detection.
/// </summary>
/// <param name="ProjectId">The identifier of the project to query.</param>
/// <param name="SearchText">Optional text to filter the results.</param>
/// <param name="SelectedSolutionLabels">Optional list of solution labels to include.</param>
/// <param name="SelectedElements">Optional list of elements to include.</param>
/// <param name="NumberFilters">Optional dictionary of number filters per column.</param>
/// <param name="UseOxide">Indicates whether to convert values to oxides.</param>
/// <param name="UseInt">Indicates whether to use intensity values instead of corrected concentrations.</param>
/// <param name="DecimalPlaces">The number of decimal places for formatting.</param>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The size of each page.</param>
/// <param name="Aggregation">The aggregation function to apply (default is First).</param>
/// <param name="MergeRepeats">Indicates whether to merge repeated samples (average) or treat them separately.</param>
public record AdvancedPivotRequest(
    Guid ProjectId,
    string? SearchText = null,
    List<string>? SelectedSolutionLabels = null,
    List<string>? SelectedElements = null,
    Dictionary<string, NumberFilter>? NumberFilters = null,
    bool UseOxide = false,
    bool UseInt = false,
    int DecimalPlaces = 2,
    int Page = 1,
    int PageSize = 100,
    PivotAggregation Aggregation = PivotAggregation.First,
    bool MergeRepeats = false
);

/// <summary>
/// Defines the aggregation functions available for pivot tables.
/// </summary>
public enum PivotAggregation
{
    First,
    Last,
    Mean,
    Sum,
    Min,
    Max,
    Count
}

/// <summary>
/// Represents the result of an advanced pivot table query.
/// </summary>
/// <param name="Columns">The list of column headers.</param>
/// <param name="Rows">The list of data rows.</param>
/// <param name="TotalCount">The total number of rows matching the query.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The current page size.</param>
/// <param name="Metadata">Additional metadata including repeat stats.</param>
public record AdvancedPivotResultDto(
    List<string> Columns,
    List<AdvancedPivotRowDto> Rows,
    int TotalCount,
    int Page,
    int PageSize,
    AdvancedPivotMetadataDto Metadata
);

/// <summary>
/// Represents a single row in the advanced pivot table.
/// </summary>
/// <param name="SolutionLabel">The solution label for this row.</param>
/// <param name="Values">The dictionary of values keyed by column name.</param>
/// <param name="OriginalIndex">The original index in the source data.</param>
/// <param name="SetIndex">The repeat set index (0 for original, >0 for repeats).</param>
/// <param name="SetSize">The total number of samples in this repeat set.</param>
public record AdvancedPivotRowDto(
    string SolutionLabel,
    Dictionary<string, decimal?> Values,
    int OriginalIndex,
    int SetIndex,
    int SetSize
);

/// <summary>
/// Contains metadata about the advanced pivot table, including repeat usage.
/// </summary>
/// <param name="AllSolutionLabels">A complete list of solution labels available.</param>
/// <param name="AllElements">A complete list of elements available.</param>
/// <param name="ColumnStats">Statistical summaries for columns.</param>
/// <param name="HasRepeats">Indicates if any repeats were detected.</param>
/// <param name="SetSizes">A map of solution labels to their repeat set sizes.</param>
/// <param name="RepeatedElements">A map defining which elements are repeated within sets.</param>
public record AdvancedPivotMetadataDto(
    List<string> AllSolutionLabels,
    List<string> AllElements,
    Dictionary<string, ColumnStatsDto> ColumnStats,
    bool HasRepeats,
    Dictionary<string, int> SetSizes,
    Dictionary<string, List<string>> RepeatedElements
);