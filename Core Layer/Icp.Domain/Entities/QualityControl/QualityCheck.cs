using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Entities.QualityControl
{
    /// <summary>
    /// کنترل کیفیت
    /// </summary>
    public class QualityCheck : BaseEntity
    {
        /// <summary>
        /// شناسه نمونه
        /// </summary>
        public int SampleId { get; set; }

        /// <summary>
        /// نمونه مرتبط
        /// </summary>
        public virtual Sample Sample { get; set; } = null!;

        /// <summary>
        /// نوع کنترل
        /// </summary>
        public CheckType CheckType { get; set; }

        /// <summary>
        /// وضعیت
        /// </summary>
        public CheckStatus Status { get; set; }

        /// <summary>
        /// مقدار مورد انتظار
        /// </summary>
        public decimal? ExpectedValue { get; set; }

        /// <summary>
        /// مقدار اندازه‌گیری شده
        /// </summary>
        public decimal? MeasuredValue { get; set; }

        /// <summary>
        /// انحراف (درصد)
        /// </summary>
        public decimal? Deviation { get; set; }

        /// <summary>
        /// حد انحراف قابل قبول (درصد)
        /// </summary>
        public decimal? AcceptableDeviationLimit { get; set; }

        /// <summary>
        /// تاریخ کنترل
        /// </summary>
        public DateTime CheckDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// پیام/توضیحات
        /// </summary>
        public string? Message { get; set; }
    }
}