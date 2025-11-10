using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// ایزوتوپ یک عنصر (مثلاً Ce140, La139)
    /// </summary>
    public class Isotope : BaseEntity
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
        /// عدد جرمی (مثلاً 140 برای Ce140)
        /// </summary>
        public int MassNumber { get; set; }

        /// <summary>
        /// فراوانی طبیعی (درصد)
        /// </summary>
        public decimal? NaturalAbundance { get; set; }

        /// <summary>
        /// نیمه‌عمر (برای ایزوتوپ‌های ناپایدار)
        /// </summary>
        public string? HalfLife { get; set; }

        /// <summary>
        /// آیا این ایزوتوپ برای اندازه‌گیری استفاده می‌شود؟
        /// </summary>
        public bool IsUsedForMeasurement { get; set; } = true;
    }
}