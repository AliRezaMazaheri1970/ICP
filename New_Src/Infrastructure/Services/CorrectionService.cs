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

public class CorrectionService : ICorrectionService
{
    private readonly IsatisDbContext _db;
    private readonly IChangeLogService _changeLogService;
    private readonly ILogger<CorrectionService> _logger;

    public CorrectionService(
        IsatisDbContext db,
        IChangeLogService changeLogService,
        ILogger<CorrectionService> logger)
    {
        _db = db;
        _changeLogService = changeLogService;
        _logger = logger;
    }

    #region Find Bad Weights/Volumes

    public async Task<Result<List<BadSampleDto>>> FindBadWeightsAsync(FindBadWeightsRequest request)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<BadSampleDto>>.Fail("No data found for project");

            var badWeights = new List<BadSampleDto>();
            var expectedWeight = (request.WeightMin + request.WeightMax) / 2;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Type", out var typeElement) &&
                        typeElement.GetString() != "Samp")
                        continue;

                    if (!root.TryGetProperty("Act Wgt", out var weightElement))
                        continue;

                    // Handle null values
                    if (weightElement.ValueKind == JsonValueKind.Null)
                        continue;

                    decimal weight;
                    if (weightElement.ValueKind == JsonValueKind.Number)
                        weight = weightElement.GetDecimal();
                    else if (weightElement.ValueKind == JsonValueKind.String && 
                             decimal.TryParse(weightElement.GetString(), out var parsedWeight))
                        weight = parsedWeight;
                    else
                        continue;

                    if (weight < request.WeightMin || weight > request.WeightMax)
                    {
                        decimal corrCon = 0m;
                        if (root.TryGetProperty("Corr Con", out var corrConElement) && 
                            corrConElement.ValueKind != JsonValueKind.Null)
                        {
                            if (corrConElement.ValueKind == JsonValueKind.Number)
                                corrCon = corrConElement.GetDecimal();
                            else if (corrConElement.ValueKind == JsonValueKind.String &&
                                     decimal.TryParse(corrConElement.GetString(), out var parsedCorrCon))
                                corrCon = parsedCorrCon;
                        }

                        var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                            ? labelElement.GetString() ?? row.SampleId ?? "Unknown"
                            : row.SampleId ?? "Unknown";

                        if (!badWeights.Any(b => b.SolutionLabel == solutionLabel))
                        {
                            badWeights.Add(new BadSampleDto(
                                solutionLabel,
                                weight,
                                corrCon,
                                expectedWeight,
                                Math.Abs(weight - expectedWeight)
                            ));
                        }
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            return Result<List<BadSampleDto>>.Success(badWeights.OrderByDescending(b => b.Deviation).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find bad weights for project {ProjectId}", request.ProjectId);
            return Result<List<BadSampleDto>>.Fail($"Failed to find bad weights: {ex.Message}");
        }
    }

    public async Task<Result<List<BadSampleDto>>> FindBadVolumesAsync(FindBadVolumesRequest request)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<BadSampleDto>>.Fail("No data found for project");

            var badVolumes = new List<BadSampleDto>();

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Type", out var typeElement) &&
                        typeElement.GetString() != "Samp")
                        continue;

                    if (!root.TryGetProperty("Act Vol", out var volumeElement))
                        continue;

                    // Handle null values
                    if (volumeElement.ValueKind == JsonValueKind.Null)
                        continue;

                    decimal volume;
                    if (volumeElement.ValueKind == JsonValueKind.Number)
                        volume = volumeElement.GetDecimal();
                    else if (volumeElement.ValueKind == JsonValueKind.String && 
                             decimal.TryParse(volumeElement.GetString(), out var parsedVolume))
                        volume = parsedVolume;
                    else
                        continue;

                    if (volume != request.ExpectedVolume)
                    {
                        decimal corrCon = 0m;
                        if (root.TryGetProperty("Corr Con", out var corrConElement) && 
                            corrConElement.ValueKind != JsonValueKind.Null)
                        {
                            if (corrConElement.ValueKind == JsonValueKind.Number)
                                corrCon = corrConElement.GetDecimal();
                            else if (corrConElement.ValueKind == JsonValueKind.String &&
                                     decimal.TryParse(corrConElement.GetString(), out var parsedCorrCon))
                                corrCon = parsedCorrCon;
                        }

                        var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                            ? labelElement.GetString() ?? row.SampleId ?? "Unknown"
                            : row.SampleId ?? "Unknown";

                        if (!badVolumes.Any(b => b.SolutionLabel == solutionLabel))
                        {
                            badVolumes.Add(new BadSampleDto(
                                solutionLabel,
                                volume,
                                corrCon,
                                request.ExpectedVolume,
                                Math.Abs(volume - request.ExpectedVolume)
                            ));
                        }
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            return Result<List<BadSampleDto>>.Success(badVolumes.OrderByDescending(b => b.Deviation).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find bad volumes for project {ProjectId}", request.ProjectId);
            return Result<List<BadSampleDto>>.Fail($"Failed to find bad volumes: {ex.Message}");
        }
    }

    #endregion

    #region Find Empty Rows (Based on Python empty_check.py)
    // ---------------------------------------------------------
    // متد اصلی: شناسایی ردیف‌های خالی با منطق دقیق پایتون
    // ---------------------------------------------------------
    public async Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request)
    {
        try
        {
            // 1. دریافت داده‌های خام
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<EmptyRowDto>>.Fail("هیچ داده‌ای برای این پروژه یافت نشد.");

            // ساختار: [SampleId] -> [Element] -> Value
            var pivotedData = new Dictionary<string, Dictionary<string, decimal>>();
            var allElements = new HashSet<string>();

            const decimal STD_WEIGHT = 1.0m;
            const decimal STD_VOLUME = 10.0m;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);

                    // الف) نرمال‌سازی کلیدها: ساخت دیکشنری با کلیدهای تمیز (بدون فاصله و حروف بزرگ)
                    // این بخش حیاتی است تا "Act Wgt" و "ActWgt" یکی شناخته شوند
                    var rowData = new Dictionary<string, JsonElement>();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string cleanKey = NormalizeKey(prop.Name);
                        rowData[cleanKey] = prop.Value;
                    }

                    // ب) استخراج شناسه نمونه و نام عنصر
                    // اصلاح: استفاده از متغیر واسط برای رفع هشدار Null
                    string? extractedLabel = GetValue(rowData, "SOLUTIONLABEL")?.GetString();
                    string sampleId = !string.IsNullOrWhiteSpace(extractedLabel)
                        ? extractedLabel
                        : (row.SampleId ?? "Unknown");

                    string? element = GetValue(rowData, "ELEMENT")?.GetString();
                    if (string.IsNullOrWhiteSpace(element)) continue; // ردیف هدر یا متادیتا

                    // ج) خواندن مقادیر عددی (با هندل کردن نال)
                    decimal corrCon = GetDecimalSafe(rowData, "CORRCON", "SOLNCONC");
                    decimal actWgt = GetDecimalSafe(rowData, "ACTWGT", "WEIGHT");
                    decimal actVol = GetDecimalSafe(rowData, "ACTVOL", "VOLUME");
                    decimal df = GetDecimalSafe(rowData, "DF", "DILUTION");

                    // د) اعمال فرمول‌های اصلاح (دقیقاً مشابه پایتون)
                    decimal finalVal = corrCon;

                    // 1. اصلاح وزن
                    if (actWgt != 0) finalVal = finalVal * (STD_WEIGHT / actWgt);

                    // 2. اصلاح حجم
                    finalVal = finalVal * (actVol / STD_VOLUME);

                    // 3. اصلاح رقت
                    if (df != 0) finalVal = finalVal * df;

                    // ه) ذخیره در ساختار پیوت
                    if (!pivotedData.ContainsKey(sampleId))
                        pivotedData[sampleId] = new Dictionary<string, decimal>();

                    pivotedData[sampleId][element] = finalVal;
                    allElements.Add(element);
                }
                catch (Exception ex)
                {
                    // اصلاح: استفاده از SampleId به جای Id برای لاگ
                    _logger.LogWarning("Error parsing row for sample {SampleId}: {Message}", row.SampleId, ex.Message);
                }
            }

            if (!pivotedData.Any())
                return Result<List<EmptyRowDto>>.Success(new List<EmptyRowDto>());

            // 2. محاسبه میانگین قدر مطلق هر ستون (Abs Mean)
            var columnAbsMeans = new Dictionary<string, decimal>();
            foreach (var elem in allElements)
            {
                decimal sum = 0;
                int count = 0;
                foreach (var sample in pivotedData.Values)
                {
                    if (sample.ContainsKey(elem))
                    {
                        sum += Math.Abs(sample[elem]);
                        count++;
                    }
                }
                columnAbsMeans[elem] = count > 0 ? sum / count : 0;
            }

            // 3. بررسی و امتیازدهی به ردیف‌ها
            var emptyRows = new List<EmptyRowDto>();

            // اصلاح: استفاده از decimal برای هماهنگی با نوع داده Request
            decimal effectivePercent = request.ThresholdPercent > 0 ? request.ThresholdPercent : 20m;
            decimal thresholdFactor = effectivePercent / 100m;

            foreach (var sampleEntry in pivotedData)
            {
                var sampleId = sampleEntry.Key;
                var values = sampleEntry.Value;

                int totalElementsChecked = 0;
                int belowThresholdCount = 0;
                var details = new Dictionary<string, decimal>();
                var rowValuesNullable = new Dictionary<string, decimal?>();

                foreach (var elem in allElements)
                {
                    if (!values.ContainsKey(elem)) continue;
                    if (!columnAbsMeans.ContainsKey(elem)) continue;

                    decimal val = values[elem];
                    decimal mean = columnAbsMeans[elem];
                    decimal threshold = mean * thresholdFactor;

                    rowValuesNullable[elem] = val;

                    // درصد انحراف برای نمایش
                    details[elem] = mean != 0 ? (Math.Abs(val) / mean) * 100 : 0;

                    // شرط اصلی: |Value| <= Threshold
                    // استفاده از <= برای حالتی که هم Value و هم Mean صفر هستند (مثل Blank)
                    if (Math.Abs(val) <= threshold)
                    {
                        belowThresholdCount++;
                    }
                    totalElementsChecked++;
                }

                if (totalElementsChecked > 0)
                {
                    decimal emptyScore = ((decimal)belowThresholdCount / totalElementsChecked) * 100m;

                    bool isEmpty = request.RequireAllElements
                        ? belowThresholdCount == totalElementsChecked
                        : emptyScore >= 80;

                    if (isEmpty)
                    {
                        emptyRows.Add(new EmptyRowDto(
                            sampleId,
                            rowValuesNullable,
                            columnAbsMeans,
                            details,
                            belowThresholdCount,
                            totalElementsChecked,
                            emptyScore
                        ));
                    }
                }
            }

            return Result<List<EmptyRowDto>>.Success(emptyRows.OrderByDescending(x => x.OverallScore).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FindEmptyRowsAsync");
            return Result<List<EmptyRowDto>>.Fail($"Error: {ex.Message}");
        }
    }

    // ---------------------------------------------------------
    // توابع کمکی (Helpers) - حتماً به انتهای کلاس اضافه کنید
    // ---------------------------------------------------------

    private string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        // حذف تمام کاراکترهای غیر از حروف و اعداد و تبدیل به حروف بزرگ
        // "Act Wgt" -> "ACTWGT"
        return Regex.Replace(key, "[^a-zA-Z0-9]", "").ToUpperInvariant();
    }

    private JsonElement? GetValue(Dictionary<string, JsonElement> rowData, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            string normalized = NormalizeKey(key);
            if (rowData.TryGetValue(normalized, out var val)) return val;
        }
        return null;
    }

    private decimal GetDecimalSafe(Dictionary<string, JsonElement> rowData, params string[] candidateKeys)
    {
        var element = GetValue(rowData, candidateKeys);
        if (element.HasValue)
        {
            if (element.Value.ValueKind == JsonValueKind.Number)
                return element.Value.GetDecimal();
            if (element.Value.ValueKind == JsonValueKind.String && decimal.TryParse(element.Value.GetString(), out var d))
                return d;
        }
        return 0;
    }
    #endregion

    #region Apply Corrections

    public async Task<Result<CorrectionResultDto>> ApplyWeightCorrectionAsync(WeightCorrectionRequest request)
    {
        try
        {
            if (request.NewWeight <= 0)
                return Result<CorrectionResultDto>.Fail("New weight must be positive");

            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<CorrectionResultDto>.Fail("No data found for project");

            await SaveUndoStateAsync(request.ProjectId, "WeightCorrection");

            var correctedSamples = new List<CorrectedSampleInfo>();
            var changeLogEntries = new List<(string? SolutionLabel, string? Element, string? OldValue, string? NewValue)>();
            var correctedRows = 0;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                        ? labelElement.GetString() ?? row.SampleId
                        : row.SampleId;

                    if (solutionLabel == null || !request.SolutionLabels.Contains(solutionLabel))
                        continue;

                    if (root.TryGetProperty("Type", out var typeElement) &&
                        typeElement.GetString() != "Samp")
                    {
                        // Optional: logic to skip non-samples
                    }

                    if (!root.TryGetProperty("Act Wgt", out var weightElement))
                        continue;

                    if (weightElement.ValueKind == JsonValueKind.Null)
                        continue;

                    decimal oldWeight;
                    if (weightElement.ValueKind == JsonValueKind.Number)
                        oldWeight = weightElement.GetDecimal();
                    else if (weightElement.ValueKind == JsonValueKind.String &&
                             decimal.TryParse(weightElement.GetString(), out var parsedWeight))
                        oldWeight = parsedWeight;
                    else
                        continue;

                    if (oldWeight == 0) continue;

                    decimal oldCorrCon = 0m;
                    if (root.TryGetProperty("Corr Con", out var corrConElement) &&
                        corrConElement.ValueKind != JsonValueKind.Null)
                    {
                        if (corrConElement.ValueKind == JsonValueKind.Number)
                            oldCorrCon = corrConElement.GetDecimal();
                        else if (corrConElement.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(corrConElement.GetString(), out var parsedCorrCon))
                            oldCorrCon = parsedCorrCon;
                    }

                    // --- FIX: Correct Formula (Inverse Proportionality) ---
                    // Correct: NewCorr = OldCorr * (OldWeight / NewWeight)
                    var newCorrCon = oldCorrCon * (oldWeight / request.NewWeight);
                    // ------------------------------------------------------

                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(row.ColumnData);
                    if (dict != null)
                    {
                        dict["Act Wgt"] = request.NewWeight;
                        dict["Corr Con"] = newCorrCon;

                        var newJson = JsonSerializer.Serialize(dict);
                        row.ColumnData = newJson;

                        correctedRows++;

                        if (!correctedSamples.Any(s => s.SolutionLabel == solutionLabel))
                        {
                            correctedSamples.Add(new CorrectedSampleInfo(
                                solutionLabel,
                                oldWeight,
                                request.NewWeight,
                                oldCorrCon,
                                newCorrCon
                            ));
                        }

                        var element = dict.ContainsKey("Element") ? dict["Element"]?.ToString() : "Unknown";
                        changeLogEntries.Add((solutionLabel, element, oldWeight.ToString(), request.NewWeight.ToString()));
                    }
                }
                catch { }
            }

            await _db.SaveChangesAsync();
            await _changeLogService.LogBatchChangesAsync(request.ProjectId, "WeightCorrection", changeLogEntries);

            // Example fix if the first int is total count and second is corrected count:
            return Result<CorrectionResultDto>.Success(new CorrectionResultDto(
                rawRows.Count, // Missing argument (Total rows processed)
                correctedRows, // Corrected rows count
                correctedSamples.Take(50).ToList()
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply weight correction");
            return Result<CorrectionResultDto>.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<Result<CorrectionResultDto>> ApplyVolumeCorrectionAsync(VolumeCorrectionRequest request)
    {
        try
        {
            if (request.NewVolume <= 0)
                return Result<CorrectionResultDto>.Fail("New volume must be positive");

            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<CorrectionResultDto>.Fail("No data found for project");

            await SaveUndoStateAsync(request.ProjectId, "VolumeCorrection");

            var correctedSamples = new List<CorrectedSampleInfo>();
            var changeLogEntries = new List<(string? SolutionLabel, string? Element, string? OldValue, string? NewValue)>();
            var correctedRows = 0;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                        ? labelElement.GetString() ?? row.SampleId
                        : row.SampleId;

                    if (solutionLabel == null || !request.SolutionLabels.Contains(solutionLabel))
                        continue;

                    if (root.TryGetProperty("Type", out var typeElement) &&
                        typeElement.GetString() != "Samp")
                        continue;

                    if (!root.TryGetProperty("Act Vol", out var volumeElement))
                        continue;

                    // Handle null values
                    if (volumeElement.ValueKind == JsonValueKind.Null)
                        continue;

                    decimal oldVolume;
                    if (volumeElement.ValueKind == JsonValueKind.Number)
                        oldVolume = volumeElement.GetDecimal();
                    else if (volumeElement.ValueKind == JsonValueKind.String &&
                             decimal.TryParse(volumeElement.GetString(), out var parsedVolume))
                        oldVolume = parsedVolume;
                    else
                        continue;

                    if (oldVolume == 0) continue;

                    decimal oldCorrCon = 0m;
                    if (root.TryGetProperty("Corr Con", out var corrConElement) &&
                        corrConElement.ValueKind != JsonValueKind.Null)
                    {
                        if (corrConElement.ValueKind == JsonValueKind.Number)
                            oldCorrCon = corrConElement.GetDecimal();
                        else if (corrConElement.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(corrConElement.GetString(), out var parsedCorrCon))
                            oldCorrCon = parsedCorrCon;
                    }

                    var newCorrCon = (request.NewVolume / oldVolume) * oldCorrCon;

                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                    var newDict = new Dictionary<string, object>();

                    foreach (var kvp in dict!)
                    {
                        if (kvp.Key == "Act Vol")
                            newDict[kvp.Key] = request.NewVolume;
                        else if (kvp.Key == "Corr Con")
                            newDict[kvp.Key] = newCorrCon;
                        else
                            newDict[kvp.Key] = GetJsonValue(kvp.Value);
                    }

                    row.ColumnData = JsonSerializer.Serialize(newDict);
                    correctedRows++;

                    if (!correctedSamples.Any(s => s.SolutionLabel == solutionLabel))
                    {
                        correctedSamples.Add(new CorrectedSampleInfo(
                            solutionLabel,
                            oldVolume,
                            request.NewVolume,
                            oldCorrCon,
                            newCorrCon
                        ));

                        changeLogEntries.Add((solutionLabel, "Act Vol", oldVolume.ToString(), request.NewVolume.ToString()));
                        changeLogEntries.Add((solutionLabel, "Corr Con", oldCorrCon.ToString(), newCorrCon.ToString()));
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            var project = await _db.Projects.FindAsync(request.ProjectId);
            if (project != null)
            {
                project.LastModifiedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Log changes to ChangeLog
            if (changeLogEntries.Any())
            {
                await _changeLogService.LogBatchChangesAsync(
                    request.ProjectId,
                    "Volume",
                    changeLogEntries,
                    request.ChangedBy,
                    $"Volume correction: {correctedSamples.Count} samples corrected to {request.NewVolume}"
                );
            }

            _logger.LogInformation("Volume correction applied: {CorrectedRows} rows for project {ProjectId}",
                correctedRows, request.ProjectId);

            return Result<CorrectionResultDto>.Success(new CorrectionResultDto(
                rawRows.Count,
                correctedRows,
                correctedSamples
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply volume correction for project {ProjectId}", request.ProjectId);
            return Result<CorrectionResultDto>.Fail($"Failed to apply volume correction: {ex.Message}");
        }
    }

    public async Task<Result<List<DfSampleDto>>> GetDfSamplesAsync(Guid projectId)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == projectId)
                .OrderBy(r => r.DataId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<DfSampleDto>>.Fail("No data found for project");

            var samples = new List<DfSampleDto>();
            int rowNum = 1;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                        ? labelElement.GetString() ?? row.SampleId ?? $"Row-{rowNum}"
                        : row.SampleId ?? $"Row-{rowNum}";

                    decimal df = 1m; // Default
                    if (root.TryGetProperty("DF", out var dfElement) && dfElement.ValueKind != JsonValueKind.Null)
                    {
                        if (dfElement.ValueKind == JsonValueKind.Number)
                            df = dfElement.GetDecimal();
                        else if (dfElement.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(dfElement.GetString(), out var parsedDf))
                            df = parsedDf;
                    }

                    string? sampleType = null;
                    if (root.TryGetProperty("Type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
                    {
                        sampleType = typeElement.GetString();
                    }

                    samples.Add(new DfSampleDto(rowNum, solutionLabel, df, sampleType));
                    rowNum++;
                }
                catch (JsonException)
                {
                    rowNum++;
                    continue;
                }
            }

            return Result<List<DfSampleDto>>.Success(samples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get DF samples for project {ProjectId}", projectId);
            return Result<List<DfSampleDto>>.Fail($"Failed: {ex.Message}");
        }
    }

    public async Task<Result<CorrectionResultDto>> ApplyDfCorrectionAsync(DfCorrectionRequest request)
    {
        try
        {
            if (request.NewDf <= 0)
                return Result<CorrectionResultDto>.Fail("New DF must be positive");

            _logger.LogInformation("ApplyDfCorrectionAsync called with ProjectId={ProjectId}, NewDf={NewDf}, SolutionLabels count={Count}",
                request.ProjectId, request.NewDf, request.SolutionLabels?.Count ?? 0);

            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<CorrectionResultDto>.Fail("No data found for project");

            _logger.LogInformation("Found {Count} raw rows for project", rawRows.Count);

            await SaveUndoStateAsync(request.ProjectId, "DfCorrection");

            var correctedSamples = new List<CorrectedSampleInfo>();
            var changeLogEntries = new List<(string? SolutionLabel, string? Element, string? OldValue, string? NewValue)>();
            var correctedRows = 0;
            var skippedNoLabel = 0;
            var skippedNoDf = 0;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                        ? labelElement.GetString() ?? row.SampleId
                        : row.SampleId;

                    if (solutionLabel == null || request.SolutionLabels == null || !request.SolutionLabels.Contains(solutionLabel))
                    {
                        skippedNoLabel++;
                        continue;
                    }

                    if (!root.TryGetProperty("DF", out var dfElement))
                    {
                        skippedNoDf++;
                        continue;
                    }

                    // Handle null values
                    if (dfElement.ValueKind == JsonValueKind.Null)
                        continue;

                    decimal oldDf;
                    if (dfElement.ValueKind == JsonValueKind.Number)
                        oldDf = dfElement.GetDecimal();
                    else if (dfElement.ValueKind == JsonValueKind.String &&
                             decimal.TryParse(dfElement.GetString(), out var parsedDf))
                        oldDf = parsedDf;
                    else
                        continue;

                    if (oldDf == 0) continue;

                    decimal oldCorrCon = 0m;
                    if (root.TryGetProperty("Corr Con", out var corrConElement) &&
                        corrConElement.ValueKind != JsonValueKind.Null)
                    {
                        if (corrConElement.ValueKind == JsonValueKind.Number)
                            oldCorrCon = corrConElement.GetDecimal();
                        else if (corrConElement.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(corrConElement.GetString(), out var parsedCorrCon))
                            oldCorrCon = parsedCorrCon;
                    }

                    // DF Correction formula: NewCorrCon = (NewDf / OldDf) * OldCorrCon
                    var newCorrCon = (request.NewDf / oldDf) * oldCorrCon;

                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                    var newDict = new Dictionary<string, object>();

                    foreach (var kvp in dict!)
                    {
                        if (kvp.Key == "DF")
                            newDict[kvp.Key] = request.NewDf;
                        else if (kvp.Key == "Corr Con")
                            newDict[kvp.Key] = newCorrCon;
                        else
                            newDict[kvp.Key] = GetJsonValue(kvp.Value);
                    }

                    row.ColumnData = JsonSerializer.Serialize(newDict);
                    correctedRows++;

                    if (!correctedSamples.Any(s => s.SolutionLabel == solutionLabel))
                    {
                        correctedSamples.Add(new CorrectedSampleInfo(
                            solutionLabel,
                            oldDf,
                            request.NewDf,
                            oldCorrCon,
                            newCorrCon
                        ));

                        changeLogEntries.Add((solutionLabel, "DF", oldDf.ToString(), request.NewDf.ToString()));
                        changeLogEntries.Add((solutionLabel, "Corr Con", oldCorrCon.ToString(), newCorrCon.ToString()));
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            var project = await _db.Projects.FindAsync(request.ProjectId);
            if (project != null)
            {
                project.LastModifiedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Log changes to ChangeLog
            if (changeLogEntries.Any())
            {
                await _changeLogService.LogBatchChangesAsync(
                    request.ProjectId,
                    "DF",
                    changeLogEntries,
                    request.ChangedBy,
                    $"DF correction: {correctedSamples.Count} samples corrected to {request.NewDf}"
                );
            }

            _logger.LogInformation("DF correction applied: {CorrectedRows} rows, skipped {SkippedNoLabel} (no label match), {SkippedNoDf} (no DF) for project {ProjectId}",
                correctedRows, skippedNoLabel, skippedNoDf, request.ProjectId);

            return Result<CorrectionResultDto>.Success(new CorrectionResultDto(
                rawRows.Count,
                correctedRows,
                correctedSamples
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply DF correction for project {ProjectId}", request.ProjectId);
            return Result<CorrectionResultDto>.Fail($"Failed to apply DF correction: {ex.Message}");
        }
    }

    public async Task<Result<CorrectionResultDto>> ApplyOptimizationAsync(ApplyOptimizationRequest request)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<CorrectionResultDto>.Fail("No data found for project");

            await SaveUndoStateAsync(request.ProjectId, "OptimizationApply");

            var correctedRows = 0;
            var changeLogEntries = new List<(string? SolutionLabel, string? Element, string? OldValue, string? NewValue)>();

            foreach (var row in rawRows)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                    if (dict == null) continue;

                    var solutionLabel = dict.TryGetValue("Solution Label", out var sl)
                        ? sl.GetString() ?? row.SampleId
                        : row.SampleId;

                    var newDict = new Dictionary<string, object>();
                    var modified = false;

                    foreach (var kvp in dict)
                    {
                        if (request.ElementSettings.TryGetValue(kvp.Key, out var settings) &&
                            kvp.Value.ValueKind == JsonValueKind.Number)
                        {
                            var originalValue = kvp.Value.GetDecimal();
                            // Python formula (pivot_plot_dialog.py line 617):
                            // corrected_value = (value - blank) * scale
                            var correctedValue = (originalValue - settings.Blank) * settings.Scale;
                            newDict[kvp.Key] = correctedValue;
                            modified = true;

                            changeLogEntries.Add((solutionLabel, kvp.Key, originalValue.ToString(), correctedValue.ToString()));
                        }
                        else
                        {
                            newDict[kvp.Key] = GetJsonValue(kvp.Value);
                        }
                    }

                    if (modified)
                    {
                        row.ColumnData = JsonSerializer.Serialize(newDict);
                        correctedRows++;
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            var project = await _db.Projects.FindAsync(request.ProjectId);
            if (project != null)
            {
                project.LastModifiedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Log changes to ChangeLog
            if (changeLogEntries.Any())
            {
                var elementSummary = string.Join(", ", request.ElementSettings.Select(e => $"{e.Key}(B={e.Value.Blank:F2},S={e.Value.Scale:F2})"));
                await _changeLogService.LogBatchChangesAsync(
                    request.ProjectId,
                    "BlankScale",
                    changeLogEntries,
                    request.ChangedBy,
                    $"Optimization applied: {elementSummary}"
                );
            }

            _logger.LogInformation("Optimization applied: {CorrectedRows} rows for project {ProjectId}",
                correctedRows, request.ProjectId);

            return Result<CorrectionResultDto>.Success(new CorrectionResultDto(
                rawRows.Count,
                correctedRows,
                new List<CorrectedSampleInfo>()
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply optimization for project {ProjectId}", request.ProjectId);
            return Result<CorrectionResultDto>.Fail($"Failed to apply optimization: {ex.Message}");
        }
    }

    #endregion

    #region Delete Rows

    public async Task<Result<int>> DeleteRowsAsync(DeleteRowsRequest request)
    {
        try
        {
            _logger.LogInformation("Deleting {Count} rows from project {ProjectId}",
                request.SolutionLabels.Count, request.ProjectId);

            // Save undo state
            await SaveUndoStateAsync(request.ProjectId, $"Delete_{request.SolutionLabels.Count}_rows");

            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            var rowsToDelete = new List<RawDataRow>();

            // Regex to detect CRM/RM samples that should NOT be deleted
            var rmPattern = new System.Text.RegularExpressions.Regex(
                @"^(OREAS|SRM|CRM|NIST|BCR|TILL|GBW)[\s\-_]*(\d+|BLANK)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var row in rawRows)
            {
                try
                {
                    // Use SampleId as the primary identifier (set during import)
                    var sampleId = row.SampleId ?? "Unknown";
                    
                    // PROTECT CRM/RM samples from deletion - they should never be deleted
                    // even if their Solution Label matches a blank pattern
                    if (rmPattern.IsMatch(sampleId))
                    {
                        _logger.LogDebug("Protecting CRM/RM sample from deletion: {SampleId}", sampleId);
                        continue;
                    }

                    if (request.SolutionLabels.Contains(sampleId))
                    {
                        rowsToDelete.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse row for deletion check");
                }
            }

            if (rowsToDelete.Any())
            {
                _db.RawDataRows.RemoveRange(rowsToDelete);
                await _db.SaveChangesAsync();

                // Log the deletion
                await _changeLogService.LogChangeAsync(
                    request.ProjectId,
                    "DeleteRows",
                    request.ChangedBy,
                    details: $"Deleted {rowsToDelete.Count} rows: {string.Join(", ", request.SolutionLabels.Take(10))}{(request.SolutionLabels.Count > 10 ? "..." : "")}"
                );
            }

            _logger.LogInformation("Deleted {Count} rows from project {ProjectId}",
                rowsToDelete.Count, request.ProjectId);

            return Result<int>.Success(rowsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete rows for project {ProjectId}", request.ProjectId);
            return Result<int>.Fail($"Failed to delete rows: {ex.Message}");
        }
    }

    #endregion

    #region Undo

    public async Task<Result<bool>> UndoLastCorrectionAsync(Guid projectId)
    {
        try
        {
            var lastState = await _db.ProjectStates
                .Where(s => s.ProjectId == projectId && s.Description != null && s.Description.StartsWith("Undo:"))
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();

            if (lastState == null)
                return Result<bool>.Fail("No undo state found");

            var savedData = JsonSerializer.Deserialize<List<SavedRowData>>(lastState.Data);
            if (savedData == null)
                return Result<bool>.Fail("Invalid undo state data");

            var currentRows = await _db.RawDataRows
                .Where(r => r.ProjectId == projectId)
                .ToListAsync();

            _db.RawDataRows.RemoveRange(currentRows);

            foreach (var saved in savedData)
            {
                _db.RawDataRows.Add(new RawDataRow
                {
                    ProjectId = projectId,
                    SampleId = saved.SampleId,
                    ColumnData = saved.ColumnData
                });
            }

            _db.ProjectStates.Remove(lastState);

            await _db.SaveChangesAsync();

            // Log undo action
            await _changeLogService.LogChangeAsync(
                projectId,
                "Undo",
                changedBy: null,
                details: $"Undone correction: {lastState.Description}"
            );

            _logger.LogInformation("Undo applied for project {ProjectId}", projectId);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo for project {ProjectId}", projectId);
            return Result<bool>.Fail($"Failed to undo: {ex.Message}");
        }
    }

    #endregion

    #region Private Helpers

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

    private record SavedRowData(string? SampleId, string ColumnData);

    #endregion
}