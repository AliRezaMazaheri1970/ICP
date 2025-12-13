using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Represents raw column data for a project row.
/// </summary>
/// <param name="ColumnData">JSON string representing the column data.</param>
/// <param name="SampleId">Optional sample identifier.</param>
public record RawDataDto(string ColumnData, string? SampleId);

/// <summary>
/// Represents result after saving a project.
/// </summary>
/// <param name="ProjectId">The identifier for the saved project.</param>
public record ProjectSaveResult(Guid ProjectId);

/// <summary>
/// Represents detailed project information loaded from persistence.
/// </summary>
public record ProjectLoadDto(
    Guid ProjectId,
    string ProjectName,
    DateTime CreatedAt,
    DateTime LastModifiedAt,
    string? Owner,
    List<RawDataDto> RawRows,
    string? LatestStateJson
);

/// <summary>
/// Represents a summary item for a project list.
/// </summary>
public record ProjectListItemDto(
    Guid ProjectId,
    string ProjectName,
    DateTime CreatedAt,
    DateTime LastModifiedAt,
    string? Owner,
    int RawRowsCount
);

/// <summary>
/// Defines services for creating, retrieving, listing, and deleting projects.
/// </summary>
public interface IProjectPersistenceService
{
    /// <summary>
    /// Creates or updates a project with optional raw data and initial state.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="owner">The owner of the project.</param>
    /// <param name="rawRows">Optional list of raw data rows.</param>
    /// <param name="stateJson">Optional initial state JSON.</param>
    /// <returns>The result of the save operation.</returns>
    Task<Result<ProjectSaveResult>> SaveProjectAsync(Guid projectId, string projectName, string? owner, List<RawDataDto>? rawRows, string? stateJson);

    /// <summary>
    /// Loads the full details of a specific project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The loaded project details.</returns>
    Task<Result<ProjectLoadDto>> LoadProjectAsync(Guid projectId);

    /// <summary>
    /// Retrieves a paginated list of projects.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A list of project summaries.</returns>
    Task<Result<List<ProjectListItemDto>>> ListProjectsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Deletes a project and its associated data.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>True if deletion was successful; otherwise, false.</returns>
    Task<Result<bool>> DeleteProjectAsync(Guid projectId);
}