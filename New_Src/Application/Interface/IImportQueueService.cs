using System;
using System.IO;
using System.Threading.Tasks;

namespace Application.Services
{
    public interface IImportQueueService
    {
        // Existing API for enqueuing import streams (if present)
        Task<Guid> EnqueueImportAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null);

        // New: enqueue a processing job for an existing project (background)
        Task<Guid> EnqueueProcessJobAsync(Guid projectId);

        // Existing API for status lookup
        Task<Shared.Models.ImportJobStatusDto?> GetStatusAsync(Guid jobId);
    }
}