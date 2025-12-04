using System.Text.Json;
using Application.DTOs;
using Application.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;

namespace Infrastructure.Services;

/// <summary>
/// Implementation of Blank and Scale optimization using Differential Evolution
/// Based on Python scipy.optimize.differential_evolution
/// Supports Model A (Pass Count), Model B (Huber), Model C (SSE)
/// 
/// Improvements over previous version:
/// - best1bin strategy (like scipy default)
/// - Convergence detection with tolerance
/// - Dithering (variable F between 0.5-1.0)
/// - Reproducible results with seed parameter
/// 
/// CORRECTED: Formula changed from (value + blank) to (value - blank) to match Python
/// Python formula: corrected = (original - blank) * scale
/// </summary>
public class OptimizationService : IOptimizationService
{
    private readonly IsatisDbContext _db;
    private readonly ILogger<OptimizationService> _logger;
    private Random _random;

    // DE Parameters matching scipy defaults
    private const double DefaultF = 0.8;      // Mutation factor
    private const double DefaultCR = 0.7;     // Crossover probability (scipy default)
    private const double Tolerance = 0.01;    // Convergence tolerance
    private const int ConvergenceWindow = 10; // Generations without improvement to stop

    public OptimizationService(IsatisDbContext db, ILogger<OptimizationService> logger)
    {
        _db = db;
        _logger = logger;
        _random = new Random();
    }

    public async Task<Result<BlankScaleOptimizationResult>> OptimizeBlankScaleAsync(BlankScaleOptimizationRequest request)
    {
        try
        {
            // Set seed for reproducibility if provided
            if (request.Seed.HasValue)
            {
                _random = new Random(request.Seed.Value);
            }

            var projectData = await GetProjectRmDataAsync(request.ProjectId);
            if (!projectData.Any())
                return Result<BlankScaleOptimizationResult>.Fail("No RM samples found in project");

            var crmData = await GetCrmDataAsync();
            if (!crmData.Any())
                return Result<BlankScaleOptimizationResult>.Fail("No CRM data found");

            var matchedData = MatchWithCrm(projectData, crmData);
            if (!matchedData.Any())
                return Result<BlankScaleOptimizationResult>.Fail("No matching CRM data found for RM samples");

            var elements = request.Elements ?? GetCommonElements(matchedData);
            var initialStats = CalculateStatistics(matchedData, elements, 0, 1, request.MinDiffPercent, request.MaxDiffPercent);

            var elementOptimizations = new Dictionary<string, ElementOptimization>();
            var bestBlanks = new Dictionary<string, decimal>();
            var bestScales = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                string selectedModel;
                decimal optimalBlank, optimalScale;
                int passedAfter;

                if (request.UseMultiModel)
                {
                    (optimalBlank, optimalScale, passedAfter, selectedModel) = OptimizeElementMultiModel(
                        matchedData, element, request.MinDiffPercent, request.MaxDiffPercent,
                        request.MaxIterations, request.PopulationSize);
                }
                else
                {
                    (optimalBlank, optimalScale, passedAfter) = OptimizeElementImproved(
                        matchedData, element, request.MinDiffPercent, request.MaxDiffPercent,
                        request.MaxIterations, request.PopulationSize);
                    selectedModel = "A";
                }

                var passedBefore = initialStats.ElementStats.TryGetValue(element, out var stats) ? stats.Passed : 0;

                elementOptimizations[element] = new ElementOptimization(
                    element, optimalBlank, optimalScale, passedBefore, passedAfter,
                    stats?.MeanDiff ?? 0, CalculateMeanDiff(matchedData, element, optimalBlank, optimalScale),
                    selectedModel);

                bestBlanks[element] = optimalBlank;
                bestScales[element] = optimalScale;

                _logger.LogInformation(
                    "Element {Element}: Selected Model {Model} with Blank={Blank:F4}, Scale={Scale:F4}, Passed={Passed}",
                    element, selectedModel, optimalBlank, optimalScale, passedAfter);
            }

            var optimizedData = BuildOptimizedData(matchedData, elements, bestBlanks, bestScales,
                request.MinDiffPercent, request.MaxDiffPercent);

            var totalPassedBefore = elementOptimizations.Values.Sum(e => e.PassedBefore);
            var totalPassedAfter = elementOptimizations.Values.Sum(e => e.PassedAfter);
            var improvement = totalPassedBefore > 0
                ? ((decimal)(totalPassedAfter - totalPassedBefore) / totalPassedBefore) * 100
                : 0;

            var modelACounts = elementOptimizations.Values.Count(e => e.SelectedModel == "A");
            var modelBCounts = elementOptimizations.Values.Count(e => e.SelectedModel == "B");
            var modelCCounts = elementOptimizations.Values.Count(e => e.SelectedModel == "C");

            var mostUsedModel = new[] { ("A", modelACounts), ("B", modelBCounts), ("C", modelCCounts) }
                .OrderByDescending(x => x.Item2).First().Item1;

            var modelSummary = new MultiModelSummary(
                modelACounts, modelBCounts, modelCCounts, mostUsedModel,
                $"Model A: {modelACounts} elements, Model B: {modelBCounts} elements, Model C: {modelCCounts} elements");

            var result = new BlankScaleOptimizationResult(
                matchedData.Count, totalPassedBefore, totalPassedAfter, improvement,
                elementOptimizations, optimizedData, modelSummary);

            return Result<BlankScaleOptimizationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize Blank & Scale for project {ProjectId}", request.ProjectId);
            return Result<BlankScaleOptimizationResult>.Fail("Failed to optimize: " + ex.Message);
        }
    }

    public async Task<Result<ManualBlankScaleResult>> ApplyManualBlankScaleAsync(ManualBlankScaleRequest request)
    {
        return await PreviewBlankScaleAsync(request);
    }

    public async Task<Result<ManualBlankScaleResult>> PreviewBlankScaleAsync(ManualBlankScaleRequest request)
    {
        try
        {
            var projectData = await GetProjectRmDataAsync(request.ProjectId);
            var crmData = await GetCrmDataAsync();
            var matchedData = MatchWithCrm(projectData, crmData);

            if (!matchedData.Any())
                return Result<ManualBlankScaleResult>.Fail("No matching CRM data found");

            var elements = new List<string> { request.Element };
            var blanks = new Dictionary<string, decimal> { { request.Element, request.Blank } };
            var scales = new Dictionary<string, decimal> { { request.Element, request.Scale } };

            var beforeStats = CalculateStatistics(matchedData, elements, 0, 1, -10, 10);
            var afterStats = CalculateStatistics(matchedData, elements, request.Blank, request.Scale, -10, 10);

            var optimizedData = BuildOptimizedData(matchedData, elements, blanks, scales, -10, 10);

            var passedBefore = beforeStats.ElementStats.TryGetValue(request.Element, out var bs) ? bs.Passed : 0;
            var passedAfter = afterStats.ElementStats.TryGetValue(request.Element, out var afs) ? afs.Passed : 0;

            return Result<ManualBlankScaleResult>.Success(new ManualBlankScaleResult(
                request.Element, request.Blank, request.Scale, passedBefore, passedAfter, optimizedData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview Blank & Scale");
            return Result<ManualBlankScaleResult>.Fail("Failed to preview: " + ex.Message);
        }
    }

    public async Task<Result<BlankScaleOptimizationResult>> GetCurrentStatisticsAsync(
        Guid projectId, decimal minDiff = -10m, decimal maxDiff = 10m)
    {
        try
        {
            var projectData = await GetProjectRmDataAsync(projectId);
            var crmData = await GetCrmDataAsync();
            var matchedData = MatchWithCrm(projectData, crmData);

            if (!matchedData.Any())
                return Result<BlankScaleOptimizationResult>.Fail("No matching CRM data found");

            var elements = GetCommonElements(matchedData);
            var stats = CalculateStatistics(matchedData, elements, 0, 1, minDiff, maxDiff);

            var elementOptimizations = elements.ToDictionary(
                e => e,
                e => new ElementOptimization(e, 0, 1,
                    stats.ElementStats.TryGetValue(e, out var s) ? s.Passed : 0,
                    stats.ElementStats.TryGetValue(e, out var s2) ? s2.Passed : 0,
                    s?.MeanDiff ?? 0, s2?.MeanDiff ?? 0));

            var blanks = elements.ToDictionary(e => e, e => 0m);
            var scales = elements.ToDictionary(e => e, e => 1m);
            var optimizedData = BuildOptimizedData(matchedData, elements, blanks, scales, minDiff, maxDiff);

            return Result<BlankScaleOptimizationResult>.Success(new BlankScaleOptimizationResult(
                matchedData.Count, stats.TotalPassed, stats.TotalPassed, 0, elementOptimizations, optimizedData, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current statistics");
            return Result<BlankScaleOptimizationResult>.Fail("Failed to get statistics: " + ex.Message);
        }
    }

    #region Improved Differential Evolution Algorithm (scipy-like)

    /// <summary>
    /// Improved DE algorithm with:
    /// - best1bin strategy (uses best individual for mutation)
    /// - Dithering (F varies between 0.5 and 1.0)
    /// - Convergence detection
    /// - Similar to scipy.optimize.differential_evolution defaults
    /// </summary>
    private (decimal Blank, decimal Scale, int Passed) OptimizeElementImproved(
        List<MatchedSample> data, string element, decimal minDiff, decimal maxDiff, int maxIterations, int populationSize)
    {
        var blankBounds = (-100.0, 100.0);
        var scaleBounds = (0.5, 2.0);

        // Initialize population
        var population = new List<(double Blank, double Scale)>();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add((
                _random.NextDouble() * (blankBounds.Item2 - blankBounds.Item1) + blankBounds.Item1,
                _random.NextDouble() * (scaleBounds.Item2 - scaleBounds.Item1) + scaleBounds.Item1));
        }

        var fitness = population.Select(p => EvaluateFitness(data, element, (decimal)p.Blank, (decimal)p.Scale, minDiff, maxDiff)).ToList();

        // Track best for convergence detection
        double bestFitness = fitness.Max();
        int generationsWithoutImprovement = 0;
        double previousBest = bestFitness;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // Find current best individual for best1bin strategy
            int bestIdx = fitness.IndexOf(fitness.Max());
            var best = population[bestIdx];

            for (int i = 0; i < populationSize; i++)
            {
                // Dithering: vary F between 0.5 and 1.0
                double F = 0.5 + _random.NextDouble() * 0.5;
                double CR = DefaultCR;

                // best1bin: use best individual instead of random
                var candidates = Enumerable.Range(0, populationSize)
                    .Where(j => j != i && j != bestIdx)
                    .OrderBy(_ => _random.Next())
                    .Take(2)
                    .ToList();

                var r1 = population[candidates[0]];
                var r2 = population[candidates[1]];

                // Mutation: best + F * (r1 - r2)
                var mutantBlank = best.Blank + F * (r1.Blank - r2.Blank);
                var mutantScale = best.Scale + F * (r1.Scale - r2.Scale);

                // Boundary handling (bounce-back like scipy)
                mutantBlank = BounceBack(mutantBlank, blankBounds.Item1, blankBounds.Item2);
                mutantScale = BounceBack(mutantScale, scaleBounds.Item1, scaleBounds.Item2);

                // Binomial crossover
                var current = population[i];
                int jRand = _random.Next(2); // Ensure at least one parameter from mutant

                var trialBlank = (jRand == 0 || _random.NextDouble() < CR) ? mutantBlank : current.Blank;
                var trialScale = (jRand == 1 || _random.NextDouble() < CR) ? mutantScale : current.Scale;

                var trialFitness = EvaluateFitness(data, element, (decimal)trialBlank, (decimal)trialScale, minDiff, maxDiff);

                // Selection
                if (trialFitness >= fitness[i])
                {
                    population[i] = (trialBlank, trialScale);
                    fitness[i] = trialFitness;
                }
            }

            // Convergence check
            double currentBest = fitness.Max();
            if (Math.Abs(currentBest - previousBest) < Tolerance)
            {
                generationsWithoutImprovement++;
                if (generationsWithoutImprovement >= ConvergenceWindow)
                {
                    _logger.LogDebug("Converged after {Iterations} iterations for element {Element}", iter, element);
                    break;
                }
            }
            else
            {
                generationsWithoutImprovement = 0;
            }
            previousBest = currentBest;
        }

        var finalBestIdx = fitness.IndexOf(fitness.Max());
        var finalBest = population[finalBestIdx];
        return ((decimal)finalBest.Blank, (decimal)finalBest.Scale, (int)fitness[finalBestIdx]);
    }

    /// <summary>
    /// Bounce-back boundary handling (similar to scipy)
    /// </summary>
    private double BounceBack(double value, double min, double max)
    {
        if (value < min)
        {
            var diff = min - value;
            value = min + (diff % (max - min));
        }
        else if (value > max)
        {
            var diff = value - max;
            value = max - (diff % (max - min));
        }
        return Math.Clamp(value, min, max);
    }

    /// <summary>
    /// Evaluate fitness using Python-compatible formula
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// Python: corrected = (original - blank) * scale
    /// </summary>
    private double EvaluateFitness(List<MatchedSample> data, string element, decimal blank, decimal scale, decimal minDiff, decimal maxDiff)
    {
        int passed = 0;
        foreach (var sample in data)
        {
            if (!sample.SampleValues.TryGetValue(element, out var sampleValue) || !sampleValue.HasValue)
                continue;
            if (!sample.CrmValues.TryGetValue(element, out var crmValue) || !crmValue.HasValue || crmValue.Value == 0)
                continue;

            // CORRECTED: Python formula is (original - blank) * scale
            var correctedValue = (sampleValue.Value - blank) * scale;
            var diffPercent = ((correctedValue - crmValue.Value) / crmValue.Value) * 100;

            if (diffPercent >= minDiff && diffPercent <= maxDiff)
                passed++;
        }
        return passed;
    }

    #endregion

    #region Model B & C - Advanced Objective Functions (Huber Loss)

    /// <summary>
    /// Huber loss function (same as scipy.special.huber)
    /// </summary>
    private double HuberLoss(double delta, double r)
    {
        double absR = Math.Abs(r);
        if (absR <= delta)
            return 0.5 * r * r;
        else
            return delta * (absR - 0.5 * delta);
    }

    /// <summary>
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// </summary>
    private double ObjectiveB_Huber(List<MatchedSample> data, string element, decimal blank, decimal scale, decimal delta = 1.0m)
    {
        double totalLoss = 0;
        int count = 0;

        foreach (var sample in data)
        {
            if (!sample.SampleValues.TryGetValue(element, out var sampleValue) || !sampleValue.HasValue)
                continue;
            if (!sample.CrmValues.TryGetValue(element, out var crmValue) || !crmValue.HasValue || crmValue.Value == 0)
                continue;

            // CORRECTED: Python formula is (original - blank) * scale
            var correctedValue = (sampleValue.Value - blank) * scale;
            var error = (double)((correctedValue - crmValue.Value) / crmValue.Value * 100);

            totalLoss += HuberLoss((double)delta, error);
            count++;
        }

        return count > 0 ? totalLoss / count : double.MaxValue;
    }

    /// <summary>
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// </summary>
    private double ObjectiveC_SSE(List<MatchedSample> data, string element, decimal blank, decimal scale)
    {
        double totalSSE = 0;
        int count = 0;

        foreach (var sample in data)
        {
            if (!sample.SampleValues.TryGetValue(element, out var sampleValue) || !sampleValue.HasValue)
                continue;
            if (!sample.CrmValues.TryGetValue(element, out var crmValue) || !crmValue.HasValue || crmValue.Value == 0)
                continue;

            // CORRECTED: Python formula is (original - blank) * scale
            var correctedValue = (sampleValue.Value - blank) * scale;
            var diffPercent = (double)((correctedValue - crmValue.Value) / crmValue.Value * 100);
            totalSSE += diffPercent * diffPercent;
            count++;
        }

        return count > 0 ? totalSSE / count : double.MaxValue;
    }

    private (decimal Blank, decimal Scale, int Passed, string SelectedModel) OptimizeElementMultiModel(
        List<MatchedSample> data, string element, decimal minDiff, decimal maxDiff, int maxIterations, int populationSize)
    {
        // Model A: Maximize pass count
        var resultA = OptimizeElementImproved(data, element, minDiff, maxDiff, maxIterations, populationSize);

        // Model B: Minimize Huber loss
        var resultB = OptimizeWithObjective(data, element, minDiff, maxDiff, maxIterations, populationSize,
            (d, e, b, s) => -ObjectiveB_Huber(d, e, b, s));

        // Model C: Minimize SSE
        var resultC = OptimizeWithObjective(data, element, minDiff, maxDiff, maxIterations, populationSize,
            (d, e, b, s) => -ObjectiveC_SSE(d, e, b, s));

        int passedB = (int)EvaluateFitness(data, element, resultB.Blank, resultB.Scale, minDiff, maxDiff);
        int passedC = (int)EvaluateFitness(data, element, resultC.Blank, resultC.Scale, minDiff, maxDiff);

        var candidates = new[]
        {
            (Model: "A", Blank: resultA.Blank, Scale: resultA.Scale, Passed: resultA.Passed,
             Huber: ObjectiveB_Huber(data, element, resultA.Blank, resultA.Scale),
             SSE: ObjectiveC_SSE(data, element, resultA.Blank, resultA.Scale)),
            (Model: "B", Blank: resultB.Blank, Scale: resultB.Scale, Passed: passedB,
             Huber: ObjectiveB_Huber(data, element, resultB.Blank, resultB.Scale),
             SSE: ObjectiveC_SSE(data, element, resultB.Blank, resultB.Scale)),
            (Model: "C", Blank: resultC.Blank, Scale: resultC.Scale, Passed: passedC,
             Huber: ObjectiveB_Huber(data, element, resultC.Blank, resultC.Scale),
             SSE: ObjectiveC_SSE(data, element, resultC.Blank, resultC.Scale))
        };

        // Select best model: prioritize pass count, then SSE, then Huber
        var best = candidates.OrderByDescending(c => c.Passed).ThenBy(c => c.SSE).ThenBy(c => c.Huber).First();

        _logger.LogDebug(
            "Element {Element}: Model A(Passed={PassA}), Model B(Passed={PassB}), Model C(Passed={PassC}) -> Selected: {Selected}",
            element, resultA.Passed, passedB, passedC, best.Model);

        return (best.Blank, best.Scale, best.Passed, best.Model);
    }

    private (decimal Blank, decimal Scale) OptimizeWithObjective(
        List<MatchedSample> data, string element, decimal minDiff, decimal maxDiff, int maxIterations, int populationSize,
        Func<List<MatchedSample>, string, decimal, decimal, double> objectiveFunc)
    {
        var blankBounds = (-100.0, 100.0);
        var scaleBounds = (0.5, 2.0);

        var population = new List<(double Blank, double Scale)>();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add((
                _random.NextDouble() * (blankBounds.Item2 - blankBounds.Item1) + blankBounds.Item1,
                _random.NextDouble() * (scaleBounds.Item2 - scaleBounds.Item1) + scaleBounds.Item1));
        }

        var fitness = population.Select(p => objectiveFunc(data, element, (decimal)p.Blank, (decimal)p.Scale)).ToList();

        double previousBest = fitness.Max();
        int generationsWithoutImprovement = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            int bestIdx = fitness.IndexOf(fitness.Max());
            var best = population[bestIdx];

            for (int i = 0; i < populationSize; i++)
            {
                double F = 0.5 + _random.NextDouble() * 0.5; // Dithering
                double CR = DefaultCR;

                var candidates = Enumerable.Range(0, populationSize)
                    .Where(j => j != i && j != bestIdx)
                    .OrderBy(_ => _random.Next())
                    .Take(2)
                    .ToList();

                var r1 = population[candidates[0]];
                var r2 = population[candidates[1]];

                var mutantBlank = BounceBack(best.Blank + F * (r1.Blank - r2.Blank), blankBounds.Item1, blankBounds.Item2);
                var mutantScale = BounceBack(best.Scale + F * (r1.Scale - r2.Scale), scaleBounds.Item1, scaleBounds.Item2);

                var current = population[i];
                int jRand = _random.Next(2);

                var trialBlank = (jRand == 0 || _random.NextDouble() < CR) ? mutantBlank : current.Blank;
                var trialScale = (jRand == 1 || _random.NextDouble() < CR) ? mutantScale : current.Scale;

                var trialFitness = objectiveFunc(data, element, (decimal)trialBlank, (decimal)trialScale);
                if (trialFitness > fitness[i])
                {
                    population[i] = (trialBlank, trialScale);
                    fitness[i] = trialFitness;
                }
            }

            double currentBest = fitness.Max();
            if (Math.Abs(currentBest - previousBest) < Tolerance)
            {
                generationsWithoutImprovement++;
                if (generationsWithoutImprovement >= ConvergenceWindow)
                    break;
            }
            else
            {
                generationsWithoutImprovement = 0;
            }
            previousBest = currentBest;
        }

        var finalBestIdx = fitness.IndexOf(fitness.Max());
        var finalBest = population[finalBestIdx];
        return ((decimal)finalBest.Blank, (decimal)finalBest.Scale);
    }

    #endregion

    #region Helper Methods

    private async Task<List<RmSampleData>> GetProjectRmDataAsync(Guid projectId)
    {
        var rawRows = await _db.RawDataRows.AsNoTracking().Where(r => r.ProjectId == projectId).ToListAsync();
        var result = new List<RmSampleData>();
        // Match CRM/OREAS/etc. at start, optionally followed by numbers/spaces/letters
        // Examples: "CRM 252  y", "CRM BLANK  R", "OREAS 100a", "CRM258"
        var rmPattern = new System.Text.RegularExpressions.Regex(@"^(OREAS|SRM|CRM|NIST|BCR|TILL|GBW)[\s\-_]*(\d+|BLANK)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var row in rawRows)
        {
            try
            {
                // Use SampleId field directly as the label (set during import)
                var solutionLabel = row.SampleId ?? "";
                if (string.IsNullOrEmpty(solutionLabel) || !rmPattern.IsMatch(solutionLabel)) continue;

                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                if (data == null) continue;

                var values = new Dictionary<string, decimal?>();
                foreach (var kvp in data)
                {
                    if (kvp.Key == "Solution Label") continue;
                    if (kvp.Value.ValueKind == JsonValueKind.Number)
                        values[kvp.Key] = kvp.Value.GetDecimal();
                    else if (kvp.Value.ValueKind == JsonValueKind.String && decimal.TryParse(kvp.Value.GetString(), out var val))
                        values[kvp.Key] = val;
                }
                result.Add(new RmSampleData(solutionLabel, values));
            }
            catch { }
        }
        return result;
    }

    private async Task<Dictionary<string, Dictionary<string, decimal>>> GetCrmDataAsync()
    {
        var crmRecords = await _db.CrmData.AsNoTracking().ToListAsync();
        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);

        foreach (var crm in crmRecords)
        {
            try
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(crm.ElementValues);
                if (values != null) result[crm.CrmId] = values;
            }
            catch { }
        }
        return result;
    }

    private List<MatchedSample> MatchWithCrm(List<RmSampleData> projectData, Dictionary<string, Dictionary<string, decimal>> crmData)
    {
        var result = new List<MatchedSample>();
        // Regex to extract number from CRM label (e.g., "CRM 252  y" -> "252", "OREAS 100a" -> "100")
        var numberPattern = new System.Text.RegularExpressions.Regex(@"(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (var sample in projectData)
        {
            // Extract number from sample label
            var sampleMatch = numberPattern.Match(sample.SolutionLabel);
            var sampleNumber = sampleMatch.Success ? sampleMatch.Groups[1].Value : "";
            
            // Skip BLANK samples (no number to match)
            if (string.IsNullOrEmpty(sampleNumber)) continue;
            
            // Find CRM in database that contains the same number
            var matchedCrm = crmData.Keys.FirstOrDefault(k =>
            {
                var crmMatch = numberPattern.Match(k);
                if (!crmMatch.Success) return false;
                var crmNumber = crmMatch.Groups[1].Value;
                return crmNumber == sampleNumber;
            });

            if (matchedCrm != null)
            {
                var crmValues = crmData[matchedCrm].ToDictionary(kvp => kvp.Key, kvp => (decimal?)kvp.Value);
                result.Add(new MatchedSample(sample.SolutionLabel, matchedCrm, sample.Values, crmValues));
            }
        }
        return result;
    }

    private List<string> GetCommonElements(List<MatchedSample> data)
    {
        var sampleElements = data.SelectMany(d => d.SampleValues.Keys).Distinct();
        var crmElements = data.SelectMany(d => d.CrmValues.Keys).Distinct();
        return sampleElements.Intersect(crmElements, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// Python: corrected = (original - blank) * scale
    /// </summary>
    private (int TotalPassed, Dictionary<string, ElementStats> ElementStats) CalculateStatistics(
        List<MatchedSample> data, List<string> elements, decimal blank, decimal scale, decimal minDiff, decimal maxDiff)
    {
        var elementStats = new Dictionary<string, ElementStats>();
        int totalPassed = 0;

        foreach (var element in elements)
        {
            int passed = 0;
            var diffs = new List<decimal>();

            foreach (var sample in data)
            {
                if (!sample.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
                if (!sample.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0) continue;

                // CORRECTED: Python formula is (original - blank) * scale
                var corrected = (sv.Value - blank) * scale;
                var diff = ((corrected - cv.Value) / cv.Value) * 100;
                diffs.Add(diff);

                if (diff >= minDiff && diff <= maxDiff) passed++;
            }

            elementStats[element] = new ElementStats(passed, diffs.Any() ? diffs.Average() : 0);
            totalPassed += passed;
        }
        return (totalPassed, elementStats);
    }

    /// <summary>
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// </summary>
    private decimal CalculateMeanDiff(List<MatchedSample> data, string element, decimal blank, decimal scale)
    {
        var diffs = new List<decimal>();
        foreach (var sample in data)
        {
            if (!sample.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
            if (!sample.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0) continue;

            // CORRECTED: Python formula is (original - blank) * scale
            var corrected = (sv.Value - blank) * scale;
            var diff = ((corrected - cv.Value) / cv.Value) * 100;
            diffs.Add(diff);
        }
        return diffs.Any() ? diffs.Average() : 0;
    }

    /// <summary>
    /// CORRECTED: Changed from (value + blank) to (value - blank)
    /// Python: corrected = (original - blank) * scale
    /// </summary>
    private List<OptimizedSampleDto> BuildOptimizedData(
        List<MatchedSample> data, List<string> elements, Dictionary<string, decimal> blanks,
        Dictionary<string, decimal> scales, decimal minDiff, decimal maxDiff)
    {
        var result = new List<OptimizedSampleDto>();

        foreach (var sample in data)
        {
            var optimizedValues = new Dictionary<string, decimal?>();
            var diffBefore = new Dictionary<string, decimal>();
            var diffAfter = new Dictionary<string, decimal>();
            var passBefore = new Dictionary<string, bool>();
            var passAfter = new Dictionary<string, bool>();

            foreach (var element in elements)
            {
                if (!sample.SampleValues.TryGetValue(element, out var sv)) continue;
                if (!sample.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0) continue;

                var blank = blanks.TryGetValue(element, out var b) ? b : 0;
                var scale = scales.TryGetValue(element, out var s) ? s : 1;

                var original = sv ?? 0;
                // CORRECTED: Python formula is (original - blank) * scale
                var optimized = (original - blank) * scale;
                optimizedValues[element] = optimized;

                var diffB = ((original - cv.Value) / cv.Value) * 100;
                var diffA = ((optimized - cv.Value) / cv.Value) * 100;

                diffBefore[element] = diffB;
                diffAfter[element] = diffA;
                passBefore[element] = diffB >= minDiff && diffB <= maxDiff;
                passAfter[element] = diffA >= minDiff && diffA <= maxDiff;
            }

            result.Add(new OptimizedSampleDto(
                sample.SolutionLabel, sample.CrmId, sample.SampleValues, sample.CrmValues,
                optimizedValues, diffBefore, diffAfter, passBefore, passAfter));
        }
        return result;
    }

    public async Task<object> GetDebugSamplesAsync(Guid projectId)
    {
        // Get ALL distinct SampleIds from project to find CRM/RM samples
        var distinctSampleIds = await _db.RawDataRows.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => r.SampleId)
            .Distinct()
            .ToListAsync();
        
        // Match CRM/OREAS/etc. at start, optionally followed by numbers/spaces/letters
        // Examples: "CRM 252  y", "CRM BLANK  R", "OREAS 100a", "CRM258"
        var rmPattern = new System.Text.RegularExpressions.Regex(@"^(OREAS|SRM|CRM|NIST|BCR|TILL|GBW)[\s\-_]*(\d+|BLANK)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        var allLabels = distinctSampleIds.Where(s => !string.IsNullOrEmpty(s)).ToList();
        var rmLabels = allLabels.Where(s => rmPattern.IsMatch(s!)).ToList();
        
        // Get sample raw data for debugging
        var rawRows = await _db.RawDataRows.AsNoTracking().Where(r => r.ProjectId == projectId).Take(5).ToListAsync();
        var sampleColumnData = rawRows.Select(r => r.ColumnData ?? "null").ToList();
        
        var totalRows = await _db.RawDataRows.CountAsync(r => r.ProjectId == projectId);
        var crmIds = await _db.CrmData.AsNoTracking().Select(c => c.CrmId).Distinct().Take(20).ToListAsync();
        
        return new
        {
            totalRows = totalRows,
            allLabelsCount = allLabels.Count,
            rmLabelsCount = rmLabels.Count,
            sampleLabels = allLabels.Distinct().Take(30).ToList(),
            rmLabels = rmLabels.Distinct().Take(30).ToList(),
            sampleCrmIds = crmIds,
            rawColumnDataSamples = sampleColumnData  // Debug: show raw JSON
        };
    }

    private record RmSampleData(string SolutionLabel, Dictionary<string, decimal?> Values);
    private record MatchedSample(string SolutionLabel, string CrmId, Dictionary<string, decimal?> SampleValues, Dictionary<string, decimal?> CrmValues);
    private record ElementStats(int Passed, decimal MeanDiff);

    #endregion
}