using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages Sample entities, extending the generic IRepository.
    /// </summary>
    public interface ISampleRepository : IRepository<Sample>
    {
        /// <summary>
        /// Asynchronously retrieves all samples associated with a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of samples for the specified project.</returns>
        Task<IEnumerable<Sample>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves samples based on a collection of sample identifiers.
        /// </summary>
        /// <param name="sampleIds">A collection of string identifiers for the samples.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of matching samples.</returns>
        Task<IEnumerable<Sample>> GetBySampleIdsAsync(IEnumerable<string> sampleIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all samples that have a specific status.
        /// </summary>
        /// <param name="status">The status to filter the samples by.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of samples with the specified status.</returns>
        Task<IEnumerable<Sample>> GetByStatusAsync(SampleStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all samples created or processed within a specific date range.
        /// </summary>
        /// <param name="startDate">The start date of the range.</param>
        /// <param name="endDate">The end date of the range.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of samples within the specified date range.</returns>
        Task<IEnumerable<Sample>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specific sample by its ID, including its associated measurement data.
        /// </summary>
        /// <param name="id">The unique identifier of the sample.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the sample with its measurements, or null if not found.</returns>
        Task<Sample?> GetWithMeasurementsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all samples for a specific project, including their associated measurement data.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of samples with their measurements for the specified project.</returns>
        Task<IEnumerable<Sample>> GetWithMeasurementsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}