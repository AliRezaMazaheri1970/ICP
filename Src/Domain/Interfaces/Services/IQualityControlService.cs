// مسیر فایل: Core/Icp.Domain/Interfaces/Services/IQualityControlService.cs

using Core.Icp.Domain.Enums;
using Core.Icp.Domain.Models.QualityControl;

namespace Core.Icp.Domain.Interfaces.Services;

public interface IQualityControlService
{
    /// <summary>
    /// اجرای یک نوع کنترل کیفیت خاص روی تمام نمونه‌های پروژه
    /// </summary>
    Task<int> RunCheckAsync(Guid projectId, CheckType checkType, CancellationToken cancellationToken = default);

    /// <summary>
    /// اجرای تمام کنترل‌های کیفیت (وزن، حجم، DF، خالی) به صورت یکجا
    /// </summary>
    Task<int> RunAllChecksAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت خلاصه وضعیت QC پروژه
    /// </summary>
    Task<ProjectQualitySummary> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default);
}