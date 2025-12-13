namespace Application.DTOs;

/// <summary>
/// Represents a request for a Reference Material (RM) check.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="MinDiffPercent">The minimum difference percentage allowed.</param>
/// <param name="MaxDiffPercent">The maximum difference percentage allowed.</param>
/// <param name="RmPatterns">Optional list of patterns to match RM.</param>
/// <param name="AnalysisMethod">Optional analysis method.</param>
public record RmCheckRequest(
    Guid ProjectId,
    decimal MinDiffPercent = -12m,
    decimal MaxDiffPercent = 12m,
    List<string>? RmPatterns = null,
    string? AnalysisMethod = null
);

/// <summary>
/// Represents the result of an RM check for a single sample.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="MatchedRmId">The ID of the matched Reference Material.</param>
/// <param name="AnalysisMethod">The analysis method.</param>
/// <param name="Status">The overall check status.</param>
/// <param name="ElementChecks">List of checks per element.</param>
/// <param name="PassCount">Number of elements passed.</param>
/// <param name="FailCount">Number of elements failed.</param>
/// <param name="TotalCount">Total number of elements checked.</param>
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
/// Represents an RM check result for a single element.
/// </summary>
/// <param name="Element">The element identifier.</param>
/// <param name="SampleValue">The measured value.</param>
/// <param name="ReferenceValue">The reference value.</param>
/// <param name="DiffPercent">The difference percentage.</param>
/// <param name="Status">The status (Pass/Fail/etc.).</param>
/// <param name="Message">Optional status message.</param>
public record RmElementCheckDto(
    string Element,
    decimal? SampleValue,
    decimal? ReferenceValue,
    decimal? DiffPercent,
    RmCheckStatus Status,
    string? Message
);

/// <summary>
/// Defines status codes for RM checks.
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
/// Represents the summary of RM checks for an entire project.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="TotalRmSamples">Total RM samples checked.</param>
/// <param name="PassedSamples">Number of samples passing all checks.</param>
/// <param name="FailedSamples">Number of samples failing at least one check.</param>
/// <param name="WarningSamples">Number of samples with warnings.</param>
/// <param name="Results">List of individual sample results.</param>
/// <param name="ElementSummary">Summary of checks aggregated by element.</param>
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
/// Represents summary statistics for a single element across all RM samples.
/// </summary>
/// <param name="Element">The element identifier.</param>
/// <param name="TotalChecks">Total number of checks performed.</param>
/// <param name="PassCount">Number of checks passed.</param>
/// <param name="FailCount">Number of checks failed.</param>
/// <param name="AverageDiff">Average difference observed.</param>
/// <param name="MaxDiff">Maximum difference observed.</param>
/// <param name="MinDiff">Minimum difference observed.</param>
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
/// Represents a request for weight and volume checking.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="MinWeight">Minimum valid weight.</param>
/// <param name="MaxWeight">Maximum valid weight.</param>
/// <param name="MinVolume">Minimum valid volume.</param>
/// <param name="MaxVolume">Maximum valid volume.</param>
/// <param name="ExpectedWeight">Target expected weight.</param>
/// <param name="ExpectedVolume">Target expected volume.</param>
/// <param name="TolerancePercent">Allowed deviation percentage.</param>
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
/// Represents the result of a weight and volume check for a sample.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="Weight">Measured weight.</param>
/// <param name="Volume">Measured volume.</param>
/// <param name="WeightStatus">Status of weight check.</param>
/// <param name="VolumeStatus">Status of volume check.</param>
/// <param name="WeightMessage">Message regarding weight check.</param>
/// <param name="VolumeMessage">Message regarding volume check.</param>
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
/// Defines status codes for weight and volume checks.
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
/// Represents the summary of weight and volume checks for a project.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="TotalSamples">Total samples checked.</param>
/// <param name="WeightOkCount">Count of samples with valid weight.</param>
/// <param name="WeightErrorCount">Count of samples with invalid weight.</param>
/// <param name="VolumeOkCount">Count of samples with valid volume.</param>
/// <param name="VolumeErrorCount">Count of samples with invalid volume.</param>
/// <param name="Results">List of validation results per sample.</param>
public record WeightVolumeCheckSummaryDto(
    Guid ProjectId,
    int TotalSamples,
    int WeightOkCount,
    int WeightErrorCount,
    int VolumeOkCount,
    int VolumeErrorCount,
    List<WeightVolumeCheckResultDto> Results
);