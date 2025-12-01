namespace Application.DTOs;

/// <summary>
/// Request for weight correction
/// </summary>
public record WeightCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewWeight,
    decimal? WeightMin = 0.190m,
    decimal? WeightMax = 0.210m,
    string? ChangedBy = null  // اضافه شد
);

/// <summary>
/// Request for volume correction
/// </summary>
public record VolumeCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewVolume,
    decimal? ExpectedVolume = 50m,
    string? ChangedBy = null  // اضافه شد
);

/// <summary>
/// Request to apply blank and scale optimization results
/// </summary>
public record ApplyOptimizationRequest(
    Guid ProjectId,
    Dictionary<string, BlankScaleSettings> ElementSettings,
    string? ChangedBy = null  // اضافه شد
);

/// <summary>
/// Element-specific blank and scale settings for correction
/// </summary>
public record BlankScaleSettings(
    decimal Blank,
    decimal Scale
);

/// <summary>
/// Result of correction operation
/// </summary>
public record CorrectionResultDto(
    int TotalRows,
    int CorrectedRows,
    List<CorrectedSampleInfo> Samples
);

/// <summary>
/// Information about a corrected sample
/// </summary>
public record CorrectedSampleInfo(
    string SolutionLabel,
    decimal OldValue,
    decimal NewValue,
    decimal OldCorrCon,
    decimal NewCorrCon
);

/// <summary>
/// Request to find bad weights
/// </summary>
public record FindBadWeightsRequest(
    Guid ProjectId,
    decimal WeightMin = 0.190m,
    decimal WeightMax = 0.210m
);

/// <summary>
/// Request to find bad volumes
/// </summary>
public record FindBadVolumesRequest(
    Guid ProjectId,
    decimal ExpectedVolume = 50m
);

/// <summary>
/// Bad weight/volume sample info
/// </summary>
public record BadSampleDto(
    string SolutionLabel,
    decimal ActualValue,
    decimal CorrCon,
    decimal ExpectedValue,
    decimal Deviation
);

/// <summary>
/// Request to find empty/outlier rows based on element averages
/// مشابه empty_check. py در پایتون
/// </summary>
public record FindEmptyRowsRequest(
    Guid ProjectId,
    decimal ThresholdPercent = 70m,
    List<string>? ElementsToCheck = null
);

/// <summary>
/// Information about an empty/outlier row
/// </summary>
public record EmptyRowDto(
    string SolutionLabel,
    Dictionary<string, decimal?> ElementValues,
    Dictionary<string, decimal> ElementAverages,
    Dictionary<string, decimal> PercentOfAverage,
    int ElementsBelowThreshold,
    int TotalElementsChecked,
    decimal OverallScore
);