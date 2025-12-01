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

                    var weight = weightElement.GetDecimal();

                    if (weight < request.WeightMin || weight > request.WeightMax)
                    {
                        var corrCon = root.TryGetProperty("Corr Con", out var corrConElement)
                            ? corrConElement.GetDecimal()
                            : 0m;

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

                    var volume = volumeElement.GetDecimal();

                    if (volume != request.ExpectedVolume)
                    {
                        var corrCon = root.TryGetProperty("Corr Con", out var corrConElement)
                            ? corrConElement.GetDecimal()
                            : 0m;

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

                    var oldWeight = weightElement.GetDecimal();
                    if (oldWeight == 0) continue;

                    var oldCorrCon = root.TryGetProperty("Corr Con", out var corrConElement)
                        ? corrConElement.GetDecimal()
                        : 0m;

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

            // Log changes
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

                    var oldVolume = volumeElement.GetDecimal();
                    if (oldVolume == 0) continue;

                    var oldCorrCon = root.TryGetProperty("Corr Con", out var corrConElement)
                        ? corrConElement.GetDecimal()
                        : 0m;

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

            // Log changes
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
                        // استفاده از BlankScaleSettings
                        if (request.ElementSettings.TryGetValue(kvp.Key, out var settings) &&
                            kvp.Value.ValueKind == JsonValueKind.Number)
                        {
                            var originalValue = kvp.Value.GetDecimal();
                            var correctedValue = (originalValue + settings.Blank) * settings.Scale;
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

            // Log changes
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