using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Handles data correction operations such as weight, volume, dilution factor adjustments, and optimization application.
/// </summary>
[ApiController]
[Route("api/correction")]
public class CorrectionController : ControllerBase
{
    private readonly ICorrectionService _correctionService;
    private readonly ILogger<CorrectionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrectionController"/> class.
    /// </summary>
    /// <param name="correctionService">The service for handling correction logic.</param>
    /// <param name="logger">The logger instance.</param>
    public CorrectionController(ICorrectionService correctionService, ILogger<CorrectionController> logger)
    {
        _correctionService = correctionService;
        _logger = logger;
    }

    /// <summary>
    /// Identifies samples with weight values outside the accepted range.
    /// </summary>
    /// <param name="request">The request containing project ID and weight thresholds.</param>
    /// <returns>A list of samples with invalid weights.</returns>
    [HttpPost("bad-weights")]
    public async Task<ActionResult> FindBadWeights([FromBody] FindBadWeightsRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        var result = await _correctionService.FindBadWeightsAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Identifies samples with volume values outside the accepted range.
    /// </summary>
    /// <param name="request">The request containing project ID and volume thresholds.</param>
    /// <returns>A list of samples with invalid volumes.</returns>
    [HttpPost("bad-volumes")]
    public async Task<ActionResult> FindBadVolumes([FromBody] FindBadVolumesRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        var result = await _correctionService.FindBadVolumesAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Applies weight corrections to specific samples.
    /// </summary>
    /// <param name="request">The request containing project ID, solution labels, and new weight values.</param>
    /// <returns>The result of the weight correction operation.</returns>
    [HttpPost("weight")]
    public async Task<ActionResult> ApplyWeightCorrection([FromBody] WeightCorrectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
        {
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });
        }

        var result = await _correctionService.ApplyWeightCorrectionAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Applies volume corrections to specific samples.
    /// </summary>
    /// <param name="request">The request containing project ID, solution labels, and new volume values.</param>
    /// <returns>The result of the volume correction operation.</returns>
    [HttpPost("volume")]
    public async Task<ActionResult> ApplyVolumeCorrection([FromBody] VolumeCorrectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
        {
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });
        }

        var result = await _correctionService.ApplyVolumeCorrectionAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Applies Dilution Factor (DF) corrections to specific samples.
    /// </summary>
    /// <param name="request">The request containing project ID, solution labels, and new DF values.</param>
    /// <returns>The result of the DF correction operation.</returns>
    [HttpPost("df")]
    public async Task<ActionResult> ApplyDfCorrection([FromBody] DfCorrectionRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
        {
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });
        }

        var result = await _correctionService.ApplyDfCorrectionAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Applies optimization settings (e.g., blank subtraction, scaling) to the project data.
    /// </summary>
    /// <param name="request">The request containing project ID and optimization parameters per element.</param>
    /// <returns>The result of the optimization application.</returns>
    [HttpPost("apply-optimization")]
    public async Task<ActionResult> ApplyOptimization([FromBody] ApplyOptimizationRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        if (request.ElementSettings == null || !request.ElementSettings.Any())
        {
            return BadRequest(new { succeeded = false, messages = new[] { "At least one element setting is required" } });
        }

        var result = await _correctionService.ApplyOptimizationAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Retrieves all samples along with their associated Dilution Factor (DF) values for a given project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <returns>A list of samples with DF values.</returns>
    [HttpGet("{projectId:guid}/df-samples")]
    public async Task<ActionResult> GetDfSamples([FromRoute] Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        var result = await _correctionService.GetDfSamplesAsync(projectId);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Reverts the last correction applied to the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <returns>Confirmation of the undo operation.</returns>
    [HttpPost("{projectId:guid}/undo")]
    public async Task<ActionResult> UndoLastCorrection([FromRoute] Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        var result = await _correctionService.UndoLastCorrectionAsync(projectId);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = new { undone = result.Data } });
    }

    /// <summary>
    /// Identifies rows that are considered empty or outliers based on statistical analysis.
    /// </summary>
    /// <param name="request">The request containing project ID and criteria for identifying empty rows.</param>
    /// <returns>A list of empty or outlier rows.</returns>
    [HttpPost("empty-rows")]
    public async Task<ActionResult> FindEmptyRows([FromBody] FindEmptyRowsRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        var result = await _correctionService.FindEmptyRowsAsync(request);

        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Deletes specific rows identified by their solution labels.
    /// </summary>
    /// <param name="request">The request containing project ID and the list of solution labels to delete.</param>
    /// <returns>The result of the delete operation.</returns>
    [HttpPost("delete-rows")]
    public async Task<ActionResult> DeleteRows([FromBody] DeleteRowsRequest request)
    {
        if (request.ProjectId == Guid.Empty)
        {
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });
        }

        if (request.SolutionLabels == null || !request.SolutionLabels.Any())
        {
            return BadRequest(new { succeeded = false, messages = new[] { "At least one solution label is required" } });
        }

        var result = await _correctionService.DeleteRowsAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, messages = result.Messages });
        }

        return Ok(new { succeeded = true, data = result.Data });
    }
}