using Domain.Entities;
using Domain.Enums;
using Domain.Models;

namespace Domain.Interfaces.Services;

public interface IQualityCheckStrategy
{
    CheckType CheckType { get; }

    /// <summary>
    /// اجرای منطق چک روی لیست نمونه‌ها
    /// </summary>
    /// <returns>لیست آیدی نمونه‌های مردود شده و پیام خطا</returns>
    Task<(List<Guid> FailedSampleIds, string Message)> ExecuteAsync(
        List<Sample> samples,
        ProjectSettings settings,
        CancellationToken cancellationToken);
}