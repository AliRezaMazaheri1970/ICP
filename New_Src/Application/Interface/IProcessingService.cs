using Shared.Wrapper;

namespace Application.Services;

public interface IProcessingService
{
    /// <summary>
    /// Process a project synchronously. Returns ProjectState primary key (int) in result.Data.
    /// If project not found or error occurs returns Fail.
    /// </summary>
    Task<Result<int>> ProcessProjectAsync(Guid projectId, bool overwriteLatestState = true);

    /// <summary>
    /// Process a project asynchronously (enqueue to background). Returns job id (Guid).
    /// </summary>
    Task<Result<Guid>> EnqueueProcessProjectAsync(Guid projectId);
}