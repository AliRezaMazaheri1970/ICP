// مسیر فایل: Api/Controllers/SamplesController.cs

using Application.Features.Samples.Commands.ImportSamples;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/samples")] // مسیر RESTful و سلسله‌مراتبی
public class SamplesController(ISender mediator) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import(Guid projectId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        using var stream = file.OpenReadStream();

        var command = new ImportSamplesCommand(projectId, stream, file.FileName);

        var result = await mediator.Send(command);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}