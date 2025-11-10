using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.QualityControl;
using Core.Icp.Domain.Enums;
using System.Diagnostics.Metrics;

namespace Core.Icp.Domain.Entities.Samples
{
    /// <summary>
    /// نمونه آزمایشگاهی
    /// </summary>
    public class Sample : BaseEntity
    {
        /// <summary>
        /// شناسه نمونه (از فایل Excel/CSV)
        /// </summary>
        public string SampleId { get; set; } = string.Empty;

        /// <summary>
        /// نام نمونه
        /// </summary>
        public string SampleName { get; set; } = string.Empty;

        /// <summary>
        /// تاریخ اجرای تست
        /// </summary>
        public DateTime? RunDate { get; set; }

        /// <summary>
        /// وضعیت نمونه
        /// </summary>
        public SampleStatus Status { get; set; } = SampleStatus.Pending;

        /// <summary>
        /// شناسه پروژه
        /// </summary>
        public int ProjectId { get; set; }

        /// <summary>
        /// اندازه‌گیری‌های این نمونه
        /// </summary>
        public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();

        /// <summary>
        /// کنترل‌های کیفیت
        /// </summary>
        public virtual ICollection<QualityCheck> QualityChecks { get; set; } = new List<QualityCheck>();

        /// <summary>
        /// وزن نمونه (گرم)
        /// </summary>
        public decimal? Weight { get; set; }

        /// <summary>
        /// حجم نهایی (میلی‌لیتر)
        /// </summary>
        public decimal? Volume { get; set; }

        /// <summary>
        /// ضریب رقت
        /// </summary>
        public decimal? DilutionFactor { get; set; }

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Notes { get; set; }
    }
}