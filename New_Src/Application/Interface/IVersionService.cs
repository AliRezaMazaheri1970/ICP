using Domain.Entities;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Represents a node in the project version tree.
/// </summary>
/// <param name="StateId">The unique ID of the state.</param>
/// <param name="ParentStateId">The ID of the parent state.</param>
/// <param name="VersionNumber">The version number.</param>
/// <param name="ProcessingType">The type of processing that created this version.</param>
/// <param name="Description">Description of the version.</param>
/// <param name="Timestamp">Creation timestamp.</param>
/// <param name="IsActive">Whether this is the currently active version.</param>
/// <param name="Children">Child nodes (branches).</param>
public record VersionNodeDto(
    int StateId,
    int? ParentStateId,
    int VersionNumber,
    string ProcessingType,
    string? Description,
    DateTime Timestamp,
    bool IsActive,
    List<VersionNodeDto> Children
);

/// <summary>
/// Represents a request to create a new version.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ParentStateId">The parent state identifier.</param>
/// <param name="ProcessingType">The processing type.</param>
/// <param name="Data">The serialized state data.</param>
/// <param name="Description">Optional description.</param>
public record CreateVersionDto(
    Guid ProjectId,
    int? ParentStateId,
    string ProcessingType,
    string Data,
    string? Description
);

/// <summary>
/// Defines services for managing project history, versioning, and branching.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Creates a new version state for a project.
    /// </summary>
    /// <param name="dto">The version creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created project state.</returns>
    Task<Result<ProjectState>> CreateVersionAsync(CreateVersionDto dto, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the full version tree for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Average of version nodes representing the tree.</returns>
    Task<Result<List<VersionNodeDto>>> GetVersionTreeAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all versions as a flat list.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of project states.</returns>
    Task<Result<List<ProjectState>>> GetAllVersionsAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the currently active version of a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active project state.</returns>
    Task<Result<ProjectState?>> GetActiveVersionAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Sets a specific version as the active version.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="stateId">The state ID to activate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the switch was successful.</returns>
    Task<Result<bool>> SwitchToVersionAsync(Guid projectId, int stateId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific version by its ID.
    /// </summary>
    /// <param name="stateId">The state ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The project state.</returns>
    Task<Result<ProjectState?>> GetVersionAsync(int stateId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the lineage path from the root to a specific version.
    /// </summary>
    /// <param name="stateId">The target state ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of states representing the path.</returns>
    Task<Result<List<ProjectState>>> GetVersionPathAsync(int stateId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a version and optionally its children.
    /// </summary>
    /// <param name="stateId">The state ID to delete.</param>
    /// <param name="deleteChildren">Whether to delete all descendant states.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if deletion was successful.</returns>
    Task<Result<bool>> DeleteVersionAsync(int stateId, bool deleteChildren = false, CancellationToken ct = default);

    /// <summary>
    /// Creates a fork (branch) from an existing version.
    /// </summary>
    /// <param name="parentStateId">The parent state to fork from.</param>
    /// <param name="processingType">The processing type for the new fork.</param>
    /// <param name="data">The state data.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created fork state.</returns>
    Task<Result<ProjectState>> ForkVersionAsync(int parentStateId, string processingType, string data, string? description = null, CancellationToken ct = default);
}
