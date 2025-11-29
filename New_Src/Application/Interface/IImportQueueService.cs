using Shared.Models;

namespace Application.Services;

public interface IImportQueueService
{
    /// Enqueue CSV stream for background import. Caller should provide stream content (method will copy it).
    Task<Guid> EnqueueImportAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null);

    /// Get job status by id. Returns null if not found.
    Task<ImportJobStatusDto?> GetStatusAsync(Guid jobId);
}