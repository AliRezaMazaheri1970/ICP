using Core.Icp.Domain.Entities.Elements;
using Core.Icp.Domain.Interfaces.Repositories;
using Infrastructure.Icp.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Icp.Data.Repositories
{
    /// <summary>
    ///     Repository for Element entities, extending the base repository.
    /// </summary>
    public class ElementRepository : BaseRepository<Element>, IElementRepository
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ElementRepository" /> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public ElementRepository(ICPDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Element>> GetActiveElementsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted && e.IsActive)
                .OrderBy(e => e.AtomicNumber)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Element?> GetBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .FirstOrDefaultAsync(e => !e.IsDeleted && e.Symbol == symbol, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Element>> GetBySymbolsAsync(
            IEnumerable<string> symbols,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted && symbols.Contains(e.Symbol))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Element?> GetWithIsotopesAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(e => e.Isotopes)
                .FirstOrDefaultAsync(e => !e.IsDeleted && e.Id == id, cancellationToken);
        }
    }
}