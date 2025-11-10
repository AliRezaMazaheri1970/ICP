using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس پردازش فایل‌های CSV/Excel
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// خواندن و پردازش فایل CSV
        /// </summary>
        Task<Project> ProcessCsvFileAsync(string filePath, string projectName);

        /// <summary>
        /// خواندن و پردازش فایل Excel
        /// </summary>
        Task<Project> ProcessExcelFileAsync(string filePath, string projectName);

        /// <summary>
        /// تشخیص فرمت فایل
        /// </summary>
        Task<string> DetectFileFormatAsync(string filePath);

        /// <summary>
        /// اعتبارسنجی فایل
        /// </summary>
        Task<bool> ValidateFileAsync(string filePath);

        /// <summary>
        /// استخراج نمونه‌ها از فایل
        /// </summary>
        Task<IEnumerable<Sample>> ExtractSamplesFromFileAsync(string filePath);
    }
}