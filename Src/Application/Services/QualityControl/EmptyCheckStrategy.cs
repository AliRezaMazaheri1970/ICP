using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.QualityControl.Strategies;

public class EmptyCheckStrategy : IQualityCheckStrategy
{
    public CheckType CheckType => CheckType.EmptyCheck;

    public Task<(List<Guid>, string)> ExecuteAsync(List<Sample> samples, ProjectSettings settings, CancellationToken cancellationToken)
    {
        // منطق ساده‌شده: اگر هیچ داده اندازه‌گیری ندارد یا همه صفر هستند
        // (در فازهای بعد می‌توانیم منطق میانگین آماری پایتون را اینجا بیاوریم)

        var failedIds = new List<Guid>();

        foreach (var sample in samples)
        {
            // اگر کالکشن نال است یا خالی
            if (sample.Measurements == null || !sample.Measurements.Any())
            {
                failedIds.Add(sample.Id);
                continue;
            }

            // اگر تمام مقادیر صفر یا نزدیک صفر هستند
            bool allZero = sample.Measurements.All(m => Math.Abs(m.Value) < 0.0001);
            if (allZero)
            {
                failedIds.Add(sample.Id);
            }
        }

        return Task.FromResult((failedIds, "Sample appears to be empty (No signal)."));
    }
}