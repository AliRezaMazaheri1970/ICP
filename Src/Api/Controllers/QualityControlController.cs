// مسیر فایل: Api/Controllers/QualityControlController.cs

using Application.Features.QualityControl.Commands.CorrectDrift;
using Application.Features.QualityControl.Commands.CorrectWeights;
using Application.Features.QualityControl.Commands.RunQualityCheck;
using Application.Features.QualityControl.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/qc")]
public class QualityControlController(ISender mediator) : ControllerBase
{
    // =================================================================
    // بخش ۱: اجرای چک‌ها (Unified QC Runner - Phase 3)
    // =================================================================

    /// <summary>
    /// اجرای هوشمند کنترل کیفیت.
    /// پارامتر type اختیاری است؛ اگر ارسال نشود، تمام چک‌ها اجرا می‌شوند.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<Result<int>>> RunChecks(Guid projectId, [FromQuery] CheckType? type)
    {
        var command = new RunQualityCheckCommand(projectId, type);
        var result = await mediator.Send(command);
        return Ok(result);
    }

    // =================================================================
    // بخش ۲: اصلاح داده‌ها (Data Correction)
    // =================================================================

    /// <summary>
    /// اصلاح وزن نمونه‌های انتخاب شده و محاسبه مجدد غلظت.
    /// </summary>
    [HttpPost("correct-weight")]
    public async Task<ActionResult<Result<int>>> CorrectWeights(Guid projectId, [FromBody] CorrectWeightsCommand command)
    {
        // نکته: SampleIds داخل بدنه کامند وجود دارد و در سطح کل دیتابیس یکتاست.
        // ProjectId در URL برای اطمینان از دسترسی صحیح و لاگ‌اندازی مفید است.
        var result = await mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// اصلاح دریفت دستگاه با استفاده از استانداردهای RM.
    /// </summary>
    [HttpPost("correct-drift")]
    public async Task<ActionResult<Result<List<DriftCorrectionResultDto>>>> CorrectDrift(Guid projectId, [FromBody] CorrectDriftCommand command)
    {
        // اعتبارسنجی همخوانی URL و Body
        if (projectId != command.ProjectId)
            return BadRequest(Result.Fail("Project ID in URL does not match body."));

        var result = await mediator.Send(command);
        return Ok(result);
    }
}