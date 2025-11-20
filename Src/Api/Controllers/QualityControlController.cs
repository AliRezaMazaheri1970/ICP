using Application.Features.QualityControl.Commands.CorrectDrift;
using Application.Features.QualityControl.Commands.CorrectWeights;
using Application.Features.QualityControl.Queries.GetBadWeights;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QualityControlController(ISender mediator) : ControllerBase
{
    [HttpGet("weight-check")]
    public async Task<IActionResult> CheckWeights([FromQuery] double min, [FromQuery] double max)
    {
        var query = new GetBadWeightsQuery(min, max);
        var result = await mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("correct-weight")]
    public async Task<IActionResult> CorrectWeights([FromBody] CorrectWeightsCommand command)
    {
        var result = await mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("correct-drift")]
    public async Task<IActionResult> CorrectDrift([FromBody] CorrectDriftCommand command)
    {
        var result = await mediator.Send(command);
        return Ok(result);
    }
}