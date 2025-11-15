using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Interfaces.Repositories;
using Core.Icp.Domain.Interfaces.Services;
using Infrastructure.Icp.Files.Interfaces;
using Infrastructure.Icp.Files.Models;
using Shared.Icp.Exceptions;

namespace Core.Icp.Application.Services.Files
{
    /// <summary>
    /// پیاده‌سازی سطح بالا برای پردازش فایل‌ها (CSV/Excel)
    /// با استفاده از پردازش‌گرهای Files و UnitOfWork برای ذخیره در DB.
    /// </summary>
    public class FileProcessingService : IFileProcessingService
    {
        private readonly ICsvFileProcessor _csvFileProcessor;
        private readonly IExcelFileProcessor _excelFileProcessor;
        private readonly IUnitOfWork _unitOfWork;

        public FileProcessingService(
            ICsvFileProcessor csvFileProcessor,
            IExcelFileProcessor excelFileProcessor,
            IUnitOfWork unitOfWork)
        {
            _csvFileProcessor = csvFileProcessor;
            _excelFileProcessor = excelFileProcessor;
            _unitOfWork = unitOfWork;
        }

        public async Task<Project> ImportCsvAsync(
            string filePath,
            string projectName,
            CancellationToken cancellationToken = default)
        {
            // ۱. ایمپورت فایل با استفاده از CsvFileProcessor
            FileImportResult importResult = await _csvFileProcessor.ImportSamplesAsync(filePath);

            if (!importResult.Success)
            {
                throw new FileProcessingException(
                    $"ایمپورت CSV ناموفق بود: {importResult.Message}");
            }

            return await CreateProjectWithSamplesAsync(
                projectName,
                importResult.Samples,
                importResult,
                cancellationToken);
        }

        public async Task<Project> ImportExcelAsync(
            string filePath,
            string projectName,
            string? sheetName = null,
            CancellationToken cancellationToken = default)
        {
            // ۱. ایمپورت فایل با استفاده از ExcelFileProcessor
            FileImportResult importResult = await _excelFileProcessor.ImportSamplesAsync(filePath);

            if (!importResult.Success)
            {
                throw new FileProcessingException(
                    $"ایمپورت Excel ناموفق بود: {importResult.Message}");
            }

            return await CreateProjectWithSamplesAsync(
                projectName,
                importResult.Samples,
                importResult,
                cancellationToken);
        }

        public async Task<bool> ValidateFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            FileImportResult result;
            if (extension == ".csv")
            {
                result = await _csvFileProcessor.ImportSamplesAsync(filePath);
            }
            else
            {
                result = await _excelFileProcessor.ImportSamplesAsync(filePath);
            }

            return result.Success;
        }

        public async Task<IEnumerable<Sample>> ExtractSamplesFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            FileImportResult result;
            if (extension == ".csv")
            {
                result = await _csvFileProcessor.ImportSamplesAsync(filePath);
            }
            else
            {
                result = await _excelFileProcessor.ImportSamplesAsync(filePath);
            }

            if (!result.Success)
            {
                throw new FileProcessingException(
                    $"استخراج داده از فایل ناموفق بود: {result.Message}");
            }

            return result.Samples;
        }

        /// <summary>
        /// ساخت Project و ذخیره Samples در دیتابیس.
        /// </summary>
        private async Task<Project> CreateProjectWithSamplesAsync(
            string projectName,
            IEnumerable<Sample> samples,
            FileImportResult importResult,
            CancellationToken cancellationToken)
        {
            // ساخت پروژه جدید
            var project = new Project
            {
                Name = projectName,
                Description = $"ایمپورت از فایل در تاریخ {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                CreatedAt = DateTime.UtcNow,
                Status = Core.Icp.Domain.Enums.ProjectStatus.Active
            };

            // ارتباط دادن Sampleها به پروژه
            foreach (var sample in samples)
            {
                sample.Project = project;
                project.Samples.Add(sample);
            }

            await _unitOfWork.Projects.AddAsync(project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return project;
        }
    }
}
