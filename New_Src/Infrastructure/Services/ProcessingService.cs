using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implementation of IProcessingService.
    /// - EnqueueProcessProjectAsync: create a process job via IImportQueueService so it's both persisted and enqueued.
    /// - ProcessProjectAsync: run the processing logic (compute summary, persist ProcessedData + ProjectState).
    /// </summary>
    public class ProcessingService : IProcessingService
    {
        private readonly IsatisDbContext _db;
        private readonly IImportQueueService _queue;
        private readonly ILogger<ProcessingService> _logger;

        public ProcessingService(IsatisDbContext db, IImportQueueService queue, ILogger<ProcessingService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Enqueue processing: create a job row via the queue service so it's both persisted and enqueued into the in-memory channel.
        /// Returns ProcessingResult with Data = jobId (Guid) on success.
        /// </summary>
        public async Task<ProcessingResult> EnqueueProcessProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return ProcessingResult.Failure("Invalid projectId", data: null);

            try
            {
                // Use the central queue service to create+enqueue the process job
                var jobId = await _queue.EnqueueProcessJobAsync(projectId);
                return ProcessingResult.Success(jobId, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue processing job for project {ProjectId}", projectId);
                return ProcessingResult.Failure($"Failed to enqueue job: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a project synchronously (used by background worker).
        /// Returns ProcessingResult with Data = numeric projectStateId (int) on success.
        /// </summary>
        public async Task<ProcessingResult> ProcessProjectAsync(Guid projectId, bool overwriteLatestState = true, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return ProcessingResult.Failure("Invalid projectId");

            try
            {
                // Check project exists
                var project = await _db.Projects
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(p => p.ProjectId == projectId, cancellationToken);

                if (project is null)
                    return ProcessingResult.Failure("Project not found");

                // Load raw rows' ColumnData
                var rawRows = await _db.Set<RawDataRow>()
                                       .AsNoTracking()
                                       .Where(r => r.ProjectId == projectId)
                                       .Select(r => r.ColumnData)
                                       .ToListAsync(cancellationToken);

                if (rawRows.Count == 0)
                {
                    var emptySnapshot = JsonSerializer.Serialize(new
                    {
                        projectId,
                        processedAt = DateTime.UtcNow,
                        rowCount = 0,
                        summary = new { }
                    });

                    var emptyState = new ProjectState
                    {
                        ProjectId = projectId,
                        Data = emptySnapshot,
                        Description = "Processed (no rows)",
                        Timestamp = DateTime.UtcNow
                    };

                    await _db.Set<ProjectState>().AddAsync(emptyState, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);

                    // Return the numeric state id as Data
                    return ProcessingResult.Success(emptyState.StateId, Array.Empty<string>());
                }

                // Parse JSON rows and bucket numeric values by key
                var numericBuckets = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

                foreach (var cd in rawRows)
                {
                    if (string.IsNullOrWhiteSpace(cd))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(cd);
                        if (doc.RootElement.ValueKind != JsonValueKind.Object)
                            continue;

                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var d))
                            {
                                if (!numericBuckets.TryGetValue(prop.Name, out var list))
                                {
                                    list = new List<double>();
                                    numericBuckets[prop.Name] = list;
                                }
                                list.Add(d);
                            }
                        }
                    }
                    catch (JsonException jex)
                    {
                        _logger.LogWarning(jex, "Skipping malformed raw row for project {ProjectId}", projectId);
                        continue;
                    }
                }

                // Build summary
                var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in numericBuckets)
                {
                    var arr = kv.Value;
                    var count = arr.Count;
                    var sum = arr.Sum();
                    var mean = count > 0 ? sum / count : 0.0;
                    summary[kv.Key] = new { count, mean };
                }

                var snapshot = new
                {
                    projectId,
                    processedAt = DateTime.UtcNow,
                    rowCount = rawRows.Count,
                    summary
                };

                var snapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = false });

                // Determine next ProcessedId
                var nextProcessedId = 1;
                try
                {
                    var maxProcessedId = await _db.Set<ProcessedData>()
                                                  .Where(p => p.ProjectId == projectId)
                                                  .MaxAsync(p => (int?)p.ProcessedId, cancellationToken);
                    nextProcessedId = (maxProcessedId ?? 0) + 1;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not compute max ProcessedId for project {ProjectId}, defaulting to 1", projectId);
                }

                var processed = new ProcessedData
                {
                    ProjectId = projectId,
                    ProcessedId = nextProcessedId,
                    AnalysisType = "Summary",
                    Data = snapshotJson,
                    CreatedAt = DateTime.UtcNow
                };

                var state = new ProjectState
                {
                    ProjectId = projectId,
                    Data = snapshotJson,
                    Description = "Auto-generated summary",
                    Timestamp = DateTime.UtcNow
                };

                // Persist within a transaction
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await _db.Set<ProcessedData>().AddAsync(processed, cancellationToken);
                    await _db.Set<ProjectState>().AddAsync(state, cancellationToken);

                    // Update project's LastModifiedAt
                    var existingProject = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId, cancellationToken);
                    if (existingProject != null)
                    {
                        existingProject.LastModifiedAt = DateTime.UtcNow;
                        _db.Projects.Update(existingProject);
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    _logger.LogInformation("Processed project {ProjectId}: ProcessedId={ProcessedId}, ProjectStateId={StateId}",
                        projectId, processed.ProcessedId, state.StateId);

                    // Return the numeric state id as Data
                    return ProcessingResult.Success(state.StateId, Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    try { await tx.RollbackAsync(cancellationToken); } catch { /* swallow */ }
                    _logger.LogError(ex, "Processing failed for project {ProjectId}", projectId);
                    return ProcessingResult.Failure($"Processing failed: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Processing cancelled for project {ProjectId}", projectId);
                return ProcessingResult.Failure("Processing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing project {ProjectId}", projectId);
                return ProcessingResult.Failure(ex.Message);
            }
        }
    }
}