using Core.Icp.Domain.Interfaces.Services;
using Core.Icp.Domain.Models.Files;
using Microsoft.AspNetCore.Mvc;
using Presentation.Icp.API.Models;
using Shared.Icp.DTOs.Projects;
using Shared.Icp.Helpers.Mappers;

namespace Presentation.Icp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly IProjectQueryService _projectQueryService;
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IProjectService projectService,
            IProjectQueryService projectQueryService,
            IFileProcessingService fileProcessingService,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _projectQueryService = projectQueryService;
            _fileProcessingService = fileProcessingService;
            _logger = logger;
        }

        #region CRUD

        /// <summary>
        /// لیست صفحه‌بندی‌شده پروژه‌ها.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedApiResponse<ProjectSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedApiResponse<ProjectSummaryDto>>> Get(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 20;

            _logger.LogInformation("Retrieving projects. Page={Page}, PageSize={PageSize}",
                pageNumber, pageSize);

            var result = await _projectQueryService.GetPagedAsync(
                pageNumber,
                pageSize,
                cancellationToken);

            var items = result.Items.ToSummaryDtoList();

            var response = PagedApiResponse<ProjectSummaryDto>.SuccessResponse(
                items,
                result.TotalCount,
                result.PageNumber,
                result.PageSize,
                "لیست پروژه‌ها با موفقیت بازیابی شد");

            return Ok(response);
        }

        /// <summary>
        /// دریافت جزئیات یک پروژه.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving project {ProjectId}", id);

            var details = await _projectQueryService.GetDetailsAsync(id, cancellationToken);
            if (details == null || details.Project == null)
                return NotFound(ApiResponse<ProjectDto>.FailureResponse("پروژه یافت نشد"));

            var dto = details.Project.ToDto();
            return Ok(ApiResponse<ProjectDto>.SuccessResponse(dto, "پروژه با موفقیت بازیابی شد"));
        }

        /// <summary>
        /// ایجاد پروژه جدید (بدون ایمپورت فایل).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> Create(
            [FromBody] CreateProjectDto dto)
        {
            _logger.LogInformation("Creating new project: {ProjectName}", dto.Name);

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<ProjectDto>.FailureResponse("داده‌های ورودی نامعتبر است"));

            var project = dto.ToEntity();
            project = await _projectService.CreateProjectAsync(project);

            var resultDto = project.ToDto();

            return CreatedAtAction(
                nameof(GetById),
                new { id = project.Id },
                ApiResponse<ProjectDto>.SuccessResponse(resultDto, "پروژه با موفقیت ایجاد شد"));
        }

        /// <summary>
        /// ویرایش پروژه.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> Update(
            Guid id,
            [FromBody] UpdateProjectDto dto)
        {
            _logger.LogInformation("Updating project {ProjectId}", id);

            var project = await _projectService.GetProjectByIdAsync(id);
            if (project is null)
                return NotFound(ApiResponse<ProjectDto>.FailureResponse("پروژه یافت نشد"));

            // استفاده از Mapper برای اعمال تغییرات
            project.UpdateFromDto(dto);

            project = await _projectService.UpdateProjectAsync(project);

            return Ok(
                ApiResponse<ProjectDto>.SuccessResponse(
                    project.ToDto(),
                    "پروژه با موفقیت به‌روزرسانی شد"));
        }

        /// <summary>
        /// حذف نرم پروژه.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            _logger.LogInformation("Deleting project {ProjectId}", id);

            var deleted = await _projectService.DeleteProjectAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<object>.FailureResponse("پروژه یافت نشد"));

            return NoContent();
        }

        #endregion

        #region Import from file (CSV/Excel)

        /// <summary>
        /// ایمپورت پروژه از فایل CSV/Excel و ایجاد پروژه + نمونه‌ها.
        /// </summary>
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
        [ProducesResponseType(typeof(ApiResponse<FileImportResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<FileImportResultDto>>> ImportProject(
            [FromForm] ProjectImportRequest request,
            CancellationToken cancellationToken)
        {
            var file = request.File;
            var projectName = request.ProjectName;

            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("فایلی ارسال نشده یا فایل خالی است."));
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("نام پروژه الزامی است."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // ذخیره موقت فایل روی دیسک
            var tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}{extension}");

            try
            {
                await using (var stream = System.IO.File.Create(tempFilePath))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                ProjectImportResult importResult;

                if (extension == ".csv")
                {
                    importResult = await _fileProcessingService.ImportCsvAsync(
                        tempFilePath,
                        projectName,
                        cancellationToken);
                }
                else if (extension == ".xlsx" || extension == ".xls")
                {
                    importResult = await _fileProcessingService.ImportExcelAsync(
                        tempFilePath,
                        projectName,
                        null,
                        cancellationToken);
                }
                else
                {
                    return BadRequest(ApiResponse<FileImportResultDto>
                        .FailureResponse("نوع فایل پشتیبانی نمی‌شود. فقط CSV و Excel مجاز است."));
                }

                // Map Domain Result -> API DTO
                var resultDto = new FileImportResultDto
                {
                    ProjectId = importResult.Project.Id,
                    ProjectName = importResult.Project.Name ?? string.Empty,
                    ProjectCode = null, // فعلاً کد جداگانه نداریم

                    Success = importResult.FailedRecords == 0 &&
                              importResult.Errors.Count == 0,

                    Message = importResult.FailedRecords == 0 &&
                              importResult.Errors.Count == 0
                        ? "ایمپورت با موفقیت انجام شد."
                        : "ایمپورت با هشدار/خطا انجام شد.",

                    TotalRecords = importResult.TotalRecords,
                    SuccessfulRecords = importResult.SuccessfulRecords,
                    FailedRecords = importResult.FailedRecords,
                    TotalSamples = importResult.TotalSamples,

                    Errors = importResult.Errors,
                    Warnings = importResult.Warnings
                };

                return Ok(ApiResponse<FileImportResultDto>.SuccessResponse(resultDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while importing project from file {FileName}",
                    file.FileName);

                var errorResponse = ApiResponse<FileImportResultDto>.FailureResponse(
                    "خطای غیرمنتظره در هنگام ایمپورت پروژه رخ داد.");

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }

        #endregion
    }
}
