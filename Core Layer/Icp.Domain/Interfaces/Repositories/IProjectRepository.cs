using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages Project entities.
    /// </summary>
    public interface IProjectRepository : IRepository<Project>
    {
        /// <summary>
        /// Asynchronously retrieves all projects that have a specific status.
        /// </summary>
        /// <param name="status">The status to filter the projects by.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of projects with the specified status.</returns>
        Task<IEnumerable<Project>> GetByStatusAsync(ProjectStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specific project by its ID, including its associated samples.
        /// </summary>
        /// <param name="id">The unique identifier of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the project with its samples, or null if not found.</returns>
        Task<Project?> GetWithSamplesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specific project by its ID, including all related details like samples and calibration curves.
        /// </summary>
        /// <param name="id">The unique identifier of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the project with its full details, or null if not found.</returns>
        Task<Project?> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specified number of the most recent projects.
        /// </summary>
        /// <param name="count">The maximum number of recent projects to retrieve.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of the most recent projects.</returns>
        Task<IEnumerable<Project>> GetRecentProjectsAsync(int count, CancellationToken cancellationToken = default);
    }
}