using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Services;
using MathNet.Numerics.LinearRegression; // استفاده از MathNet برای رگرسیون خطی

namespace Application.Services.CRM;

public class CrmService(IUnitOfWork unitOfWork) : ICrmService
{
    public async Task<(double Blank, double Scale)> CalculateCorrectionFactorsAsync(
        Guid projectId,
        string elementName,
        CancellationToken cancellationToken = default)
    {
        // 1. پیدا کردن تمام نمونه‌های استاندارد (CRM) در پروژه
        // فرض: کاربر در فایل اکسل نوع این نمونه‌ها را 'Standard' تعیین کرده است

        var projectSamples = await unitOfWork.Repository<Sample>()
            .GetAsync(s => s.ProjectId == projectId && s.Type == SampleType.Standard,
                      includeProperties: "Measurements");

        var crmDataPoints = new List<(double Certified, double Measured)>();

        // لود کردن تمام CRMهای مرجع از دیتابیس برای مقایسه
        var allCrms = await unitOfWork.Repository<Crm>()
            .GetAsync(c => true, includeProperties: "CertifiedValues");

        foreach (var sample in projectSamples)
        {
            // پیدا کردن CRM متناظر با نام نمونه (مثلاً اگر نام نمونه "OREAS 258" باشد)
            // این تطبیق رشته‌ای مشابه منطق crm_manager.py در پایتون است
            var matchedCrm = allCrms.FirstOrDefault(c => sample.SolutionLabel.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

            if (matchedCrm == null) continue;

            // مقدار واقعی (استاندارد)
            var certifiedVal = matchedCrm.CertifiedValues.FirstOrDefault(v => v.ElementName == elementName);

            // مقدار اندازه‌گیری شده توسط دستگاه
            var measuredVal = sample.Measurements.FirstOrDefault(m => m.ElementName == elementName);

            if (certifiedVal != null && measuredVal != null && measuredVal.Value > 0)
            {
                crmDataPoints.Add((certifiedVal.Value, measuredVal.Value));
            }
        }

        if (crmDataPoints.Count < 2)
        {
            // داده کافی برای محاسبه رگرسیون نیست (حداقل 2 نقطه لازم است)
            // پیش‌فرض: بدون تغییر (Blank=0, Scale=1)
            return (0.0, 1.0);
        }

        // 2. محاسبه Blank و Scale با رگرسیون خطی
        // مدل ریاضی: Measured = (Certified * Scale) + Blank
        // بنابراین ما رگرسیون Y=Measured بر روی X=Certified را محاسبه می‌کنیم.

        var xData = crmDataPoints.Select(p => p.Certified).ToArray(); // مقادیر واقعی (X)
        var yData = crmDataPoints.Select(p => p.Measured).ToArray();  // مقادیر دستگاه (Y)

        // خروجی: Item1 = Intercept (Blank), Item2 = Slope (Scale)
        var p = SimpleRegression.Fit(xData, yData);

        double calculatedBlank = p.Item1; // عرض از مبدا
        double calculatedScale = p.Item2; // شیب خط

        // اعتبارسنجی ساده برای جلوگیری از مقادیر پرت و غیرمنطقی
        // مثلا اگر اسکیل منفی شد یا خیلی بزرگ، آن را نادیده می‌گیریم
        if (calculatedScale <= 0.1 || calculatedScale > 10)
        {
            calculatedScale = 1.0;
            calculatedBlank = 0.0;
        }

        // خروجی: (بلنک محاسبه شده، اسکیل محاسبه شده)
        // نکته: هنگام اصلاح داده‌ها، فرمول معکوس استفاده می‌شود:
        // Corrected = (Measured - Blank) / Scale
        return (calculatedBlank, calculatedScale);
    }
}