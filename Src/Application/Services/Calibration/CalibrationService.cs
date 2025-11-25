// مسیر: Src/Application/Services/Calibration/CalibrationService.cs

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces; // IUnitOfWork
using Domain.Interfaces.Services; // ICalibrationService (Namespace جدید)
using Shared.Helpers;

namespace Application.Services.Calibration;

public class CalibrationService(IUnitOfWork unitOfWork) : ICalibrationService
{
    public async Task<CalibrationCurve> CalculateAndSaveCurveAsync(Guid projectId, string elementName, CancellationToken cancellationToken = default)
    {
        // 1. دریافت استانداردها
        var standards = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == projectId && s.Type == SampleType.Standard,
                      includeProperties: "Measurements");

        var points = new List<CalibrationPoint>();

        foreach (var std in standards)
        {
            var measurement = std.Measurements.FirstOrDefault(m => m.ElementName == elementName);

            // نکته: فرض بر این است که Weight در استانداردها معادل غلظت (Concentration) است
            if (measurement != null)
            {
                points.Add(new CalibrationPoint
                {
                    Concentration = std.Weight, // X
                    Intensity = measurement.Value, // Y
                    IsExcluded = false
                });
            }
        }

        // 2. محاسبه رگرسیون
        var xValues = points.Select(p => p.Concentration).ToList();
        var yValues = points.Select(p => p.Intensity).ToList();

        var (slope, intercept, rSquared) = MathHelper.CalculateLinearRegression(xValues, yValues);

        // 3. ساخت منحنی
        var curve = new CalibrationCurve
        {
            ProjectId = projectId,
            ElementName = elementName,
            Slope = slope,
            Intercept = intercept,
            RSquared = rSquared,
            IsActive = true,
            Points = points
        };

        // 4. غیرفعال کردن منحنی‌های قبلی
        var oldCurves = await unitOfWork.Repository<CalibrationCurve>()
            .GetAsync(c => c.ProjectId == projectId && c.ElementName == elementName && c.IsActive);

        foreach (var old in oldCurves)
        {
            old.IsActive = false;
            // await unitOfWork.Repository<CalibrationCurve>().UpdateAsync(old); // EF Core Tracking handles this automatically usually
        }

        await unitOfWork.Repository<CalibrationCurve>().AddAsync(curve);
        await unitOfWork.CommitAsync(cancellationToken);

        return curve;
    }

    public double CalculateConcentration(double intensity, CalibrationCurve curve)
    {
        if (curve.Slope == 0) return 0;
        return (intensity - curve.Intercept) / curve.Slope;
    }
}