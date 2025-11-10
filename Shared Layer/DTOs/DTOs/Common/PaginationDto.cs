namespace Shared.Icp.DTOs.Common
{
    /// <summary>
    /// DTO برای صفحه‌بندی
    /// </summary>
    public class PaginationDto
    {
        /// <summary>
        /// شماره صفحه (از 1 شروع)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// تعداد آیتم در هر صفحه
        /// </summary>
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// مرتب‌سازی بر اساس کدام فیلد
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>
        /// ترتیب صعودی یا نزولی
        /// </summary>
        public bool IsDescending { get; set; } = false;
    }
}