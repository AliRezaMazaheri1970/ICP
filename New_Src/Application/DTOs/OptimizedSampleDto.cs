namespace Application.DTOs;

/// <summary>
/// Request for Blank & Scale optimization
/// </summary>
public record BlankScaleOptimizationRequest(
    Guid ProjectId,
    List<string>? Elements = null,
    decimal MinDiffPercent = -10m,
    decimal MaxDiffPercent = 10m,
    int MaxIterations = 100,
    int PopulationSize = 50,
    bool UseMultiModel = true
);

/// <summary>
/// Result of Blank & Scale optimization
/// </summary>
public record BlankScaleOptimizationResult(
    int TotalSamples,
    int PassedBefore,
    int PassedAfter,
    decimal ImprovementPercent,
    Dictionary<string, ElementOptimization> ElementOptimizations,
    List<OptimizedSampleDto> OptimizedData,
    MultiModelSummary? ModelSummary = null
);

/// <summary>
/// Optimization result for a single element
/// </summary>
public record ElementOptimization(
    string Element,
    decimal OptimalBlank,
    decimal OptimalScale,
    int PassedBefore,
    int PassedAfter,
    decimal MeanDiffBefore,
    decimal MeanDiffAfter,
    string SelectedModel = "A"
);

/// <summary>
/// Optimized sample data
/// </summary>
public record OptimizedSampleDto(
    string SolutionLabel,
    string CrmId,
    Dictionary<string, decimal?> OriginalValues,
    Dictionary<string, decimal?> CrmValues,
    Dictionary<string, decimal?> OptimizedValues,
    Dictionary<string, decimal> DiffPercentBefore,
    Dictionary<string, decimal> DiffPercentAfter,
    Dictionary<string, bool> PassStatusBefore,
    Dictionary<string, bool> PassStatusAfter
);

/// <summary>
/// Manual Blank & Scale adjustment request
/// </summary>
public record ManualBlankScaleRequest(
    Guid ProjectId,
    string Element,
    decimal Blank,
    decimal Scale
);

/// <summary>
/// Result of manual adjustment
/// </summary>
public record ManualBlankScaleResult(
    string Element,
    decimal Blank,
    decimal Scale,
    int PassedBefore,
    int PassedAfter,
    List<OptimizedSampleDto> OptimizedData
);

#region Multi-Model Optimization Records

/// <summary>
/// Summary of multi-model optimization results
/// </summary>
public record MultiModelSummary(
    int ElementsOptimizedWithModelA,
    int ElementsOptimizedWithModelB,
    int ElementsOptimizedWithModelC,
    string MostUsedModel,
    string Summary
);

/// <summary>
/// Result of multi-model optimization comparison
/// </summary>
public record MultiModelOptimizationResult(
    string BestModel,
    string SelectionReason,
    ModelResult ModelA,
    ModelResult ModelB,
    ModelResult ModelC
);

/// <summary>
/// Result of a single optimization model
/// </summary>
public record ModelResult(
    string ModelName,
    string Description,
    bool Success,
    int PassedCount,
    double TotalDistance,
    double TotalSSE,
    Dictionary<string, ElementOptimization> Optimizations,
    string? ErrorMessage = null
);

/// <summary>
/// Detailed comparison between models for a single element
/// </summary>
public record ElementModelComparison(
    string Element,
    ElementModelResult ModelA,
    ElementModelResult ModelB,
    ElementModelResult ModelC,
    string SelectedModel,
    string SelectionReason
);

/// <summary>
/// Result of a model for a single element
/// </summary>
public record ElementModelResult(
    decimal Blank,
    decimal Scale,
    int PassedCount,
    double HuberDistance,
    double SSE,
    decimal MeanDiffPercent
);

#endregion