using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

public interface ICorrectionService
{
    /// <summary>
    /// Find samples with bad weights (outside min/max range)
    /// </summary>
    Task<Result<List<BadSampleDto>>> FindBadWeightsAsync(FindBadWeightsRequest request);

    /// <summary>
    /// Find samples with bad volumes (not matching expected volume)
    /// </summary>
    Task<Result<List<BadSampleDto>>> FindBadVolumesAsync(FindBadVolumesRequest request);

    /// <summary>
    /// Apply weight correction to selected samples
    /// Formula: NewCorrCon = (NewWeight / OldWeight) * OldCorrCon
    /// </summary>
    Task<Result<CorrectionResultDto>> ApplyWeightCorrectionAsync(WeightCorrectionRequest request);

    /// <summary>
    /// Apply volume correction to selected samples
    /// Formula: NewCorrCon = (NewVolume / OldVolume) * OldCorrCon
    /// </summary>
    Task<Result<CorrectionResultDto>> ApplyVolumeCorrectionAsync(VolumeCorrectionRequest request);

    /// <summary>
    /// Apply blank and scale optimization to project data
    /// Formula: CorrectedValue = (OriginalValue + Blank) * Scale
    /// </summary>
    Task<Result<CorrectionResultDto>> ApplyOptimizationAsync(ApplyOptimizationRequest request);

    /// <summary>
    /// Find empty/outlier rows where most elements are below threshold of column average
    /// Based on Python empty_check.py logic
    /// </summary>
    Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request);

    /// <summary>
    /// Undo last correction for a project
    /// </summary>
    Task<Result<bool>> UndoLastCorrectionAsync(Guid projectId);
}