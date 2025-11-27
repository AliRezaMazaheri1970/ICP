using Application.Features.Calibration.Commands.ApplyCrmCorrection;
using Application.Features.Calibration.Commands.CalculateConcentrations; // 👈 دستور جدید محاسبه غلظت
using Application.Features.Calibration.Commands.CalculateCurve;
using Application.Features.Calibration.DTOs; // برای CalibrationCurveDto
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Isatis.Api.Controllers;

[Route("api/projects/{projectId}/calibration")]
[ApiController]
public class CalibrationController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// محاسبه منحنی کالیبراسیون برای یک عنصر خاص
    /// </summary>
    /// <param name="projectId">شناسه پروژه</param>
    /// <param name="element">نام عنصر (مثلاً Li 7)</param>
    [HttpPost("calculate/{element}")]
    public async Task<ActionResult<Result<CalibrationCurveDto>>> CalculateCurve(Guid projectId, string element)
    {
        var command = new CalculateCurveCommand(projectId, element);

        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    /// <summary>
    /// اعمال دستی اصلاحیه CRM روی داده‌های خام
    /// </summary>
    [HttpPost("apply-crm")]
    public async Task<ActionResult<Result<int>>> ApplyCrmCorrection(Guid projectId, [FromBody] ApplyCrmCorrectionCommand command)
    {
        // اطمینان از یکی بودن ProjectId در URL و Body
        if (command.ProjectId != Guid.Empty && command.ProjectId != projectId)
            return BadRequest("Project ID mismatch.");

        var finalCommand = command with { ProjectId = projectId };
        var result = await mediator.Send(finalCommand);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }

    /// <summary>
    /// ✅ متد جدید: محاسبه غلظت نهایی تمام نمونه‌های پروژه
    /// این متد منحنی‌های فعال را روی شدت‌ها اعمال کرده و غلظت (ppm) را حساب می‌کند.
    /// </summary>
    [HttpPost("calculate-concentrations")]
    public async Task<ActionResult<Result<int>>> CalculateConcentrations(Guid projectId)
    {
        var command = new CalculateConcentrationsCommand(projectId);
        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }
}