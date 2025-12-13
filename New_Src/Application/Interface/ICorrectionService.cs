namespace Application.Services;

/// <summary>
/// Defines services for data correction operations like weight, volume, and dilution factor adjustments.
/// </summary>
public interface ICorrectionService
{
    /// <summary>
    /// Identifies samples with weights outside a specified acceptable range.
    /// </summary>
    /// <param name="request">The parameters specifying the weight range and project.</param>
    /// <returns>A list of samples with invalid weights.</returns>
    Task<Result<List<BadSampleDto>>> FindBadWeightsAsync(FindBadWeightsRequest request);

    /// <summary>
    /// Identifies samples with volumes different from the expected value.
    /// </summary>
    /// <param name="request">The parameters specifying the expected volume and project.</param>
    /// <returns>A list of samples with invalid volumes.</returns>
    Task<Result<List<BadSampleDto>>> FindBadVolumesAsync(FindBadVolumesRequest request);

    /// <summary>
    /// Applies a weight correction to specific samples.
    /// Updates corrected concentration based on the ratio of new to old weight.
    /// </summary>
    /// <param name="request">The request details including samples and new weight.</param>
    /// <returns>The result summary of the operation.</returns>
    Task<Result<CorrectionResultDto>> ApplyWeightCorrectionAsync(WeightCorrectionRequest request);

    /// <summary>
    /// Applies a volume correction to specific samples.
    /// Updates corrected concentration based on the ratio of new to old volume.
    /// </summary>
    /// <param name="request">The request details including samples and new volume.</param>
    /// <returns>The result summary of the operation.</returns>
    Task<Result<CorrectionResultDto>> ApplyVolumeCorrectionAsync(VolumeCorrectionRequest request);

    /// <summary>
    /// Applies a dilution factor (DF) correction to specific samples.
    /// Updates corrected concentration based on the ratio of new to old DF.
    /// </summary>
    /// <param name="request">The request details including samples and new DF.</param>
    /// <returns>The result summary of the operation.</returns>
    Task<Result<CorrectionResultDto>> ApplyDfCorrectionAsync(DfCorrectionRequest request);

    /// <summary>
    /// Retrieves a list of all samples with their current dilution factors.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of samples and their DF values.</returns>
    Task<Result<List<DfSampleDto>>> GetDfSamplesAsync(Guid projectId);

    /// <summary>
    /// Applies global or element-specific optimization (Blank subtraction and Scale factor) to the project data.
    /// </summary>
    /// <param name="request">The optimization parameters.</param>
    /// <returns>The result summary of the operation.</returns>
    Task<Result<CorrectionResultDto>> ApplyOptimizationAsync(ApplyOptimizationRequest request);

    /// <summary>
    /// Identifies rows that are potentially empty or statistical outliers based on element averages.
    /// </summary>
    /// <param name="request">The criteria for detecting empty rows.</param>
    /// <returns>A list of flagged rows.</returns>
    Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request);

    /// <summary>
    /// Deletes specified rows from the project.
    /// </summary>
    /// <param name="request">The request identifying samples to delete.</param>
    /// <returns>The count of deleted rows.</returns>
    Task<Result<int>> DeleteRowsAsync(DeleteRowsRequest request);

    /// <summary>
    /// Reverts the most recent correction applied to the project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>True if restoration was successful; otherwise, false.</returns>
    Task<Result<bool>> UndoLastCorrectionAsync(Guid projectId);
}