using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages Element entities.
    /// </summary>
    public interface IElementRepository : IRepository<Element>
    {
        /// <summary>
        /// Asynchronously retrieves all elements that are currently active.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of active elements.</returns>
        Task<IEnumerable<Element>> GetActiveElementsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves an element by its chemical symbol.
        /// </summary>
        /// <param name="symbol">The chemical symbol of the element (e.g., "Fe").</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found element or null.</returns>
        Task<Element?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a collection of elements based on their chemical symbols.
        /// </summary>
        /// <param name="symbols">A collection of chemical symbols.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of matching elements.</returns>
        Task<IEnumerable<Element>> GetBySymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specific element by its ID, including its associated isotopes.
        /// </summary>
        /// <param name="id">The unique identifier of the element.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the element with its isotopes, or null if not found.</returns>
        Task<Element?> GetWithIsotopesAsync(Guid id, CancellationToken cancellationToken = default);
    }
}