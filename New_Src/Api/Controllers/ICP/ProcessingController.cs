using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProcessingController : ControllerBase
{
    private readonly IProcessingService _processingService;

    public ProcessingController(IProcessingService processingService)
    {
        _processingService = processingService;
    }

    // POST /api/projects/{projectId}/process
    // optional form/query param background=true to enqueue
    [HttpPost("{projectId:guid}/process")]
    public async Task<ActionResult<Result<object>>> ProcessProject(Guid projectId, [FromQuery] bool background = false)
    {
        if (background)
        {
            var res = await _processingService.EnqueueProcessProjectAsync(projectId);
            if (res.Succeeded) return Accepted(Result<object>.Success(new { JobId = res.Data }));
            var firstMsg = (res.Messages ?? Array.Empty<string>()).FirstOrDefault();
            return BadRequest(Result<object>.Fail(firstMsg ?? "Enqueue failed"));
        }

        var resSync = await _processingService.ProcessProjectAsync(projectId);
        if (resSync.Succeeded) return Ok(Result<object>.Success(new { ProjectStateId = resSync.Data }));
        var firstMsgSync = (resSync.Messages ?? Array.Empty<string>()).FirstOrDefault();
        return BadRequest(Result<object>.Fail(firstMsgSync ?? "Processing failed"));
    }
}