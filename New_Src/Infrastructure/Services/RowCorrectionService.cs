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

public class RowCorrectionService : BaseCorrectionService, IRowCorrectionService
{
    public RowCorrectionService(
        IsatisDbContext db,
        IChangeLogService changeLogService,
        ILogger<RowCorrectionService> logger)
        : base(db, changeLogService, logger)
    {
    }

    // ✅ Updated Method based on your request
    public async Task<Result<List<EmptyRowDto>>> FindEmptyRowsAsync(FindEmptyRowsRequest request)
    {
        try
        {
            var rawRows = await _db.RawDataRows
                .AsNoTracking()
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            if (!rawRows.Any())
                return Result<List<EmptyRowDto>>.Fail("No data found for this project.");

            var pivotedData = new Dictionary<string, Dictionary<string, decimal?>>();
            var allElements = new HashSet<string>();

            // Default: Na, Ca, Al, Mg, K
            var elementsToCheck = request.ElementsToCheck?.Any() == true
                ? request.ElementsToCheck.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Na", "Ca", "Al", "Mg", "K" };

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ColumnData);
                    var root = doc.RootElement;

                    string sampleId = row.SampleId ?? "Unknown";
                    if (root.TryGetProperty("Solution Label", out var labelElement))
                    {
                        var label = labelElement.GetString();
                        if (!string.IsNullOrWhiteSpace(label))
                            sampleId = label;
                    }

                    if (!root.TryGetProperty("Element", out var elementProp))
                        continue;
                    string? elementName = elementProp.GetString();
                    if (string.IsNullOrWhiteSpace(elementName))
                        continue;

                    string elementPrefix = elementName.Split(' ')[0];
                    if (!elementsToCheck.Contains(elementPrefix))
                        continue;

                    // Extract Soln Conc
                    decimal? value = null;
                    if (root.TryGetProperty("Soln Conc", out var solnConcElement))
                    {
                        if (solnConcElement.ValueKind == JsonValueKind.Number)
                        {
                            value = solnConcElement.GetDecimal();
                        }
                        else if (solnConcElement.ValueKind == JsonValueKind.String)
                        {
                            var strVal = solnConcElement.GetString();
                            // "-----" = null
                            if (!string.IsNullOrWhiteSpace(strVal) &&
                                strVal != "-----" &&
                                decimal.TryParse(strVal, out var parsed))
                            {
                                value = parsed;
                            }
                        }
                        // If it was null in JSON, value remains null
                    }

                    if (!pivotedData.ContainsKey(sampleId))
                        pivotedData[sampleId] = new Dictionary<string, decimal?>();

                    pivotedData[sampleId][elementName] = value;
                    allElements.Add(elementName);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Error parsing row {SampleId}: {Message}", row.SampleId, ex.Message);
                }
            }

            if (!pivotedData.Any())
                return Result<List<EmptyRowDto>>.Success(new List<EmptyRowDto>());

            // Calculate mean (only non-null values)
            var columnMeans = new Dictionary<string, decimal>();
            foreach (var elem in allElements)
            {
                var validValues = new List<decimal>();
                foreach (var sample in pivotedData.Values)
                {
                    if (sample.TryGetValue(elem, out var val) && val.HasValue)
                    {
                        validValues.Add(val.Value);
                    }
                }
                columnMeans[elem] = validValues.Any() ? validValues.Average() : 0m;
            }

            var emptyRows = new List<EmptyRowDto>();

            // threshold = mean * (1 - percent/100)
            decimal effectivePercent = request.ThresholdPercent > 0 ? request.ThresholdPercent : 70m;
            decimal thresholdFactor = 1m - (effectivePercent / 100m);

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
                    if (!values.TryGetValue(elem, out var valNullable))
                        continue;
                    if (!columnMeans.TryGetValue(elem, out var mean))
                        continue;

                    decimal threshold = mean * thresholdFactor;
                    rowValuesNullable[elem] = valNullable;

                    // Count all elements
                    totalElementsChecked++;

                    // ✅ Only null = missing
                    // 0 is a real value (like Blank)
                    if (!valNullable.HasValue)
                    {
                        details[elem] = 0;
                        continue;  // null = NOT below threshold
                    }

                    decimal val = valNullable.Value;
                    details[elem] = mean != 0 ? (val / mean) * 100m : 0m;

                    // val < threshold
                    if (val < threshold)
                    {
                        belowThresholdCount++;
                    }
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
                            columnMeans,
                            details,
                            belowThresholdCount,
                            totalElementsChecked,
                            emptyScore
                        ));
                    }
                }
            }

            _logger.LogInformation("Found {Count} empty rows", emptyRows.Count);

            return Result<List<EmptyRowDto>>.Success(emptyRows.OrderByDescending(x => x.OverallScore).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FindEmptyRowsAsync");
            return Result<List<EmptyRowDto>>.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<Result<int>> DeleteRowsAsync(DeleteRowsRequest request)
    {
        try
        {
            _logger.LogInformation("Deleting {Count} rows from project {ProjectId}",
                request.SolutionLabels.Count, request.ProjectId);

            await SaveUndoStateAsync(request.ProjectId, $"Delete_{request.SolutionLabels.Count}_rows");

            var rawRows = await _db.RawDataRows
                .Where(r => r.ProjectId == request.ProjectId)
                .ToListAsync();

            var rowsToDelete = new List<RawDataRow>();

            var rmPattern = new Regex(
                @"^(OREAS|SRM|CRM|NIST|BCR|TILL|GBW)[\s\-_]*(\d+|BLANK)?",
                RegexOptions.IgnoreCase);

            foreach (var row in rawRows)
            {
                try
                {
                    var sampleId = row.SampleId ?? "Unknown";

                    if (rmPattern.IsMatch(sampleId))
                    {
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

                await _changeLogService.LogChangeAsync(
                    request.ProjectId,
                    "DeleteRows",
                    request.ChangedBy,
                    details: $"Deleted {rowsToDelete.Count} rows: {string.Join(", ", request.SolutionLabels.Take(10))}{(request.SolutionLabels.Count > 10 ? "..." : "")}"
                );
            }

            return Result<int>.Success(rowsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete rows for project {ProjectId}", request.ProjectId);
            return Result<int>.Fail($"Failed to delete rows: {ex.Message}");
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
}