using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/crm")]
public class CrmController : ControllerBase
{
    private readonly ICrmService _crmService;
    private readonly ILogger<CrmController> _logger;

    public CrmController(ICrmService crmService, ILogger<CrmController> logger)
    {
        _crmService = crmService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of CRMs with optional filtering
    /// GET /api/crm? analysisMethod=4-Acid&searchText=258&ourOreasOnly=true&page=1&pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetCrmList(
        [FromQuery] string? analysisMethod = null,
        [FromQuery] string? searchText = null,
        [FromQuery] bool? ourOreasOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _crmService.GetCrmListAsync(analysisMethod, searchText, ourOreasOnly, page, pageSize);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get a single CRM by database ID
    /// GET /api/crm/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult> GetCrmById(int id)
    {
        var result = await _crmService.GetCrmByIdAsync(id);
        if (!result.Succeeded)
            return NotFound(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get CRMs by CRM ID string (e.g., "OREAS 258")
    /// GET /api/crm/search/{crmId}? analysisMethod=4-Acid
    /// </summary>
    [HttpGet("search/{crmId}")]
    public async Task<ActionResult> GetCrmByCrmId(string crmId, [FromQuery] string? analysisMethod = null)
    {
        var result = await _crmService.GetCrmByCrmIdAsync(crmId, analysisMethod);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Get available analysis methods
    /// GET /api/crm/methods
    /// </summary>
    [HttpGet("methods")]
    public async Task<ActionResult> GetAnalysisMethods()
    {
        var result = await _crmService.GetAnalysisMethodsAsync();
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Calculate differences between project data and CRM values
    /// POST /api/crm/diff
    /// </summary>
    [HttpPost("diff")]
    public async Task<ActionResult> CalculateDiff([FromBody] CrmDiffRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _crmService.CalculateDiffAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = result.Data });
    }

    /// <summary>
    /// Add or update a CRM record
    /// POST /api/crm
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> UpsertCrm([FromBody] CrmUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CrmId))
            return BadRequest(new { succeeded = false, messages = new[] { "CrmId is required" } });

        var result = await _crmService.UpsertCrmAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = new { id = result.Data } });
    }

    /// <summary>
    /// Delete a CRM record
    /// DELETE /api/crm/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteCrm(int id)
    {
        var result = await _crmService.DeleteCrmAsync(id);
        if (!result.Succeeded)
            return NotFound(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = new { deleted = true } });
    }

    /// <summary>
    /// Import CRMs from CSV file
    /// POST /api/crm/import
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<ActionResult> ImportCrms([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { succeeded = false, messages = new[] { "File is required" } });

        using var stream = file.OpenReadStream();
        var result = await _crmService.ImportCrmsFromCsvAsync(stream);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Ok(new { succeeded = true, data = new { importedCount = result.Data } });
    }
}