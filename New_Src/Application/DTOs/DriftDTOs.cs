using System.Text.Json.Serialization;

namespace Application.DTOs;

/// <summary>
/// Represents a request for drift correction analysis.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SelectedElements">Optional list of specific elements to correct.</param>
/// <param name="Method">The drift correction algorithm to use.</param>
/// <param name="UseSegmentation">Whether to use segmentation (splitting by standard blocks).</param>
/// <param name="BasePattern">Pattern to identify base standards.</param>
/// <param name="ConePattern">Pattern to identify cone standards.</param>
/// <param name="ChangedBy">The user applying the correction.</param>
public record DriftCorrectionRequest(
    Guid ProjectId,
    List<string>? SelectedElements = null,
    DriftMethod Method = DriftMethod.Linear,
    bool UseSegmentation = true,
    string? BasePattern = null,
    string? ConePattern = null,
    string? ChangedBy = null
);

/// <summary>
/// Defines the available drift correction methods.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriftMethod
{
    None = 0,
    Linear = 1,
    Stepwise = 2,
    Polynomial = 3
}

/// <summary>
/// Represents the result of a drift correction operation.
/// </summary>
/// <param name="TotalSamples">The total number of samples processed.</param>
/// <param name="CorrectedSamples">The count of samples that were corrected.</param>
/// <param name="SegmentsFound">The number of drift segments identified.</param>
/// <param name="Segments">List of detected drift segments.</param>
/// <param name="ElementDrifts">Drift statistics data per element.</param>
/// <param name="CorrectedData">List of samples with applied corrections.</param>
public record DriftCorrectionResult(
    int TotalSamples,
    int CorrectedSamples,
    int SegmentsFound,
    List<DriftSegment> Segments,
    Dictionary<string, ElementDriftInfo> ElementDrifts,
    List<CorrectedSampleDto> CorrectedData
);

/// <summary>
/// Represents a segment of data bracketed by standards.
/// </summary>
/// <param name="SegmentIndex">The sequential index of the segment.</param>
/// <param name="StartIndex">The starting row index.</param>
/// <param name="EndIndex">The ending row index.</param>
/// <param name="StartStandard">The label of the standard at the start of the segment.</param>
/// <param name="EndStandard">The label of the standard at the end of the segment.</param>
/// <param name="SampleCount">The number of samples in this segment.</param>
public record DriftSegment(
    int SegmentIndex,
    int StartIndex,
    int EndIndex,
    string? StartStandard,
    string? EndStandard,
    int SampleCount
);

/// <summary>
/// Represents drift characteristics for a single element.
/// </summary>
/// <param name="Element">The element symbol.</param>
/// <param name="InitialRatio">The ratio at the start of the drift.</param>
/// <param name="FinalRatio">The ratio at the end of the drift.</param>
/// <param name="DriftPercent">The total percentage of drift.</param>
/// <param name="Slope">The slope of the drift line.</param>
/// <param name="Intercept">The intercept of the drift line.</param>
public record ElementDriftInfo(
    string Element,
    decimal InitialRatio,
    decimal FinalRatio,
    decimal DriftPercent,
    decimal Slope,
    decimal Intercept
);

/// <summary>
/// Represents a single sample with both original and corrected values.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="GroupId">The detection group identifier.</param>
/// <param name="OriginalIndex">The original row index.</param>
/// <param name="SegmentIndex">The segment index this sample belongs to.</param>
/// <param name="OriginalValues">The original values.</param>
/// <param name="CorrectedValues">The values after correction.</param>
/// <param name="CorrectionFactors">The factors applied to each element.</param>
public record CorrectedSampleDto(
    string SolutionLabel,
    int GroupId,
    int OriginalIndex,
    int SegmentIndex,
    Dictionary<string, decimal?> OriginalValues,
    Dictionary<string, decimal?> CorrectedValues,
    Dictionary<string, decimal> CorrectionFactors
);

/// <summary>
/// Represents a request to optimize drift slope for an element.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Element">The element to optimize.</param>
/// <param name="Action">The optimization action to take.</param>
/// <param name="TargetSlope">Optional target slope if overriding.</param>
public record SlopeOptimizationRequest(
    Guid ProjectId,
    string Element,
    SlopeAction Action,
    decimal? TargetSlope = null
);

/// <summary>
/// Defines actions available for slope optimization.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlopeAction
{
    ZeroSlope = 0,
    RotateUp = 1,
    RotateDown = 2,
    SetCustom = 3
}

/// <summary>
/// Represents the result of a slope optimization.
/// </summary>
/// <param name="Element">The element optimized.</param>
/// <param name="OriginalSlope">The slope before optimization.</param>
/// <param name="NewSlope">The new slope applied.</param>
/// <param name="OriginalIntercept">The intercept before optimization.</param>
/// <param name="NewIntercept">The new intercept applied.</param>
/// <param name="CorrectedData">Data re-calculated with the new slope.</param>
public record SlopeOptimizationResult(
    string Element,
    decimal OriginalSlope,
    decimal NewSlope,
    decimal OriginalIntercept,
    decimal NewIntercept,
    List<CorrectedSampleDto> CorrectedData
);