using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly IImportQueueService _importQueue;

    public ImportController(IImportService importService, IImportQueueService importQueue)
    {
        _importService = importService;
        _importQueue = importQueue;
    }

    /// Import CSV - supports background enqueue if form field "background" == "true"
    [HttpPost("import")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<Result<object>>> ImportCsv([FromForm] IFormFile? file, [FromForm] string? projectName, [FromForm] string? owner, [FromForm] string? stateJson, [FromForm] string? background)
    {
        if (file == null) return BadRequest(Result<object>.Fail("File is required"));
        if (file.Length == 0) return BadRequest(Result<object>.Fail("File is empty"));

        // default project name
        projectName ??= "ImportedProject";

        if (!string.IsNullOrEmpty(background) && bool.TryParse(background, out var bkg) && bkg)
        {
            // copy stream and enqueue
            using var ms = new MemoryStream();
            await file.OpenReadStream().CopyToAsync(ms);
            ms.Position = 0;

            var jobId = await _importQueue.EnqueueImportAsync(ms, projectName, owner, stateJson);
            return Accepted(Result<object>.Success(new { JobId = jobId }));
        }

        using var stream = file.OpenReadStream();
        var res = await _importService.ImportCsvAsync(stream, projectName, owner, stateJson);
        if (res.Succeeded) return Ok(Result<object>.Success(new { ProjectId = res.Data!.ProjectId }));
        return BadRequest(Result<object>.Fail(res.Messages.FirstOrDefault() ?? "Import failed"));
    }

    [HttpGet("import/{jobId:guid}/status")]
    public async Task<ActionResult<Result<Shared.Models.ImportJobStatusDto>>> GetStatus(Guid jobId)
    {
        var st = await _importQueue.GetStatusAsync(jobId);
        if (st == null) return NotFound(Result<Shared.Models.ImportJobStatusDto>.Fail("Job not found"));
        return Ok(Result<Shared.Models.ImportJobStatusDto>.Success(st));
    }
}