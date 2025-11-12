using Core.Icp.Domain.Entities.CRM;

namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages Certified Reference Material (CRM) entities.
    /// </summary>
    public interface ICRMRepository : IRepository<CRM>
    {
        /// <summary>
        /// Asynchronously retrieves all CRMs that are currently active.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of active CRMs.</returns>
        Task<IEnumerable<CRM>> GetActiveCRMsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a CRM by its custom string identifier.
        /// </summary>
        /// <param name="crmId">The custom identifier of the CRM.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found CRM or null.</returns>
        Task<CRM?> GetByCRMIdAsync(string crmId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a CRM by its name.
        /// </summary>
        /// <param name="name">The name of the CRM.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found CRM or null.</returns>
        Task<CRM?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a specific CRM by its ID, including its associated certified values.
        /// </summary>
        /// <param name="id">The unique identifier of the CRM.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the CRM with its certified values, or null if not found.</returns>
        Task<CRM?> GetWithCertifiedValuesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all CRMs produced by a specific manufacturer.
        /// </summary>
        /// <param name="manufacturer">The name of the manufacturer.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of CRMs from the specified manufacturer.</returns>
        Task<IEnumerable<CRM>> GetByManufacturerAsync(string manufacturer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves all CRMs that will expire before a specified date.
        /// </summary>
        /// <param name="beforeDate">The date to check for expiration.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of expiring CRMs.</returns>
        Task<IEnumerable<CRM>> GetExpiringCRMsAsync(DateTime beforeDate, CancellationToken cancellationToken = default);
    }
}