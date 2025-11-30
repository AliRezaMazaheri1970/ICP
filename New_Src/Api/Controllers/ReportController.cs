using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    // ✅ بدون هیچ فاصله‌ای! 
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string CsvContentType = "text/csv";
    private const string JsonContentType = "application/json";

    public ReportController(IReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult> GenerateReport([FromBody] ReportRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _reportService.GenerateReportAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return File(result.Data!.Data, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet("{projectId:guid}/excel")]
    public async Task<ActionResult> ExportToExcel(
        Guid projectId,
        [FromQuery] bool useOxide = false,
        [FromQuery] int decimalPlaces = 2)
    {
        var options = new ReportOptions(UseOxide: useOxide, DecimalPlaces: decimalPlaces);
        var result = await _reportService.ExportToExcelAsync(projectId, options);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return File(result.Data!, ExcelContentType, $"export_{projectId}_{DateTime.Now:yyyyMMdd}. xlsx");
    }

    [HttpGet("{projectId:guid}/csv")]
    public async Task<ActionResult> ExportToCsv(Guid projectId, [FromQuery] bool useOxide = false)
    {
        var result = await _reportService.ExportToCsvAsync(projectId, useOxide);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return File(result.Data!, CsvContentType, $"export_{projectId}_{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpGet("{projectId:guid}/json")]
    public async Task<ActionResult> ExportToJson(Guid projectId)
    {
        var result = await _reportService.ExportToJsonAsync(projectId);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return File(result.Data!, JsonContentType, $"export_{projectId}_{DateTime.Now:yyyyMMdd}. json");
    }

    [HttpGet("{projectId:guid}/html")]
    public async Task<ActionResult> GenerateHtmlReport(Guid projectId)
    {
        var result = await _reportService.GenerateHtmlReportAsync(projectId);

        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        return Content(result.Data!, "text/html");
    }

    [HttpPost("export")]
    public async Task<ActionResult> ExportData([FromBody] ExportRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { succeeded = false, messages = new[] { "ProjectId is required" } });

        var result = await _reportService.ExportDataAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { succeeded = false, messages = result.Messages });

        var (contentType, extension) = request.Format switch
        {
            ReportFormat.Excel => (ExcelContentType, "xlsx"),
            ReportFormat.Csv => (CsvContentType, "csv"),
            ReportFormat.Json => (JsonContentType, "json"),
            _ => ("application/octet-stream", "bin")
        };

        return File(result.Data!, contentType, $"export_{request.ProjectId}_{DateTime.Now:yyyyMMdd}. {extension}");
    }
}