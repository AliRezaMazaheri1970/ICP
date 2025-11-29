using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Wrapper;

namespace Isatis.Api.Controllers;

[ApiController]
[Route("api/projects/import")]
public class ImportJobsController : ControllerBase
{
    private readonly IsatisDbContext _db;

    public ImportJobsController(IsatisDbContext db)
    {
        _db = db;
    }

    // GET /api/projects/import/jobs?page=1&pageSize=20
    [HttpGet("jobs")]
    public async Task<ActionResult<Result<object>>> ListJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.ProjectImportJobs.OrderByDescending(j => j.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(j => new
            {
                j.JobId,
                j.ProjectName,
                j.State,
                j.TotalRows,
                j.ProcessedRows,
                j.Percent,
                j.Message,
                j.ResultProjectId,
                j.CreatedAt,
                j.UpdatedAt
            }).ToListAsync();

        return Ok(Result<object>.Success(new { total, page, pageSize, items }));
    }

    // GET /api/projects/import/{jobId}
    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<Result<object>>> GetJob(Guid jobId)
    {
        var job = await _db.ProjectImportJobs.FindAsync(jobId);
        if (job == null) return NotFound(Result<object>.Fail("Job not found"));

        return Ok(Result<object>.Success(new
        {
            job.JobId,
            job.ProjectName,
            job.State,
            job.TotalRows,
            job.ProcessedRows,
            job.Percent,
            job.Message,
            job.ResultProjectId,
            job.TempFilePath,
            job.CreatedAt,
            job.UpdatedAt
        }));
    }

    // POST /api/projects/import/{jobId}/cancel
    [HttpPost("{jobId:guid}/cancel")]
    public async Task<ActionResult<Result<object>>> CancelJob(Guid jobId)
    {
        var job = await _db.ProjectImportJobs.FindAsync(jobId);
        if (job == null) return NotFound(Result<object>.Fail("Job not found"));

        // Basic cancel: mark as Failed/Cancelled and message; running job will not be forcibly aborted immediately.
        job.State = (int)ImportJobState.Failed;
        job.Message = "Cancelled by user";
        job.UpdatedAt = DateTime.UtcNow;
        _db.ProjectImportJobs.Update(job);
        await _db.SaveChangesAsync();

        return Ok(Result<object>.Success(new { jobId }));
    }
}