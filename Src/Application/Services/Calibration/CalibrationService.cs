using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Services;
using MathNet.Numerics;
using MathNet.Numerics.LinearRegression; // پکیج ریاضی

namespace Application.Services.Calibration;

public class CalibrationService(
    IUnitOfWork unitOfWork,
    ICrmService crmService // 👈 تزریق سرویس CRM برای خواندن غلظت‌های واقعی
    ) : ICalibrationService
{
    public async Task<CalibrationCurve> CalculateAndSaveCurveAsync(
        Guid projectId,
        string elementName,
        CancellationToken cancellationToken = default)
    {
        // 1. دریافت تمام نمونه‌های استاندارد (Standard) این پروژه
        var standards = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == projectId && s.Type == SampleType.Standard,
                      includeProperties: "Measurements");

        var points = new List<CalibrationPoint>();

        // 2. ساخت نقاط نمودار (X: غلظت واقعی، Y: شدت دستگاه)
        foreach (var std in standards)
        {
            // الف) خواندن شدت (Intensity) اندازه‌گیری شده توسط دستگاه
            var measurement = std.Measurements
                .FirstOrDefault(m => m.ElementName.Equals(elementName, StringComparison.OrdinalIgnoreCase));

            if (measurement == null || measurement.Value <= 0) continue;

            // ب) دریافت غلظت واقعی (Certified Value) از سرویس CRM
            // نکته: نام نمونه (std.SolutionLabel) باید با نام CRM در دیتابیس یکی باشد (مثلاً "OREAS 258")
            var certifiedValues = await crmService.GetCertifiedValuesAsync(std.SolutionLabel, cancellationToken);

            // اگر این استاندارد در سیستم تعریف شده بود و غلظت این عنصر را داشت
            if (certifiedValues.TryGetValue(elementName, out double actualConcentration) && actualConcentration > 0)
            {
                points.Add(new CalibrationPoint
                {
                    Id = Guid.NewGuid(),
                    Concentration = actualConcentration, // محور X (معلوم)
                    Intensity = measurement.Value,       // محور Y (مشاهده شده)
                    IsExcluded = false
                });
            }
        }

        // 3. اگر تعداد نقاط کافی نبود (حداقل 2 نقطه برای خط لازم است)، منحنی پیش‌فرض بساز
        if (points.Count < 2)
        {
            return await CreateAndSaveDefaultCurve(projectId, elementName, points, cancellationToken);
        }

        // 4. محاسبه رگرسیون خطی: Intensity = (Slope * Concentration) + Intercept
        var xData = points.Select(p => p.Concentration).ToArray();
        var yData = points.Select(p => p.Intensity).ToArray();

        var p = SimpleRegression.Fit(xData, yData); // خروجی: (Intercept, Slope)
        double intercept = p.Item1;
        double slope = p.Item2;

        // محاسبه ضریب تعیین (R-Squared) برای سنجش کیفیت کالیبراسیون
        // فرمول: 1 - (SSres / SStot)
        double rSquared = GoodnessOfFit.RSquared(xData.Select(x => intercept + slope * x), yData);

        // 5. ذخیره منحنی در دیتابیس
        var curve = new CalibrationCurve
        {
            ProjectId = projectId,
            ElementName = elementName,
            Slope = slope,
            Intercept = intercept,
            RSquared = rSquared,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Points = points
        };

        // غیرفعال کردن منحنی‌های قدیمی این عنصر در این پروژه
        await DeactivateOldCurves(projectId, elementName);

        await unitOfWork.Repository<CalibrationCurve>().AddAsync(curve);
        await unitOfWork.CommitAsync(cancellationToken);

        return curve;
    }

    public double CalculateConcentration(double intensity, CalibrationCurve curve)
    {
        // فرمول معکوس: Concentration = (Intensity - Intercept) / Slope
        if (Math.Abs(curve.Slope) < 0.000001) return 0; // جلوگیری از تقسیم بر صفر

        var concentration = (intensity - curve.Intercept) / curve.Slope;

        // غلظت منفی معمولاً در شیمی معنی ندارد (مگر در شرایط نویز خاص که صفر گزارش می‌شود)
        return concentration > 0 ? concentration : 0;
    }

    // --- Helper Methods ---

    private async Task DeactivateOldCurves(Guid projectId, string elementName)
    {
        var oldCurves = await unitOfWork.Repository<CalibrationCurve>()
            .GetAsync(c => c.ProjectId == projectId && c.ElementName == elementName && c.IsActive);

        foreach (var old in oldCurves)
        {
            old.IsActive = false;
            await unitOfWork.Repository<CalibrationCurve>().UpdateAsync(old);
        }
    }

    private async Task<CalibrationCurve> CreateAndSaveDefaultCurve(
        Guid projectId,
        string elementName,
        List<CalibrationPoint> points,
        CancellationToken cancellationToken)
    {
        // منحنی پیش‌فرض (y = x) برای جلوگیری از خطای سیستم در صورت نبود استاندارد
        var curve = new CalibrationCurve
        {
            ProjectId = projectId,
            ElementName = elementName,
            Slope = 1,
            Intercept = 0,
            RSquared = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Points = points
        };

        await DeactivateOldCurves(projectId, elementName);
        await unitOfWork.Repository<CalibrationCurve>().AddAsync(curve);
        await unitOfWork.CommitAsync(cancellationToken);

        return curve;
    }
}