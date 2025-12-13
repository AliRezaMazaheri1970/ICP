namespace Application.Services;

/// <summary>
/// Defines services for analyzing and correcting instrument drift throughout a run.
/// </summary>
public interface IDriftCorrectionService
{
    /// <summary>
    /// Analyzes the dataset to calculate drift statistics without applying corrections.
    /// </summary>
    /// <param name="request">The parameters specifying the drift analysis method and scope.</param>
    /// <returns>The result containing drift statistics and segments.</returns>
    Task<Result<DriftCorrectionResult>> AnalyzeDriftAsync(DriftCorrectionRequest request);

    /// <summary>
    /// Calculates and applies drift correction factors to the project data.
    /// </summary>
    /// <param name="request">The parameters specifying the drift correction options.</param>
    /// <returns>The result containing correction details and corrected data.</returns>
    Task<Result<DriftCorrectionResult>> ApplyDriftCorrectionAsync(DriftCorrectionRequest request);

    /// <summary>
    /// Detects drift segments within the run based on the placement of standards.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="basePattern">Regex pattern to identify base standards.</param>
    /// <param name="conePattern">Regex pattern to identify cone standards.</param>
    /// <returns>A list of detected segments.</returns>
    Task<Result<List<DriftSegment>>> DetectSegmentsAsync(Guid projectId, string? basePattern = null, string? conePattern = null);

    /// <summary>
    /// Calculates the intensity ratios for specified elements across consecutive standards.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="elements">Optional list of elements to include.</param>
    /// <returns>A dictionary of drift ratios keyed by element.</returns>
    Task<Result<Dictionary<string, List<decimal>>>> CalculateDriftRatiosAsync(Guid projectId, List<string>? elements = null);

    /// <summary>
    /// Optimizes the drift slope for a specific element to minimize error or flatten the trend.
    /// </summary>
    /// <param name="request">The optimization parameters.</param>
    /// <returns>The result of the slope optimization.</returns>
    Task<Result<SlopeOptimizationResult>> OptimizeSlopeAsync(SlopeOptimizationRequest request);

    /// <summary>
    /// Resets the drift slope for a specific element to zero (effectively removing drift correction for that element).
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="element">The element to reset.</param>
    /// <returns>The result showing the zeroed slope.</returns>
    Task<Result<SlopeOptimizationResult>> ZeroSlopeAsync(Guid projectId, string element);
}