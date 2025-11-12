using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Entities.QualityControl
{
    /// <summary>
    /// Represents the result of a single quality control check performed on a sample.
    /// </summary>
    public class QualityCheck : BaseEntity
    {
        /// <summary>
        /// Gets or sets the type of quality control check that was performed (e.g., WeightCheck, CRMCheck).
        /// </summary>
        public CheckType CheckType { get; set; }

        /// <summary>
        /// Gets or sets the status of the quality check result (e.g., Pass, Fail, Warning).
        /// </summary>
        public CheckStatus Status { get; set; }

        /// <summary>
        /// Gets or sets a summary message indicating the outcome of the check.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets detailed information or data related to the quality check.
        /// </summary>
        public string? Details { get; set; }

        // Foreign Keys
        /// <summary>
        /// Gets or sets the foreign key for the sample on which this check was performed.
        /// </summary>
        public Guid SampleId { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the associated Sample entity.
        /// </summary>
        public Sample Sample { get; set; } = null!;
    }
}