using System.Text.RegularExpressions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.Models;

namespace Application.Services.QualityControl.Strategies;

public class DilutionFactorCheckStrategy : IQualityCheckStrategy
{
    public CheckType CheckType => CheckType.DilutionFactorCheck;

    public Task<(List<Guid>, string)> ExecuteAsync(List<Sample> samples, ProjectSettings settings, CancellationToken cancellationToken)
    {
        // منطق پایتون: اگر در لیبل D10 باشد، یعنی DF باید 10 باشد.
        // اگر تنظیمات مینیمم/ماکزیمم هم باشد، چک می‌شود.

        double min = settings.MinDilutionFactor ?? 0.9;
        double max = settings.MaxDilutionFactor ?? 1000.0;
        var failedIds = new List<Guid>();

        foreach (var sample in samples)
        {
            bool isFailed = false;

            // 1. چک محدوده کلی
            if (sample.DilutionFactor < min || sample.DilutionFactor > max)
            {
                isFailed = true;
            }

            // 2. چک تطابق با لیبل (Smart Check)
            // مثال: "Sample D10" باید DF=10 داشته باشد
            var match = Regex.Match(sample.SolutionLabel, @"D(\d+)(?:-|\b|$)");
            if (match.Success && double.TryParse(match.Groups[1].Value, out double expectedDf))
            {
                // اگر اختلاف بیشتر از مقدار ناچیز باشد
                if (Math.Abs(sample.DilutionFactor - expectedDf) > 0.01)
                {
                    isFailed = true;
                }
            }

            if (isFailed) failedIds.Add(sample.Id);
        }

        return Task.FromResult((failedIds, "Dilution Factor mismatch or out of range."));
    }
}