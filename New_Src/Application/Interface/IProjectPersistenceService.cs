using Shared.Wrapper;

namespace Application.Services;

public record RawDataDto(string ColumnData, string? SampleId);
public record ProjectSaveResult(Guid ProjectId);
public record ProjectLoadDto(Guid ProjectId, string ProjectName, DateTime CreatedAt, DateTime LastModifiedAt, string? Owner, List<RawDataDto> RawRows, string? LatestStateJson);

// DTO used for listing projects
public record ProjectListItemDto(Guid ProjectId, string ProjectName, DateTime CreatedAt, DateTime LastModifiedAt, string? Owner, int RawRowsCount);

public interface IProjectPersistenceService
{
    Task<Result<ProjectSaveResult>> SaveProjectAsync(Guid projectId, string projectName, string? owner, List<RawDataDto>? rawRows, string? stateJson);
    Task<Result<ProjectLoadDto>> LoadProjectAsync(Guid projectId);

    // new: list with simple pagination (page starting at 1)
    Task<Result<List<ProjectListItemDto>>> ListProjectsAsync(int page = 1, int pageSize = 20);

    // new: delete project (cascades to related rows as configured in EF model)
    Task<Result<bool>> DeleteProjectAsync(Guid projectId);
}