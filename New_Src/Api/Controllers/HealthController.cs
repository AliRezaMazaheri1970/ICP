using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    private readonly IsatisDbContext _db;

    public HealthController(IsatisDbContext db)
    {
        _db = db;
    }

    [HttpGet("ping")]
    public ActionResult<string> Ping() => Ok("pong");

    [HttpGet("ready")]
    public async Task<ActionResult<bool>> Ready()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (canConnect) return Ok(true);
            return StatusCode(503, false);
        }
        catch
        {
            return StatusCode(503, false);
        }
    }

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