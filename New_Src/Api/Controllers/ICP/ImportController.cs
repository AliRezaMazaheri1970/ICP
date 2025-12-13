using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly IImportQueueService _importQueue;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService importService,
        IImportQueueService importQueue,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _importQueue = importQueue;
        _logger = logger;
    }

    /// <summary>
    /// Basic CSV import (existing)
    /// POST /api/import/import
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<Result<object>>> ImportCsv(
        [FromForm] IFormFile? file,
        [FromForm] string? projectName,
        [FromForm] string? owner,
        [FromForm] string? stateJson,
        [FromForm] string? background)
    {
        if (file == null) return BadRequest(Result<object>.Fail("File is required"));
        if (file.Length == 0) return BadRequest(Result<object>.Fail("File is empty"));

        projectName ??= "ImportedProject";

        if (!string.IsNullOrEmpty(background) && bool.TryParse(background, out var bkg) && bkg)
        {
            using var ms = new MemoryStream();
            await file.OpenReadStream().CopyToAsync(ms);
            ms.Position = 0;

            var jobId = await _importQueue.EnqueueImportAsync(ms, projectName, owner, stateJson);
            return Accepted(Result<object>.Success(new { JobId = jobId }));
        }

        using var stream = file.OpenReadStream();
        var res = await _importService.ImportCsvAsync(stream, projectName, owner, stateJson);
        if (res.Succeeded) return Ok(Result<object>.Success(new { ProjectId = res.Data!.ProjectId }));
        var firstMsg = (res.Messages ?? Array.Empty<string>()).FirstOrDefault();
        return BadRequest(Result<object>.Fail(firstMsg ?? "Import failed"));
    }

    /// <summary>
    /// Detect file format
    /// POST /api/import/detect-format
    /// </summary>
    [HttpPost("detect-format")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult> DetectFormat([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { succeeded = false, messages = new[] { "File is required" } });

        using var stream = file.OpenReadStream();
        var result = await _importService.DetectFormatAsync(stream, file.FileName);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Preview file before import
    /// POST /api/import/preview
    /// </summary>
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult> PreviewFile(
        [FromForm] IFormFile file,
        [FromForm] int previewRows = 10)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { succeeded = false, messages = new[] { "File is required" } });

        using var stream = file.OpenReadStream();
        var result = await _importService.PreviewFileAsync(stream, file.FileName, previewRows);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Advanced import with options
    /// POST /api/import/advanced
    /// </summary>
    [HttpPost("advanced")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult> ImportAdvanced(
        [FromForm] IFormFile file,
        [FromForm] string projectName,
        [FromForm] string? owner = null,
        [FromForm] string? forceFormat = null,
        [FromForm] string? delimiter = null,
        [FromForm] int? headerRow = null,
        [FromForm] bool skipLastRow = true,
        [FromForm] bool autoDetectType = true,
        [FromForm] string? defaultType = "Samp")
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { succeeded = false, messages = new[] { "File is required" } });

        if (string.IsNullOrWhiteSpace(projectName))
            return BadRequest(new { succeeded = false, messages = new[] { "Project name is required" } });

        FileFormat? format = null;
        if (!string.IsNullOrEmpty(forceFormat) && Enum.TryParse<FileFormat>(forceFormat, true, out var parsed))
        {
            format = parsed;
        }

        var request = new AdvancedImportRequest(
            projectName,
            owner,
            format,
            delimiter,
            headerRow,
            null,
            skipLastRow,
            autoDetectType,
            defaultType
        );

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportAdvancedAsync(stream, file.FileName, request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Import additional file to existing project
    /// POST /api/import/{projectId}/additional
    /// </summary>
    [HttpPost("{projectId:guid}/additional")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult> ImportAdditional(
        Guid projectId,
        [FromForm] IFormFile file)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        if (file == null || file.Length == 0)
            return BadRequest(new { succeeded = false, messages = new[] { "File is required" } });

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportAdditionalAsync(projectId, stream, file.FileName);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get import job status
    /// GET /api/import/{jobId}/status
    /// </summary>
    [HttpGet("{jobId:guid}/status")]
    public async Task<ActionResult<Result<Shared.Models.ImportJobStatusDto>>> GetStatus(Guid jobId)
    {
        var st = await _importQueue.GetStatusAsync(jobId);
        if (st == null) return NotFound(Result<Shared.Models.ImportJobStatusDto>.Fail("Job not found"));
        return Ok(Result<Shared.Models.ImportJobStatusDto>.Success(st));
    }
}