using Application.Services.QualityControl;
using Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ثبت تمام Commandها و Queryهای موجود در این پروژه
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.AddScoped<IQualityControlService, QualityControlService>();

        return services;
    }
}