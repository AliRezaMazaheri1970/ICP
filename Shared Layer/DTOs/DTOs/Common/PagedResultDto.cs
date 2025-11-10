namespace Shared.Icp.DTOs.Common
{
    /// <summary>
    /// نتیجه صفحه‌بندی شده
    /// </summary>
    public class PagedResultDto<T>
    {
        /// <summary>
        /// لیست آیتم‌ها
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// شماره صفحه فعلی
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// تعداد آیتم در صفحه
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// تعداد کل آیتم‌ها
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// تعداد کل صفحات
        /// </summary>
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        /// <summary>
        /// آیا صفحه قبلی وجود دارد؟
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// آیا صفحه بعدی وجود دارد؟
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;
    }
}