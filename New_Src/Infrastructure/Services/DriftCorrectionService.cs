using Application.DTOs;
using Application.Services;
using Infrastructure.Persistence;
using MathNet.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

/// <summary>
/// Implementation of drift correction algorithms
/// Based on Python RM_check.py logic - 100% Python Compatible
/// </summary>
public class DriftCorrectionService : IDriftCorrectionService
{
    private readonly IsatisDbContext _db;
    private readonly ILogger<DriftCorrectionService> _logger;

    // ============================================================
    // Default patterns - PYTHON COMPATIBLE
    // Python: keyword = "RM" (default in keyword_entry = QLineEdit("RM"))
    // ============================================================
    private const string DefaultBasePattern = @"^RM";
    private const string? DefaultConePattern = null;

    // RmPattern for other standards (when custom pattern provided)
    private static readonly Regex RmPattern = new(@"^(OREAS|SRM|CRM|STANDARD|STD)(?!\s*BLANK)\s*\d*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DriftCorrectionService(IsatisDbContext db, ILogger<DriftCorrectionService> logger)
    {
        _db = db;
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

            // Get raw data (PIVOTED - each sample is one row)
            var rawData = await GetParsedDataAsync(request.ProjectId);
            if (!rawData.Any())
                return Result<DriftCorrectionResult>.Fail("No data found for project");

            _logger.LogInformation("AnalyzeDrift: {Count} samples loaded", rawData.Count);

            // Detect segments (Python-compatible)
            var segments = DetectSegments(rawData, request.BasePattern, request.ConePattern);

            // Get elements to analyze
            var elements = request.SelectedElements ?? GetAllElements(rawData);

            // Calculate segment-specific ratios
            var segmentRatios = CalculateSegmentRatios(rawData, segments, elements);

            // Calculate drift info for each element
            var elementDrifts = new Dictionary<string, ElementDriftInfo>();
            foreach (var element in elements)
            {
                var driftInfo = CalculateElementDriftPiecewise(rawData, element, segments, segmentRatios);
                if (driftInfo != null)
                    elementDrifts[element] = driftInfo;
            }

            var result = new DriftCorrectionResult(
                TotalSamples: rawData.Count,
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
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

            if (project == null)
                return Result<DriftCorrectionResult>.Fail("Project not found");

            var rawData = await GetParsedDataAsync(request.ProjectId);
            if (!rawData.Any())
                return Result<DriftCorrectionResult>.Fail("No data found for project");

            var segments = DetectSegments(rawData, request.BasePattern, request.ConePattern);
            var elements = request.SelectedElements ?? GetAllElements(rawData);
            var segmentRatios = CalculateSegmentRatios(rawData, segments, elements);

            var elementDrifts = new Dictionary<string, ElementDriftInfo>();
            foreach (var element in elements)
            {
                var driftInfo = CalculateElementDriftPiecewise(rawData, element, segments, segmentRatios);
                if (driftInfo != null)
                    elementDrifts[element] = driftInfo;
            }

            // Apply correction based on method
            var correctedData = request.Method switch
            {
                DriftMethod.Linear => ApplyLinearCorrectionPiecewise(rawData, segments, elements, segmentRatios),
                DriftMethod.Stepwise => ApplyStepwiseCorrectionArithmetic(rawData, segments, elements, segmentRatios),
                DriftMethod.Polynomial => ApplyPolynomialCorrection(rawData, elements),
                _ => rawData.Select((d, i) => new CorrectedSampleDto(
                    d.SolutionLabel,
                    i,
                    0,
                    d.Values,
                    d.Values,
                    new Dictionary<string, decimal>()
                )).ToList()
            };

            var result = new DriftCorrectionResult(
                TotalSamples: rawData.Count,
                CorrectedSamples: correctedData.Count(c => c.CorrectionFactors.Any()),
                SegmentsFound: segments.Count,
                Segments: segments,
                ElementDrifts: elementDrifts,
                CorrectedData: correctedData
            );

            _logger.LogInformation(
                "Drift correction applied: Method={Method}, Segments={Segments}, CorrectedSamples={Corrected}",
                request.Method, segments.Count, result.CorrectedSamples);

            return Result<DriftCorrectionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply drift correction for project {ProjectId}", request.ProjectId);
            return Result<DriftCorrectionResult>.Fail($"Failed to apply drift correction: {ex.Message}");
        }
    }

    public async Task<Result<List<DriftSegment>>> DetectSegmentsAsync(
        Guid projectId,
        string? basePattern = null,
        string? conePattern = null)
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
        Guid projectId,
        List<string>? elements = null)
    {
        try
        {
            var rawData = await GetParsedDataAsync(projectId);
            if (!rawData.Any())
                return Result<Dictionary<string, List<decimal>>>.Fail("No data found");

            var allElements = elements ?? GetAllElements(rawData);
            var ratios = new Dictionary<string, List<decimal>>();

            var standardIndices = rawData
                .Select((d, i) => (Data: d, Index: i))
                .Where(x => IsStandardSample(x.Data.SolutionLabel))
                .Select(x => x.Index)
                .ToList();

            foreach (var element in allElements)
            {
                var elementRatios = new List<decimal>();

                for (int i = 1; i < standardIndices.Count; i++)
                {
                    var prevIdx = standardIndices[i - 1];
                    var currIdx = standardIndices[i];

                    var prevValue = GetElementValue(rawData[prevIdx], element);
                    var currValue = GetElementValue(rawData[currIdx], element);

                    if (prevValue.HasValue && currValue.HasValue && prevValue.Value != 0)
                    {
                        var ratio = currValue.Value / prevValue.Value;
                        elementRatios.Add(ratio);
                    }
                }

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

    public async Task<Result<SlopeOptimizationResult>> OptimizeSlopeAsync(SlopeOptimizationRequest request)
    {
        try
        {
            var rawData = await GetParsedDataAsync(request.ProjectId);
            if (!rawData.Any())
                return Result<SlopeOptimizationResult>.Fail("No data found");

            var values = rawData
                .Select((d, i) => (Index: i, Value: GetElementValue(d, request.Element)))
                .Where(x => x.Value.HasValue)
                .ToList();

            if (values.Count < 2)
                return Result<SlopeOptimizationResult>.Fail("Not enough data points for slope optimization");

            var xData = values.Select(v => (double)v.Index).ToArray();
            var yData = values.Select(v => (double)v.Value!.Value).ToArray();

            var (intercept, slope) = Fit.Line(xData, yData);

            double newSlope = request.Action switch
            {
                SlopeAction.ZeroSlope => 0,
                SlopeAction.RotateUp => slope + Math.Abs(slope) * 0.1,
                SlopeAction.RotateDown => slope - Math.Abs(slope) * 0.1,
                SlopeAction.SetCustom => (double)(request.TargetSlope ?? 0),
                _ => slope
            };

            var centerX = xData.Average();
            var centerY = yData.Average();
            var newIntercept = centerY - newSlope * centerX;

            var correctedData = new List<CorrectedSampleDto>();
            for (int i = 0; i < rawData.Count; i++)
            {
                var originalValue = GetElementValue(rawData[i], request.Element);
                var correctionFactor = 1.0m;

                if (originalValue.HasValue)
                {
                    var originalFitted = intercept + slope * i;
                    var newFitted = newIntercept + newSlope * i;

                    if (originalFitted != 0)
                        correctionFactor = (decimal)(newFitted / originalFitted);
                }

                var correctedValues = new Dictionary<string, decimal?>(rawData[i].Values);
                if (originalValue.HasValue)
                    correctedValues[request.Element] = originalValue.Value * correctionFactor;

                correctedData.Add(new CorrectedSampleDto(
                    rawData[i].SolutionLabel,
                    i,
                    0,
                    rawData[i].Values,
                    correctedValues,
                    new Dictionary<string, decimal> { { request.Element, correctionFactor } }
                ));
            }

            return Result<SlopeOptimizationResult>.Success(new SlopeOptimizationResult(
                request.Element,
                (decimal)slope,
                (decimal)newSlope,
                (decimal)intercept,
                (decimal)newIntercept,
                correctedData
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize slope for project {ProjectId}", request.ProjectId);
            return Result<SlopeOptimizationResult>.Fail($"Failed to optimize slope: {ex.Message}");
        }
    }

    public async Task<Result<SlopeOptimizationResult>> ZeroSlopeAsync(Guid projectId, string element)
    {
        return await OptimizeSlopeAsync(new SlopeOptimizationRequest(projectId, element, SlopeAction.ZeroSlope));
    }

    #endregion

    #region Private Helper Methods - Data Access (PIVOTED - Python Compatible)

    /// <summary>
    /// Get parsed data with PIVOT - each sample becomes one row with all elements
    /// This matches Python's pivot_table behavior
    /// </summary>
    private async Task<List<ParsedRow>> GetParsedDataAsync(Guid projectId)
    {
        var rawRows = await _db.RawDataRows
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.DataId)
            .ToListAsync();

        // Step 1: Group by Solution Label and pivot
        var pivotedData = new Dictionary<string, Dictionary<string, decimal?>>();
        var sampleOrder = new List<string>();

        foreach (var row in rawRows)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                if (data == null) continue;

                // Get Solution Label
                var solutionLabel = data.TryGetValue("Solution Label", out var sl)
                    ? sl.GetString() ?? row.SampleId ?? $"Row_{row.DataId}"
                    : row.SampleId ?? $"Row_{row.DataId}";

                // Get Element name
                string? elementName = null;
                if (data.TryGetValue("Element", out var elemProp))
                {
                    elementName = elemProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(elementName))
                    continue;

                // Initialize sample if not exists
                if (!pivotedData.ContainsKey(solutionLabel))
                {
                    pivotedData[solutionLabel] = new Dictionary<string, decimal?>();
                    sampleOrder.Add(solutionLabel);
                }

                // Get value (prefer Soln Conc for drift, like Python uses Corr Con)
                decimal? value = null;

                // Try Corr Con first (Python uses this for RM check)
                if (data.TryGetValue("Corr Con", out var corrCon))
                {
                    value = ExtractDecimalValue(corrCon);
                }

                // Fallback to Soln Conc
                if (!value.HasValue && data.TryGetValue("Soln Conc", out var solnConc))
                {
                    value = ExtractDecimalValue(solnConc);
                }

                pivotedData[solutionLabel][elementName] = value;
            }
            catch
            {
                // Skip malformed rows
            }
        }

        // Step 2: Convert to ParsedRow list (maintaining original order)
        var result = new List<ParsedRow>();
        foreach (var sampleLabel in sampleOrder)
        {
            if (pivotedData.TryGetValue(sampleLabel, out var values))
            {
                result.Add(new ParsedRow(sampleLabel, values));
            }
        }

        _logger.LogInformation("GetParsedDataAsync: {RawRows} raw rows → {Samples} samples (pivoted)",
            rawRows.Count, result.Count);

        return result;
    }

    private decimal? ExtractDecimalValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDecimal();
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var strVal = element.GetString();
            if (!string.IsNullOrWhiteSpace(strVal) && strVal != "-----" &&
                decimal.TryParse(strVal, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    #endregion

    #region Private Helper Methods - Segment Detection (Python-Compatible)

    /// <summary>
    /// Detect segments based on RM/Standard positions
    /// Python-compatible logic from RM_check.py
    /// </summary>
    private List<DriftSegment> DetectSegments(List<ParsedRow> data, string? basePattern, string? conePattern)
    {
        var segments = new List<DriftSegment>();

        // Use provided patterns or defaults
        var effectiveBasePattern = basePattern ?? DefaultBasePattern;
        var baseRegex = new Regex(effectiveBasePattern, RegexOptions.IgnoreCase);

        Regex? coneRegex = null;
        if (!string.IsNullOrWhiteSpace(conePattern))
        {
            coneRegex = new Regex(conePattern, RegexOptions.IgnoreCase);
        }

        // Step 1: Identify all RM/Standard positions with their types
        var standardIndices = new List<(int Index, string Label, int RmNum, string RmType)>();

        for (int i = 0; i < data.Count; i++)
        {
            var label = data[i].SolutionLabel;
            string rmType = "Unknown";
            int rmNum = 0;

            // Check basePattern first (Python: label.str.match(rf'^{keyword}'))
            if (baseRegex.IsMatch(label))
            {
                (rmNum, rmType) = ExtractRmInfo(label, effectiveBasePattern.TrimStart('^').Split('|')[0].TrimStart('('));
                standardIndices.Add((i, label, rmNum, rmType));
            }
            // Check conePattern if provided
            else if (coneRegex != null && coneRegex.IsMatch(label))
            {
                (rmNum, _) = ExtractRmInfo(label, "CONE");
                standardIndices.Add((i, label, rmNum, "Cone"));
            }
            // Check RmPattern for other standards (OREAS, CRM, etc.)
            else if (RmPattern.IsMatch(label))
            {
                (rmNum, rmType) = ExtractRmInfo(label, "RM");
                standardIndices.Add((i, label, rmNum, rmType));
            }
        }

        _logger.LogInformation("DetectSegments: Found {Count} standards", standardIndices.Count);

        if (standardIndices.Count < 2)
        {
            // Single segment for entire data
            segments.Add(new DriftSegment(0, 0, data.Count - 1, null, null, data.Count));
            return segments;
        }

        // Step 2: Python-compatible segmentation with Cone detection
        int currentSegmentIndex = 0;
        int? refRmNum = null;
        var segmentStarts = new List<int> { 0 };

        for (int i = 0; i < standardIndices.Count; i++)
        {
            var (index, label, rmNum, rmType) = standardIndices[i];

            // Cone detection → start new segment
            if (rmType == "Cone")
            {
                currentSegmentIndex++;
                refRmNum = null;
                segmentStarts.Add(index);
            }

            // First Base/Check in segment becomes reference
            if (refRmNum == null && (rmType == "Base" || rmType == "Check"))
            {
                refRmNum = rmNum;
            }
        }

        // Step 3: Create segments between consecutive standards
        for (int i = 0; i < standardIndices.Count - 1; i++)
        {
            var start = standardIndices[i];
            var end = standardIndices[i + 1];

            int segIdx = 0;
            for (int s = segmentStarts.Count - 1; s >= 0; s--)
            {
                if (start.Index >= segmentStarts[s])
                {
                    segIdx = s;
                    break;
                }
            }

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

    private bool IsStandardSample(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        // Check default RM pattern
        var defaultRegex = new Regex(DefaultBasePattern, RegexOptions.IgnoreCase);
        if (defaultRegex.IsMatch(label))
            return true;

        return RmPattern.IsMatch(label);
    }

    private decimal? GetElementValue(ParsedRow row, string element)
    {
        return row.Values.TryGetValue(element, out var val) ? val : null;
    }

    #endregion

    #region Private Helper Methods - Ratio Calculation

    private Dictionary<int, Dictionary<string, decimal>> CalculateSegmentRatios(
        List<ParsedRow> data,
        List<DriftSegment> segments,
        List<string> elements)
    {
        var ratios = new Dictionary<int, Dictionary<string, decimal>>();

        foreach (var segment in segments)
        {
            var segmentRatios = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var startValue = GetElementValue(data[segment.StartIndex], element);
                var endValue = GetElementValue(data[segment.EndIndex], element);

                if (startValue.HasValue && endValue.HasValue && startValue.Value != 0)
                {
                    segmentRatios[element] = endValue.Value / startValue.Value;
                }
                else
                {
                    segmentRatios[element] = 1.0m;
                }
            }

            ratios[segment.SegmentIndex] = segmentRatios;
        }

        return ratios;
    }

    private ElementDriftInfo? CalculateElementDriftPiecewise(
        List<ParsedRow> data,
        string element,
        List<DriftSegment> segments,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios)
    {
        var values = data
            .Select((d, i) => (Index: i, Value: GetElementValue(d, element)))
            .Where(x => x.Value.HasValue)
            .ToList();

        if (values.Count < 2)
            return null;

        var xData = values.Select(v => (double)v.Index).ToArray();
        var yData = values.Select(v => (double)v.Value!.Value).ToArray();

        var (intercept, slope) = Fit.Line(xData, yData);

        var firstValue = yData.First();
        var lastValue = yData.Last();
        var driftPercent = firstValue != 0 ? ((lastValue - firstValue) / Math.Abs(firstValue)) * 100 : 0;

        return new ElementDriftInfo(
            element,
            1.0m,
            1.0m,
            (decimal)driftPercent,
            (decimal)slope,
            (decimal)intercept
        );
    }

    #endregion

    #region Private Helper Methods - Correction Application

    private List<CorrectedSampleDto> ApplyLinearCorrectionPiecewise(
        List<ParsedRow> data,
        List<DriftSegment> segments,
        List<string> elements,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios)
    {
        var result = new List<CorrectedSampleDto>();

        for (int i = 0; i < data.Count; i++)
        {
            var segment = segments.FirstOrDefault(s => i >= s.StartIndex && i <= s.EndIndex);
            var segmentIndex = segment?.SegmentIndex ?? 0;

            var correctedValues = new Dictionary<string, decimal?>();
            var correctionFactors = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var originalValue = GetElementValue(data[i], element);

                if (originalValue.HasValue && segment != null &&
                    segmentRatios.TryGetValue(segmentIndex, out var ratios) &&
                    ratios.TryGetValue(element, out var ratio))
                {
                    int n = segment.EndIndex - segment.StartIndex;
                    if (n > 0)
                    {
                        decimal progress = (decimal)(i - segment.StartIndex) / n;
                        decimal effectiveRatio = 1.0m + (ratio - 1.0m) * progress;

                        if (effectiveRatio != 0)
                        {
                            correctedValues[element] = originalValue.Value / effectiveRatio;
                            correctionFactors[element] = 1.0m / effectiveRatio;
                        }
                        else
                        {
                            correctedValues[element] = originalValue;
                        }
                    }
                    else
                    {
                        correctedValues[element] = originalValue;
                    }
                }
                else
                {
                    correctedValues[element] = originalValue;
                }
            }

            result.Add(new CorrectedSampleDto(
                data[i].SolutionLabel,
                i,
                segmentIndex,
                data[i].Values,
                correctedValues,
                correctionFactors
            ));
        }

        return result;
    }

    private List<CorrectedSampleDto> ApplyStepwiseCorrectionArithmetic(
        List<ParsedRow> data,
        List<DriftSegment> segments,
        List<string> elements,
        Dictionary<int, Dictionary<string, decimal>> segmentRatios)
    {
        var result = new List<CorrectedSampleDto>();

        for (int i = 0; i < data.Count; i++)
        {
            var segment = segments.FirstOrDefault(s => i >= s.StartIndex && i <= s.EndIndex);
            var segmentIndex = segment?.SegmentIndex ?? 0;

            var correctedValues = new Dictionary<string, decimal?>();
            var correctionFactors = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var originalValue = GetElementValue(data[i], element);

                if (originalValue.HasValue && segment != null &&
                    segmentRatios.TryGetValue(segmentIndex, out var ratios) &&
                    ratios.TryGetValue(element, out var ratio))
                {
                    // Python formula from RM_check.py:
                    // delta = ratio - 1.0
                    // step_delta = delta / n
                    // effective_ratio = 1.0 + step_delta * (step_index + 1)
                    // corrected = original * effective_ratio

                    decimal delta = ratio - 1.0m;
                    int n = segment.EndIndex - segment.StartIndex;
                    decimal stepDelta = n > 0 ? delta / n : 0;
                    int stepIndex = i - segment.StartIndex;

                    decimal effectiveRatio;
                    if (i == segment.StartIndex)
                    {
                        effectiveRatio = 1.0m;
                    }
                    else
                    {
                        effectiveRatio = 1.0m + (stepDelta * stepIndex);
                    }

                    correctedValues[element] = originalValue.Value * effectiveRatio;
                    correctionFactors[element] = effectiveRatio;
                }
                else
                {
                    correctedValues[element] = originalValue;
                }
            }

            result.Add(new CorrectedSampleDto(
                data[i].SolutionLabel,
                i,
                segmentIndex,
                data[i].Values,
                correctedValues,
                correctionFactors
            ));
        }

        return result;
    }

    private List<CorrectedSampleDto> ApplyPolynomialCorrection(List<ParsedRow> data, List<string> elements)
    {
        var result = new List<CorrectedSampleDto>();
        var polynomialFits = new Dictionary<string, (double[] Coefficients, double Mean)>();

        foreach (var element in elements)
        {
            var values = data
                .Select((d, i) => (Index: i, Value: GetElementValue(d, element)))
                .Where(x => x.Value.HasValue)
                .ToList();

            if (values.Count >= 3)
            {
                var xData = values.Select(v => (double)v.Index).ToArray();
                var yData = values.Select(v => (double)v.Value!.Value).ToArray();

                var coefficients = Fit.Polynomial(xData, yData, 2);
                var mean = yData.Average();

                polynomialFits[element] = (coefficients, mean);
            }
        }

        for (int i = 0; i < data.Count; i++)
        {
            var correctedValues = new Dictionary<string, decimal?>();
            var correctionFactors = new Dictionary<string, decimal>();

            foreach (var element in elements)
            {
                var originalValue = GetElementValue(data[i], element);

                if (originalValue.HasValue && polynomialFits.TryGetValue(element, out var fit))
                {
                    var fittedValue = fit.Coefficients[0] + fit.Coefficients[1] * i + fit.Coefficients[2] * i * i;
                    var factor = fittedValue != 0 ? (decimal)(fit.Mean / fittedValue) : 1.0m;

                    correctedValues[element] = originalValue.Value * factor;
                    correctionFactors[element] = factor;
                }
                else
                {
                    correctedValues[element] = originalValue;
                }
            }

            result.Add(new CorrectedSampleDto(
                data[i].SolutionLabel,
                i,
                0,
                data[i].Values,
                correctedValues,
                correctionFactors
            ));
        }

        return result;
    }

    #endregion

    #region Private Helper Methods - RM Info Extraction (Python-Compatible)

    /// <summary>
    /// Extract RM number and type from Solution Label
    /// Based on Python RM_check.py extract_rm_info() function
    /// </summary>
    private static (int RmNumber, string RmType) ExtractRmInfo(string label, string keyword = "RM")
    {
        label = (label ?? "").Trim();
        var labelLower = label.ToLower();

        var keywordPattern = new Regex($@"^{Regex.Escape(keyword)}\s*[-_]?\s*", RegexOptions.IgnoreCase);
        var cleaned = keywordPattern.Replace(labelLower, "");

        var rmType = "Base";
        var rmNumber = 0;

        var typeMatch = Regex.Match(cleaned, @"(chek|check|cone)");
        string beforeText;

        if (typeMatch.Success)
        {
            var typ = typeMatch.Groups[1].Value;
            rmType = typ is "chek" or "check" ? "Check" : "Cone";
            beforeText = cleaned.Substring(0, typeMatch.Index);
        }
        else
        {
            beforeText = cleaned;
        }

        var numbers = Regex.Matches(beforeText, @"\d+");
        if (numbers.Count > 0)
        {
            rmNumber = int.Parse(numbers[numbers.Count - 1].Value);
        }

        return (rmNumber, rmType);
    }

    #endregion

    #region Private Types

    private record ParsedRow(string SolutionLabel, Dictionary<string, decimal?> Values);

    #endregion
}