namespace Application.Services;

/// <summary>
/// Defines services for generating and managing pivot tables from project data.
/// </summary>
public interface IPivotService
{
    /// <summary>
    /// Generates a standard pivot table from project raw data.
    /// </summary>
    /// <param name="request">The pivot configuration parameters.</param>
    /// <returns>The pivot table result.</returns>
    Task<Result<PivotResultDto>> GetPivotTableAsync(PivotRequest request);

    /// <summary>
    /// Generates an advanced pivot table with support for GCD calculation and repeat detection.
    /// </summary>
    /// <param name="request">The advanced pivot configuration parameters.</param>
    /// <returns>The advanced pivot result with metadata.</returns>
    Task<Result<AdvancedPivotResultDto>> GetAdvancedPivotTableAsync(AdvancedPivotRequest request);

    /// <summary>
    /// Retrieves a list of all unique solution labels present in the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of solution labels.</returns>
    Task<Result<List<string>>> GetSolutionLabelsAsync(Guid projectId);

    /// <summary>
    /// Retrieves a list of all unique elements associated with the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of element symbols.</returns>
    Task<Result<List<string>>> GetElementsAsync(Guid projectId);

    /// <summary>
    /// Detects duplicate rows in the project based on naming patterns.
    /// </summary>
    /// <param name="request">The duplicate detection criteria.</param>
    /// <returns>A list of detected duplicate pairs.</returns>
    Task<Result<List<DuplicateResultDto>>> DetectDuplicatesAsync(DuplicateDetectionRequest request);

    /// <summary>
    /// Calculates statistical summaries (min, max, mean, stdDev) for each column in the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A dictionary of statistics keyed by column name.</returns>
    Task<Result<Dictionary<string, ColumnStatsDto>>> GetColumnStatsAsync(Guid projectId);

    /// <summary>
    /// Exports the content of a pivot table to a CSV byte array.
    /// </summary>
    /// <param name="request">The pivot configuration to export.</param>
    /// <returns>The CSV file content as a byte array.</returns>
    Task<Result<byte[]>> ExportToCsvAsync(PivotRequest request);

    /// <summary>
    /// Analyzes the project data to identify repeating patterns (e.g., sets of samples).
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The analysis result showing repeat structures.</returns>
    Task<Result<RepeatAnalysisDto>> AnalyzeRepeatsAsync(Guid projectId);
}

/// <summary>
/// Represents the result of a repeat pattern analysis.
/// </summary>
/// <param name="HasRepeats">Indicates if any repeating patterns were found.</param>
/// <param name="TotalSamples">Total number of samples analyzed.</param>
/// <param name="SetSizes">Map of solution labels to their repeat set size.</param>
/// <param name="RepeatedElements">Map of solution labels to elements that repeat within them.</param>
/// <param name="ElementCounts">Count of occurrences per element.</param>
public record RepeatAnalysisDto(
    bool HasRepeats,
    int TotalSamples,
    Dictionary<string, int> SetSizes,
    Dictionary<string, List<string>> RepeatedElements,
    Dictionary<string, int> ElementCounts
);