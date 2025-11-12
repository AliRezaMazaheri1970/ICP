namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Represents the Unit of Work interface for managing transactions and coordinating work across multiple repositories.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Gets the repository for Sample entities.
        /// </summary>
        ISampleRepository Samples { get; }

        /// <summary>
        /// Gets the repository for Element entities.
        /// </summary>
        IElementRepository Elements { get; }

        /// <summary>
        /// Gets the repository for CRM (Certified Reference Material) entities.
        /// </summary>
        ICRMRepository CRMs { get; }

        /// <summary>
        /// Gets the repository for Project entities.
        /// </summary>
        IProjectRepository Projects { get; }

        /// <summary>
        /// Gets the repository for QualityCheck entities.
        /// </summary>
        IQualityCheckRepository QualityChecks { get; }

        /// <summary>
        /// Asynchronously saves all changes made in this context to the underlying database.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously starts a new database transaction.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously commits the current database transaction.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously rolls back the current database transaction.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}