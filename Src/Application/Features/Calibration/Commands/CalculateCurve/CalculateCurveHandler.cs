using MediatR;
using Shared.Wrapper;
using Application.Features.Calibration.DTOs;
using Domain.Interfaces.Services;

namespace Application.Features.Calibration.Commands.CalculateCurve;

public class CalculateCurveHandler(ICalibrationService calibrationService)
    : IRequestHandler<CalculateCurveCommand, Result<CalibrationCurveDto>>
{
    public async Task<Result<CalibrationCurveDto>> Handle(CalculateCurveCommand request, CancellationToken cancellationToken)
    {
        // 1. فراخوانی سرویس برای محاسبه ریاضی و ذخیره در دیتابیس
        var curve = await calibrationService.CalculateAndSaveCurveAsync(
            request.ProjectId,
            request.ElementName,
            cancellationToken);

        // بررسی اینکه آیا منحنی با موفقیت ساخته شد (حداقل ۲ نقطه استاندارد نیاز است)
        if (curve.Points == null || !curve.Points.Any())
        {
            return await Result<CalibrationCurveDto>.FailAsync(
                $"Could not create calibration curve for {request.ElementName}. Ensure at least 2 standard samples exist.");
        }

        // 2. تبدیل Entity به DTO برای نمایش به کاربر
        var dto = new CalibrationCurveDto
        {
            Id = curve.Id,
            ElementName = curve.ElementName,
            Slope = curve.Slope,
            Intercept = curve.Intercept,
            RSquared = curve.RSquared,
            IsActive = curve.IsActive,
            CreatedAt = curve.CreatedAt,
            Points = curve.Points.Select(p => new CalibrationPointDto
            {
                Concentration = p.Concentration,
                Intensity = p.Intensity,
                IsExcluded = p.IsExcluded
            }).ToList()
        };

        return await Result<CalibrationCurveDto>.SuccessAsync(dto, "Calibration curve calculated successfully.");
    }
}