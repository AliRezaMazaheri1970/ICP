using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IsatisDbContext _db;

    public HealthController(IsatisDbContext db)
    {
        _db = db;
    }

    // GET /api/health
    [HttpGet]
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

    // GET /api/health/ping
    [HttpGet("ping")]
    public ActionResult<string> Ping() => Ok("pong");

    // GET /api/health/live
    // Liveness: معمولاً فقط نشان می‌دهد سرویس بالا هست (بدون وابستگی به DB)
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { Status = "Live" });
    }

    // GET /api/health/ready
    // Readiness: آیا سرویس آماده سرویس‌دهی هست؟ (اینجا وابسته به DB)
    [HttpGet("ready")]
    public async Task<ActionResult<bool>> Ready()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (canConnect) return Ok(true);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, false);
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, false);
        }
    }

    // GET /api/health/dbinfo
    [HttpGet("dbinfo")]
    public ActionResult<object> DbInfo()
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            return Ok(new
            {
                DataSource = conn.DataSource,
                Database = conn.Database,
                ConnectionString = "***masked***"
            });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }
}
