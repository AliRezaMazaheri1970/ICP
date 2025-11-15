using Infrastructure.Icp.Reports.Interfaces;
//using Infrastructure.Icp.Reports.Services; // فرض بر اینه سرویس‌های پیاده‌سازی اینجان
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Icp.Reports;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureReports(this IServiceCollection services)
    {
        return services;
    }
}
