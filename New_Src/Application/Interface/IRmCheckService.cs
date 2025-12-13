namespace Application.Services;

/// <summary>
/// Defines services for validating samples against Reference Materials (RM).
/// </summary>
public interface IRmCheckService
{
    /// <summary>
    /// Performs a full RM validation check on all RM samples in the project.
    /// </summary>
    /// <param name="request">The check parameters such as tolerances.</param>
    /// <returns>A summary of the RM check results.</returns>
    Task<Result<RmCheckSummaryDto>> CheckRmAsync(RmCheckRequest request);

    /// <summary>
    /// Retrieves a list of solution labels identified as reference materials in the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="patterns">Optional patterns to override default RM detection.</param>
    /// <returns>A list of identified RM solution labels.</returns>
    Task<Result<List<string>>> GetRmSamplesAsync(Guid projectId, List<string>? patterns = null);

    /// <summary>
    /// Validates the weight and volume of samples against expected values.
    /// </summary>
    /// <param name="request">The validation parameters.</param>
    /// <returns>A summary of the weight and volume checks.</returns>
    Task<Result<WeightVolumeCheckSummaryDto>> CheckWeightVolumeAsync(WeightVolumeCheckRequest request);

    /// <summary>
    /// Retrieves a list of specific samples that failed weight or volume checks.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of problematic samples.</returns>
    Task<Result<List<WeightVolumeCheckResultDto>>> GetWeightVolumeIssuesAsync(Guid projectId);
}