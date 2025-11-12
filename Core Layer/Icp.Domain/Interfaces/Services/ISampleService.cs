using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that manages analytical samples.
    /// </summary>
    public interface ISampleService
    {
        /// <summary>
        /// Asynchronously retrieves all samples.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all samples.</returns>
        Task<IEnumerable<Sample>> GetAllSamplesAsync();

        /// <summary>
        /// Asynchronously retrieves a specific sample by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the sample.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the found sample or null if not found.</returns>
        Task<Sample?> GetSampleByIdAsync(int id);

        /// <summary>
        /// Asynchronously retrieves all samples belonging to a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of samples for the specified project.</returns>
        Task<IEnumerable<Sample>> GetSamplesByProjectIdAsync(int projectId);

        /// <summary>
        /// Asynchronously searches for samples based on a given search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for in sample properties.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of matching samples.</returns>
        Task<IEnumerable<Sample>> SearchSamplesAsync(string searchTerm);

        /// <summary>
        /// Asynchronously creates a new sample.
        /// </summary>
        /// <param name="sample">The sample entity to create.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created sample.</returns>
        Task<Sample> CreateSampleAsync(Sample sample);

        /// <summary>
        /// Asynchronously updates an existing sample.
        /// </summary>
        /// <param name="sample">The sample entity with updated information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated sample.</returns>
        Task<Sample> UpdateSampleAsync(Sample sample);

        /// <summary>
        /// Asynchronously deletes a sample by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the sample to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the deletion was successful; otherwise, false.</returns>
        Task<bool> DeleteSampleAsync(int id);

        /// <summary>
        /// Asynchronously changes the status of a specific sample.
        /// </summary>
        /// <param name="id">The unique identifier of the sample.</param>
        /// <param name="status">The new status to set for the sample.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the sample with the updated status.</returns>
        Task<Sample> ChangeSampleStatusAsync(int id, SampleStatus status);

        /// <summary>
        /// Asynchronously retrieves a sample along with its associated measurements.
        /// </summary>
        /// <param name="id">The unique identifier of the sample.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the sample with its measurements, or null if not found.</returns>
        Task<Sample?> GetSampleWithMeasurementsAsync(int id);

        /// <summary>
        /// Asynchronously adds a new measurement to an existing sample.
        /// </summary>
        /// <param name="sampleId">The unique identifier of the sample to which the measurement will be added.</param>
        /// <param name="measurement">The measurement entity to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddMeasurementToSampleAsync(int sampleId, Measurement measurement);
    }
}