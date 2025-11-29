using Domain.Entities;
using Shared.Wrapper;

namespace Application.Services;

public record RawDataDto(string ColumnData, string? SampleId);
public record ProjectSaveResult(Guid ProjectId);
public record ProjectLoadDto(Guid ProjectId, string ProjectName, DateTime CreatedAt, DateTime LastModifiedAt, string? Owner, List<RawDataDto> RawRows, string? LatestStateJson);

public interface IProjectPersistenceService
{
    Task<Result<ProjectSaveResult>> SaveProjectAsync(Guid projectId, string projectName, string? owner, List<RawDataDto>? rawRows, string? stateJson);
    Task<Result<ProjectLoadDto>> LoadProjectAsync(Guid projectId);
}