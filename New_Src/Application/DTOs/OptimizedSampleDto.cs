namespace Application.DTOs;

/// <summary>
/// Represents a request for blank and scale optimization.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Elements">Optional list of specific elements to optimize.</param>
/// <param name="MinDiffPercent">The minimum difference percentage allowed.</param>
/// <param name="MaxDiffPercent">The maximum difference percentage allowed.</param>
/// <param name="MaxIterations">The maximum number of optimization iterations.</param>
/// <param name="PopulationSize">The size of the population for the algorithm.</param>
/// <param name="UseMultiModel">Whether to use multiple optimization models.</param>
/// <param name="Seed">Optional random seed for reproducibility.</param>
public record BlankScaleOptimizationRequest(
    Guid ProjectId,
    List<string>? Elements = null,
    decimal MinDiffPercent = -10m,
    decimal MaxDiffPercent = 10m,
    int MaxIterations = 100,
    int PopulationSize = 20,
    bool UseMultiModel = true,
    int? Seed = null
);

/// <summary>
/// Represents the result of a blank and scale optimization operation.
/// </summary>
/// <param name="TotalSamples">The total number of samples processed.</param>
/// <param name="PassedBefore">Count of samples passing criteria before optimization.</param>
/// <param name="PassedAfter">Count of samples passing criteria after optimization.</param>
/// <param name="ImprovementPercent">The percentage improvement achieved.</param>
/// <param name="ElementOptimizations">Detailed optimization results per element.</param>
/// <param name="OptimizedData">List of samples with optimized values.</param>
/// <param name="ModelSummary">Summary of multi-model performance, if applicable.</param>
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
/// Represents optimization details for a single element.
/// </summary>
/// <param name="Element">The element symbol.</param>
/// <param name="OptimalBlank">The calculated optimal blank value.</param>
/// <param name="OptimalScale">The calculated optimal scale factor.</param>
/// <param name="PassedBefore">Pass count before optimization.</param>
/// <param name="PassedAfter">Pass count after optimization.</param>
/// <param name="MeanDiffBefore">Mean difference before optimization.</param>
/// <param name="MeanDiffAfter">Mean difference after optimization.</param>
/// <param name="SelectedModel">The model selected for this element (e.g., "A").</param>
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
/// Represents a single sample with optimized data.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="CrmId">The associated CRM identifier.</param>
/// <param name="OriginalValues">The original measured values.</param>
/// <param name="CrmValues">The certified CRM values.</param>
/// <param name="OptimizedValues">The values after optimization.</param>
/// <param name="DiffPercentBefore">Difference percentage before optimization.</param>
/// <param name="DiffPercentAfter">Difference percentage after optimization.</param>
/// <param name="PassStatusBefore">Pass status before optimization.</param>
/// <param name="PassStatusAfter">Pass status after optimization.</param>
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
/// Represents a request for manual blank and scale adjustment.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Element">The element to adjust.</param>
/// <param name="Blank">The blank value to apply.</param>
/// <param name="Scale">The scale factor to apply.</param>
public record ManualBlankScaleRequest(
    Guid ProjectId,
    string Element,
    decimal Blank,
    decimal Scale
);

/// <summary>
/// Represents the result of a manual blank and scale adjustment.
/// </summary>
/// <param name="Element">The element adjusted.</param>
/// <param name="Blank">The applied blank value.</param>
/// <param name="Scale">The applied scale factor.</param>
/// <param name="PassedBefore">Pass count before adjustment.</param>
/// <param name="PassedAfter">Pass count after adjustment.</param>
/// <param name="OptimizedData">The resulting data.</param>
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
/// Summarizes the results of multi-model optimization.
/// </summary>
/// <param name="ElementsOptimizedWithModelA">Count of elements best fit by Model A.</param>
/// <param name="ElementsOptimizedWithModelB">Count of elements best fit by Model B.</param>
/// <param name="ElementsOptimizedWithModelC">Count of elements best fit by Model C.</param>
/// <param name="MostUsedModel">The identifier of the most frequently selected model.</param>
/// <param name="Summary">A textual summary of the results.</param>
public record MultiModelSummary(
    int ElementsOptimizedWithModelA,
    int ElementsOptimizedWithModelB,
    int ElementsOptimizedWithModelC,
    string MostUsedModel,
    string Summary
);

/// <summary>
/// Represents detailed comparison results between multiple optimization models.
/// </summary>
/// <param name="BestModel">The identifier of the selected best model.</param>
/// <param name="SelectionReason">The reason for selecting the best model.</param>
/// <param name="ModelA">Result for Model A.</param>
/// <param name="ModelB">Result for Model B.</param>
/// <param name="ModelC">Result for Model C.</param>
public record MultiModelOptimizationResult(
    string BestModel,
    string SelectionReason,
    ModelResult ModelA,
    ModelResult ModelB,
    ModelResult ModelC
);

/// <summary>
/// Represents the result of a single optimization model execution.
/// </summary>
/// <param name="ModelName">The name of the model.</param>
/// <param name="Description">Description of the model strategy.</param>
/// <param name="Success">Indicates if the model converged successfully.</param>
/// <param name="PassedCount">Number of samples passing criteria.</param>
/// <param name="TotalDistance">Total distance metric.</param>
/// <param name="TotalSSE">Total sum of squared errors.</param>
/// <param name="Optimizations">Optimization parameters per element.</param>
/// <param name="ErrorMessage">Error message if failed.</param>
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
/// Represents a comparison of models for a specific element.
/// </summary>
/// <param name="Element">The element symbol.</param>
/// <param name="ModelA">Result for Model A.</param>
/// <param name="ModelB">Result for Model B.</param>
/// <param name="ModelC">Result for Model C.</param>
/// <param name="SelectedModel">The selected best model.</param>
/// <param name="SelectionReason">The reason for selection.</param>
public record ElementModelComparison(
    string Element,
    ElementModelResult ModelA,
    ElementModelResult ModelB,
    ElementModelResult ModelC,
    string SelectedModel,
    string SelectionReason
);

/// <summary>
/// Represents the metrics of a model for a specific element.
/// </summary>
/// <param name="Blank">The optimized blank value.</param>
/// <param name="Scale">The optimized scale factor.</param>
/// <param name="PassedCount">Number of passing samples.</param>
/// <param name="HuberDistance">Huber loss distance.</param>
/// <param name="SSE">Sum of squared errors.</param>
/// <param name="MeanDiffPercent">Mean difference percentage.</param>
public record ElementModelResult(
    decimal Blank,
    decimal Scale,
    int PassedCount,
    double HuberDistance,
    double SSE,
    decimal MeanDiffPercent
);

#endregion