using Core.Icp.Domain.Base;
using Core.Icp.Domain.Interfaces.Repositories;
using Infrastructure.Icp.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Icp.Data.Repositories
{
    /// <summary>
    ///     Provides a base implementation for a generic repository.
    /// </summary>
    /// <typeparam name="T">The type of the entity. Must be a subclass of <see cref="BaseEntity" />.</typeparam>
    public class BaseRepository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ICPDbContext _context;
        protected readonly DbSet<T> _dbSet;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BaseRepository{T}" /> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public BaseRepository(ICPDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        #region Query Methods

        /// <inheritdoc />
        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<T?> FirstOrDefaultAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .FirstOrDefaultAsync(predicate, cancellationToken);
        }

        #endregion

        #region Existence & Count

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .AnyAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> AnyAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .AnyAsync(predicate, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .CountAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .CountAsync(predicate, cancellationToken);
        }

        #endregion

        #region Add Methods

        /// <inheritdoc />
        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.IsDeleted = false;

            await _dbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<T>> AddRangeAsync(
            IEnumerable<T> entities,
            CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            var now = DateTime.UtcNow;

            foreach (var entity in entityList)
            {
                entity.CreatedAt = now;
                entity.IsDeleted = false;
            }

            await _dbSet.AddRangeAsync(entityList, cancellationToken);
            return entityList;
        }

        #endregion

        #region Update Methods

        /// <inheritdoc />
        public Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
            return Task.FromResult(entity);
        }

        /// <inheritdoc />
        public Task<IEnumerable<T>> UpdateRangeAsync(
            IEnumerable<T> entities,
            CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            var now = DateTime.UtcNow;

            foreach (var entity in entityList)
            {
                entity.UpdatedAt = now;
            }

            _dbSet.UpdateRange(entityList);
            return Task.FromResult<IEnumerable<T>>(entityList);
        }

        #endregion

        #region Delete Methods (Soft Delete)

        /// <inheritdoc />
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity != null)
            {
                await DeleteAsync(entity, cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteRangeAsync(
            IEnumerable<T> entities,
            CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            var now = DateTime.UtcNow;

            foreach (var entity in entityList)
            {
                entity.IsDeleted = true;
                entity.UpdatedAt = now;
            }

            _dbSet.UpdateRange(entityList);
            return Task.CompletedTask;
        }

        #endregion

        #region Hard Delete Methods

        /// <inheritdoc />
        public async Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                await HardDeleteAsync(entity, cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task HardDeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        #endregion
    }
}