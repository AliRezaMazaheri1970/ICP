using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// منحنی کالیبراسیون یک عنصر
    /// </summary>
    public class CalibrationCurve : BaseEntity
    {
        /// <summary>
        /// شناسه عنصر
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// عنصر مرتبط
        /// </summary>
        public virtual Element Element { get; set; } = null!;

        /// <summary>
        /// شناسه پروژه
        /// </summary>
        public int ProjectId { get; set; }

        /// <summary>
        /// تاریخ کالیبراسیون
        /// </summary>
        public DateTime CalibrationDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// شیب (Slope)
        /// </summary>
        public decimal Slope { get; set; }

        /// <summary>
        /// عرض از مبدأ (Intercept)
        /// </summary>
        public decimal Intercept { get; set; }

        /// <summary>
        /// ضریب همبستگی (R²)
        /// </summary>
        public decimal RSquared { get; set; }

        /// <summary>
        /// نقاط کالیبراسیون
        /// </summary>
        public virtual ICollection<CalibrationPoint> CalibrationPoints { get; set; } = new List<CalibrationPoint>();

        /// <summary>
        /// آیا این منحنی فعال است؟
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// توضیحات
        /// </summary>
        public string? Notes { get; set; }
    }
}