namespace Shared.Icp.Helpers.Extensions
{
    /// <summary>
    /// متدهای کمکی برای Collection ها
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// بررسی null یا خالی بودن
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
        {
            return collection == null || !collection.Any();
        }

        /// <summary>
        /// صفحه‌بندی
        /// </summary>
        public static IEnumerable<T> Paginate<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
        {
            return source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
        }

        /// <summary>
        /// تبدیل به رشته با جداکننده
        /// </summary>
        public static string JoinToString<T>(this IEnumerable<T> collection, string separator = ", ")
        {
            return string.Join(separator, collection);
        }

        /// <summary>
        /// اضافه کردن چندین آیتم
        /// </summary>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// حذف موارد با شرط
        /// </summary>
        public static void RemoveWhere<T>(this ICollection<T> collection, Func<T, bool> predicate)
        {
            var itemsToRemove = collection.Where(predicate).ToList();
            foreach (var item in itemsToRemove)
            {
                collection.Remove(item);
            }
        }
    }
}