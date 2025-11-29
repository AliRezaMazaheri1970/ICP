using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Wrapper;
using System.Text.Json;

namespace Infrastructure.Services;

public class ProcessingService : IProcessingService
{
    private readonly IsatisDbContext _db;
    private readonly IEnumerable<IRowProcessor> _processors;
    private readonly ILogger<ProcessingService> _logger;

    public ProcessingService(IsatisDbContext db, IEnumerable<IRowProcessor> processors, ILogger<ProcessingService> logger)
    {
        _db = db;
        _processors = processors;
        _logger = logger;
    }

    // Note: return type changed to int to match ProjectState primary key type
    public async Task<Result<int>> ProcessProjectAsync(Guid projectId, bool overwriteLatestState = true)
    {
        try
        {
            var project = await _db.Projects
                .Include(p => p.RawDataRows)
                .FirstOrDefaultAsync(p => p.ProjectId == projectId);

            if (project == null) return Result<int>.Fail("Project not found");

            var accumulators = new List<Dictionary<string, object?>>();
            foreach (var _ in _processors) accumulators.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

            foreach (var row in project.RawDataRows.OrderBy(r => r.DataId))
            {
                Dictionary<string, object?> parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.ColumnData) ?? new Dictionary<string, object?>();
                }
                catch
                {
                    _logger.LogWarning("Failed to parse ColumnData for Row DataId={DataId} in Project {ProjectId}", row.DataId, projectId);
                    continue;
                }

                int idx = 0;
                foreach (var proc in _processors)
                {
                    proc.ProcessRow(parsed, accumulators[idx]);
                    idx++;
                }
            }

            for (int i = 0; i < _processors.Count(); i++)
            {
                _processors.ElementAt(i).Finalize(accumulators[i]);
            }

            var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < accumulators.Count; i++)
            {
                foreach (var kv in accumulators[i])
                {
                    var key = kv.Key;
                    if (merged.ContainsKey(key)) key = $"proc{i}_{key}";
                    merged[key] = kv.Value;
                }
            }

            var stateJson = JsonSerializer.Serialize(merged);
            var now = DateTime.UtcNow;

            var state = new ProjectState
            {
                ProjectId = project.ProjectId,
                Data = stateJson,
                Timestamp = now,
                Description = "ProcessedSummary"
            };

            _db.ProjectStates.Add(state);

            if (overwriteLatestState)
            {
                project.LastModifiedAt = now;
                _db.Projects.Update(project);
            }

            await _db.SaveChangesAsync();

            // IMPORTANT: ProjectState primary key is assumed to be int (StateId). Return that.
            return Result<int>.Success(state.StateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for project {ProjectId}", projectId);
            return Result<int>.Fail($"Processing failed: {ex.Message}");
        }
    }

    public Task<Result<Guid>> EnqueueProcessProjectAsync(Guid projectId)
    {
        _ = Task.Run(async () =>
        {
            await ProcessProjectAsync(projectId);
        });

        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}