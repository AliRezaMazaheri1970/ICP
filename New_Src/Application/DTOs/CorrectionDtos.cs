namespace Application.DTOs;

// ============================================
// Request DTOs
// ============================================

/// <summary>
/// Request to find samples with bad weights (outside expected range)
/// </summary>
public record FindBadWeightsRequest(
    Guid ProjectId,
    decimal WeightMin = 0.09m,
    decimal WeightMax = 0.11m
);

/// <summary>
/// Request to find samples with bad volumes
/// </summary>
public record FindBadVolumesRequest(
    Guid ProjectId,
    decimal ExpectedVolume = 10m
);

/// <summary>
/// Request to find empty/outlier rows based on element averages
/// مشابه empty_check.py در پایتون
/// </summary>
public record FindEmptyRowsRequest(
    Guid ProjectId,
    decimal ThresholdPercent = 70m,
    List<string>? ElementsToCheck = null,
    bool RequireAllElements = true  // ✅ اضافه شد - true = مثل پایتون (همه عناصر باید زیر آستانه باشند)
);

/// <summary>
/// Request to apply weight correction
/// </summary>
public record WeightCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewWeight,
    string? ChangedBy = null
);

/// <summary>
/// Request to apply volume correction
/// </summary>
public record VolumeCorrectionRequest(
    Guid ProjectId,
    List<string> SolutionLabels,
    decimal NewVolume,
    string? ChangedBy = null
);

/// <summary>
/// Request to apply optimization (Blank & Scale)
/// </summary>
public record ApplyOptimizationRequest(
    Guid ProjectId,
    Dictionary<string, ElementSettings> ElementSettings,
    string? ChangedBy = null
);

/// <summary>
/// Element-specific Blank and Scale settings
/// </summary>
public record ElementSettings(
    decimal Blank,
    decimal Scale
);

// ============================================
// Response DTOs
// ============================================

/// <summary>
/// Information about a sample with bad weight or volume
/// </summary>
public record BadSampleDto(
    string SolutionLabel,
    decimal ActualValue,
    decimal CorrCon,
    decimal ExpectedValue,
    decimal Deviation
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

/// <summary>
/// Result of applying a correction
/// </summary>
public record CorrectionResultDto(
    int TotalRows,
    int CorrectedRows,
    List<CorrectedSampleInfo> CorrectedSamples
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