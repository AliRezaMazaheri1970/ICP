using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Wrapper;

namespace Application.Services;

public class ProjectPersistenceService : IProjectPersistenceService
{
    private readonly IsatisDbContext _db;

    public ProjectPersistenceService(IsatisDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ProjectSaveResult>> SaveProjectAsync(Guid projectId, string projectName, string? owner, List<RawDataDto>? rawRows, string? stateJson)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);
            var now = DateTime.UtcNow;

            if (project == null)
            {
                project = new Project
                {
                    ProjectId = projectId == Guid.Empty ? Guid.NewGuid() : projectId,
                    ProjectName = projectName,
                    CreatedAt = now,
                    LastModifiedAt = now,
                    Owner = owner
                };
                _db.Projects.Add(project);
            }
            else
            {
                project.ProjectName = projectName;
                project.LastModifiedAt = now;
                project.Owner = owner;
                _db.Projects.Update(project);
            }

            if (rawRows != null && rawRows.Count > 0)
            {
                // For simplicity, append raw rows. If you want replace semantics, clear old rows first.
                foreach (var r in rawRows)
                {
                    _db.RawDataRows.Add(new RawDataRow
                    {
                        ProjectId = project.ProjectId,
                        ColumnData = r.ColumnData,
                        SampleId = r.SampleId
                    });
                }
            }

            if (!string.IsNullOrEmpty(stateJson))
            {
                _db.ProjectStates.Add(new ProjectState
                {
                    ProjectId = project.ProjectId,
                    Data = stateJson,
                    Timestamp = now,
                    Description = "ManualSave"
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result<ProjectSaveResult>.Success(new ProjectSaveResult(project.ProjectId));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Result<ProjectSaveResult>.Fail($"Save failed: {ex.Message}");
        }
    }

    public async Task<Result<ProjectLoadDto>> LoadProjectAsync(Guid projectId)
    {
        try
        {
            var project = await _db.Projects
                .Include(p => p.RawDataRows)
                .Include(p => p.ProjectStates)
                .FirstOrDefaultAsync(p => p.ProjectId == projectId);

            if (project == null)
                return Result<ProjectLoadDto>.Fail("Project not found.");

            var rawRows = project.RawDataRows
                .OrderBy(r => r.DataId)
                .Select(r => new RawDataDto(r.ColumnData, r.SampleId))
                .ToList();

            var latestState = project.ProjectStates
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault()?.Data;

            var dto = new ProjectLoadDto(project.ProjectId, project.ProjectName, project.CreatedAt, project.LastModifiedAt, project.Owner, rawRows, latestState);

            return Result<ProjectLoadDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<ProjectLoadDto>.Fail($"Load failed: {ex.Message}");
        }
    }
}