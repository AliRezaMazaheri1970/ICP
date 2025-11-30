namespace Application.DTOs;

/// <summary>
/// Request for RM (Reference Material) check
/// </summary>
public record RmCheckRequest(
    Guid ProjectId,
    decimal MinDiffPercent = -12m,
    decimal MaxDiffPercent = 12m,
    List<string>? RmPatterns = null,
    string? AnalysisMethod = null
);

/// <summary>
/// Result of RM check for a single sample
/// </summary>
public record RmCheckResultDto(
    string SolutionLabel,
    string MatchedRmId,
    string AnalysisMethod,
    RmCheckStatus Status,
    List<RmElementCheckDto> ElementChecks,
    int PassCount,
    int FailCount,
    int TotalCount
);

/// <summary>
/// Element-level check result
/// </summary>
public record RmElementCheckDto(
    string Element,
    decimal? SampleValue,
    decimal? ReferenceValue,
    decimal? DiffPercent,
    RmCheckStatus Status,
    string? Message
);

/// <summary>
/// Status of RM check
/// </summary>
public enum RmCheckStatus
{
    Pass,
    Fail,
    Warning,
    NoReference,
    Skipped
}

/// <summary>
/// Summary of RM check for entire project
/// </summary>
public record RmCheckSummaryDto(
    Guid ProjectId,
    int TotalRmSamples,
    int PassedSamples,
    int FailedSamples,
    int WarningSamples,
    List<RmCheckResultDto> Results,
    Dictionary<string, RmElementSummaryDto> ElementSummary
);

/// <summary>
/// Summary for a single element across all RM samples
/// </summary>
public record RmElementSummaryDto(
    string Element,
    int TotalChecks,
    int PassCount,
    int FailCount,
    decimal? AverageDiff,
    decimal? MaxDiff,
    decimal? MinDiff
);

/// <summary>
/// Weight/Volume check request
/// </summary>
public record WeightVolumeCheckRequest(
    Guid ProjectId,
    decimal? MinWeight = null,
    decimal? MaxWeight = null,
    decimal? MinVolume = null,
    decimal? MaxVolume = null,
    decimal? ExpectedWeight = 0.25m,
    decimal? ExpectedVolume = 50m,
    decimal TolerancePercent = 10m
);

/// <summary>
/// Weight/Volume check result
/// </summary>
public record WeightVolumeCheckResultDto(
    string SolutionLabel,
    decimal? Weight,
    decimal? Volume,
    WeightVolumeStatus WeightStatus,
    WeightVolumeStatus VolumeStatus,
    string? WeightMessage,
    string? VolumeMessage
);

/// <summary>
/// Status for weight/volume
/// </summary>
public enum WeightVolumeStatus
{
    Ok,
    TooLow,
    TooHigh,
    Missing,
    Skipped
}

/// <summary>
/// Summary of weight/volume check
/// </summary>
public record WeightVolumeCheckSummaryDto(
    Guid ProjectId,
    int TotalSamples,
    int WeightOkCount,
    int WeightErrorCount,
    int VolumeOkCount,
    int VolumeErrorCount,
    List<WeightVolumeCheckResultDto> Results
);