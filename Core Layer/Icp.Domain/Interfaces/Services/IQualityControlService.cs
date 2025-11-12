using Core.Icp.Domain.Entities.QualityControl;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that performs various quality control checks on samples.
    /// </summary>
    public interface IQualityControlService
    {
        /// <summary>
        /// Asynchronously performs a weight check on a given sample.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformWeightCheckAsync(Sample sample);

        /// <summary>
        /// Asynchronously performs a volume check on a given sample.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformVolumeCheckAsync(Sample sample);

        /// <summary>
        /// Asynchronously performs a dilution factor check on a given sample.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformDilutionFactorCheckAsync(Sample sample);

        /// <summary>
        /// Asynchronously performs a check for empty or blank values in a sample's data.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformEmptyCheckAsync(Sample sample);

        /// <summary>
        /// Asynchronously performs a Certified Reference Material (CRM) check on a sample.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <param name="crmId">The identifier of the CRM to check against.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformCRMCheckAsync(Sample sample, int crmId);

        /// <summary>
        /// Asynchronously performs a drift calibration check on a sample to ensure instrument stability.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="QualityCheck"/> result.</returns>
        Task<QualityCheck> PerformDriftCalibrationAsync(Sample sample);

        /// <summary>
        /// Asynchronously performs all applicable quality control checks on a given sample.
        /// </summary>
        /// <param name="sample">The sample to be checked.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of all <see cref="QualityCheck"/> results.</returns>
        Task<IEnumerable<QualityCheck>> PerformAllQualityChecksAsync(Sample sample);

        /// <summary>
        /// Asynchronously retrieves all quality check results for a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of quality checks for the specified project.</returns>
        Task<IEnumerable<QualityCheck>> GetProjectQualityChecksAsync(int projectId);
    }
}