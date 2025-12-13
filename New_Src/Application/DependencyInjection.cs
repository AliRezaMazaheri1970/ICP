using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>
/// Provides extension methods for registering application layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer services into the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application layer services (validators, mappers, handlers, etc.)
        // Note: Do NOT register infrastructure services (like IProjectPersistenceService implementation) here.
        return services;
    }
}