using Core.Icp.Domain.Entities.CRM;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that manages Certified Reference Materials (CRMs).
    /// </summary>
    public interface ICRMService
    {
        /// <summary>
        /// Asynchronously retrieves all Certified Reference Materials (CRMs).
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all CRMs.</returns>
        Task<IEnumerable<CRM>> GetAllCRMsAsync();

        /// <summary>
        /// Asynchronously retrieves a specific CRM by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the CRM.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found CRM or null if not found.</returns>
        Task<CRM?> GetCRMByIdAsync(int id);

        /// <summary>
        /// Asynchronously retrieves all CRMs that are currently active.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of active CRMs.</returns>
        Task<IEnumerable<CRM>> GetActiveCRMsAsync();

        /// <summary>
        /// Asynchronously creates a new CRM.
        /// </summary>
        /// <param name="crm">The CRM entity to create.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created CRM.</returns>
        Task<CRM> CreateCRMAsync(CRM crm);

        /// <summary>
        /// Asynchronously updates an existing CRM.
        /// </summary>
        /// <param name="crm">The CRM entity with updated information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated CRM.</returns>
        Task<CRM> UpdateCRMAsync(CRM crm);

        /// <summary>
        /// Asynchronously deletes a CRM by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the CRM to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the deletion was successful; otherwise, false.</returns>
        Task<bool> DeleteCRMAsync(int id);

        /// <summary>
        /// Asynchronously retrieves the certified value for a specific element within a CRM.
        /// </summary>
        /// <param name="crmId">The unique identifier of the CRM.</param>
        /// <param name="elementId">The unique identifier of the element.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="CRMValue"/> or null if not found.</returns>
        Task<CRMValue?> GetCertifiedValueAsync(int crmId, int elementId);

        /// <summary>
        /// Asynchronously compares a measured value against the certified value in a CRM.
        /// </summary>
        /// <param name="crmId">The unique identifier of the CRM.</param>
        /// <param name="elementId">The unique identifier of the element.</param>
        /// <param name="measuredValue">The value that was measured and needs to be compared.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the measured value is within the acceptable range of the CRM's certified value; otherwise, false.</returns>
        Task<bool> CompareMeasurementWithCRMAsync(
            int crmId,
            int elementId,
            decimal measuredValue);
    }
}