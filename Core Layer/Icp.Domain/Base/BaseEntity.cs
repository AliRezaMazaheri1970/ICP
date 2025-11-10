namespace Core.Icp.Domain.Base
{
    /// <summary>
    /// کلاس پایه برای تمام Entity ها
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// شناسه یکتا
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// تاریخ ایجاد
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// تاریخ آخرین ویرایش
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// آیا حذف شده؟ (Soft Delete)
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// کاربر ایجادکننده
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// کاربر ویرایش‌کننده
        /// </summary>
        public string? UpdatedBy { get; set; }
    }
}