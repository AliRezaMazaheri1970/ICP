using Core.Icp.Domain.Entities.QualityControl;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages QualityCheck entities.
    /// </summary>
    public interface IQualityCheckRepository : IRepository<QualityCheck>
    {
        /// <summary>
        /// Asynchronously retrieves all quality checks associated with a specific sample.
        /// </summary>
        /// <param name="sampleId">The unique identifier of the sample.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of quality checks for the specified sample.</returns>
        Task<IEnumerable<QualityCheck>> GetBySampleIdAsync(Guid sampleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all quality checks of a specific type.
        /// </summary>
        /// <param name="checkType">The type of the check to filter by.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of quality checks of the specified type.</returns>
        Task<IEnumerable<QualityCheck>> GetByCheckTypeAsync(CheckType checkType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all quality checks that have failed.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of failed quality checks.</returns>
        Task<IEnumerable<QualityCheck>> GetFailedChecksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all quality checks associated with a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of quality checks for the specified project.</returns>
        Task<IEnumerable<QualityCheck>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}