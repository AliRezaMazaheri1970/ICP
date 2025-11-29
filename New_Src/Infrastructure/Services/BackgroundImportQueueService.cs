using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Wrapper;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Infrastructure.Services;

public class BackgroundImportQueueService : BackgroundService, IImportQueueService
{
    private readonly Channel<ImportRequest> _channel;
    private readonly ConcurrentDictionary<Guid, ImportJobStatusDto> _statuses;
    private readonly ILogger<BackgroundImportQueueService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _tempDir;
    private readonly int _persistEveryN;

    public BackgroundImportQueueService(IServiceProvider serviceProvider, ILogger<BackgroundImportQueueService> logger, IConfiguration configuration)
    {
        _channel = Channel.CreateUnbounded<ImportRequest>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _statuses = new ConcurrentDictionary<Guid, ImportJobStatusDto>();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read temp dir from configuration or fallback
        _tempDir = configuration?["Import:TempPath"] ?? Path.Combine(Path.GetTempPath(), "icp_imports");
        Directory.CreateDirectory(_tempDir);

        // how often to persist progress to DB (rows)
        _persistEveryN = int.TryParse(configuration?["Import:PersistEveryRows"], out var n) ? Math.Max(1, n) : 10;
    }

    private class ImportRequest
    {
        public Guid JobId { get; init; }
        public string TempFilePath { get; init; } = string.Empty;
        public string ProjectName { get; init; } = string.Empty;
        public string? Owner { get; init; }
        public string? StateJson { get; init; }
    }

    public async Task<Guid> EnqueueImportAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null)
    {
        if (csvStream == null) throw new ArgumentNullException(nameof(csvStream));

        var jobId = Guid.NewGuid();

        // persist file to disk first
        var fileName = $"{jobId:N}.csv";
        var filePath = Path.Combine(_tempDir, fileName);

        try
        {
            // write upload stream to temp file (ensure stream positioned at 0 if needed)
            if (csvStream.CanSeek) csvStream.Position = 0;
            using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await csvStream.CopyToAsync(fs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write uploaded csv to temp file for job {JobId}", jobId);
            throw;
        }

        // Persist initial job record to DB with TempFilePath
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IsatisDbContext>();

            var jobEntity = new ProjectImportJob
            {
                JobId = jobId,
                ProjectName = projectName,
                State = (int)ImportJobState.Pending,
                TotalRows = 0,
                ProcessedRows = 0,
                Percent = 0,
                Message = "Queued",
                TempFilePath = filePath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.ProjectImportJobs.Add(jobEntity);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist initial import job {JobId} to DB", jobId);
            // continue enqueue even if persist fails; status will remain in-memory
        }

        var initialStatus = new ImportJobStatusDto(jobId, ImportJobState.Pending, TotalRows: 0, ProcessedRows: 0, Message: "Queued", ProjectId: null, Percent: 0);
        _statuses[jobId] = initialStatus;

        var req = new ImportRequest
        {
            JobId = jobId,
            TempFilePath = filePath,
            ProjectName = projectName,
            Owner = owner,
            StateJson = stateJson
        };

        await _channel.Writer.WriteAsync(req);
        _logger.LogInformation("Enqueued import job {JobId} (file={FilePath}) for project {ProjectName}", jobId, filePath, projectName);

        return jobId;
    }

    public Task<ImportJobStatusDto?> GetStatusAsync(Guid jobId)
    {
        _statuses.TryGetValue(jobId, out var status);
        return Task.FromResult<ImportJobStatusDto?>(status);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundImportQueueService started.");
        try
        {
            await foreach (var req in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) break;

                // update in-memory status => Running
                _statuses.AddOrUpdate(req.JobId,
                    _ => new ImportJobStatusDto(req.JobId, ImportJobState.Running, 0, 0, "Running", null, 0),
                    (_, __) => new ImportJobStatusDto(req.JobId, ImportJobState.Running, 0, 0, "Running", null, 0));

                try
                {
                    _logger.LogInformation("Processing import job {JobId} from file {FilePath}", req.JobId, req.TempFilePath);

                    // open file stream for processing
                    using var fsRead = new FileStream(req.TempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

                    // Create a scope for scoped services (IImportService, DbContext, etc.)
                    using var scope = _serviceProvider.CreateScope();
                    var importService = scope.ServiceProvider.GetRequiredService<IImportService>();
                    var db = scope.ServiceProvider.GetRequiredService<IsatisDbContext>();

                    // Try to load the job entity to update progress in DB
                    var jobEntity = await db.ProjectImportJobs.FindAsync(new object[] { req.JobId }, cancellationToken: stoppingToken);
                    if (jobEntity == null)
                    {
                        // if not found, create one (defensive)
                        jobEntity = new ProjectImportJob
                        {
                            JobId = req.JobId,
                            ProjectName = req.ProjectName,
                            State = (int)ImportJobState.Running,
                            TotalRows = 0,
                            ProcessedRows = 0,
                            Percent = 0,
                            Message = "Running",
                            TempFilePath = req.TempFilePath,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        db.ProjectImportJobs.Add(jobEntity);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    else
                    {
                        jobEntity.State = (int)ImportJobState.Running;
                        jobEntity.UpdatedAt = DateTime.UtcNow;
                        jobEntity.Message = "Running";
                        db.ProjectImportJobs.Update(jobEntity);
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    // Create a progress reporter that updates the status dictionary and persists occasionally
                    var progress = new Progress<(int total, int processed)>(t =>
                    {
                        var (totalRows, processedRows) = t;
                        var percent = totalRows == 0 ? 0 : (int)Math.Round((processedRows * 100.0) / totalRows);

                        // update in-memory copy
                        _statuses.AddOrUpdate(req.JobId,
                            _ => new ImportJobStatusDto(req.JobId, ImportJobState.Running, totalRows, processedRows, "Running", null, percent),
                            (_, __) => new ImportJobStatusDto(req.JobId, ImportJobState.Running, totalRows, processedRows, "Running", null, percent));

                        // persist occasionally (every _persistEveryN rows or at completion) - fire-and-forget on scope db
                        if (processedRows % _persistEveryN == 0 || processedRows == totalRows)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var entity = await db.ProjectImportJobs.FindAsync(req.JobId);
                                    if (entity != null)
                                    {
                                        entity.TotalRows = totalRows;
                                        entity.ProcessedRows = processedRows;
                                        entity.Percent = percent;
                                        entity.UpdatedAt = DateTime.UtcNow;
                                        entity.Message = "Running";
                                        db.ProjectImportJobs.Update(entity);
                                        await db.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to persist progress for job {JobId}", req.JobId);
                                }
                            });
                        }
                    });

                    // Call import service using the FileStream wrapped as Stream
                    var result = await importService.ImportCsvAsync(fsRead, req.ProjectName, req.Owner, req.StateJson, progress);

                    if (result.Succeeded)
                    {
                        var projectId = result.Data?.ProjectId;

                        // update DB record final state
                        try
                        {
                            var entity = await db.ProjectImportJobs.FindAsync(req.JobId);
                            if (entity != null)
                            {
                                entity.ResultProjectId = projectId;
                                entity.State = (int)ImportJobState.Completed;
                                entity.Percent = 100;
                                entity.ProcessedRows = entity.TotalRows; // best-effort
                                entity.UpdatedAt = DateTime.UtcNow;
                                entity.Message = "Completed";
                                db.ProjectImportJobs.Update(entity);
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to persist completion for job {JobId}", req.JobId);
                        }

                        _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Completed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, "Completed", projectId, 100);
                        _logger.LogInformation("Import job {JobId} completed. ProjectId: {ProjectId}", req.JobId, projectId);
                    }
                    else
                    {
                        var msg = result.Messages.FirstOrDefault() ?? "Import failed";

                        // persist failure
                        try
                        {
                            var entity = await db.ProjectImportJobs.FindAsync(req.JobId);
                            if (entity != null)
                            {
                                entity.State = (int)ImportJobState.Failed;
                                entity.Message = msg;
                                entity.UpdatedAt = DateTime.UtcNow;
                                db.ProjectImportJobs.Update(entity);
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to persist failure for job {JobId}", req.JobId);
                        }

                        _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Failed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, msg, null, _statuses[req.JobId].Percent);
                        _logger.LogWarning("Import job {JobId} failed: {Msg}", req.JobId, msg);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception while processing import job {JobId}", req.JobId);
                    _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Failed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, ex.Message, null, _statuses[req.JobId].Percent);

                    // try persist error
                    try
                    {
                        using var scope2 = _serviceProvider.CreateScope();
                        var db2 = scope2.ServiceProvider.GetRequiredService<IsatisDbContext>();
                        var entity2 = await db2.ProjectImportJobs.FindAsync(req.JobId);
                        if (entity2 != null)
                        {
                            entity2.State = (int)ImportJobState.Failed;
                            entity2.Message = ex.Message;
                            entity2.UpdatedAt = DateTime.UtcNow;
                            db2.ProjectImportJobs.Update(entity2);
                            await db2.SaveChangesAsync();
                        }
                    }
                    catch (Exception warnEx)
                    {
                        _logger.LogWarning(warnEx, "Failed to persist exception for job {JobId}", req.JobId);
                    }
                }
                finally
                {
                    // delete temp file after processing attempt (success or failure)
                    try
                    {
                        if (File.Exists(req.TempFilePath))
                        {
                            File.Delete(req.TempFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file for job {JobId} at {Path}", req.JobId, req.TempFilePath);
                    }

                    try { /* nothing */ } catch { }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BackgroundImportQueueService cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackgroundImportQueueService terminated with error.");
        }
        _logger.LogInformation("BackgroundImportQueueService stopped.");
    }
}