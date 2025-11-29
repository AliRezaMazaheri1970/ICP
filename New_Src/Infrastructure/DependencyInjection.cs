using Application.Services;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Registers infrastructure services, persistence and hosted/background services.
/// Keep registrations here minimal and infrastructure-specific.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<IsatisDbContext>(options => options.UseSqlServer(connectionString));

        // Persistence implementations
        services.AddScoped<IProjectPersistenceService, ProjectPersistenceService>();

        // Import service (scoped because it uses DbContext)
        services.AddScoped<IImportService, ImportService>();

        // Background import queue - singleton hosted service that creates scopes for scoped services
        services.AddSingleton<BackgroundImportQueueService>();
        services.AddSingleton<IImportQueueService>(sp => sp.GetRequiredService<BackgroundImportQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundImportQueueService>());

        // Processing services and processors (scoped)
        services.AddScoped<IProcessingService, ProcessingService>();
        services.AddScoped<IRowProcessor, Infrastructure.Services.Processors.ComputeStatisticsProcessor>(); // add more processors as needed

        // Cleanup hosted service for old jobs/temp files
        services.AddSingleton<CleanupHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<CleanupHostedService>());

        return services;
    }
}