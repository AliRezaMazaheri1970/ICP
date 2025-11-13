using Infrastructure.Icp.Files.Interfaces;
using Infrastructure.Icp.Files.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Icp.Files
{
    /// <summary>
    /// تزریق وابستگی‌های لایه Files
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureFiles(
            this IServiceCollection services)
        {
            // File Processors
            services.AddScoped<ICsvFileProcessor, CsvFileProcessor>();
            services.AddScoped<IExcelFileProcessor, ExcelFileProcessor>();

            // Validation Service
            services.AddScoped<FileValidationService>();

            // Parsers (اگر می‌خوای از DI استفاده کنی)
            services.AddScoped<Parsers.SampleDataParser>();
            services.AddScoped<Parsers.ICPMSDataParser>();

            return services;
        }
    }
}