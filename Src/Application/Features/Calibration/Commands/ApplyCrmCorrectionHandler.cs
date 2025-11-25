using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Calibration.Commands.ApplyCrmCorrection;

public class ApplyCrmCorrectionHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<ApplyCrmCorrectionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ApplyCrmCorrectionCommand request, CancellationToken cancellationToken)
    {
        // 1. دریافت نمونه‌های پروژه
        // معمولاً اصلاحات CRM روی نمونه‌های اصلی (Sample) اعمال می‌شود، نه روی خود استانداردها یا بلانک‌ها
        // مگر اینکه فلگ ApplyToStandards زده شده باشد.
        var query = unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == request.ProjectId, includeProperties: "Measurements");

        var samples = (await query).ToList();

        if (!request.ApplyToStandards)
        {
            samples = samples.Where(s => s.Type == SampleType.Sample).ToList();
        }

        int updatedCount = 0;

        // 2. اعمال فرمول اصلاح
        // فرمول طبق منطق پایتون (pivot_plot_dialog.py): 
        // Corrected = (Value - Blank) * Scale

        foreach (var sample in samples)
        {
            var measurement = sample.Measurements
                .FirstOrDefault(m => m.ElementName.Equals(request.ElementName, StringComparison.OrdinalIgnoreCase));

            if (measurement != null)
            {
                // جلوگیری از مقادیر منفی یا نامعتبر (اختیاری، بسته به منطق بیزنس)
                double originalValue = measurement.Value;

                // محاسبه مقدار جدید
                double newValue = (originalValue - request.Blank) * request.Scale;

                measurement.Value = newValue;
                // measurement.LastModified = DateTime.UtcNow; // توسط BaseEntity مدیریت می‌شود

                updatedCount++;
            }
        }

        // 3. ذخیره تغییرات
        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(updatedCount,
            $"Correction applied to {updatedCount} samples for element {request.ElementName}. (Blank: {request.Blank}, Scale: {request.Scale})");
    }
}