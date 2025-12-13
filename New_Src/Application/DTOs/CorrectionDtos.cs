using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Application.DTOs;

#region Request DTOs

/// <summary>
/// Represents a request to identify samples with weights outside an expected range.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="WeightMin">The minimum acceptable weight.</param>
/// <param name="WeightMax">The maximum acceptable weight.</param>
public record FindBadWeightsRequest(
    Guid ProjectId,
    decimal WeightMin = 0.09m,
    decimal WeightMax = 0.11m
);

/// <summary>
/// Represents a request to identify samples with volumes differing from the expected value.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ExpectedVolume">The expected volume value.</param>
public record FindBadVolumesRequest(
    Guid ProjectId,
    decimal ExpectedVolume = 10m
);

/// <summary>
/// Represents a request to find empty or outlier rows based on element averages.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ThresholdPercent">The percentage threshold for outlier detection.</param>
/// <param name="ElementsToCheck">Optional list of specific elements to check.</param>
/// <param name="RequireAllElements">Whether all checked elements must be below threshold to flag the row.</param>
public record FindEmptyRowsRequest(
    Guid ProjectId,
    decimal ThresholdPercent = 70m,
    List<string>? ElementsToCheck = null,
    bool RequireAllElements = true
);

/// <summary>
/// Represents a request to apply a weight correction to specific samples.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SolutionLabels">The list of sample labels to correct.</param>
/// <param name="NewWeight">The new weight value to apply.</param>
/// <param name="ChangedBy">The user applying the change.</param>
public record WeightCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewWeight,
    string? ChangedBy = null
);

/// <summary>
/// Represents a request to apply a volume correction to specific samples.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SolutionLabels">The list of sample labels to correct.</param>
/// <param name="NewVolume">The new volume value to apply.</param>
/// <param name="ChangedBy">The user applying the change.</param>
public record VolumeCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewVolume,
    string? ChangedBy = null
);

/// <summary>
/// Represents a request to apply a dilution factor (DF) correction.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SolutionLabels">The list of sample labels to correct.</param>
/// <param name="NewDf">The new dilution factor.</param>
/// <param name="ChangedBy">The user applying the change.</param>
public record DfCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewDf,
    string? ChangedBy = null
);

/// <summary>
/// Represents a request to delete specific rows.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SolutionLabels">The list of sample labels to delete.</param>
/// <param name="ChangedBy">The user performing the deletion.</param>
public record DeleteRowsRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    string? ChangedBy = null
);

/// <summary>
/// Represents a request to apply optimization settings (Blank and Scale).
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ElementSettings">The optimization settings per element.</param>
/// <param name="ChangedBy">The user applying the optimization.</param>
public record ApplyOptimizationRequest(
    Guid ProjectId,
    Dictionary<string, ElementSettings> ElementSettings,
    string? ChangedBy = null
);

/// <summary>
/// Defines the input parameter for blank and scale settings.
/// </summary>
/// <param name="Blank">The blank value adjustment.</param>
/// <param name="Scale">The scale factor adjustment.</param>
public record ElementSettings(
    decimal Blank,
    decimal Scale
);

#endregion

#region Response DTOs

/// <summary>
/// Represents information about a sample's dilution factor.
/// </summary>
/// <param name="RowNumber">The row index.</param>
/// <param name="SolutionLabel">The solution label.</param>
/// <param name="CurrentDf">The current dilution factor.</param>
/// <param name="SampleType">The type of the sample.</param>
public record DfSampleDto(
    int RowNumber,
    string SolutionLabel,
    decimal CurrentDf,
    string? SampleType
);

/// <summary>
/// Represents information about a sample flagged for bad weight or volume.
/// </summary>
/// <param name="SolutionLabel">The solution label.</param>
/// <param name="ActualValue">The actual recorded value.</param>
/// <param name="CorrCon">The corrected concentration.</param>
/// <param name="ExpectedValue">The expected value.</param>
/// <param name="Deviation">The deviation from the expected value.</param>
public record BadSampleDto(
    string SolutionLabel,
    decimal ActualValue,
    decimal CorrCon,
    decimal ExpectedValue,
    decimal Deviation
);

/// <summary>
/// Represents a row identified as potentially empty or an outlier.
/// </summary>
/// <param name="SolutionLabel">The solution label.</param>
/// <param name="ElementValues">The raw element values.</param>
/// <param name="ElementAverages">The computed average values per element.</param>
/// <param name="PercentOfAverage">The value as a percentage of the average.</param>
/// <param name="ElementsBelowThreshold">Count of elements below the threshold.</param>
/// <param name="TotalElementsChecked">Total count of elements checked.</param>
/// <param name="OverallScore">The calculated outlier score.</param>
public record EmptyRowDto(
    string SolutionLabel,
    Dictionary<string, decimal?> ElementValues,
    Dictionary<string, decimal> ElementAverages,
    Dictionary<string, decimal> PercentOfAverage,
    int ElementsBelowThreshold,
    int TotalElementsChecked,
    decimal OverallScore
)
{
    /// <summary>
    /// Gets a safe identifier string suitable for use in HTML IDs.
    /// </summary>
    [JsonIgnore]
    public string SafeId => Regex.Replace(SolutionLabel ?? Guid.NewGuid().ToString(), @"[^a-zA-Z0-9-_]", "_");
}

/// <summary>
/// Represents the result of a correction operation.
/// </summary>
/// <param name="TotalRows">The total number of rows processed.</param>
/// <param name="CorrectedRows">The number of rows actually corrected.</param>
/// <param name="CorrectedSamples">The list of detailed correction info per sample.</param>
public record CorrectionResultDto(
    int TotalRows,
    int CorrectedRows,
    List<CorrectedSampleInfo> CorrectedSamples
);

/// <summary>
/// Provides details about a single corrected sample.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="OldValue">The value before correction.</param>
/// <param name="NewValue">The value after correction.</param>
/// <param name="OldCorrCon">The corrected concentration before change.</param>
/// <param name="NewCorrCon">The corrected concentration after change.</param>
public record CorrectedSampleInfo(
    string SolutionLabel,
    decimal OldValue,
    decimal NewValue,
    decimal OldCorrCon,
    decimal NewCorrCon
);

#endregion