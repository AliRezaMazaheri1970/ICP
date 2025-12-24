using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

/// <summary>
/// Blank & Scale optimization service (Python-aligned).
/// Optimization uses blank rows to compute blank adjustment, but exposed Blank values are the
/// effective blanks to subtract: corrected = (original - effectiveBlank) * scale.
///
/// Key fixes:
/// - CRM duplicate-safe (CrmId duplicates won't crash)
/// - MatchWithCrm expects Dictionary CRM map
/// - ApplyManualBlankScaleAsync persists to DB + creates Undo snapshot (Drift-like)
/// </summary>
public class OptimizationService : IOptimizationService
{
    private readonly IsatisDbContext _db;
    private readonly ILogger<OptimizationService> _logger;
    private Random _random;
    private static readonly string[] CrmIds = { "258", "252", "906", "506", "233", "255", "263", "260" };
    private static readonly Regex CrmPattern = new Regex(
        $@"(?i)(?:(?:^|(?<=\s))(?:CRM|OREAS)?\s*({string.Join("|", CrmIds)})(?:[a-zA-Z0-9]{{0,2}})?\b)",
        RegexOptions.Compiled);
    private static readonly Regex BlankPattern = new Regex(
        @"(?i)(?:CRM\s*)?(?:BLANK|BLNK|Blank|blnk|blank)(?:\s+.*)?",
        RegexOptions.Compiled);

    private const double DefaultCR = 0.7;     // scipy-like default crossover
    private const double Tolerance = 0.01;    // convergence tolerance
    private const int ConvergenceWindow = 10; // generations without improvement

    public OptimizationService(IsatisDbContext db, ILogger<OptimizationService> logger)
    {
        _db = db;
        _logger = logger;
        _random = new Random();
    }

    // ---------------------------
    // Public API
    // ---------------------------

    public async Task<Result<BlankScaleOptimizationResult>> OptimizeBlankScaleAsync(BlankScaleOptimizationRequest request)
    {
        try
        {
            // 1. تنظیم Seed برای تکرارپذیری (در صورت نیاز)
            if (request.Seed.HasValue)
                _random = new Random(request.Seed.Value);

            // 2. دریافت داده‌های پروژه (Read-Only برای محاسبات)
            var crmData = await GetCrmDataAsync();
            if (crmData.Count == 0)
                return Result<BlankScaleOptimizationResult>.Fail("No CRM data found");

            var projectData = await GetProjectRmDataAsync(request.ProjectId, crmData);
            if (projectData.Count == 0)
                return Result<BlankScaleOptimizationResult>.Fail("No RM samples found in project");
            var baseBlanks = GetBaseBlankValues(projectData);

            // 4. تطبیق داده‌های پروژه با CRM
            var matchedData = MatchWithCrm(projectData, crmData);
            if (matchedData.Count == 0)
                return Result<BlankScaleOptimizationResult>.Fail("No matching CRM data found for RM samples");

            // 5. مشخص کردن عناصر مورد نظر
            var elements = (request.Elements != null && request.Elements.Count > 0)
                ? request.Elements
                : GetCommonElements(matchedData);

            // 6. محاسبه آمار اولیه (قبل از بهینه‌سازی)
            var initialStats = CalculateStatistics(matchedData, elements, 0m, 1m, request.MinDiffPercent, request.MaxDiffPercent);

            var elementOptimizations = new Dictionary<string, ElementOptimization>(StringComparer.OrdinalIgnoreCase);
            var bestBlanks = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var bestScales = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            // 7. شروع حلقه بهینه‌سازی (محاسبات ریاضی)
            foreach (var element in elements)
            {
                string selectedModel;
                decimal optimalBlank, optimalScale;
                int passedAfter;

                if (request.UseMultiModel)
                {
                    (optimalBlank, optimalScale, passedAfter, selectedModel) = OptimizeElementMultiModel(
                        matchedData, element,
                        request.MinDiffPercent, request.MaxDiffPercent,
                        request.MaxIterations, request.PopulationSize);
                }
                else
                {
                    (optimalBlank, optimalScale, passedAfter) = OptimizeElementImproved(
                        matchedData, element,
                        request.MinDiffPercent, request.MaxDiffPercent,
                        request.MaxIterations, request.PopulationSize);
                    selectedModel = "A";
                }

                // ذخیره وضعیت آماری
                initialStats.ElementStats.TryGetValue(element, out var before);
                var passedBefore = before?.Passed ?? 0;
                var meanBefore = before?.MeanDiff ?? 0m;
                var meanAfter = CalculateMeanDiff(matchedData, element, optimalBlank, optimalScale);
                var baseBlank = GetBaseBlankValue(matchedData, element);
                var effectiveBlank = baseBlank - optimalBlank;

                elementOptimizations[element] = new ElementOptimization(
                    element,
                    effectiveBlank,
                    optimalScale,
                    passedBefore,
                    passedAfter,
                    meanBefore,
                    meanAfter,
                    selectedModel
                );

                // ذخیره بهترین مقادیر پیدا شده در دیکشنری موقت
                bestBlanks[element] = optimalBlank;
                bestScales[element] = optimalScale;
            }

            // =================================================================================
            // [اصلاح جدید] شروع فاز ذخیره‌سازی در دیتابیس
            // =================================================================================

            // الف) ایجاد اسنپ‌شات برای Undo (فقط یک بار برای کل عملیات)
            if (!request.PreviewOnly)
            {
                await SaveUndoStateAsync(request.ProjectId, "Optimization: Auto Blank/Scale (All Elements)");

                // ?) ??? ???? ???????? ??????? ???? ????? ??????? (Tracking ???? ???)
                var rowsToUpdate = await _db.RawDataRows
                    .Where(r => r.ProjectId == request.ProjectId)
                    .ToListAsync();

                var projectToUpdate = await _db.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

                int totalDbUpdates = 0;

                // ?) ????? ??????? ??? JSON ?? ???? ?? ???? ?????? ????? ???
                foreach (var element in elements)
                {
                    if (bestBlanks.TryGetValue(element, out var b) && bestScales.TryGetValue(element, out var s))
                    {
                        // ??? ??? ?????? ColumnData ?? ?? rowsToUpdate ????? ??????
                        totalDbUpdates += ApplyBlankScaleToDatabase(rowsToUpdate, element, b, s, baseBlanks);
                    }
                }

                // ?) ??? ???? ??????? ? ????? ????? ?? ???????
                if (projectToUpdate != null)
                {
                    projectToUpdate.LastModifiedAt = DateTime.UtcNow;
                }

                if (totalDbUpdates > 0)
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Auto-optimization applied to DB. Project: {Id}, Updated Rows: {Count}", request.ProjectId, totalDbUpdates);
                }
                else
                {
                    _logger.LogWarning("Auto-optimization calculated but no rows were updated in DB. Project: {Id}", request.ProjectId);
                }
            }
            else
            {
                _logger.LogInformation("PreviewOnly optimization requested. Skipping DB updates. Project: {Id}", request.ProjectId);
            }

            // =================================================================================
            // پایان فاز ذخیره‌سازی
            // =================================================================================

            // 8. آماده‌سازی داده‌های خروجی برای نمایش به کاربر
            var optimizedData = BuildOptimizedData(
                matchedData,
                elements,
                bestBlanks,
                bestScales,
                request.MinDiffPercent,
                request.MaxDiffPercent);

            var totalPassedBefore = elementOptimizations.Values.Sum(x => x.PassedBefore);
            var totalPassedAfter = elementOptimizations.Values.Sum(x => x.PassedAfter);

            var improvement = totalPassedBefore > 0
                ? ((decimal)(totalPassedAfter - totalPassedBefore) / totalPassedBefore) * 100m
                : 0m;

            MultiModelSummary? modelSummary = null;
            if (request.UseMultiModel)
            {
                var modelACounts = elementOptimizations.Values.Count(e => e.SelectedModel == "A");
                var modelBCounts = elementOptimizations.Values.Count(e => e.SelectedModel == "B");
                var modelCCounts = elementOptimizations.Values.Count(e => e.SelectedModel == "C");

                var mostUsedModel = new[] { ("A", modelACounts), ("B", modelBCounts), ("C", modelCCounts) }
                    .OrderByDescending(x => x.Item2).First().Item1;

                modelSummary = new MultiModelSummary(
                    modelACounts,
                    modelBCounts,
                    modelCCounts,
                    mostUsedModel,
                    $"Model A: {modelACounts} elements, Model B: {modelBCounts} elements, Model C: {modelCCounts} elements");
            }

            return Result<BlankScaleOptimizationResult>.Success(new BlankScaleOptimizationResult(
                matchedData.Count,
                totalPassedBefore,
                totalPassedAfter,
                improvement,
                elementOptimizations,
                optimizedData,
                modelSummary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OptimizeBlankScale failed for {ProjectId}", request.ProjectId);
            return Result<BlankScaleOptimizationResult>.Fail("Failed to optimize: " + ex.Message);
        }
    }

    /// <summary>
    /// APPLY manual blank/scale (Drift-like):
    /// - Create snapshot for undo
    /// - Update DB ("Corr Con") for the target element
    /// </summary>
    public async Task<Result<ManualBlankScaleResult>> ApplyManualBlankScaleAsync(ManualBlankScaleRequest request)
    {
        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);
            if (project == null)
                return Result<ManualBlankScaleResult>.Fail("Project not found");

            // Snapshot BEFORE apply
            await SaveUndoStateAsync(request.ProjectId, $"Optimization(ManualBlankScale:{request.Element})");

            // Apply to DB
            var trackedRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .OrderBy(r => r.DataId)
                .ToListAsync();

            var crmData = await GetCrmDataAsync();
            var projectData = await GetProjectRmDataAsync(request.ProjectId, crmData);
            var blankAdjust = GetBaseBlankValue(projectData, request.Element) - request.Blank;
            var baseBlanks = GetBaseBlankValues(projectData);

            var updated = ApplyBlankScaleToDatabase(trackedRows, request.Element, blankAdjust, request.Scale, baseBlanks);

            project.LastModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "ManualBlankScale applied. Project={ProjectId} Element={Element} Blank={Blank} Scale={Scale} UpdatedRows={Updated}",
                request.ProjectId, request.Element, request.Blank, request.Scale, updated);

            // Return preview-style result for UI
            return await PreviewBlankScaleAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApplyManualBlankScale failed for {ProjectId}", request.ProjectId);
            return Result<ManualBlankScaleResult>.Fail("Failed to apply: " + ex.Message);
        }
    }

    public async Task<Result<ManualBlankScaleResult>> PreviewBlankScaleAsync(ManualBlankScaleRequest request)
    {
        try
        {
            var crmData = await GetCrmDataAsync();
            var projectData = await GetProjectRmDataAsync(request.ProjectId, crmData);
            var matchedData = MatchWithCrm(projectData, crmData);

            if (matchedData.Count == 0)
                return Result<ManualBlankScaleResult>.Fail("No matching CRM data found");

            var elements = new List<string> { request.Element };
            var blankAdjust = GetBaseBlankValue(matchedData, request.Element) - request.Blank;
            var blanks = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [request.Element] = blankAdjust };
            var scales = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [request.Element] = request.Scale };

            var beforeStats = CalculateStatistics(matchedData, elements, 0m, 1m, -10m, 10m);
            var afterStats = CalculateStatistics(matchedData, elements, blankAdjust, request.Scale, -10m, 10m);

            var optimizedData = BuildOptimizedData(matchedData, elements, blanks, scales, -10m, 10m);

            var passedBefore = beforeStats.ElementStats.TryGetValue(request.Element, out var bs) ? bs.Passed : 0;
            var passedAfter = afterStats.ElementStats.TryGetValue(request.Element, out var afs) ? afs.Passed : 0;

            return Result<ManualBlankScaleResult>.Success(new ManualBlankScaleResult(
                request.Element, request.Blank, request.Scale, passedBefore, passedAfter, optimizedData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PreviewBlankScale failed");
            return Result<ManualBlankScaleResult>.Fail("Failed to preview: " + ex.Message);
        }
    }

    public async Task<Result<BlankScaleOptimizationResult>> GetCurrentStatisticsAsync(Guid projectId, decimal minDiff = -10m, decimal maxDiff = 10m)
    {
        try
        {
            var crmData = await GetCrmDataAsync();
            var projectData = await GetProjectRmDataAsync(projectId, crmData);
            var matchedData = MatchWithCrm(projectData, crmData);

            if (matchedData.Count == 0)
                return Result<BlankScaleOptimizationResult>.Fail("No matching CRM data found");

            var elements = GetCommonElements(matchedData);
            var stats = CalculateStatistics(matchedData, elements, 0m, 1m, minDiff, maxDiff);

            var elementOptimizations = elements.ToDictionary(
                e => e,
                e =>
                {
                    stats.ElementStats.TryGetValue(e, out var s);
                    var baseBlank = GetBaseBlankValue(matchedData, e);
                    return new ElementOptimization(e, baseBlank, 1m, s?.Passed ?? 0, s?.Passed ?? 0, s?.MeanDiff ?? 0m, s?.MeanDiff ?? 0m);
                },
                StringComparer.OrdinalIgnoreCase);

            var blanks = elements.ToDictionary(e => e, _ => 0m, StringComparer.OrdinalIgnoreCase);
            var scales = elements.ToDictionary(e => e, _ => 1m, StringComparer.OrdinalIgnoreCase);

            var optimizedData = BuildOptimizedData(matchedData, elements, blanks, scales, minDiff, maxDiff);

            return Result<BlankScaleOptimizationResult>.Success(new BlankScaleOptimizationResult(
                matchedData.Count,
                stats.TotalPassed,
                stats.TotalPassed,
                0m,
                elementOptimizations,
                optimizedData,
                null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentStatistics failed for {ProjectId}", projectId);
            return Result<BlankScaleOptimizationResult>.Fail("Failed to get statistics: " + ex.Message);
        }
    }

    public async Task<object> GetDebugSamplesAsync(Guid projectId)
    {
        var labels = await _db.RawDataRows.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => r.SampleId)
            .Where(x => x != null && x != "")
            .Distinct()
            .ToListAsync();

        var crmIds = await _db.CrmData.AsNoTracking()
            .Select(c => c.CrmId)
            .Where(x => x != null && x != "")
            .Distinct()
            .ToListAsync();

        return new
        {
            totalLabels = labels.Count,
            sampleLabels = labels.Take(50).ToList(),
            totalCrm = crmIds.Count,
            sampleCrm = crmIds.Take(50).ToList()
        };
    }

    // ---------------------------
    // Snapshot + DB Apply (Drift-like)
    // ---------------------------

    private async Task SaveUndoStateAsync(Guid projectId, string operation)
    {
        var rows = await _db.RawDataRows.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.DataId)
            .Select(r => new SavedRowData
            {
                DataId = (int)r.DataId,
                SampleId = r.SampleId,
                ColumnData = r.ColumnData ?? ""
            })
            .ToListAsync();

        var json = JsonSerializer.Serialize(rows);

        _db.ProjectStates.Add(new ProjectState
        {
            ProjectId = projectId,
            Data = json,
            Description = $"Undo:{operation}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Apply Python formula: corrected = (original - blank_val + blank_adjust) * scale.
    /// </summary>
    private int ApplyBlankScaleToDatabase(
        List<Domain.Entities.RawDataRow> trackedRows,
        string targetElement,
        decimal blankAdjust,
        decimal scale,
        Dictionary<string, decimal>? blankValues = null)
    {
        if (trackedRows.Count == 0) return 0;

        // Get blank_val for target element
        var blankVal = blankValues != null && blankValues.TryGetValue(targetElement, out var bv) ? bv : 0m;

        int updated = 0;

        foreach (var row in trackedRows)
        {
            if (string.IsNullOrWhiteSpace(row.ColumnData))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(row.ColumnData);
                var root = doc.RootElement;

                // Element
                string? rawElement = null;
                if (root.TryGetProperty("Element", out var elProp))
                    rawElement = elProp.ValueKind == JsonValueKind.String ? elProp.GetString() : elProp.ToString();

                if (string.IsNullOrWhiteSpace(rawElement))
                    continue;

                var cleanElement = ExtractElementKey(rawElement);
                if (string.IsNullOrWhiteSpace(cleanElement))
                    continue;
                if (!string.Equals(cleanElement, targetElement, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Corr Con
                if (!TryGetDecimal(root, "Corr Con", out var corrCon))
                    continue;

                // Python formula: (original - blank_val + blank_adjust) * scale
                var corrected = (corrCon - blankVal + blankAdjust) * scale;

                row.ColumnData = UpdateJsonNumber(row.ColumnData, "Corr Con", corrected);
                updated++;
            }
            catch
            {
                // ignore parse errors
            }
        }

        return updated;
    }

    private static bool TryGetDecimal(JsonElement root, string propName, out decimal value)
    {
        value = 0m;
        if (!root.TryGetProperty(propName, out var p))
            return false;

        if (p.ValueKind == JsonValueKind.Number)
            return p.TryGetDecimal(out value);

        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var d))
        {
            value = d;
            return true;
        }

        return false;
    }

    private static string UpdateJsonNumber(string json, string key, decimal newValue)
    {
        using var doc = JsonDocument.Parse(json);

        var props = new List<(string Name, JsonElement Value)>();
        foreach (var p in doc.RootElement.EnumerateObject())
            props.Add((p.Name, p.Value.Clone()));

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();

            bool wrote = false;
            foreach (var (name, val) in props)
            {
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteNumber(key, newValue);
                    wrote = true;
                }
                else
                {
                    writer.WritePropertyName(name);
                    val.WriteTo(writer);
                }
            }

            if (!wrote)
                writer.WriteNumber(key, newValue);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private sealed class SavedRowData
    {
        public int DataId { get; set; }
        public string? SampleId { get; set; }
        public string ColumnData { get; set; } = "";
    }

    private sealed record ParsedRow(
        string SolutionLabel,
        string Type,
        string Element,
        decimal? CorrCon,
        decimal? SolnConc);

    private static string? TryGetString(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var p))
            return null;

        if (p.ValueKind == JsonValueKind.String)
            return p.GetString();

        return p.ToString();
    }

    private static string ExtractElementKey(string rawElement)
    {
        if (string.IsNullOrWhiteSpace(rawElement))
            return "";

        var noUnderscore = rawElement.Split('_', StringSplitOptions.RemoveEmptyEntries)[0];
        var parts = noUnderscore.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : noUnderscore.Trim();
    }

    private static bool IsSampleType(string type)
    {
        return string.Equals(type, "Samp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "Sample", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildCrmIdLookup(Dictionary<string, Dictionary<string, decimal>> crmData)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var numberPattern = new Regex(@"(\d+)", RegexOptions.IgnoreCase);

        foreach (var key in crmData.Keys)
        {
            var m = numberPattern.Match(key);
            if (!m.Success)
                continue;

            var num = m.Groups[1].Value;
            if (!CrmIds.Contains(num))
                continue;

            if (!map.ContainsKey(num))
                map[num] = key;
        }

        return map;
    }

    private static string? ExtractCrmId(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var match = CrmPattern.Match(label);
        return match.Success ? match.Groups[1].Value : null;
    }

    private Dictionary<string, decimal> ComputeBestBlankValues(
        List<ParsedRow> rows,
        Dictionary<string, Dictionary<string, decimal>> crmData)
    {
        var blankCandidates = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        var crmSamples = new Dictionary<string, List<(decimal SampleValue, decimal CrmValue)>>(StringComparer.OrdinalIgnoreCase);
        var crmIdLookup = BuildCrmIdLookup(crmData);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.SolutionLabel) || string.IsNullOrWhiteSpace(row.Element))
                continue;

            if (!IsSampleType(row.Type))
                continue;

            if (BlankPattern.IsMatch(row.SolutionLabel))
            {
                var candidate = row.CorrCon ?? row.SolnConc;
                if (!candidate.HasValue)
                    continue;

                if (!blankCandidates.TryGetValue(row.Element, out var list))
                {
                    list = new List<decimal>();
                    blankCandidates[row.Element] = list;
                }

                list.Add(candidate.Value);
                continue;
            }

            var crmId = ExtractCrmId(row.SolutionLabel);
            if (crmId == null || !crmIdLookup.TryGetValue(crmId, out var crmKey))
                continue;

            if (!crmData.TryGetValue(crmKey, out var crmValues))
                continue;

            if (!crmValues.TryGetValue(row.Element, out var crmValue))
                continue;

            var sampleValue = row.CorrCon ?? row.SolnConc;
            if (!sampleValue.HasValue)
                continue;

            if (!crmSamples.TryGetValue(row.Element, out var list))
            {
                list = new List<(decimal SampleValue, decimal CrmValue)>();
                crmSamples[row.Element] = list;
            }

            list.Add((sampleValue.Value, crmValue));
        }

        var best = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in blankCandidates)
        {
            var element = kvp.Key;
            var candidates = kvp.Value;
            if (candidates.Count == 0)
                continue;

            if (!crmSamples.TryGetValue(element, out var samples) || samples.Count == 0)
            {
                best[element] = 0m;
                continue;
            }

            var bestBlank = 0m;
            var minDistance = decimal.MaxValue;
            var inRangeFound = false;

            foreach (var candidate in candidates)
            {
                foreach (var (sampleValue, crmValue) in samples)
                {
                    var corrected = sampleValue - candidate;
                    var range = CalculateDynamicRange(crmValue);
                    var lower = crmValue - range;
                    var upper = crmValue + range;

                    if (corrected >= lower && corrected <= upper)
                    {
                        bestBlank = candidate;
                        inRangeFound = true;
                        break;
                    }

                    var distance = Math.Abs(corrected - crmValue);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestBlank = candidate;
                    }
                }

                if (inRangeFound)
                    break;
            }

            best[element] = bestBlank;
        }

        return best;
    }

    // ---------------------------
    // Data Loading / Matching
    // ---------------------------

    private async Task<List<RmSampleData>> GetProjectRmDataAsync(
        Guid projectId,
        Dictionary<string, Dictionary<string, decimal>> crmData)
    {
        var rawRows = await _db.RawDataRows.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.DataId)
            .ToListAsync();

        var parsedRows = new List<ParsedRow>();

        foreach (var row in rawRows)
        {
            if (string.IsNullOrWhiteSpace(row.ColumnData))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(row.ColumnData);
                var root = doc.RootElement;

                var solutionLabel = TryGetString(root, "Solution Label") ?? row.SampleId ?? "";
                var type = TryGetString(root, "Type") ?? "";
                var elementRaw = TryGetString(root, "Element") ?? "";
                var element = ExtractElementKey(elementRaw);

                decimal? corrCon = TryGetDecimal(root, "Corr Con", out var cc) ? cc : null;
                decimal? solnConc = TryGetDecimal(root, "Soln Conc", out var sc) ? sc : null;

                parsedRows.Add(new ParsedRow(solutionLabel, type, element, corrCon, solnConc));
            }
            catch
            {
                // ignore broken json row
            }
        }

        var blankValues = ComputeBestBlankValues(parsedRows, crmData);
        var result = new List<RmSampleData>();
        var rmPattern = new Regex(@"^(OREAS|SRM|CRM|NIST|BCR|TILL|GBW|RM|PAR)", RegexOptions.IgnoreCase);

        foreach (var row in parsedRows)
        {
            if (string.IsNullOrWhiteSpace(row.SolutionLabel))
                continue;

            if (!rmPattern.IsMatch(row.SolutionLabel) &&
                !row.SolutionLabel.Contains("par", StringComparison.OrdinalIgnoreCase) &&
                !row.SolutionLabel.Contains("rm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Element))
                continue;

            var conc = row.CorrCon ?? row.SolnConc;
            if (!conc.HasValue)
                continue;

            var values = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                [row.Element] = conc.Value
            };

            var blanks = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                [row.Element] = blankValues.TryGetValue(row.Element, out var bv) ? bv : 0m
            };

            result.Add(new RmSampleData(row.SolutionLabel, values, blanks));
        }

        return result;
    }

/// <summary>
    /// Returns CRM map: CrmId -> (Element -> Value)
    /// Duplicate CrmId rows will be merged safely (prevents "same key added" crash).
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, decimal>>> GetCrmDataAsync()
    {
        var crmRecords = await _db.CrmData.AsNoTracking().ToListAsync();

        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);

        foreach (var crm in crmRecords)
        {
            if (string.IsNullOrWhiteSpace(crm.CrmId))
                continue;

            if (string.IsNullOrWhiteSpace(crm.ElementValues))
                continue;

            Dictionary<string, decimal>? values;
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(crm.ElementValues);
            }
            catch
            {
                continue;
            }

            if (values == null || values.Count == 0)
                continue;

            if (!result.TryGetValue(crm.CrmId, out var dict))
            {
                dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                result[crm.CrmId] = dict;
            }

            // merge strategy: keep MAX (safe + stable)
            foreach (var kv in values)
            {
                if (!dict.TryGetValue(kv.Key, out var old))
                    dict[kv.Key] = kv.Value;
                else
                    dict[kv.Key] = Math.Max(old, kv.Value);
            }
        }

        return result;
    }

    private List<MatchedSample> MatchWithCrm(List<RmSampleData> projectData, Dictionary<string, Dictionary<string, decimal>> crmData)
    {
        var result = new List<MatchedSample>();
        if (projectData.Count == 0 || crmData.Count == 0) return result;

        // extracts first number (e.g. "OREAS 100a" -> "100", "CRM 252 y" -> "252")
        var numberPattern = new Regex(@"(\d+)", RegexOptions.IgnoreCase);

        foreach (var sample in projectData)
        {
            var sm = numberPattern.Match(sample.SolutionLabel);
            var sampleNum = sm.Success ? sm.Groups[1].Value : "";
            if (string.IsNullOrEmpty(sampleNum))
                continue;

            string? matchedCrmId = null;

            foreach (var crmId in crmData.Keys)
            {
                var cm = numberPattern.Match(crmId);
                if (!cm.Success) continue;

                if (cm.Groups[1].Value == sampleNum)
                {
                    matchedCrmId = crmId;
                    break;
                }
            }

            if (matchedCrmId == null)
                continue;

            var crmValues = crmData[matchedCrmId]
                .ToDictionary(k => k.Key, v => (decimal?)v.Value, StringComparer.OrdinalIgnoreCase);

            result.Add(new MatchedSample(sample.SolutionLabel, matchedCrmId, sample.Values, crmValues, sample.BlankValues));
        }

        return result;
    }

    private static List<string> GetCommonElements(List<MatchedSample> data)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in data)
        {
            foreach (var k in s.SampleValues.Keys)
            {
                if (s.CrmValues.ContainsKey(k))
                    set.Add(k);
            }
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------------------------
    // Statistics + Build result data
    // ---------------------------

    /// <summary>
    /// Python formula: adjusted = (sample_val - blank_val + blank_adjust) * scale
    /// Here: blank = blank_adjust (optimization parameter)
    ///       blank_val = from sample's BlankValues dictionary
    /// </summary>
    private (int TotalPassed, Dictionary<string, ElementStats> ElementStats) CalculateStatistics(
        List<MatchedSample> data,
        List<string> elements,
        decimal blankAdjust,
        decimal scale,
        decimal minDiff,
        decimal maxDiff)
    {
        int total = 0;
        var stats = new Dictionary<string, ElementStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            int passed = 0;
            var diffs = new List<decimal>();

            foreach (var s in data)
            {
                if (!s.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
                if (!s.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0m) continue;

                // Get blank_val for this element (default to 0 if not found)
                var blankVal = s.BlankValues.TryGetValue(element, out var bv) && bv.HasValue ? bv.Value : 0m;

                // Python formula: (sample_val - blank_val + blank_adjust) * scale
                var corrected = (sv.Value - blankVal + blankAdjust) * scale;
                var diff = ((corrected - cv.Value) / cv.Value) * 100m;

                diffs.Add(diff);

                if (IsInDynamicRange(corrected, cv.Value))
                    passed++;
            }

            var mean = diffs.Count > 0 ? diffs.Average() : 0m;
            stats[element] = new ElementStats(passed, mean);
            total += passed;
        }

        return (total, stats);
    }

    private decimal CalculateMeanDiff(List<MatchedSample> data, string element, decimal blankAdjust, decimal scale)
    {
        var diffs = new List<decimal>();

        foreach (var s in data)
        {
            if (!s.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
            if (!s.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0m) continue;

            // Get blank_val for this element
            var blankVal = s.BlankValues.TryGetValue(element, out var bv) && bv.HasValue ? bv.Value : 0m;

            // Python formula: (sample_val - blank_val + blank_adjust) * scale
            var corrected = (sv.Value - blankVal + blankAdjust) * scale;
            var diff = ((corrected - cv.Value) / cv.Value) * 100m;
            diffs.Add(diff);
        }

        return diffs.Count > 0 ? diffs.Average() : 0m;
    }

    private static bool IsInDynamicRange(decimal corrected, decimal crmValue)
    {
        var range = CalculateDynamicRange(crmValue);
        return corrected >= crmValue - range && corrected <= crmValue + range;
    }

    private static decimal CalculateDynamicRange(decimal value)
    {
        var absValue = Math.Abs(value);

        if (absValue < 10m)
            return 2m;
        if (absValue < 100m)
            return absValue * 0.20m;
        if (absValue < 1000m)
            return absValue * 0.10m;
        if (absValue < 10000m)
            return absValue * 0.08m;
        if (absValue < 100000m)
            return absValue * 0.05m;

        return absValue * 0.03m;
    }

    private static List<OptimizedSampleDto> BuildOptimizedData(
        List<MatchedSample> data,
        List<string> elements,
        Dictionary<string, decimal> blankAdjusts,
        Dictionary<string, decimal> scales,
        decimal minDiff,
        decimal maxDiff)
    {
        var result = new List<OptimizedSampleDto>();

        foreach (var sample in data)
        {
            var optimizedValues = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var diffBefore = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var diffAfter = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var passBefore = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var passAfter = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in elements)
            {
                if (!sample.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
                if (!sample.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0m) continue;

                var blankAdjust = blankAdjusts.TryGetValue(element, out var ba) ? ba : 0m;
                var scale = scales.TryGetValue(element, out var ss) ? ss : 1m;

                // Get blank_val for this element
                var blankVal = sample.BlankValues.TryGetValue(element, out var bv) && bv.HasValue ? bv.Value : 0m;

                var original = sv.Value;

                // Python formula: (sample_val - blank_val + blank_adjust) * scale
                var optimized = (original - blankVal + blankAdjust) * scale;

                optimizedValues[element] = optimized;

                var db = ((original - cv.Value) / cv.Value) * 100m;
                var da = ((optimized - cv.Value) / cv.Value) * 100m;

                diffBefore[element] = db;
                diffAfter[element] = da;

                var correctedBefore = original - blankVal;
                passBefore[element] = IsInDynamicRange(correctedBefore, cv.Value);
                passAfter[element] = IsInDynamicRange(optimized, cv.Value);
            }

            result.Add(new OptimizedSampleDto(
                sample.SolutionLabel,
                sample.CrmId,
                sample.SampleValues,
                sample.CrmValues,
                optimizedValues,
                diffBefore,
                diffAfter,
                passBefore,
                passAfter
            ));
        }

        return result;
    }

    // ---------------------------
    // Optimization algorithms
    // ---------------------------

    private (decimal Blank, decimal Scale, int Passed) OptimizeElementImproved(
        List<MatchedSample> data,
        string element,
        decimal minDiff,
        decimal maxDiff,
        int maxIterations,
        int populationSize)
    {
        var samples = BuildElementSamples(data, element);
        if (samples.Count == 0)
            return (0m, 1m, 0);

        var outcome = OptimizeModelA(samples, maxIterations, populationSize);
        return (outcome.Blank, outcome.Scale, outcome.Passed);
    }

    private (decimal Blank, decimal Scale, int Passed, string SelectedModel) OptimizeElementMultiModel(
        List<MatchedSample> data,
        string element,
        decimal minDiff,
        decimal maxDiff,
        int maxIterations,
        int populationSize)
    {
        var samples = BuildElementSamples(data, element);
        if (samples.Count == 0)
            return (0m, 1m, 0, "A");

        var modelA = OptimizeModelA(samples, maxIterations, populationSize);
        var modelB = OptimizeModelB(samples, maxIterations, populationSize);
        var modelC = OptimizeModelC(samples, maxIterations, populationSize);

        var modelADistance = modelA.Scale == 1m ? (double?)null : modelB.AvgDistance;

        var candidates = new List<ModelCandidate>
        {
            new("A", modelA.Blank, modelA.Scale, modelA.Passed, modelADistance),
            new("B", modelB.Blank, modelB.Scale, modelB.Passed, modelB.AvgDistance),
            new("C", modelC.Blank, modelC.Scale, modelC.Passed, modelC.AvgDistance)
        };

        var best = candidates
            .OrderByDescending(c => c.Passed)
            .ThenByDescending(c => c.Distance.HasValue ? -c.Distance.Value : double.PositiveInfinity)
            .First();

        return (best.Blank, best.Scale, best.Passed, best.Model);
    }

    private static List<ElementSample> BuildElementSamples(List<MatchedSample> data, string element)
    {
        var samples = new List<ElementSample>();

        foreach (var s in data)
        {
            if (!s.SampleValues.TryGetValue(element, out var sv) || !sv.HasValue) continue;
            if (!s.CrmValues.TryGetValue(element, out var cv) || !cv.HasValue || cv.Value == 0m) continue;

            var blankVal = s.BlankValues.TryGetValue(element, out var bv) && bv.HasValue ? bv.Value : 0m;
            samples.Add(new ElementSample((double)sv.Value, (double)cv.Value, (double)blankVal));
        }

        return samples;
    }

    private static decimal GetBaseBlankValue(List<MatchedSample> data, string element)
    {
        foreach (var sample in data)
        {
            if (sample.BlankValues.TryGetValue(element, out var bv) && bv.HasValue)
                return bv.Value;
        }

        return 0m;
    }

    private static Dictionary<string, decimal> GetBaseBlankValues(List<RmSampleData> data)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var sample in data)
        {
            foreach (var kvp in sample.BlankValues)
            {
                if (kvp.Value.HasValue && !result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = kvp.Value.Value;
                }
            }
        }

        return result;
    }

    private static decimal GetBaseBlankValue(List<RmSampleData> data, string element)
    {
        foreach (var sample in data)
        {
            if (sample.BlankValues.TryGetValue(element, out var bv) && bv.HasValue)
                return bv.Value;
        }

        return 0m;
    }

    private ModelOutcome OptimizeModelA(List<ElementSample> samples, int maxIterations, int populationSize)
    {
        var total = samples.Count;
        var (blankMin, blankMax) = GetBlankBounds(samples);
        var (scaleMin, scaleMax) = GetScaleBounds();

        double Objective(double blankAdjust, double scale)
        {
            var inRange = CountInRange(samples, blankAdjust, scale);
            var reg = 10.0 * Math.Abs(scale - 1.0);
            return -inRange + (reg / total);
        }

        var result = RunDifferentialEvolution(Objective, (blankMin, blankMax), (scaleMin, scaleMax), maxIterations, populationSize);
        var blank = result.Blank;
        var scale = result.Scale;
        var inRangeAfter = CountInRange(samples, blank, scale);

        var blankOnly = RunDifferentialEvolution((b, _) => Objective(b, 1.0), (blankMin, blankMax), (1.0, 1.0), maxIterations, populationSize);
        var inRangeBlank = CountInRange(samples, blankOnly.Blank, 1.0);

        if (inRangeBlank > inRangeAfter || (inRangeBlank == inRangeAfter && (double)inRangeBlank / total >= 0.75))
        {
            blank = blankOnly.Blank;
            scale = 1.0;
            inRangeAfter = inRangeBlank;
        }

        var avgDistance = AverageDistance(samples, blank, scale);
        return new ModelOutcome("A", (decimal)blank, (decimal)scale, inRangeAfter, avgDistance);
    }

    private ModelOutcome OptimizeModelB(List<ElementSample> samples, int maxIterations, int populationSize)
    {
        var total = samples.Count;
        var meanAbsSample = AverageAbsSample(samples);
        var (blankMin, blankMax) = GetBlankBounds(samples);
        var (scaleMin, scaleMax) = GetScaleBounds();

        double Objective(double blankAdjust, double scale)
        {
            var totalDistance = TotalHuberDistance(samples, blankAdjust, scale);
            var reg = 0.1 * (Math.Abs(scale - 1.0) * meanAbsSample);
            return (totalDistance / total) + reg;
        }

        var result = RunDifferentialEvolution(Objective, (blankMin, blankMax), (scaleMin, scaleMax), maxIterations, populationSize);
        var blank = result.Blank;
        var scale = result.Scale;
        var inRangeAfter = CountInRange(samples, blank, scale);
        var avgDistance = AverageDistance(samples, blank, scale);

        var blankOnly = RunDifferentialEvolution((b, _) => Objective(b, 1.0), (blankMin, blankMax), (1.0, 1.0), maxIterations, populationSize);
        var inRangeBlank = CountInRange(samples, blankOnly.Blank, 1.0);
        var avgDistanceBlank = AverageDistance(samples, blankOnly.Blank, 1.0);

        if (inRangeBlank > inRangeAfter || (inRangeBlank == inRangeAfter && (double)inRangeBlank / total >= 0.75))
        {
            blank = blankOnly.Blank;
            scale = 1.0;
            inRangeAfter = inRangeBlank;
            avgDistance = avgDistanceBlank;
        }

        return new ModelOutcome("B", (decimal)blank, (decimal)scale, inRangeAfter, avgDistance);
    }

    private ModelOutcome OptimizeModelC(List<ElementSample> samples, int maxIterations, int populationSize)
    {
        var total = samples.Count;
        var meanAbsSample = AverageAbsSample(samples);
        var (blankMin, blankMax) = GetBlankBounds(samples);
        var (scaleMin, scaleMax) = GetScaleBounds();

        double Objective(double blankAdjust, double scale)
        {
            var totalSse = TotalSse(samples, blankAdjust, scale);
            var reg = 0.1 * (Math.Abs(scale - 1.0) * meanAbsSample);
            return (totalSse / total) + reg;
        }

        var result = RunDifferentialEvolution(Objective, (blankMin, blankMax), (scaleMin, scaleMax), maxIterations, populationSize);
        var blank = result.Blank;
        var scale = result.Scale;
        var inRangeAfter = CountInRange(samples, blank, scale);
        var avgDistance = AverageDistance(samples, blank, scale);

        var blankOnly = RunDifferentialEvolution((b, _) => Objective(b, 1.0), (blankMin, blankMax), (1.0, 1.0), maxIterations, populationSize);
        var inRangeBlank = CountInRange(samples, blankOnly.Blank, 1.0);
        var avgDistanceBlank = AverageDistance(samples, blankOnly.Blank, 1.0);

        if (inRangeBlank > inRangeAfter || (inRangeBlank == inRangeAfter && (double)inRangeBlank / total >= 0.75))
        {
            blank = blankOnly.Blank;
            scale = 1.0;
            inRangeAfter = inRangeBlank;
            avgDistance = avgDistanceBlank;
        }

        return new ModelOutcome("C", (decimal)blank, (decimal)scale, inRangeAfter, avgDistance);
    }

    private static (double Min, double Max) GetBlankBounds(List<ElementSample> samples)
    {
        var avgCert = samples.Average(s => s.CrmValue);
        var maxVal = samples
            .Select(s => Math.Abs(s.SampleValue))
            .Concat(samples.Select(s => Math.Abs(s.BlankValue)))
            .Append(Math.Abs(avgCert))
            .Append(1.0)
            .Max();

        var bound = maxVal * 10.0;
        return (-bound, bound);
    }

    private static (double Min, double Max) GetScaleBounds()
        => (1.0 - 0.3, 1.0 + 0.3);

    private static int CountInRange(List<ElementSample> samples, double blankAdjust, double scale)
    {
        int passed = 0;

        foreach (var sample in samples)
        {
            var corrected = (sample.SampleValue - sample.BlankValue + blankAdjust) * scale;
            var range = CalculateDynamicRange(sample.CrmValue);
            var lower = sample.CrmValue - range;
            var upper = sample.CrmValue + range;

            if (corrected >= lower && corrected <= upper)
                passed++;
        }

        return passed;
    }

    private static double AverageAbsSample(List<ElementSample> samples)
    {
        if (samples.Count == 0)
            return 0.0;

        return samples.Average(s => Math.Abs(s.SampleValue));
    }

    private static double AverageDistance(List<ElementSample> samples, double blankAdjust, double scale)
    {
        if (samples.Count == 0)
            return 0.0;

        var distances = samples.Select(s => DistanceToRange((s.SampleValue - s.BlankValue + blankAdjust) * scale, s.CrmValue));
        return distances.Average();
    }

    private static double TotalHuberDistance(List<ElementSample> samples, double blankAdjust, double scale)
    {
        double total = 0.0;

        foreach (var sample in samples)
        {
            var corrected = (sample.SampleValue - sample.BlankValue + blankAdjust) * scale;
            var dist = DistanceToRange(corrected, sample.CrmValue);
            total += Huber(1.0, dist);
        }

        return total;
    }

    private static double TotalSse(List<ElementSample> samples, double blankAdjust, double scale)
    {
        double total = 0.0;

        foreach (var sample in samples)
        {
            var corrected = (sample.SampleValue - sample.BlankValue + blankAdjust) * scale;
            var diff = corrected - sample.CrmValue;
            total += diff * diff;
        }

        return total;
    }

    private (double Blank, double Scale, double Objective) RunDifferentialEvolution(
        Func<double, double, double> objective,
        (double Min, double Max) blankBounds,
        (double Min, double Max) scaleBounds,
        int maxIterations,
        int populationSize)
    {
        var size = Math.Max(populationSize, 6);
        var population = new List<(double Blank, double Scale)>(size);

        for (int i = 0; i < size; i++)
        {
            population.Add((
                _random.NextDouble() * (blankBounds.Max - blankBounds.Min) + blankBounds.Min,
                _random.NextDouble() * (scaleBounds.Max - scaleBounds.Min) + scaleBounds.Min));
        }

        var fitness = population
            .Select(p => -objective(p.Blank, p.Scale))
            .ToList();

        double previousBest = fitness.Max();
        int stagnant = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            int bestIdx = fitness.IndexOf(fitness.Max());
            var best = population[bestIdx];

            for (int i = 0; i < size; i++)
            {
                double F = 0.5 + _random.NextDouble() * 0.5;
                double CR = DefaultCR;

                var candidates = Enumerable.Range(0, size)
                    .Where(j => j != i && j != bestIdx)
                    .OrderBy(_ => _random.Next())
                    .Take(2)
                    .ToList();

                var r1 = population[candidates[0]];
                var r2 = population[candidates[1]];

                var mutantBlank = BounceBack(best.Blank + F * (r1.Blank - r2.Blank), blankBounds.Min, blankBounds.Max);
                var mutantScale = BounceBack(best.Scale + F * (r1.Scale - r2.Scale), scaleBounds.Min, scaleBounds.Max);

                var current = population[i];
                int jRand = _random.Next(2);

                var trialBlank = (jRand == 0 || _random.NextDouble() < CR) ? mutantBlank : current.Blank;
                var trialScale = (jRand == 1 || _random.NextDouble() < CR) ? mutantScale : current.Scale;

                var trialFitness = -objective(trialBlank, trialScale);
                if (trialFitness > fitness[i])
                {
                    population[i] = (trialBlank, trialScale);
                    fitness[i] = trialFitness;
                }
            }

            var currentBest = fitness.Max();
            if (Math.Abs(currentBest - previousBest) < Tolerance)
                stagnant++;
            else
                stagnant = 0;

            if (stagnant >= ConvergenceWindow)
                break;

            previousBest = currentBest;
        }

        int finalBestIdx = fitness.IndexOf(fitness.Max());
        var finalBest = population[finalBestIdx];
        var finalObjective = -fitness[finalBestIdx];

        return (finalBest.Blank, finalBest.Scale, finalObjective);
    }

    private static double CalculateDynamicRange(double value)
    {
        var absValue = Math.Abs(value);

        if (absValue < 10.0)
            return 2.0;
        if (absValue < 100.0)
            return absValue * 0.20;
        if (absValue < 1000.0)
            return absValue * 0.10;
        if (absValue < 10000.0)
            return absValue * 0.08;
        if (absValue < 100000.0)
            return absValue * 0.05;

        return absValue * 0.03;
    }

    private static double DistanceToRange(double corrected, double crmValue)
    {
        var range = CalculateDynamicRange(crmValue);
        var lower = crmValue - range;
        var upper = crmValue + range;

        if (corrected < lower)
            return lower - corrected;
        if (corrected > upper)
            return corrected - upper;

        return 0.0;
    }

    private static double Huber(double delta, double x)
    {
        var abs = Math.Abs(x);
        if (abs <= delta)
            return 0.5 * abs * abs;

        return delta * (abs - 0.5 * delta);
    }

    private static double BounceBack(double value, double min, double max)
    {
        if (value < min) return min + (min - value);
        if (value > max) return max - (value - max);
        return value;
    }
    // ---------------------------
    // Internal types
    // ---------------------------

    private sealed record RmSampleData(string SolutionLabel, Dictionary<string, decimal?> Values, Dictionary<string, decimal?> BlankValues);

    private sealed record MatchedSample(
        string SolutionLabel,
        string CrmId,
        Dictionary<string, decimal?> SampleValues,
        Dictionary<string, decimal?> CrmValues,
        Dictionary<string, decimal?> BlankValues);

    private sealed record ElementSample(double SampleValue, double CrmValue, double BlankValue);

    private sealed record ModelOutcome(string Model, decimal Blank, decimal Scale, int Passed, double AvgDistance);

    private sealed record ModelCandidate(string Model, decimal Blank, decimal Scale, int Passed, double? Distance);

    private sealed record ElementStats(int Passed, decimal MeanDiff);
}
