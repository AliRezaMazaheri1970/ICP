using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// عنصر شیمیایی (مثل Ce, La, Nd)
    /// </summary>
    public class Element : BaseEntity
    {
        /// <summary>
        /// نماد شیمیایی (Ce, La, Nd, ...)
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// نام کامل عنصر
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// عدد اتمی
        /// </summary>
        public int AtomicNumber { get; set; }

        /// <summary>
        /// جرم اتمی
        /// </summary>
        public decimal AtomicMass { get; set; }

        /// <summary>
        /// ایزوتوپ‌های این عنصر
        /// </summary>
        public virtual ICollection<Isotope> Isotopes { get; set; } = new List<Isotope>();

        /// <summary>
        /// منحنی‌های کالیبراسیون
        /// </summary>
        public virtual ICollection<CalibrationCurve> CalibrationCurves { get; set; } = new List<CalibrationCurve>();

        /// <summary>
        /// اندازه‌گیری‌های این عنصر
        /// </summary>
        public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();

        /// <summary>
        /// آیا این عنصر فعال است؟
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// ترتیب نمایش
        /// </summary>
        public int DisplayOrder { get; set; }
    }
}