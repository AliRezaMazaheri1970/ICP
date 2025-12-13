namespace Application.Services;

/// <summary>
/// Represents the result of a processing operation.
/// </summary>
/// <param name="Succeeded">Indicates if the operation was successful.</param>
/// <param name="Data">Optional data returned by the operation.</param>
/// <param name="Messages">Optional list of messages (info, warning, etc.).</param>
/// <param name="Error">Optional error message if failed.</param>
public sealed record ProcessingResult(
    bool Succeeded,
    object? Data = null,
    IEnumerable<string>? Messages = null,
    string? Error = null
)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="data">The success data.</param>
    /// <param name="messages">Optional success messages.</param>
    /// <returns>A successful ProcessingResult.</returns>
    public static ProcessingResult Success(object? data = null, IEnumerable<string>? messages = null)
        => new ProcessingResult(true, data, messages, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="data">Optional partial data.</param>
    /// <param name="messages">Optional messages.</param>
    /// <returns>A failed ProcessingResult.</returns>
    public static ProcessingResult Failure(string error, object? data = null, IEnumerable<string>? messages = null)
        => new ProcessingResult(false, data, messages, error);
}

/// <summary>
/// Defines services for processing project data.
/// </summary>
public interface IProcessingService
{
    /// <summary>
    /// Processes a project synchronously.
    /// </summary>
    /// <param name="projectId">The identifier of the project to process.</param>
    /// <param name="overwriteLatestState">Whether to overwrite the latest state.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the processing.</returns>
    Task<ProcessingResult> ProcessProjectAsync(Guid projectId, bool overwriteLatestState = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a project for background processing.
    /// </summary>
    /// <param name="projectId">The identifier of the project to process.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result containing job ID or status.</returns>
    Task<ProcessingResult> EnqueueProcessProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}