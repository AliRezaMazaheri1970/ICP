using Application.Services.Interfaces;
using Domain.Interfaces;
using Domain.Interfaces.Services;
using Infrastructure.FileProcessing;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. تنظیمات دیتابیس (Entity Framework Core)
        // اگر از SQLite استفاده می‌کنید:
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // اگر در آینده خواستید از SQL Server استفاده کنید، خط بالا را کامنت و خط زیر را فعال کنید:
        // services.AddDbContext<ApplicationDbContext>(options =>
        //     options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));


        // 2. ثبت سرویس‌های فایل (ClosedXML Implementation)
        // این خط پیاده‌سازی ExcelService را به اینترفیس IExcelService متصل می‌کند
        services.AddScoped<IFileImportService, ExcelService>();
        services.AddScoped<IFileImportService, CsvFileService>();

        // 3. ثبت ریپازیتوری‌ها (Generic Repository & Unit of Work)
        // ثبت به صورت Generic برای اینکه بتوانید برای هر Entity از آن استفاده کنید
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // ثبت UnitOfWork برای مدیریت تراکنش‌ها
        services.AddScoped<IUnitOfWork, UnitOfWork>();


        return services;
    }
}