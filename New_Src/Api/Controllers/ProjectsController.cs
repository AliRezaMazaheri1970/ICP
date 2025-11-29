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
        _projectPersistence = projectPersistence ?? throw new ArgumentNullException(nameof(projectPersistence));
    }

    // Debug helper: implementation type that DI provided
    [HttpGet("impl")]
    public ActionResult<string> GetPersistenceImpl()
        => Ok(_projectPersistence.GetType().FullName);

    // DTOs used by controller for incoming requests
    public record SaveProjectRequest(string ProjectName, string? Owner, List<RawRowDto>? RawRows, string? StateJson);
    public record RawRowDto(string ColumnData, string? SampleId);

    [HttpPost("{projectId:guid}/save")]
    public async Task<ActionResult<Result<object>>> SaveProject(Guid projectId, [FromBody] SaveProjectRequest request)
    {
        if (request == null) return BadRequest(Result<object>.Fail("Request body is required"));

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

    // GET /api/projects?page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<Result<IEnumerable<ProjectListItemDto>>>> ListProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var res = await _projectPersistence.ListProjectsAsync(page, pageSize);
        if (res.Succeeded)
            return Ok(Result<IEnumerable<ProjectListItemDto>>.Success(res.Data!));

        return BadRequest(Result<IEnumerable<ProjectListItemDto>>.Fail(res.Messages.FirstOrDefault() ?? "List failed"));
    }

    // DELETE /api/projects/{projectId}
    [HttpDelete("{projectId:guid}")]
    public async Task<ActionResult<Result<object>>> DeleteProject(Guid projectId)
    {
        var res = await _projectPersistence.DeleteProjectAsync(projectId);
        if (res.Succeeded) return Ok(Result<object>.Success(new { Deleted = true }));
        return BadRequest(Result<object>.Fail(res.Messages.FirstOrDefault() ?? "Delete failed"));
    }
}