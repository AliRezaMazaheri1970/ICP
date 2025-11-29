using Shared.Wrapper;

namespace Application.Services;

public interface IImportService
{
    /// <summary>
    /// Import CSV stream into a new project.
    /// progress: optional IProgress<(int total, int processed)> used to report progress.
    /// Note: stream must be seekable (MemoryStream) for counting; if not seekable, method will copy it.
    /// </summary>
    Task<Result<ProjectSaveResult>> ImportCsvAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null, IProgress<(int total, int processed)>? progress = null);
}