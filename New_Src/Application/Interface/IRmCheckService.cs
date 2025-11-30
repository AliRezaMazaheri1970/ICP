using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Service for Reference Material (RM) checking. 
/// Equivalent to rm_check.py in Python code. 
/// </summary>
public interface IRmCheckService
{
    /// <summary>
    /// Check all RM samples in project against CRM reference values
    /// </summary>
    Task<Result<RmCheckSummaryDto>> CheckRmAsync(RmCheckRequest request);

    /// <summary>
    /// Get list of detected RM samples in project
    /// </summary>
    Task<Result<List<string>>> GetRmSamplesAsync(Guid projectId, List<string>? patterns = null);

    /// <summary>
    /// Check weight and volume values
    /// </summary>
    Task<Result<WeightVolumeCheckSummaryDto>> CheckWeightVolumeAsync(WeightVolumeCheckRequest request);

    /// <summary>
    /// Get samples with weight/volume issues
    /// </summary>
    Task<Result<List<WeightVolumeCheckResultDto>>> GetWeightVolumeIssuesAsync(Guid projectId);
}