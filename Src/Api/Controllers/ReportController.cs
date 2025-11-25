using Application.Services.Interfaces;
using Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/reports")]
public class ReportController(IReportService reportService) : ControllerBase
{
    /// <summary>
    /// دریافت داده‌ها به فرمت JSON Pivot برای نمایش در گرید
    /// </summary>
    [HttpGet("pivot")]
    public async Task<ActionResult<Result<object>>> GetPivotReport(Guid projectId)
    {
        var data = await reportService.GetProjectPivotReportAsync(projectId);
        return Ok(Result<object>.Success(data));
    }

    /// <summary>
    /// دانلود فایل اکسل گزارش نهایی
    /// </summary>
    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportToExcel(Guid projectId)
    {
        var fileContent = await reportService.ExportProjectToExcelAsync(projectId);
        var fileName = $"Project_{projectId}_Report.xlsx";

        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}