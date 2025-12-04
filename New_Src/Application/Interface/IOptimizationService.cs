using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Service for Blank & Scale optimization using evolutionary algorithms
/// </summary>
public interface IOptimizationService
{
    /// <summary>
    /// Optimize Blank and Scale values to maximize pass rate
    /// Uses Differential Evolution algorithm
    /// </summary>
    Task<Result<BlankScaleOptimizationResult>> OptimizeBlankScaleAsync(BlankScaleOptimizationRequest request);

    /// <summary>
    /// Apply manual Blank and Scale values
    /// </summary>
    Task<Result<ManualBlankScaleResult>> ApplyManualBlankScaleAsync(ManualBlankScaleRequest request);

    /// <summary>
    /// Preview the effect of Blank and Scale values without saving
    /// </summary>
    Task<Result<ManualBlankScaleResult>> PreviewBlankScaleAsync(ManualBlankScaleRequest request);

    /// <summary>
    /// Get current pass/fail statistics for CRM comparison
    /// </summary>
    Task<Result<BlankScaleOptimizationResult>> GetCurrentStatisticsAsync(Guid projectId, decimal minDiff = -10m, decimal maxDiff = 10m);

    /// <summary>
    /// Debug: Get sample labels from project for troubleshooting
    /// </summary>
    Task<object> GetDebugSamplesAsync(Guid projectId);
}