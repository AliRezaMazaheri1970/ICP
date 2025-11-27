using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Services;
using MediatR;
using Shared.Wrapper;

namespace Application.Features.Calibration.Commands.CalculateConcentrations;

public class CalculateConcentrationsHandler(
    IUnitOfWork unitOfWork,
    ICalibrationService calibrationService
    ) : IRequestHandler<CalculateConcentrationsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CalculateConcentrationsCommand request, CancellationToken cancellationToken)
    {
        // 1. دریافت منحنی‌های فعال پروژه
        var activeCurves = (await unitOfWork.Repository<CalibrationCurve>()
            .GetAsync(c => c.ProjectId == request.ProjectId && c.IsActive))
            .ToDictionary(c => c.ElementName, StringComparer.OrdinalIgnoreCase);

        if (!activeCurves.Any())
            return await Result<int>.FailAsync("هیچ منحنی کالیبراسیونی برای این پروژه محاسبه نشده است.");

        // 2. دریافت نمونه‌های مجهول (Sample) و استانداردها (اگر نیاز به بازخوانی باشد)
        // معمولاً برای Sample و Unknown محاسبه انجام می‌شود
        var samplesToCalculate = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == request.ProjectId &&
                           (s.Type == SampleType.Sample || s.Type == SampleType.Unknown),
                      includeProperties: "Measurements");

        int calculationCount = 0;

        foreach (var sample in samplesToCalculate)
        {
            foreach (var measurement in sample.Measurements)
            {
                if (activeCurves.TryGetValue(measurement.ElementName, out var curve))
                {
                    // محاسبه غلظت از روی شدت (Value)
                    double concentration = calibrationService.CalculateConcentration(measurement.Value, curve);

                    // اعمال ضریب رقت
                    if (sample.DilutionFactor > 0)
                    {
                        concentration *= sample.DilutionFactor;
                    }

                    // ذخیره در فیلد غلظت (فرض بر این است که این فیلد را در Measurement اضافه کردید)
                    // اگر هنوز اضافه نکردید، فعلا در Value نریزید چون Value شدت است!
                    measurement.Concentration = concentration;

                    calculationCount++;
                }
            }
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return await Result<int>.SuccessAsync(calculationCount, $"محاسبات برای {calculationCount} عنصر با موفقیت انجام شد.");
    }
}