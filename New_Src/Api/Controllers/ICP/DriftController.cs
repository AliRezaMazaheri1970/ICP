using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/drift")]
public class DriftController : ControllerBase
{
    private readonly IDriftCorrectionService _driftService;
    private readonly ILogger<DriftController> _logger;

    public DriftController(IDriftCorrectionService driftService, ILogger<DriftController> logger)
    {
        _driftService = driftService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze drift without applying corrections
    /// POST /api/drift/analyze
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult> AnalyzeDrift([FromBody] DriftCorrectionRequest request)
    {
        var result = await _driftService.AnalyzeDriftAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Apply drift correction
    /// POST /api/drift/correct
    /// </summary>
    [HttpPost("correct")]
    public async Task<ActionResult> ApplyDriftCorrection([FromBody] DriftCorrectionRequest request)
    {
        var result = await _driftService.ApplyDriftCorrectionAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Detect segments in data
    /// GET /api/drift/{projectId}/segments
    /// </summary>
    [HttpGet("{projectId:guid}/segments")]
    public async Task<ActionResult> DetectSegments(
        Guid projectId,
        [FromQuery] string? basePattern = null,
        [FromQuery] string? conePattern = null)
    {
        var result = await _driftService.DetectSegmentsAsync(projectId, basePattern, conePattern);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Calculate drift ratios between standards
    /// GET /api/drift/{projectId}/ratios
    /// </summary>
    [HttpGet("{projectId:guid}/ratios")]
    public async Task<ActionResult> CalculateDriftRatios(Guid projectId)
    {
        var result = await _driftService.CalculateDriftRatiosAsync(projectId);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Optimize slope for an element
    /// POST /api/drift/slope
    /// </summary>
    [HttpPost("slope")]
    public async Task<ActionResult> OptimizeSlope([FromBody] SlopeOptimizationRequest request)
    {
        var result = await _driftService.OptimizeSlopeAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Zero the slope for an element
    /// POST /api/drift/{projectId}/zero-slope/{element}
    /// </summary>
    [HttpPost("{projectId:guid}/zero-slope/{element}")]
    public async Task<ActionResult> ZeroSlope(Guid projectId, string element)
    {
        var result = await _driftService.ZeroSlopeAsync(projectId, element);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }
}