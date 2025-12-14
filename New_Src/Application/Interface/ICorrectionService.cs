using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Defines the contract for services that perform data correctness checks and apply modifications
/// such as weight, volume, dilution factor corrections, and optimizations.
/// </summary>
public interface ICorrectionService
{
    /// <summary>
    /// Scans the project data to identify samples where the recorded weight falls outside the specified acceptable range.
    /// </summary>
    /// <param name="request">A request object containing the project ID and weight thresholds.</param>
    /// <returns>A result containing a list of <see cref="BadSampleDto"/> items representing invalid samples.</returns>
    Task<Result<List<BadSampleDto>>> FindBadWeightsAsync(FindBadWeightsRequest request);

    /// <summary>
    /// Scans the project data to identify samples where the recorded volume deviates from the expected standard value.
    /// </summary>
    /// <param name="request">A request object containing the project ID and expected volume.</param>
    /// <returns>A result containing a list of <see cref="BadSampleDto"/> items representing invalid samples.</returns>
    Task<Result<List<BadSampleDto>>> FindBadVolumesAsync(FindBadVolumesRequest request);

    /// <summary>
    /// Applies corrections to the recorded weight for a specified list of samples and recalculates their concentrations.
    /// </summary>
    /// <param name="request">A request object specifying the samples to update and the new weight value.</param>
    /// <returns>A result summary detailing the number of rows affected and correction specifics.</returns>
    Task<Result<CorrectionResultDto>> ApplyWeightCorrectionAsync(WeightCorrectionRequest request);

    /// <summary>
    /// Applies corrections to the recorded volume for a specified list of samples and recalculates their concentrations.
    /// </summary>
    /// <param name="request">A request object specifying the samples to update and the new volume value.</param>
    /// <returns>A result summary detailing the number of rows affected and correction specifics.</returns>
    Task<Result<CorrectionResultDto>> ApplyVolumeCorrectionAsync(VolumeCorrectionRequest request);

    /// <summary>
    /// Updates the dilution factor (DF) for a specified list of samples and adjusts the corrected concentrations accordingly.
    /// </summary>
    /// <param name="request">A request object specifying the samples to update and the new dilution factor.</param>
    /// <returns>A result summary detailing the number of rows affected and correction specifics.</returns>
    Task<Result<CorrectionResultDto>> ApplyDfCorrectionAsync(DfCorrectionRequest request);

    /// <summary>
    /// Retrieves a list of all samples in a project along with their currently assigned dilution factors.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <returns>A result containing a list of <see cref="DfSampleDto"/> items.</returns>
    Task<Result<List<DfSampleDto>>> GetDfSamplesAsync(Guid projectId);

    /// <summary>
    /// Applies specified optimization settings (blank subtraction and scaling) to the project data.
    /// </summary>
    /// <param name="request">A request object containing the per-element optimization parameters.</param>
    /// <returns>A result summary detailing the operations performed.</returns>
    Task<Result<CorrectionResultDto>> ApplyOptimizationAsync(ApplyOptimizationRequest request);

    /// <summary>
    /// Analyzes the project data to identify rows that appear to be empty or contain statistical outliers.
    /// </summary>
    /// <param name="request">A request object defining the threshold criteria for detection.</param>
    /// <returns>A result containing a list of <see cref="EmptyRowDto"/> identifying the flagged rows.</returns>
    Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request);

    /// <summary>
    /// Permanently removes the specified sample rows from the project.
    /// </summary>
    /// <param name="request">A request object containing the list of solution labels to delete.</param>
    /// <returns>A result containing the count of successfully deleted rows.</returns>
    Task<Result<int>> DeleteRowsAsync(DeleteRowsRequest request);

    /// <summary>
    /// Reverts the most recently applied correction or bulk modification action for the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <returns>A result indicating true if the undo operation was successful.</returns>
    Task<Result<bool>> UndoLastCorrectionAsync(Guid projectId);
}