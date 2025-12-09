using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

/// <summary>
/// Drift Correction Service - 100% Python Compatible (Optimized)
/// 
/// Key optimization: Only stores RM/Standard values as initial (like Python's initial_rm_df)
/// Not the entire dataset - much faster!
/// </summary>
public class DriftCorrectionService : IDriftCorrectionService
{
    private readonly IsatisDbContext _db;
    private readonly IChangeLogService _changeLogService;
    private readonly ILogger<DriftCorrectionService> _logger;

    private const string DefaultBasePattern = @"^RM";
    private const string InitialRmStateDescription = "Initial:DriftRM";

    private static readonly Regex RmPattern = new(
        @"^(OREAS|SRM|CRM|STANDARD|STD)(?!\s*BLANK)\s*\d*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DriftCorrectionService(
        IsatisDbContext db,
        IChangeLogService changeLogService,
        ILogger<DriftCorrectionService> logger)
    {
        _db = db;
        _changeLogService = changeLogService;
        _logger = logger;
    }

    #region Public Methods

    public async Task<Result<DriftCorrectionResult>> AnalyzeDriftAsync(DriftCorrectionRequest request)
    {
        try
        {
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

            if (project == null)
                return Result<DriftCorrectionResult>.Fail("Project not found");

            var currentData = await GetParsedDataAsync(request.ProjectId);
            if (!currentData.Any())
                return Result<DriftCorrectionResult>.Fail("No data found for project");

            var segments = DetectSegments(currentData, request.BasePattern, request.ConePattern);
            var elements = request.SelectedElements ?? GetAllElements(currentData);

            // Get initial RM values (if saved)
            var initialRmValues = await GetInitialRmValuesAsync(request.ProjectId);

            // Calculate ratios using initial values if available
            var segmentRatios = CalculateSegmentRatios(currentData, segments, elements, initialRmValues);

            var elementDrifts = new Dictionary<string, ElementDriftInfo>();
            foreach (var element in elements)
            {
                var driftInfo = CalculateElementDrift(currentData, element, segments, segmentRatios, initialRmValues);
                if (driftInfo != null)
                    elementDrifts[element] = driftInfo;
            }

            var result = new DriftCorrectionResult(
                TotalSamples: currentData.Count,
                CorrectedSamples: 0,
                SegmentsFound: segments.Count,
                Segments: segments,
                ElementDrifts: elementDrifts,
                CorrectedData: new List<CorrectedSampleDto>()
            );

            return Result<DriftCorrectionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze drift for project {ProjectId}", request.ProjectId);
            return Result<DriftCorrectionResult>.Fail($"Failed to analyze drift: {ex.Message}");
        }
    }

    public async Task<Result<DriftCorrectionResult>> ApplyDriftCorrectionAsync(DriftCorrectionRequest request)
    {
        try
        {
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

            if (project == null)
                return Result<DriftCorrectionResult>.Fail("Project not found");

            var currentData = await GetParsedDataAsync(request.ProjectId);
            if (!currentData.Any())
                return Result<DriftCorrectionResult>.Fail("No data found for project");

            var segments = DetectSegments(currentData, request.BasePattern, request.ConePattern);
            var elements = request.SelectedElements ?? GetAllElements(currentData);

            // Save initial RM values if not exists (Python: initial_rm_df)
            await SaveInitialRmValuesIfNotExistsAsync(request.ProjectId, currentData, segments);

            // Get initial RM values
            var initialRmValues = await GetInitialRmValuesAsync(request.ProjectId);

            var segmentRatios = CalculateSegmentRatios(currentData, segments, elements, initialRmValues);

            var elementDrifts = new Dictionary<string, ElementDriftInfo>();
            foreach (var element in elements)
            {
                var driftInfo = CalculateElementDrift(currentData, element, segments, segmentRatios, initialRmValues);
                if (driftInfo != null)
                    elementDrifts[element] = driftInfo;
            }

            // Apply correction
            var correctedData = request.Method switch
            {
                DriftMethod.Linear => ApplyLinearCorrection(currentData, segments, elements, segmentRatios),
                DriftMethod.Stepwise => ApplyStepwiseCorrection(currentData, segments, elements, segmentRatios),
                _ => ApplyLinearCorrection(currentData, segments, elements, segmentRatios)
            };

            // Save undo state
            await SaveUndoStateAsync(request.ProjectId, $"DriftCorrection_{request.Method}");

            // Save to database
            var savedCount = await SaveCorrectedDataToDatabase(request.ProjectId, correctedData, elements);

            project.LastModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _changeLogService.LogChangeAsync(
                request.ProjectId,
                "DriftCorrection",
                request.ChangedBy,
                details: $"Applied {request.Method} drift correction. Segments: {segments.Count}, Corrected: {savedCount}"
            );

            _logger.LogInformation(
                "Drift correction applied: Method={Method}, Segments={Segments}, Saved={Saved}",
                request.Method, segments.Count, savedCount);

            var result = new DriftCorrectionResult(
                TotalSamples: currentData.Count,
                CorrectedSamples: savedCount,
                SegmentsFound: segments.Count,
                Segments: segments,
                ElementDrifts: elementDrifts,
                CorrectedData: correctedData
            );

            return Result<DriftCorrectionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply drift correction for project {ProjectId}", request.ProjectId);
            return Result<DriftCorrectionResult>.Fail($"Failed to apply drift correction: {ex.Message}");
        }
    }

    public async Task<Result<List<DriftSegment>>> DetectSegmentsAsync(
        Guid projectId, string? basePattern = null, string? conePattern = null)
    {
        try
        {
            var rawData = await GetParsedDataAsync(projectId);
            if (!rawData.Any())
                return Result<List<DriftSegment>>.Fail("No data found");

            var segments = DetectSegments(rawData, basePattern, conePattern);
            return Result<List<DriftSegment>>.Success(segments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect segments for project {ProjectId}", projectId);
            return Result<List<DriftSegment>>.Fail($"Failed to detect segments: {ex.Message}");
        }
    }

    public async Task<Result<Dictionary<string, List<decimal>>>> CalculateDriftRatiosAsync(
        Guid projectId, List<string>? elements = null)
    {
        try
        {
            var currentData = await GetParsedDataAsync(projectId);
            if (!currentData.Any())
                return Result<Dictionary<string, List<decimal>>>.Fail("No data found");

            var allElements = elements ?? GetAllElements(currentData);
            var segments = DetectSegments(currentData, null, null);
            var initialRmValues = await GetInitialRmValuesAsync(projectId);
            var segmentRatios = CalculateSegmentRatios(currentData, segments, allElements, initialRmValues);

            var ratios = new Dictionary<string, List<decimal>>();
            foreach (var element in allElements)
            {
                var elementRatios = segments
                    .Where(s => segmentRatios.ContainsKey(s.SegmentIndex) &&
                                segmentRatios[s.SegmentIndex].ContainsKey(element))
                    .Select(s => segmentRatios[s.SegmentIndex][element])
                    .ToList();
                ratios[element] = elementRatios;
            }

            return Result<Dictionary<string, List<decimal>>>.Success(ratios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate drift ratios for project {ProjectId}", projectId);
            return Result<Dictionary<string, List<decimal>>>.Fail($"Failed to calculate drift ratios: {ex.Message}");
        }
    }

    public Task<Result<SlopeOptimizationResult>> OptimizeSlopeAsync(SlopeOptimizationRequest request)
        => Task.FromResult(Result<SlopeOptimizationResult>.Fail("Not implemented"));

    public Task<Result<SlopeOptimizationResult>> ZeroSlopeAsync(Guid projectId, string element)
        => Task.FromResult(Result<SlopeOptimizationResult>.Fail("Not implemented"));

    #endregion

    #region Private Methods - Initial RM Values (Python: initial_rm_df) - OPTIMIZED

    /// <summary>
    /// Save only RM values as initial - much smaller than full dataset
    /// Python equivalent: initial_rm_df (only stores RM rows, not all data)
    /// </summary>
    private async Task SaveInitialRmValuesIfNotExistsAsync(
        Guid projectId,
        List<ParsedRow> data,
        List<DriftSegment> segments)
    {
        var existing = await _db.ProjectStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId &&
                                      s.Description == InitialRmStateDescription);

        if (existing != null)
            return;

        // Extract only RM/Standard values at segment boundaries
        var rmValues = new Dictionary<string, Dictionary<string, decimal?>>();

        foreach (var segment in segments)
        {
            // Start standard
            var startLabel = data[segment.StartIndex].SolutionLabel;
            if (!rmValues.ContainsKey(startLabel))
                rmValues[startLabel] = new Dictionary<string, decimal?>(data[segment.StartIndex].Values);

            // End standard
            var endLabel = data[segment.EndIndex].SolutionLabel;
            if (!rmValues.ContainsKey(endLabel))
                rmValues[endLabel] = new Dictionary<string, decimal?>(data[segment.EndIndex].Values);
        }

        var stateJson = JsonSerializer.Serialize(rmValues);

        _db.ProjectStates.Add(new ProjectState
        {
            ProjectId = projectId,
            Data = stateJson,
            Description = InitialRmStateDescription,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Saved initial RM values for project {ProjectId}, {Count} standards",
            projectId, rmValues.Count);
    }

    /// <summary>
    /// Get initial RM values - fast lookup
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, decimal?>>?> GetInitialRmValuesAsync(Guid projectId)
    {
        var state = await _db.ProjectStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId &&
                                      s.Description == InitialRmStateDescription);

        if (state == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal?>>>(state.Data);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Private Methods - Database Operations

    private async Task<int> SaveCorrectedDataToDatabase(
        Guid projectId,
        List<CorrectedSampleDto> correctedData,
        List<string> elements)
    {
        var rawRows = await _db.RawDataRows
            .Where(r => r.ProjectId == projectId)
            .ToListAsync();

        var savedCount = 0;

        var corrections = new Dictionary<(string Label, string Element), decimal>();
        foreach (var sample in correctedData)
        {
            if (sample.CorrectionFactors == null || !sample.CorrectionFactors.Any())
                continue;

            foreach (var element in elements)
            {
                if (sample.CorrectedValues.TryGetValue(element, out var correctedValue) && correctedValue.HasValue)
                {
                    corrections[(sample.SolutionLabel, element)] = correctedValue.Value;
                }
            }
        }

        foreach (var row in rawRows)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                if (dict == null) continue;

                var solutionLabel = dict.TryGetValue("Solution Label", out var sl)
                    ? sl.GetString() ?? row.SampleId ?? ""
                    : row.SampleId ?? "";

                string? elementName = null;
                if (dict.TryGetValue("Element", out var elemProp))
                    elementName = elemProp.GetString();

                if (string.IsNullOrWhiteSpace(elementName))
                    continue;

                if (!corrections.TryGetValue((solutionLabel, elementName), out var newValue))
                    continue;

                var newDict = new Dictionary<string, object>();
                foreach (var kvp in dict)
                {
                    if (kvp.Key == "Corr Con")
                        newDict["Corr Con"] = newValue;
                    else
                        newDict[kvp.Key] = GetJsonValue(kvp.Value);
                }

                if (!newDict.ContainsKey("Corr Con"))
                    newDict["Corr Con"] = newValue;

                row.ColumnData = JsonSerializer.Serialize(newDict);
                savedCount++;
            }
            catch { }
        }

        await _db.SaveChangesAsync();
        return savedCount;
    }

    private async Task SaveUndoStateAsync(Guid projectId, string operation)
    {
        var rows = await _db.RawDataRows
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => new SavedRowData(r.SampleId, r.ColumnData))
            .ToListAsync();

        var stateJson = JsonSerializer.Serialize(rows);

        _db.ProjectStates.Add(new ProjectState
        {
            ProjectId = projectId,
            Data = stateJson,
            Description = $"Undo:{operation}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    #endregion

    #region Private Methods - Data Access

    private async Task<List<ParsedRow>> GetParsedDataAsync(Guid projectId)
    {
        var rawRows = await _db.RawDataRows
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.DataId)
            .ToListAsync();

        var pivotedData = new Dictionary<string, Dictionary<string, decimal?>>();
        var sampleOrder = new List<string>();

        foreach (var row in rawRows)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                if (data == null) continue;

                var solutionLabel = data.TryGetValue("Solution Label", out var sl)
                    ? sl.GetString() ?? row.SampleId ?? $"Row_{row.DataId}"
                    : row.SampleId ?? $"Row_{row.DataId}";

                string? elementName = null;
                if (data.TryGetValue("Element", out var elemProp))
                    elementName = elemProp.GetString();

                if (string.IsNullOrWhiteSpace(elementName))
                    continue;

                if (!pivotedData.ContainsKey(solutionLabel))
                {
                    pivotedData[solutionLabel] = new Dictionary<string, decimal?>();
                    sampleOrder.Add(solutionLabel);
                }

                decimal? value = null;
                if (data.TryGetValue("Corr Con", out var corrCon))
                    value = ExtractDecimalValue(corrCon);

                if (!value.HasValue && data.TryGetValue("Soln Conc", out var solnConc))
                    value = ExtractDecimalValue(solnConc);

                pivotedData[solutionLabel][elementName] = value;
            }
            catch { }
        }

        var result = new List<ParsedRow>();
        foreach (var label in sampleOrder)
        {
            if (pivotedData.TryGetValue(label, out var values))
                result.Add(new ParsedRow(label, values));
        }

        return result;
    }

    private static decimal? ExtractDecimalValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetDecimal();

        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (!string.IsNullOrWhiteSpace(str) && str != "-----" && decimal.TryParse(str, out var val))
                return val;
        }
        return null;
    }

    private static object GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetDecimal(out var d) ? d : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.GetRawText()
        };
    }

    #endregion

    #region Private Methods - Segment Detection

    private List<DriftSegment> DetectSegments(List<ParsedRow> data, string? basePattern, string? conePattern)
    {
        var segments = new List<DriftSegment>();
        var effectivePattern = basePattern ?? DefaultBasePattern;
        var baseRegex = new Regex(effectivePattern, RegexOptions.IgnoreCase);

        Regex? coneRegex = null;
        if (!string.IsNullOrWhiteSpace(conePattern))
            coneRegex = new Regex(conePattern, RegexOptions.IgnoreCase);

        var standardIndices = new List<(int Index, string Label)>();

        for (int i = 0; i < data.Count; i++)
        {
            var label = data[i].SolutionLabel;

            if (baseRegex.IsMatch(label) ||
                (coneRegex != null && coneRegex.IsMatch(label)) ||
                RmPattern.IsMatch(label))
            {
                standardIndices.Add((i, label));
            }
        }

        if (standardIndices.Count < 2)
        {
            segments.Add(new DriftSegment(0, 0, data.Count - 1, null, null, data.Count));
            return segments;
        }

        for (int i = 0; i < standardIndices.Count - 1; i++)
        {
            var start = standardIndices[i];
            var end = standardIndices[i + 1];

            segments.Add(new DriftSegment(
                segments.Count,
                start.Index,
                end.Index,
                start.Label,
                end.Label,
                end.Index - start.Index
            ));
        }

        return segments;
    }

    private List<string> GetAllElements(List<ParsedRow> data)
    {
        return data
            .SelectMany(d => d.Values.Keys)
            .Distinct()
            .OrderBy(e => e)
            .ToList();
    }

    private decimal? GetElementValue(ParsedRow row, string element)
    {
        return row.Values.TryGetValue(element, out var value) ? value : null;
    }

    #endregion

    #region Private Methods - Ratio Calculation

    /// <summary>
    /// Calculate ratios for drift correction - Python compatible
    /// 
    /// First time (no initial saved):
    ///   ratio = endValue / startValue (drift within segment)
    /// 
    /// After correction (initial exists):
    ///   ratio = currentValue / initialValue (should be ~1.0 if corrected)
    /// </summary>
    private Dictionary<int, Dictionary<string, decimal>> CalculateSegmentRatios(
        List<ParsedRow> currentData,
        List<DriftSegment> segments,
        List<string> elements,
        Dictionary<string, Dictionary<string, decimal?>>? initialRmValues)
    {
        var result = new Dictionary<int, Dictionary<string, decimal>>();

        foreach (var segment in segments)
        {
            var ratios = new Dictionary<string, decimal>();
            var startLabel = currentData[segment.StartIndex].SolutionLabel;
            var endLabel = currentData[segment.EndIndex].SolutionLabel;

            foreach (var element in elements)
            {
                decimal? startValue;
                decimal? endValue;

                if (initialRmValues != null)
                {
                    // After first correction: compare current to initial
                    // ratio = current / initial (should be ~1.0 after correction)

                    // Get initial start value
                    if (initialRmValues.TryGetValue(startLabel, out var startRm) &&
                        startRm.TryGetValue(element, out var initStart))
                    {
                        startValue = initStart;
                    }
                    else
                    {
                        startValue = GetElementValue(currentData[segment.StartIndex], element);
                    }

                    // Get current end value
                    endValue = GetElementValue(currentData[segment.EndIndex], element);
                }
                else
                {
                    // First time: calculate drift within segment
                    // ratio = endValue / startValue
                    startValue = GetElementValue(currentData[segment.StartIndex], element);
                    endValue = GetElementValue(currentData[segment.EndIndex], element);
                }

                if (startValue.HasValue && endValue.HasValue && startValue.Value != 0)
                {
                    ratios[element] = endValue.Value / startValue.Value;
                }
                else
                {
                    ratios[element] = 1.0m;
                }
            }

            result[segment.SegmentIndex] = ratios;
        }

        return result;
    }

    private ElementDriftInfo? CalculateElementDrift(
        List<ParsedRow> currentData,
        string element,
        List<DriftSegment> segments,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios,
        Dictionary<string, Dictionary<string, decimal?>>? initialRmValues)
    {
        if (segments.Count == 0) return null;

        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        var startLabel = currentData[firstSegment.StartIndex].SolutionLabel;

        // Get initial value
        decimal? initialFirst = null;
        if (initialRmValues != null &&
            initialRmValues.TryGetValue(startLabel, out var rmValues) &&
            rmValues.TryGetValue(element, out var initVal))
        {
            initialFirst = initVal;
        }
        else
        {
            initialFirst = GetElementValue(currentData[firstSegment.StartIndex], element);
        }

        var currentLast = GetElementValue(currentData[lastSegment.EndIndex], element);

        if (!initialFirst.HasValue || !currentLast.HasValue || initialFirst.Value == 0)
            return null;

        var driftPercent = ((currentLast.Value - initialFirst.Value) / Math.Abs(initialFirst.Value)) * 100;

        return new ElementDriftInfo(
            element,
            1.0m,
            1.0m,
            driftPercent,
            0m,
            initialFirst.Value
        );
    }

    #endregion

    #region Private Methods - Correction Application

    /// <summary>
    /// Get segment index for a sample - Python compatible
    /// Samples before first standard: -1 (no correction)
    /// Samples after a standard: belong to that standard's segment
    /// </summary>
    private int GetSegmentIndexForSample(int sampleIndex, List<DriftSegment> segments)
    {
        if (segments.Count == 0)
            return -1;

        // Samples before first standard → no correction
        if (sampleIndex < segments[0].StartIndex)
            return -1;

        // Find the segment this sample belongs to
        // Sample belongs to segment if: segment.StartIndex <= sampleIndex < nextSegment.StartIndex
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (sampleIndex >= segments[i].StartIndex)
                return segments[i].SegmentIndex;
        }

        return -1;
    }

    /// <summary>
    /// Check if sample is a standard (RM/CRM/etc)
    /// </summary>
    private bool IsStandardSample(string solutionLabel)
    {
        var label = solutionLabel.ToUpper();
        return label.StartsWith("RM") ||
               label.StartsWith("CRM") ||
               label.StartsWith("SRM") ||
               label.StartsWith("OREAS") ||
               label.StartsWith("STD") ||
               label.StartsWith("STANDARD");
    }

    /// <summary>
    /// Check if sample is the START standard of a segment (should not be corrected)
    /// Python behavior: only the FIRST RM of each segment stays unchanged
    /// </summary>
    private bool IsSegmentStartStandard(int sampleIndex, List<DriftSegment> segments)
    {
        return segments.Any(s => s.StartIndex == sampleIndex);
    }

    private List<CorrectedSampleDto> ApplyLinearCorrection(
        List<ParsedRow> data,
        List<DriftSegment> segments,
        List<string> elements,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios)
    {
        var result = new List<CorrectedSampleDto>();
        const decimal tolerance = 0.0001m;

        for (int i = 0; i < data.Count; i++)
        {
            var segmentIndex = GetSegmentIndexForSample(i, segments);

            // Python behavior: correct everything EXCEPT:
            // 1. Samples before first standard (segmentIndex < 0)
            // 2. The START standard of each segment
            var isStartStandard = IsSegmentStartStandard(i, segments);
            var shouldCorrect = segmentIndex >= 0 && !isStartStandard;

            var correctedValues = new Dictionary<string, decimal?>();
            var correctionFactors = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var originalValue = GetElementValue(data[i], element);

                if (shouldCorrect && originalValue.HasValue &&
                    segmentRatios.TryGetValue(segmentIndex, out var ratios) &&
                    ratios.TryGetValue(element, out var ratio) &&
                    ratio != 0 && Math.Abs(ratio - 1.0m) > tolerance)
                {
                    // Linear: apply fixed ratio for entire segment
                    var correctionFactor = 1.0m / ratio;
                    correctedValues[element] = originalValue.Value * correctionFactor;
                    correctionFactors[element] = correctionFactor;
                }
                else
                {
                    correctedValues[element] = originalValue;
                }
            }

            result.Add(new CorrectedSampleDto(
                data[i].SolutionLabel,
                i,
                Math.Max(0, segmentIndex),
                data[i].Values,
                correctedValues,
                correctionFactors
            ));
        }

        return result;
    }

    private List<CorrectedSampleDto> ApplyStepwiseCorrection(
        List<ParsedRow> data,
        List<DriftSegment> segments,
        List<string> elements,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios)
    {
        var result = new List<CorrectedSampleDto>();
        const decimal tolerance = 0.0001m;

        for (int i = 0; i < data.Count; i++)
        {
            var segmentIndex = GetSegmentIndexForSample(i, segments);
            var segment = segmentIndex >= 0 ? segments.FirstOrDefault(s => s.SegmentIndex == segmentIndex) : null;

            // Python behavior: correct everything EXCEPT start standard
            var isStartStandard = IsSegmentStartStandard(i, segments);
            var shouldCorrect = segment != null && !isStartStandard;

            var correctedValues = new Dictionary<string, decimal?>();
            var correctionFactors = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var originalValue = GetElementValue(data[i], element);

                if (shouldCorrect && originalValue.HasValue &&
                    segmentRatios.TryGetValue(segmentIndex, out var ratios) &&
                    ratios.TryGetValue(element, out var ratio) &&
                    Math.Abs(ratio - 1.0m) > tolerance)
                {
                    // Stepwise: gradual correction within segment
                    decimal delta = ratio - 1.0m;
                    int n = segment!.EndIndex - segment.StartIndex;
                    decimal stepDelta = n > 0 ? delta / n : 0;
                    int stepIndex = i - segment.StartIndex;

                    decimal effectiveRatio;
                    if (stepIndex <= 0)
                        effectiveRatio = 1.0m;  // At start standard, no correction
                    else
                        effectiveRatio = 1.0m + (stepDelta * stepIndex);

                    var correctionFactor = effectiveRatio != 0 ? 1.0m / effectiveRatio : 1.0m;
                    correctedValues[element] = originalValue.Value * correctionFactor;
                    correctionFactors[element] = correctionFactor;
                }
                else
                {
                    correctedValues[element] = originalValue;
                }
            }

            result.Add(new CorrectedSampleDto(
                data[i].SolutionLabel,
                i,
                Math.Max(0, segmentIndex),
                data[i].Values,
                correctedValues,
                correctionFactors
            ));
        }

        return result;
    }

    #endregion

    #region Private Types

    private record ParsedRow(string SolutionLabel, Dictionary<string, decimal?> Values);
    private record SavedRowData(string? SampleId, string ColumnData);

    #endregion
}