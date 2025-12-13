namespace Application.Services;

/// <summary>
/// Defines services for queuing import and processing jobs for background execution.
/// </summary>
public interface IImportQueueService
{
    /// <summary>
    /// Enqueues a request to import a CSV stream.
    /// </summary>
    /// <param name="csvStream">The data stream to import.</param>
    /// <param name="projectName">The name of the target project.</param>
    /// <param name="owner">The owner of the project.</param>
    /// <param name="stateJson">Optional initial state JSON.</param>
    /// <returns>The unique identifier of the queued job.</returns>
    Task<Guid> EnqueueImportAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null);

    /// <summary>
    /// Enqueues a processing job for an existing project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The unique identifier of the queued job.</returns>
    Task<Guid> EnqueueProcessJobAsync(Guid projectId);

    /// <summary>
    /// Retrieves the current status of a queued job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <returns>The status object, or null if not found.</returns>
    Task<Shared.Models.ImportJobStatusDto?> GetStatusAsync(Guid jobId);
}