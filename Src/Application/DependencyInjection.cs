using Application.Services.Calibration;
using Application.Services.CRM;
using Application.Services.QualityControl;
using Domain.Interfaces.Services; // برای دسترسی به IQualityCheckStrategy
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ثبت تمام Commandها و Queryهای موجود در این پروژه (MediatR)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // ثبت سرویس‌های اصلی (Services)
        services.AddScoped<IQualityControlService, QualityControlService>();
        services.AddScoped<ICalibrationService, CalibrationService>();
        services.AddScoped<ICrmService, CrmService>();

        var strategyType = typeof(IQualityCheckStrategy);
        var strategies = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => strategyType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var strategy in strategies)
        {
            // نکته کلیدی: همه را با یک نوع اینترفیس (IQualityCheckStrategy) ثبت می‌کنیم
            // تا بتوانیم IEnumerable<IQualityCheckStrategy> را تزریق کنیم.
            services.AddScoped(typeof(IQualityCheckStrategy), strategy);
        }

        return services;
    }
}