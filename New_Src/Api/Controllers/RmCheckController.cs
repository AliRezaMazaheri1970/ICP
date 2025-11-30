using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/rmcheck")]
public class RmCheckController : ControllerBase
{
    private readonly IRmCheckService _rmCheckService;
    private readonly ILogger<RmCheckController> _logger;

    public RmCheckController(IRmCheckService rmCheckService, ILogger<RmCheckController> logger)
    {
        _rmCheckService = rmCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Check RM samples against CRM reference values
    /// POST /api/rmcheck
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> CheckRm([FromBody] RmCheckRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _rmCheckService.CheckRmAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get RM samples in project
    /// GET /api/rmcheck/{projectId}/samples
    /// </summary>
    [HttpGet("{projectId:guid}/samples")]
    public async Task<ActionResult> GetRmSamples(Guid projectId)
    {
        var result = await _rmCheckService.GetRmSamplesAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Check weight and volume values
    /// POST /api/rmcheck/weight-volume
    /// </summary>
    [HttpPost("weight-volume")]
    public async Task<ActionResult> CheckWeightVolume([FromBody] WeightVolumeCheckRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _rmCheckService.CheckWeightVolumeAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get samples with weight/volume issues
    /// GET /api/rmcheck/{projectId}/weight-volume-issues
    /// </summary>
    [HttpGet("{projectId:guid}/weight-volume-issues")]
    public async Task<ActionResult> GetWeightVolumeIssues(Guid projectId)
    {
        var result = await _rmCheckService.GetWeightVolumeIssuesAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }
}