using Core.Icp.Application.Services.Files;
using Core.Icp.Application.Services.Projects;
using Core.Icp.Application.Services.Samples;
using Core.Icp.Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Icp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Projects
        services.AddScoped<IProjectService, ProjectService>();

        // Samples
        services.AddScoped<ISampleService, SampleService>();

        // File Processing (Import CSV/Excel)
        services.AddScoped<IFileProcessingService, FileProcessingService>();

        return services;
    }
}
