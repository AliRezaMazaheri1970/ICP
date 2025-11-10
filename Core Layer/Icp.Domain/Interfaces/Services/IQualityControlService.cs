using Core.Icp.Domain.Entities.QualityControl;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// سرویس کنترل کیفیت
    /// </summary>
    public interface IQualityControlService
    {
        /// <summary>
        /// بررسی وزن نمونه
        /// </summary>
        Task<QualityCheck> PerformWeightCheckAsync(Sample sample);

        /// <summary>
        /// بررسی حجم نمونه
        /// </summary>
        Task<QualityCheck> PerformVolumeCheckAsync(Sample sample);

        /// <summary>
        /// بررسی ضریب رقت
        /// </summary>
        Task<QualityCheck> PerformDilutionFactorCheckAsync(Sample sample);

        /// <summary>
        /// بررسی سطرهای خالی
        /// </summary>
        Task<QualityCheck> PerformEmptyCheckAsync(Sample sample);

        /// <summary>
        /// بررسی CRM
        /// </summary>
        Task<QualityCheck> PerformCRMCheckAsync(Sample sample, int crmId);

        /// <summary>
        /// کالیبراسیون Drift
        /// </summary>
        Task<QualityCheck> PerformDriftCalibrationAsync(Sample sample);

        /// <summary>
        /// اجرای تمام کنترل‌های کیفیت
        /// </summary>
        Task<IEnumerable<QualityCheck>> PerformAllQualityChecksAsync(Sample sample);

        /// <summary>
        /// دریافت نتایج کنترل کیفیت پروژه
        /// </summary>
        Task<IEnumerable<QualityCheck>> GetProjectQualityChecksAsync(int projectId);
    }
}