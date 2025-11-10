using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// نقطه در منحنی کالیبراسیون
    /// </summary>
    public class CalibrationPoint : BaseEntity
    {
        /// <summary>
        /// شناسه منحنی کالیبراسیون
        /// </summary>
        public int CalibrationCurveId { get; set; }

        /// <summary>
        /// منحنی کالیبراسیون مرتبط
        /// </summary>
        public virtual CalibrationCurve CalibrationCurve { get; set; } = null!;

        /// <summary>
        /// غلظت استاندارد (ppm)
        /// </summary>
        public decimal StandardConcentration { get; set; }

        /// <summary>
        /// شدت اندازه‌گیری شده
        /// </summary>
        public decimal MeasuredIntensity { get; set; }

        /// <summary>
        /// ترتیب نقطه
        /// </summary>
        public int PointOrder { get; set; }
    }
}