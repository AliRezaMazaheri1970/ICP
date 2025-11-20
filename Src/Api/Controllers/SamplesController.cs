using Application.Features.Samples.Commands.ImportSamples;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SamplesController(ISender mediator) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // تبدیل فایل آپلود شده به Stream
        using var stream = file.OpenReadStream();

        var command = new ImportSamplesCommand(stream, file.FileName);

        var result = await mediator.Send(command);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}