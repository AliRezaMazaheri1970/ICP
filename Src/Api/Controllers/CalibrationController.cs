using Application.Features.Calibration.Commands.CalculateCurve;
using Application.Features.Calibration.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper; // اضافه شد برای Result

namespace Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/calibration")]
public class CalibrationController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// محاسبه و ذخیره منحنی کالیبراسیون برای یک عنصر خاص
    /// </summary>
    [HttpPost("calculate/{elementName}")]
    public async Task<ActionResult<Result<CalibrationCurveDto>>> CalculateCurve(Guid projectId, string elementName)
    {
        var command = new CalculateCurveCommand(projectId, elementName);
        var result = await mediator.Send(command);
        return Ok(result);
    }
}