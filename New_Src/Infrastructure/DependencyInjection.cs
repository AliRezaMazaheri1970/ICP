using Application.Services;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<IsatisDbContext>(options => options.UseSqlServer(connectionString));

        // Persistence implementations
        services.AddScoped<IProjectPersistenceService, ProjectPersistenceService>();

        // Import service
        services.AddScoped<IImportService, ImportService>();

        // Background import queue
        services.AddSingleton<BackgroundImportQueueService>();
        services.AddSingleton<IImportQueueService>(sp => sp.GetRequiredService<BackgroundImportQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundImportQueueService>());

        // Processing services
        services.AddScoped<IProcessingService, ProcessingService>();
        services.AddScoped<IRowProcessor, Infrastructure.Services.Processors.ComputeStatisticsProcessor>();

        // CRM Service
        services.AddScoped<ICrmService, CrmService>();

        // Pivot Service
        services.AddScoped<IPivotService, PivotService>();

        services.AddScoped<IRmCheckService, RmCheckService>();

        // Report Service
        services.AddScoped<IReportService, ReportService>();

        // Cleanup hosted service
        services.AddSingleton<CleanupHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<CleanupHostedService>());

        return services;
    }
}