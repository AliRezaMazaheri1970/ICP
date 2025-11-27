using Application.Features.Samples.Commands.ImportSamples;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Wrapper;

namespace Isatis.Api.Controllers; // اصلاح Namespace برای هماهنگی با سایر کنترلرها

[Route("api/projects/{projectId}/samples")]
[ApiController]
public class SamplesController(IMediator mediator) : ControllerBase
{
    [HttpPost("import")]
    public async Task<ActionResult<Result<int>>> Import(Guid projectId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(Result<int>.Fail("لطفاً یک فایل معتبر انتخاب کنید."));

        // تبدیل فایل ورودی به استریم
        // نکته: با پایان متد، استریم بسته می‌شود که چون Send را await می‌کنیم، مشکلی پیش نمی‌آید.
        using var stream = file.OpenReadStream();

        // استفاده از Object Initializer (روش استاندارد برای کلاس‌های Command)
        var command = new ImportSamplesCommand
        {
            ProjectId = projectId,
            FileName = file.FileName,
            FileStream = stream
        };

        var result = await mediator.Send(command);

        if (result.Succeeded)
            return Ok(result);

        return BadRequest(result);
    }
}