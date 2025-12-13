namespace Application.Services;

/// <summary>
/// Defines services for logging and retrieving project modifications.
/// </summary>
public interface IChangeLogService
{
    /// <summary>
    /// Logs a single change to the audit log.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="changeType">The type of change (e.g., Update, Insert).</param>
    /// <param name="solutionLabel">The identifier of the affected sample, if applicable.</param>
    /// <param name="element">The affected element, if applicable.</param>
    /// <param name="oldValue">The value before the change.</param>
    /// <param name="newValue">The value after the change.</param>
    /// <param name="changedBy">The user who made the change.</param>
    /// <param name="details">Additional details about the change.</param>
    /// <param name="batchId">Optional batch identifier to group related changes.</param>
    Task LogChangeAsync(Guid projectId, string changeType, string? solutionLabel = null,
        string? element = null, string? oldValue = null, string? newValue = null,
        string? changedBy = null, string? details = null, Guid? batchId = null);

    /// <summary>
    /// Logs a batch of changes efficiently.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="changeType">The type of change.</param>
    /// <param name="changes">Collection of change details (SolutionLabel, Element, OldValue, NewValue).</param>
    /// <param name="changedBy">The user who made the changes.</param>
    /// <param name="details">Additional details.</param>
    Task LogBatchChangesAsync(Guid projectId, string changeType,
        IEnumerable<(string? SolutionLabel, string? Element, string? OldValue, string? NewValue)> changes,
        string? changedBy = null, string? details = null);

    /// <summary>
    /// Retrieves a paginated list of changes for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A list of change log entries.</returns>
    Task<List<ChangeLog>> GetChangeLogAsync(Guid projectId, int page = 1, int pageSize = 50);

    /// <summary>
    /// Retrieves changes filtered by change type.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="changeType">The change type to filter by.</param>
    /// <returns>A list of matching updated change logs.</returns>
    Task<List<ChangeLog>> GetChangesByTypeAsync(Guid projectId, string changeType);

    /// <summary>
    /// Retrieves changes filtered by sample solution label.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="solutionLabel">The solution label to filter by.</param>
    /// <returns>A list of matching updated change logs.</returns>
    Task<List<ChangeLog>> GetChangesBySampleAsync(Guid projectId, string solutionLabel);
}