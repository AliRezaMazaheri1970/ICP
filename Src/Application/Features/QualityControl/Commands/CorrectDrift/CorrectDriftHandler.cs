using Application.Features.QualityControl.DTOs;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;
using Domain.Enums;

namespace Application.Features.QualityControl.Commands.CorrectDrift;

public class CorrectDriftHandler(IUnitOfWork unitOfWork) : IRequestHandler<CorrectDriftCommand, Result<List<DriftCorrectionResultDto>>>
{
    public async Task<Result<List<DriftCorrectionResultDto>>> Handle(CorrectDriftCommand request, CancellationToken cancellationToken)
    {
        // 1. دریافت همه نمونه‌های پروژه (مرتب شده بر اساس زمان ورود/ساخت)
        // نکته: ترتیب نمونه‌ها در Drift حیاتی است. ما از CreatedAt استفاده می‌کنیم.
        var samples = (await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == request.ProjectId, includeProperties: "Measurements"))
            .OrderBy(s => s.CreatedAt) // یا RunDate اگر دارید
            .ToList();

        if (!samples.Any())
            return await Result<List<DriftCorrectionResultDto>>.FailAsync("No samples found in project.");

        // پیدا کردن تمام عنصرهایی که در این پروژه اندازه‌گیری شده‌اند
        var allElements = samples
            .SelectMany(s => s.Measurements)
            .Select(m => m.ElementName)
            .Distinct()
            .ToList();

        var results = new List<DriftCorrectionResultDto>();

        // 2. اجرای اصلاح برای هر عنصر جداگانه (چون Drift هر عنصر فرق دارد)
        foreach (var element in allElements)
        {
            int correctedCount = 0;
            double totalDrift = 0;
            int driftCount = 0;

            // جدا کردن مقادیر RM برای این عنصر
            // فرض: اولین RM، استاندارد طلایی (Baseline) است.
            var rmSamples = samples
                .Where(s => s.SolutionLabel.Contains(request.RmKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rmSamples.Count < 2)
            {
                // برای اصلاح دریفت حداقل ۲ تا RM لازم داریم (یکی اول، یکی آخر)
                continue;
            }

            // حلقه روی بازه‌های بین RMها (Segment)
            for (int i = 0; i < rmSamples.Count - 1; i++)
            {
                var startRm = rmSamples[i];
                var endRm = rmSamples[i + 1];

                // پیدا کردن مقدار خوانده شده عنصر در RM شروع و پایان
                var startVal = startRm.Measurements.FirstOrDefault(m => m.ElementName == element)?.Value ?? 0;
                var endVal = endRm.Measurements.FirstOrDefault(m => m.ElementName == element)?.Value ?? 0;

                if (startVal == 0 || endVal == 0) continue;

                // ضریب دریفت (چقدر تغییر کرده؟)
                double driftRatio = startVal / endVal; // هدف: رساندن مقدار پایان به شروع

                // برای آمار
                totalDrift += driftRatio;
                driftCount++;

                // پیدا کردن نمونه‌های بین این دو RM
                var startIndex = samples.IndexOf(startRm);
                var endIndex = samples.IndexOf(endRm);

                var batchSamples = samples
                    .Skip(startIndex + 1)
                    .Take(endIndex - startIndex - 1)
                    .Where(s => s.Type == SampleType.Sample) // فقط نمونه‌های اصلی را اصلاح کن
                    .ToList();

                int steps = batchSamples.Count + 1;
                int currentStep = 1;

                foreach (var sample in batchSamples)
                {
                    var measurement = sample.Measurements.FirstOrDefault(m => m.ElementName == element);
                    if (measurement != null)
                    {
                        // فرمول اصلاح
                        double correctionFactor;
                        if (request.IsStepwise)
                        {
                            // اصلاح خطی (تدریجی): هر نمونه کمی بیشتر اصلاح می‌شود
                            // Factor = 1 + (TotalChange * (Step / TotalSteps))
                            double delta = driftRatio - 1.0;
                            correctionFactor = 1.0 + (delta * ((double)currentStep / steps));
                        }
                        else
                        {
                            // اصلاح ثابت (پله‌ای)
                            correctionFactor = driftRatio;
                        }

                        measurement.Value *= correctionFactor;
                        correctedCount++;
                    }
                    currentStep++;
                }
            }

            if (correctedCount > 0)
            {
                results.Add(new DriftCorrectionResultDto
                {
                    ElementName = element,
                    SamplesCorrected = correctedCount,
                    AverageDriftFactor = driftCount > 0 ? totalDrift / driftCount : 1.0
                });
            }
        }

        // 3. ذخیره تغییرات در دیتابیس
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<List<DriftCorrectionResultDto>>.SuccessAsync(results, "Drift correction applied successfully.");
    }
}