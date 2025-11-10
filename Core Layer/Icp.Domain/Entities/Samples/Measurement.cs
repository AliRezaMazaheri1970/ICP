using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;
using System.Xml.Linq;

namespace Core.Icp.Domain.Entities.Samples
{
    /// <summary>
    /// اندازه‌گیری یک عنصر در یک نمونه
    /// </summary>
    public class Measurement : BaseEntity
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
        /// شناسه عنصر
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// عنصر مرتبط
        /// </summary>
        public virtual Element Element { get; set; } = null!;

        /// <summary>
        /// ایزوتوپ (مثلاً 140 برای Ce140)
        /// </summary>
        public int? Isotope { get; set; }

        /// <summary>
        /// شدت خالص (Net Intensity)
        /// </summary>
        public decimal NetIntensity { get; set; }

        /// <summary>
        /// غلظت محاسبه شده (ppm یا ppb)
        /// </summary>
        public decimal? Concentration { get; set; }

        /// <summary>
        /// واحد اندازه‌گیری
        /// </summary>
        public string Unit { get; set; } = "ppm";

        /// <summary>
        /// غلظت نهایی (پس از اعمال وزن و حجم)
        /// </summary>
        public decimal? FinalConcentration { get; set; }

        /// <summary>
        /// خطای استاندارد
        /// </summary>
        public decimal? StandardError { get; set; }

        /// <summary>
        /// آیا این اندازه‌گیری معتبر است؟
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Notes { get; set; }
    }
}