using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Isatis ICP API",
            Version = "2.0.0"
        });
    }

    [HttpGet("ready")]
    [AllowAnonymous]
    public IActionResult Ready()
    {
        return Ok(new { Status = "Ready" });
    }

    [HttpGet("live")]
    [AllowAnonymous]
    public IActionResult Live()
    {
        return Ok(new { Status = "Live" });
    }
}