using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/correction")]
public class CorrectionController : ControllerBase
{
    private readonly ICorrectionService _correctionService;
    private readonly ILogger<CorrectionController> _logger;

    public CorrectionController(ICorrectionService correctionService, ILogger<CorrectionController> logger)
    {
        _correctionService = correctionService;
        _logger = logger;
    }

    /// <summary>
    /// Find samples with bad weights (outside min/max range)
    /// POST /api/correction/bad-weights
    /// </summary>
    [HttpPost("bad-weights")]
    public async Task<ActionResult> FindBadWeights([FromBody] FindBadWeightsRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _correctionService.FindBadWeightsAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Find samples with bad volumes
    /// POST /api/correction/bad-volumes
    /// </summary>
    [HttpPost("bad-volumes")]
    public async Task<ActionResult> FindBadVolumes([FromBody] FindBadVolumesRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _correctionService.FindBadVolumesAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Apply weight correction to selected samples
    /// POST /api/correction/weight
    /// </summary>
    [HttpPost("weight")]
    public async Task<ActionResult> ApplyWeightCorrection([FromBody] WeightCorrectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });

        var result = await _correctionService.ApplyWeightCorrectionAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Apply volume correction to selected samples
    /// POST /api/correction/volume
    /// </summary>
    [HttpPost("volume")]
    public async Task<ActionResult> ApplyVolumeCorrection([FromBody] VolumeCorrectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });

        var result = await _correctionService.ApplyVolumeCorrectionAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Apply blank and scale optimization results to project data
    /// POST /api/correction/apply-optimization
    /// </summary>
    [HttpPost("apply-optimization")]
    public async Task<ActionResult> ApplyOptimization([FromBody] ApplyOptimizationRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        // Changed: ElementOptimizations → ElementSettings
        if (request.ElementSettings == null || !request.ElementSettings.Any())
            return BadRequest(new { succeeded = false, messages = new[] { "At least one element setting is required" } });

        var result = await _correctionService.ApplyOptimizationAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Undo last correction for a project
    /// POST /api/correction/{projectId}/undo
    /// </summary>
    [HttpPost("{projectId:guid}/undo")]
    public async Task<ActionResult> UndoLastCorrection([FromRoute] Guid projectId)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _correctionService.UndoLastCorrectionAsync(projectId);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = new { undone = result.Data } });
    }

    /// <summary>
    /// Find empty/outlier rows where most elements are below average
    /// POST /api/correction/empty-rows
    /// Based on Python empty_check.py logic
    /// </summary>
    [HttpPost("empty-rows")]
    public async Task<ActionResult> FindEmptyRows([FromBody] FindEmptyRowsRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _correctionService.FindEmptyRowsAsync(request);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }
}