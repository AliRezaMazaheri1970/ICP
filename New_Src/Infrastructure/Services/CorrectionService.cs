using System.Text.Json;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;

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

    /// <summary>
    /// Find empty/outlier rows based on element averages
    /// Logic from Python empty_check.py:
    /// - Calculate mean for each element column
    /// - If ALL (or most) elements in a row are less than threshold% of their column mean,
    ///   mark the row as "empty" or "outlier"
    /// 
    /// Python equivalent (empty_check.py line 500-502):
    ///   below_threshold = df_numeric < threshold_values
    ///   empty_rows_mask = below_threshold.all(axis=1)
    /// </summary>
    public async Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<EmptyRowDto>>.Fail("No data found for project");

            // Step 1: Parse all rows and collect element values
            var parsedRows = new List<(string SolutionLabel, Dictionary<string, decimal?> Values)>();
            var allElements = new HashSet<string>();

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    // Only check "Samp" type rows
                    if (root.TryGetProperty("Type", out var typeElement) &&
                        typeElement.GetString() != "Samp")
                        continue;

                    var solutionLabel = root.TryGetProperty("Solution Label", out var labelElement)
                        ? labelElement.GetString() ?? row.SampleId ?? "Unknown"
                        : row.SampleId ?? "Unknown";

                    var values = new Dictionary<string, decimal?>();

                    foreach (var prop in root.EnumerateObject())
                    {
                        // Skip non-element columns
                        if (prop.Name is "Solution Label" or "Type" or "Act Wgt" or "Act Vol" or "DF")
                            continue;

                        if (prop.Value.ValueKind == JsonValueKind.Number)
                        {
                            values[prop.Name] = prop.Value.GetDecimal();
                            allElements.Add(prop.Name);
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(prop.Value.GetString(), out var val))
                        {
                            values[prop.Name] = val;
                            allElements.Add(prop.Name);
                        }
                    }

                    if (values.Any())
                    {
                        parsedRows.Add((solutionLabel, values));
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            if (!parsedRows.Any())
                return Result<List<EmptyRowDto>>.Success(new List<EmptyRowDto>());

            // Filter elements to check if specified
            // Python default: {'Na', 'Ca', 'Al', 'Mg', 'K'}
            var elementsToCheck = request.ElementsToCheck?.ToHashSet() ?? allElements;

            // Step 2: Calculate mean for each element column
            // Python equivalent: column_means = df_numeric.mean()
            var elementMeans = new Dictionary<string, decimal>();
            foreach (var element in elementsToCheck)
            {
                var validValues = parsedRows
                    .Where(r => r.Values.TryGetValue(element, out var v) && v.HasValue && v.Value > 0)
                    .Select(r => r.Values[element]!.Value)
                    .ToList();

                if (validValues.Any())
                {
                    elementMeans[element] = validValues.Average();
                }
            }

            if (!elementMeans.Any())
                return Result<List<EmptyRowDto>>.Success(new List<EmptyRowDto>());

            // Step 3: Check each row against the threshold
            // Python equivalent: threshold_values = column_means * (1 - self.mean_percentage_threshold / 100)
            var emptyRows = new List<EmptyRowDto>();
            var thresholdFactor = (100m - request.ThresholdPercent) / 100m;

            foreach (var (solutionLabel, values) in parsedRows)
            {
                var percentOfAverage = new Dictionary<string, decimal>();
                var elementsBelowThreshold = 0;
                var totalChecked = 0;

                foreach (var element in elementsToCheck.Where(e => elementMeans.ContainsKey(e)))
                {
                    if (!values.TryGetValue(element, out var value) || !value.HasValue)
                        continue;

                    var mean = elementMeans[element];
                    if (mean <= 0) continue;

                    totalChecked++;
                    var percent = (value.Value / mean) * 100m;
                    percentOfAverage[element] = percent;

                    // Check if value is below threshold of mean
                    // Python: below_threshold = df_numeric < threshold_values
                    if (value.Value < mean * thresholdFactor)
                    {
                        elementsBelowThreshold++;
                    }
                }

                // Determine if row is empty based on mode
                if (totalChecked > 0)
                {
                    var overallScore = (decimal)elementsBelowThreshold / totalChecked * 100m;

                    // ✅ اصلاح شده: پشتیبانی از هر دو حالت
                    // RequireAllElements = true  → مثل پایتون: همه عناصر باید زیر آستانه باشند
                    // RequireAllElements = false → حالت قبلی: بیش از 50% کافیه
                    bool isEmptyRow;

                    if (request.RequireAllElements)
                    {
                        // Python-compatible: ALL elements must be below threshold
                        // Python equivalent (empty_check.py line 502): empty_rows_mask = below_threshold.all(axis=1)
                        isEmptyRow = elementsBelowThreshold == totalChecked && totalChecked > 0;
                    }
                    else
                    {
                        // Flexible mode: More than 50% below threshold
                        isEmptyRow = elementsBelowThreshold > 0 && overallScore >= 50;
                    }

                    if (isEmptyRow)
                    {
                        emptyRows.Add(new EmptyRowDto(
                            solutionLabel,
                            values,
                            elementMeans.Where(e => values.ContainsKey(e.Key))
                                .ToDictionary(e => e.Key, e => e.Value),
                            percentOfAverage,
                            elementsBelowThreshold,
                            totalChecked,
                            overallScore
                        ));
                    }
                }
            }

            // Sort by score (most "empty" first)
            var result = emptyRows.OrderByDescending(e => e.OverallScore).ToList();

            _logger.LogInformation("Found {Count} empty/outlier rows in project {ProjectId} (RequireAllElements={RequireAll})",
                result.Count, request.ProjectId, request.RequireAllElements);

            return Result<List<EmptyRowDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find empty rows for project {ProjectId}", request.ProjectId);
            return Result<List<EmptyRowDto>>.Fail($"Failed to find empty rows: {ex.Message}");
        }
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
                        continue;

                    if (!root.TryGetProperty("Act Wgt", out var weightElement))
                        continue;

                    // Handle null values
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

                    var newCorrCon = (request.NewWeight / oldWeight) * oldCorrCon;

                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ColumnData);
                    var newDict = new Dictionary<string, object>();

                    foreach (var kvp in dict!)
                    {
                        if (kvp.Key == "Act Wgt")
                            newDict[kvp.Key] = request.NewWeight;
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
                            oldWeight,
                            request.NewWeight,
                            oldCorrCon,
                            newCorrCon
                        ));

                        changeLogEntries.Add((solutionLabel, "Act Wgt", oldWeight.ToString(), request.NewWeight.ToString()));
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
                    "Weight",
                    changeLogEntries,
                    request.ChangedBy,
                    $"Weight correction: {correctedSamples.Count} samples corrected to {request.NewWeight}"
                );
            }

            _logger.LogInformation("Weight correction applied: {CorrectedRows} rows for project {ProjectId}",
                correctedRows, request.ProjectId);

            return Result<CorrectionResultDto>.Success(new CorrectionResultDto(
                rawRows.Count,
                correctedRows,
                correctedSamples
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply weight correction for project {ProjectId}", request.ProjectId);
            return Result<CorrectionResultDto>.Fail($"Failed to apply weight correction: {ex.Message}");
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