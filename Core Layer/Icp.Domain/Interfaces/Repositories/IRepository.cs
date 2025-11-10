using System.Linq.Expressions;
using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط پایه برای تمام Repository ها
    /// </summary>
    /// <typeparam name="T">نوع Entity</typeparam>
    public interface IRepository<T> where T : BaseEntity
    {
        // ========== Read Operations ==========

        /// <summary>
        /// دریافت با شناسه
        /// </summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// دریافت همه
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// جستجو با شرط
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// دریافت اولین مورد با شرط
        /// </summary>
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// بررسی وجود با شناسه
        /// </summary>
        Task<bool> ExistsAsync(int id);

        /// <summary>
        /// بررسی وجود با شرط
        /// </summary>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// شمارش
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// شمارش با شرط
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);

        // ========== Write Operations ==========

        /// <summary>
        /// اضافه کردن
        /// </summary>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// اضافه کردن چند مورد
        /// </summary>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// ویرایش
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// ویرایش چند مورد
        /// </summary>
        Task UpdateRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// حذف با شناسه
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// حذف
        /// </summary>
        Task DeleteAsync(T entity);

        /// <summary>
        /// حذف چند مورد
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// حذف نرم (Soft Delete)
        /// </summary>
        Task SoftDeleteAsync(int id);
    }
}