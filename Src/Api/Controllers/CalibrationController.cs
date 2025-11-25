using Application.Features.Calibration.Commands.ApplyCrmCorrection; // اضافه شد
using Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/calibration")]
public class CalibrationController(ISender mediator, ICrmService crmService) : ControllerBase
{
    // ... (متد CalculateCurve قبلی شما اینجا بود) ...

    /// <summary>
    /// گام ۱: پیشنهاد فاکتورهای اصلاح (فقط محاسبه می‌کند، تغییری نمی‌دهد)
    /// </summary>
    [HttpGet("crm-factors/{elementName}")]
    public async Task<ActionResult<Result<object>>> GetCrmCorrectionFactors(Guid projectId, string elementName)
    {
        // این سرویس را قبلاً ساخته‌اید، اینجا فراخوانی می‌شود تا به UI پیشنهاد بدهد
        var (blank, scale) = await crmService.CalculateCorrectionFactorsAsync(projectId, elementName);

        return Ok(Result<object>.Success(new { Blank = blank, Scale = scale }, "Factors calculated."));
    }

    /// <summary>
    /// گام ۲: اعمال نهایی اصلاحات روی دیتابیس
    /// </summary>
    [HttpPost("apply-crm/{elementName}")]
    public async Task<ActionResult<Result<int>>> ApplyCrmCorrection(
        Guid projectId,
        string elementName,
        [FromBody] ApplyCrmCorrectionDto dto) // یک DTO ساده برای بادی
    {
        var command = new ApplyCrmCorrectionCommand(projectId, elementName, dto.Blank, dto.Scale, dto.ApplyToStandards);
        var result = await mediator.Send(command);
        return Ok(result);
    }
}

// DTO موقت برای ورودی کنترلر (می‌توانید در لایه Application هم تعریف کنید)
public class ApplyCrmCorrectionDto
{
    public double Blank { get; set; }
    public double Scale { get; set; }
    public bool ApplyToStandards { get; set; } = false;
}