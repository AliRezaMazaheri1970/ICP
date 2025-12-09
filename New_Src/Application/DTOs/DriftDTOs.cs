using System.Text.Json.Serialization;

namespace Application.DTOs;

/// <summary>
/// Request for drift correction analysis
/// </summary>
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
/// Drift correction method
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
/// Result of drift correction
/// </summary>
public record DriftCorrectionResult(
    int TotalSamples,
    int CorrectedSamples,
    int SegmentsFound,
    List<DriftSegment> Segments,
    Dictionary<string, ElementDriftInfo> ElementDrifts,
    List<CorrectedSampleDto> CorrectedData
);

/// <summary>
/// A segment between two standards
/// </summary>
public record DriftSegment(
    int SegmentIndex,
    int StartIndex,
    int EndIndex,
    string? StartStandard,
    string? EndStandard,
    int SampleCount
);

/// <summary>
/// Drift information for an element
/// </summary>
public record ElementDriftInfo(
    string Element,
    decimal InitialRatio,
    decimal FinalRatio,
    decimal DriftPercent,
    decimal Slope,
    decimal Intercept
);

/// <summary>
/// Corrected sample data
/// </summary>
public record CorrectedSampleDto(
    string SolutionLabel,
    int OriginalIndex,
    int SegmentIndex,
    Dictionary<string, decimal?> OriginalValues,
    Dictionary<string, decimal?> CorrectedValues,
    Dictionary<string, decimal> CorrectionFactors
);

/// <summary>
/// Request for slope optimization
/// </summary>
public record SlopeOptimizationRequest(
    Guid ProjectId,
    string Element,
    SlopeAction Action,
    decimal? TargetSlope = null
);

/// <summary>
/// Slope optimization actions
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
/// Result of slope optimization
/// </summary>
public record SlopeOptimizationResult(
    string Element,
    decimal OriginalSlope,
    decimal NewSlope,
    decimal OriginalIntercept,
    decimal NewIntercept,
    List<CorrectedSampleDto> CorrectedData
);