using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// قرارداد سرویس سطح بالا برای پردازش فایل‌های ورودی (CSV/Excel)
    /// و ساخت/به‌روزرسانی پروژه‌ها و نمونه‌ها.
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// ایمپورت داده از فایل CSV و ایجاد یک پروژه جدید به‌همراه Samples.
        /// </summary>
        /// <param name="filePath">مسیر فایل CSV روی دیسک.</param>
        /// <param name="projectName">نام پروژه‌ای که باید ساخته شود.</param>
        /// <param name="cancellationToken">توکن لغو.</param>
        /// <returns>پروژه ایجاد شده با Samples مربوطه.</returns>
        Task<Project> ImportCsvAsync(
            string filePath,
            string projectName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ایمپورت داده از فایل Excel و ایجاد یک پروژه جدید به‌همراه Samples.
        /// </summary>
        /// <param name="filePath">مسیر فایل Excel روی دیسک.</param>
        /// <param name="projectName">نام پروژه‌ای که باید ساخته شود.</param>
        /// <param name="sheetName">نام شیت (در صورت نیاز؛ اگر null باشد، از شیت پیش‌فرض استفاده می‌شود).</param>
        /// <param name="cancellationToken">توکن لغو.</param>
        /// <returns>پروژه ایجاد شده با Samples مربوطه.</returns>
        Task<Project> ImportExcelAsync(
            string filePath,
            string projectName,
            string? sheetName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// اعتبارسنجی ساختار و محتوای فایل قبل از ایمپورت.
        /// </summary>
        /// <param name="filePath">مسیر فایل.</param>
        /// <param name="cancellationToken">توکن لغو.</param>
        /// <returns>اگر فایل قابل پردازش باشد true؛ در غیر این صورت false.</returns>
        Task<bool> ValidateFileAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// استخراج Sampleها از فایل، بدون ذخیره‌سازی در دیتابیس.
        /// </summary>
        /// <param name="filePath">مسیر فایل.</param>
        /// <param name="cancellationToken">توکن لغو.</param>
        /// <returns>لیست Sampleهای استخراج‌شده.</returns>
        Task<IEnumerable<Sample>> ExtractSamplesFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }
}
