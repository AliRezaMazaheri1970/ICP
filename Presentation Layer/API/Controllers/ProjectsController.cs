using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Presentation.Icp.API.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Icp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IFileProcessingService fileProcessingService,
            ILogger<ProjectsController> logger)
        {
            _fileProcessingService = fileProcessingService;
            _logger = logger;
        }

        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
        [ProducesResponseType(typeof(ApiResponse<FileImportResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<FileImportResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<FileImportResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<FileImportResultDto>>> ImportProject(
            [FromForm] ProjectImportRequest request,
            CancellationToken cancellationToken)
        {
            // 1) ولیدیشن سطح ModelState (در صورتی که DataAnnotation اضافه شود)
            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("ورودی نامعتبر است.", validationErrors));
            }

            var file = request.File;
            var projectName = request.ProjectName?.Trim();

            // 2) ولیدیشن دستی فایل
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("فایلی ارسال نشده یا فایل خالی است."));
            }

            // 3) ولیدیشن نام پروژه
            if (string.IsNullOrWhiteSpace(projectName))
            {
                var errors = new Dictionary<string, string[]>
                {
                    ["projectName"] = new[] { "نام پروژه الزامی است." }
                };

                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("ورودی نامعتبر است.", errors));
            }

            var tempFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

            try
            {
                // 4) ذخیره فایل روی دیسک به صورت async
                await using (var stream = new FileStream(
                                 tempFilePath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 useAsync: true))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                _logger.LogInformation(
                    "Importing project from file {FileName} to temp path {Path}",
                    file.FileName,
                    tempFilePath);

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                Project project;

                // 5) انتخاب متد ایمپورت بر اساس پسوند فایل
                switch (extension)
                {
                    case ".csv":
                        project = await _fileProcessingService.ImportCsvAsync(
                            tempFilePath,
                            projectName!,
                            cancellationToken);
                        break;

                    case ".xlsx":
                    case ".xls":
                        project = await _fileProcessingService.ImportExcelAsync(
                            tempFilePath,
                            projectName!,
                            null,
                            cancellationToken);
                        break;

                    default:
                        return BadRequest(ApiResponse<FileImportResultDto>
                            .FailureResponse("نوع فایل پشتیبانی نمی‌شود. فقط CSV و Excel مجاز است."));
                }

                if (project == null)
                {
                    // اگر سرویس null برگرداند، یعنی مشکل جدی پیش آمده
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ApiResponse<FileImportResultDto>.FailureResponse(
                            "ایمپورت فایل با شکست مواجه شد. پروژه‌ای ایجاد نشد."));
                }

                // 6) ساخت DTO خروجی
                var resultDto = new FileImportResultDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    Success = true,
                    Message = "ایمپورت فایل با موفقیت انجام شد.",
                    TotalSamples = project.Samples?.Count ?? 0,
                    // TODO: وقتی منطق ایمپورت رکوردی را کامل کردی، این‌ها را از سرویس پر کن
                    TotalRecords = 0,
                    SuccessfulRecords = 0,
                    FailedRecords = 0,
                    Errors = new List<string>(),
                    Warnings = new List<string>()
                };

                return Ok(ApiResponse<FileImportResultDto>.SuccessResponse(resultDto));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database error while importing project from file {FileName}",
                    file.FileName);

                var baseMessage = "در هنگام ذخیره‌سازی داده‌ها خطای پایگاه‌داده رخ داد.";
                Dictionary<string, string[]>? errors = null;

                // تشخیص خاص FK مربوط به ElementId
                if (ex.InnerException != null &&
                    ex.InnerException.Message.Contains("FK_Measurements_Elements_ElementId",
                                                       StringComparison.OrdinalIgnoreCase))
                {
                    baseMessage =
                        "خطای پایگاه‌داده: برخی از عناصر شیمیایی (Elements) در سیستم تعریف نشده‌اند یا شناسه آن‌ها نامعتبر است.";
                    errors = new Dictionary<string, string[]>
                    {
                        ["ElementId"] = new[]
                        {
                            "شناسه عنصر شیمیایی معتبر نیست یا در جدول Elements تعریف نشده است."
                        }
                    };
                }

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<FileImportResultDto>.FailureResponse(baseMessage, errors));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Import of project from file {FileName} was cancelled",
                    file.FileName);

                return BadRequest(ApiResponse<FileImportResultDto>
                    .FailureResponse("عملیات ایمپورت توسط کاربر یا سرور لغو شد."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error while importing project from file {FileName}",
                    file.FileName);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<FileImportResultDto>.FailureResponse(
                        "در هنگام پردازش فایل خطای غیرمنتظره‌ای رخ داد."));
            }
            finally
            {
                // 7) پاک کردن فایل موقتی، حتی در صورت خطا
                try
                {
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx,
                        "Could not delete temp file {Path}",
                        tempFilePath);
                }
            }
        }
    }
}
