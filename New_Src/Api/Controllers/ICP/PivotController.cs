using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/pivot")]
public class PivotController : ControllerBase
{
    private readonly IPivotService _pivotService;
    private readonly ILogger<PivotController> _logger;

    public PivotController(IPivotService pivotService, ILogger<PivotController> logger)
    {
        _pivotService = pivotService;
        _logger = logger;
    }

    /// <summary>
    /// Get basic pivot table for a project
    /// POST /api/pivot
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> GetPivotTable([FromBody] PivotRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _pivotService.GetPivotTableAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get advanced pivot table with GCD/Repeat support
    /// POST /api/pivot/advanced
    /// </summary>
    [HttpPost("advanced")]
    public async Task<ActionResult> GetAdvancedPivotTable([FromBody] AdvancedPivotRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _pivotService.GetAdvancedPivotTableAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Analyze repeat patterns in project data
    /// GET /api/pivot/{projectId}/analyze-repeats
    /// </summary>
    [HttpGet("{projectId:guid}/analyze-repeats")]
    public async Task<ActionResult> AnalyzeRepeats(Guid projectId)
    {
        var result = await _pivotService.AnalyzeRepeatsAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get pivot table with GET (simple query)
    /// GET /api/pivot/{projectId}? page=1&pageSize=50&searchText=... 
    /// </summary>
    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult> GetPivotTableSimple(
        Guid projectId,
        [FromQuery] string? searchText = null,
        [FromQuery] bool useOxide = false,
        [FromQuery] int decimalPlaces = 2,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var request = new PivotRequest(
            projectId,
            searchText,
            null, null, null,
            useOxide,
            decimalPlaces,
            page,
            pageSize
        );

        var result = await _pivotService.GetPivotTableAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get all solution labels in project
    /// GET /api/pivot/{projectId}/labels
    /// </summary>
    [HttpGet("{projectId:guid}/labels")]
    public async Task<ActionResult> GetSolutionLabels(Guid projectId)
    {
        var result = await _pivotService.GetSolutionLabelsAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get all elements in project
    /// GET /api/pivot/{projectId}/elements
    /// </summary>
    [HttpGet("{projectId:guid}/elements")]
    public async Task<ActionResult> GetElements(Guid projectId)
    {
        var result = await _pivotService.GetElementsAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get column statistics
    /// GET /api/pivot/{projectId}/stats
    /// </summary>
    [HttpGet("{projectId:guid}/stats")]
    public async Task<ActionResult> GetColumnStats(Guid projectId)
    {
        var result = await _pivotService.GetColumnStatsAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Detect duplicate rows
    /// POST /api/pivot/duplicates
    /// </summary>
    [HttpPost("duplicates")]
    public async Task<ActionResult> DetectDuplicates([FromBody] DuplicateDetectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _pivotService.DetectDuplicatesAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Export pivot table to CSV
    /// POST /api/pivot/export
    /// </summary>
    [HttpPost("export")]
    public async Task<ActionResult> ExportToCsv([FromBody] PivotRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _pivotService.ExportToCsvAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return File(result.Data!, "text/csv", $"pivot_{request.ProjectId}. csv");
    }
}