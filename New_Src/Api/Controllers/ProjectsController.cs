using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Isatis.Api.Controllers;

[Route("api/projects")]
[ApiController]
public class ProjectsController : ControllerBase
{
    private readonly IProjectPersistenceService _projectPersistence;

    public ProjectsController(IProjectPersistenceService projectPersistence)
    {
        _projectPersistence = projectPersistence;
    }

    // DTOs used by controller for simplicity
    public record SaveProjectRequest(string ProjectName, string? Owner, List<RawRowDto>? RawRows, string? StateJson);
    public record RawRowDto(string ColumnData, string? SampleId);

    [HttpPost("{projectId:guid}/save")]
    public async Task<ActionResult<Result<object>>> SaveProject(Guid projectId, [FromBody] SaveProjectRequest request)
    {
        var rawDtos = request.RawRows?.Select(r => new RawDataDto(r.ColumnData, r.SampleId)).ToList();
        var res = await _projectPersistence.SaveProjectAsync(projectId, request.ProjectName, request.Owner, rawDtos, request.StateJson);

        if (res.Succeeded)
            return Ok(Result<object>.Success(new { ProjectId = res.Data!.ProjectId }));

        return BadRequest(Result<object>.Fail(res.Messages.FirstOrDefault() ?? "Save failed"));
    }

    [HttpGet("{projectId:guid}/load")]
    public async Task<ActionResult<Result<object>>> LoadProject(Guid projectId)
    {
        var res = await _projectPersistence.LoadProjectAsync(projectId);

        if (res.Succeeded)
        {
            var d = res.Data!;
            return Ok(Result<object>.Success(new
            {
                d.ProjectId,
                d.ProjectName,
                d.CreatedAt,
                d.LastModifiedAt,
                d.Owner,
                RawRows = d.RawRows,
                LatestStateJson = d.LatestStateJson
            }));
        }

        return NotFound(Result<object>.Fail(res.Messages.FirstOrDefault() ?? "Project not found"));
    }
}