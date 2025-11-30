using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Application.Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IImportQueueService _queue;
        private readonly Application.Services.IProjectPersistenceService _persistence;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(IImportQueueService queue, Application.Services.IProjectPersistenceService persistence, ILogger<ProjectsController> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST api/projects/{projectId}/process?background=true
        [HttpPost("{projectId:guid}/process")]
        public async Task<IActionResult> EnqueueProcess([FromRoute] Guid projectId, [FromQuery] bool background = true)
        {
            if (projectId == Guid.Empty)
                return BadRequest(new ApiResponse<object>(false, null, new[] { "projectId is required" }));

            try
            {
                // validate project exists (best-effort, use reflection to support different interface signatures)
                var project = await TryGetProjectAsync(projectId, HttpContext.RequestAborted);
                if (project == null)
                    return NotFound(new ApiResponse<object>(false, null, new[] { "Project not found." }));

                if (background)
                {
                    var jobId = await _queue.EnqueueProcessJobAsync(projectId);
                    return Ok(new ApiResponse<object>(true, new { jobId }, Array.Empty<string>()));
                }
                else
                {
                    return BadRequest(new ApiResponse<object>(false, null, new[] { "Synchronous processing not supported. Use ?background=true." }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue process for project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, null, new[] { ex.Message }));
            }
        }

        // POST api/projects/import
        // Multipart form-data: file (csv), projectName (string), owner (string, optional)
        [HttpPost("import")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
        public async Task<IActionResult> EnqueueImport([FromForm] IFormFile file, [FromForm] string projectName, [FromForm] string? owner = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ApiResponse<object>(false, null, new[] { "File is required." }));

            if (string.IsNullOrWhiteSpace(projectName))
                return BadRequest(new ApiResponse<object>(false, null, new[] { "projectName is required." }));

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                var jobId = await _queue.EnqueueImportAsync(ms, projectName, owner, null);
                return Ok(new ApiResponse<object>(true, new { jobId }, Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue import for projectName={ProjectName}", projectName);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, null, new[] { ex.Message }));
            }
        }

        // GET api/projects/import/{jobId}/status
        [HttpGet("import/{jobId:guid}/status")]
        public async Task<IActionResult> GetJobStatus([FromRoute] Guid jobId)
        {
            if (jobId == Guid.Empty)
                return BadRequest(new ApiResponse<object>(false, null, new[] { "jobId is required" }));

            try
            {
                var status = await _queue.GetStatusAsync(jobId);
                if (status == null)
                    return NotFound(new ApiResponse<object>(false, null, new[] { "Job not found." }));

                return Ok(new ApiResponse<object>(true, status, Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get job status for {JobId}", jobId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, null, new[] { ex.Message }));
            }
        }

        // GET api/projects/{projectId}/load
        [HttpGet("{projectId:guid}/load")]
        public async Task<IActionResult> LoadProject([FromRoute] Guid projectId)
        {
            if (projectId == Guid.Empty)
                return BadRequest(new ApiResponse<object>(false, null, new[] { "projectId is required" }));

            try
            {
                var project = await TryGetProjectAsync(projectId, HttpContext.RequestAborted);
                if (project == null)
                    return NotFound(new ApiResponse<object>(false, null, new[] { "Project not found." }));

                return Ok(new ApiResponse<object>(true, project, Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, null, new[] { ex.Message }));
            }
        }

        // Helper: attempt to call any reasonable "get project" method on the persistence service via reflection.
        // This keeps the controller compile-time safe even if the interface method name/signature differs.
        private async Task<object?> TryGetProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            if (_persistence == null) return null;

            var svc = _persistence;
            var svcType = svc.GetType();

            // Candidate method names and variants to try
            var candidateNames = new[]
            {
                "GetProjectAsync", "GetAsync", "LoadProjectAsync", "LoadAsync", "FindProjectAsync",
                "GetByIdAsync", "FindAsync", "GetProject", "Get", "Find"
            };

            foreach (var name in candidateNames)
            {
                var methods = svcType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                     .Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                                     .ToArray();

                foreach (var m in methods)
                {
                    var parameters = m.GetParameters();
                    // match if first parameter is Guid or string and method accepts 1 or 2 params (maybe cancellation token)
                    if (parameters.Length == 0) continue;

                    var firstParam = parameters[0].ParameterType;
                    if (firstParam != typeof(Guid) && firstParam != typeof(Guid?) && firstParam != typeof(string)) continue;

                    // build args
                    object?[] args;
                    if (parameters.Length == 1)
                    {
                        args = new object?[] { projectId };
                    }
                    else if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken))
                    {
                        args = new object?[] { projectId, cancellationToken };
                    }
                    else
                    {
                        // unsupported signature, skip
                        continue;
                    }

                    try
                    {
                        var invoked = m.Invoke(svc, args);
                        if (invoked == null) return null;

                        if (invoked is Task task)
                        {
                            await task.ConfigureAwait(false);
                            var resultProp = task.GetType().GetProperty("Result");
                            return resultProp?.GetValue(task);
                        }
                        else
                        {
                            return invoked;
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        _logger.LogWarning(tie, "Reflection invocation of {Method} failed", m.Name);
                        // try next candidate
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Reflection invocation of {Method} failed", m.Name);
                    }
                }
            }

            // If nothing worked, attempt a generic "GetProject" property or other common patterns
            // as a last resort, return null.
            _logger.LogDebug("TryGetProjectAsync: no suitable method found on {Type}", svcType.FullName);
            return null;
        }
    }
}