using Application.Services;
using Shared.Models;
using Shared.Wrapper;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class BackgroundImportQueueService : BackgroundService, IImportQueueService
{
    private readonly Channel<ImportRequest> _channel;
    private readonly ConcurrentDictionary<Guid, ImportJobStatusDto> _statuses;
    private readonly IImportService _importService;
    private readonly ILogger<BackgroundImportQueueService> _logger;

    public BackgroundImportQueueService(IImportService importService, ILogger<BackgroundImportQueueService> logger)
    {
        _channel = Channel.CreateUnbounded<ImportRequest>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _statuses = new ConcurrentDictionary<Guid, ImportJobStatusDto>();
        _importService = importService;
        _logger = logger;
    }

    private class ImportRequest
    {
        public Guid JobId { get; init; }
        public MemoryStream CsvStream { get; init; } = default!;
        public string ProjectName { get; init; } = string.Empty;
        public string? Owner { get; init; }
        public string? StateJson { get; init; }
    }

    public async Task<Guid> EnqueueImportAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null)
    {
        if (csvStream == null) throw new ArgumentNullException(nameof(csvStream));

        // Copy stream into MemoryStream to decouple lifecycle from HTTP request
        var ms = new MemoryStream();
        await csvStream.CopyToAsync(ms);
        ms.Position = 0;

        var jobId = Guid.NewGuid();
        var initialStatus = new ImportJobStatusDto(jobId, ImportJobState.Pending, TotalRows: 0, ProcessedRows: 0, Message: "Queued", ProjectId: null, Percent: 0);
        _statuses[jobId] = initialStatus;

        var req = new ImportRequest
        {
            JobId = jobId,
            CsvStream = ms,
            ProjectName = projectName,
            Owner = owner,
            StateJson = stateJson
        };

        await _channel.Writer.WriteAsync(req);
        _logger.LogInformation("Enqueued import job {JobId} for project {ProjectName}", jobId, projectName);

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

                // update status => Running
                _statuses.AddOrUpdate(req.JobId,
                    _ => new ImportJobStatusDto(req.JobId, ImportJobState.Running, 0, 0, "Running", null, 0),
                    (_, __) => new ImportJobStatusDto(req.JobId, ImportJobState.Running, 0, 0, "Running", null, 0));

                try
                {
                    _logger.LogInformation("Processing import job {JobId}", req.JobId);

                    req.CsvStream.Position = 0;

                    // Create a progress reporter that updates the status dictionary
                    var progress = new Progress<(int total, int processed)>(t =>
                    {
                        var (totalRows, processedRows) = t;
                        var percent = totalRows == 0 ? 0 : (int)Math.Round((processedRows * 100.0) / totalRows);
                        _statuses.AddOrUpdate(req.JobId,
                            _ => new ImportJobStatusDto(req.JobId, ImportJobState.Running, totalRows, processedRows, "Running", null, percent),
                            (_, __) => new ImportJobStatusDto(req.JobId, ImportJobState.Running, totalRows, processedRows, "Running", null, percent));
                    });

                    var result = await _importService.ImportCsvAsync(req.CsvStream, req.ProjectName, req.Owner, req.StateJson, progress);

                    if (result.Succeeded)
                    {
                        var projectId = result.Data?.ProjectId;
                        _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Completed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, "Completed", projectId, 100);
                        _logger.LogInformation("Import job {JobId} completed. ProjectId: {ProjectId}", req.JobId, projectId);
                    }
                    else
                    {
                        var msg = result.Messages.FirstOrDefault() ?? "Import failed";
                        _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Failed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, msg, null, _statuses[req.JobId].Percent);
                        _logger.LogWarning("Import job {JobId} failed: {Msg}", req.JobId, msg);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception while processing import job {JobId}", req.JobId);
                    _statuses[req.JobId] = new ImportJobStatusDto(req.JobId, ImportJobState.Failed, _statuses[req.JobId].TotalRows, _statuses[req.JobId].ProcessedRows, ex.Message, null, _statuses[req.JobId].Percent);
                }
                finally
                {
                    try { req.CsvStream.Dispose(); } catch { }
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