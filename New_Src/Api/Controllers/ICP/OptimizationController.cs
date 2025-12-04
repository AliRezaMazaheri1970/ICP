using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/optimization")]
public class OptimizationController : ControllerBase
{
    private readonly IOptimizationService _optimizationService;
    private readonly ILogger<OptimizationController> _logger;

    public OptimizationController(IOptimizationService optimizationService, ILogger<OptimizationController> logger)
    {
        _optimizationService = optimizationService;
        _logger = logger;
    }

    /// <summary>
    /// Optimize Blank and Scale using Differential Evolution
    /// POST /api/optimization/blank-scale
    /// </summary>
    [HttpPost("blank-scale")]
    public async Task<ActionResult> OptimizeBlankScale([FromBody] BlankScaleOptimizationRequest request)
    {
        var result = await _optimizationService.OptimizeBlankScaleAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Preview manual Blank and Scale adjustment
    /// POST /api/optimization/preview
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult> PreviewBlankScale([FromBody] ManualBlankScaleRequest request)
    {
        var result = await _optimizationService.PreviewBlankScaleAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Apply manual Blank and Scale
    /// POST /api/optimization/apply
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult> ApplyBlankScale([FromBody] ManualBlankScaleRequest request)
    {
        var result = await _optimizationService.ApplyManualBlankScaleAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get current CRM comparison statistics
    /// GET /api/optimization/{projectId}/statistics
    /// </summary>
    [HttpGet("{projectId:guid}/statistics")]
    public async Task<ActionResult> GetStatistics(
        Guid projectId,
        [FromQuery] decimal minDiff = -10,
        [FromQuery] decimal maxDiff = 10)
    {
        var result = await _optimizationService.GetCurrentStatisticsAsync(projectId, minDiff, maxDiff);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Debug: Get project sample labels (for troubleshooting CRM matching)
    /// GET /api/optimization/{projectId}/debug-samples
    /// </summary>
    [HttpGet("{projectId:guid}/debug-samples")]
    public async Task<ActionResult> DebugSamples(Guid projectId)
    {
        var result = await _optimizationService.GetDebugSamplesAsync(projectId);
        return Ok(new { succeeded = true, data = result });
    }
}