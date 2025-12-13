namespace Application.Services;

/// <summary>
/// Defines services for optimizing data correction parameters (Blank &amp; Scale) using evolutionary algorithms.
/// </summary>
public interface IOptimizationService
{
    /// <summary>
    /// Optimizes blank and scale values to maximize the pass rate of RM checks using Differential Evolution.
    /// </summary>
    /// <param name="request">The parameters for the optimization process.</param>
    /// <returns>The result of the optimization including optimal parameters per element.</returns>
    Task<Result<BlankScaleOptimizationResult>> OptimizeBlankScaleAsync(BlankScaleOptimizationRequest request);

    /// <summary>
    /// Applies manually specified blank and scale values to the project data.
    /// </summary>
    /// <param name="request">The manual adjustment parameters.</param>
    /// <returns>The result of applying the values.</returns>
    Task<Result<ManualBlankScaleResult>> ApplyManualBlankScaleAsync(ManualBlankScaleRequest request);

    /// <summary>
    /// Previews the effect of blank and scale adjustments without persisting the changes.
    /// </summary>
    /// <param name="request">The parameters to preview.</param>
    /// <returns>The preview result.</returns>
    Task<Result<ManualBlankScaleResult>> PreviewBlankScaleAsync(ManualBlankScaleRequest request);

    /// <summary>
    /// Retrieves current pass/fail statistics for CRM comparisons based on current data.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="minDiff">The minimum acceptable difference percentage.</param>
    /// <param name="maxDiff">The maximum acceptable difference percentage.</param>
    /// <returns>The current statistics.</returns>
    Task<Result<BlankScaleOptimizationResult>> GetCurrentStatisticsAsync(Guid projectId, decimal minDiff = -10m, decimal maxDiff = 10m);

    /// <summary>
    /// Retrieves a subset of sample data for debugging purposes.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A generic object containing debug data.</returns>
    Task<object> GetDebugSamplesAsync(Guid projectId);
}