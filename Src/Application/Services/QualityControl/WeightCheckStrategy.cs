using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.QualityControl.Strategies;

public class WeightCheckStrategy : IQualityCheckStrategy
{
    public CheckType CheckType => CheckType.WeightCheck;

    public Task<(List<Guid>, string)> ExecuteAsync(List<Sample> samples, ProjectSettings settings, CancellationToken cancellationToken)
    {
        // خواندن تنظیمات با مقادیر پیش‌فرض (مشابه فایل پایتون)
        double min = settings.MinAcceptableWeight ?? 0.190;
        double max = settings.MaxAcceptableWeight ?? 0.210;

        var failedIds = samples
            .Where(s => s.Weight < min || s.Weight > max)
            .Select(s => s.Id)
            .ToList();

        return Task.FromResult((failedIds, $"Weight out of range ({min}-{max} g)."));
    }
}