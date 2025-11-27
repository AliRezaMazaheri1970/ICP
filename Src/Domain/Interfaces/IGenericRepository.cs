using Domain.Common;
using System.Linq.Expressions;

namespace Domain.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<T>> GetAllAsync();

    // متد برای فیلتر کردن (جایگزین فیلترهای Pandas)
    Task<IReadOnlyList<T>> GetAsync(
        Expression<Func<T, bool>> predicate,
        string? includeProperties = null);

    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}