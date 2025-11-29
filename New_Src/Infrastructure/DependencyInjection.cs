using Application.Services;
using Infrastructure.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<IsatisDbContext>(options => options.UseSqlServer(connectionString));

        // persistence implementation
        services.AddScoped<Application.Services.IProjectPersistenceService, Infrastructure.Services.ProjectPersistenceService>();

        // import service (sync)
        services.AddScoped<Application.Services.IImportService, Infrastructure.Services.ImportService>();

        // Background import queue:
        // Register the BackgroundImportQueueService as a singleton, expose it as IImportQueueService,
        // and also register it as a hosted service so the background worker runs.
        services.AddSingleton<BackgroundImportQueueService>();
        services.AddSingleton<Application.Services.IImportQueueService>(sp => sp.GetRequiredService<BackgroundImportQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundImportQueueService>());

        return services;
    }
}