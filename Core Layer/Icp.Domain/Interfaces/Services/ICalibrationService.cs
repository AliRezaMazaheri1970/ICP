using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس کالیبراسیون عناصر
    /// </summary>
    public interface ICalibrationService
    {
        /// <summary>
        /// ایجاد منحنی کالیبراسیون
        /// </summary>
        Task<CalibrationCurve> CreateCalibrationCurveAsync(
            int elementId,
            int projectId,
            IEnumerable<CalibrationPoint> points);

        /// <summary>
        /// محاسبه غلظت از شدت
        /// </summary>
        Task<decimal> CalculateConcentrationAsync(
            int calibrationCurveId,
            decimal intensity);

        /// <summary>
        /// دریافت منحنی کالیبراسیون فعال یک عنصر
        /// </summary>
        Task<CalibrationCurve?> GetActiveCalibrationCurveAsync(
            int elementId,
            int projectId);

        /// <summary>
        /// ارزیابی کیفیت کالیبراسیون
        /// </summary>
        Task<bool> ValidateCalibrationQualityAsync(CalibrationCurve curve);

        /// <summary>
        /// محاسبه R² منحنی
        /// </summary>
        Task<decimal> CalculateRSquaredAsync(CalibrationCurve curve);
    }
}