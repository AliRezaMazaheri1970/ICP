using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Projects;
using Core.Icp.Domain.Entities.QualityControl;
using Core.Icp.Domain.Enums;

namespace Core.Icp.Domain.Entities.Samples
{
    /// <summary>
    /// Represents an analytical sample that has been prepared and measured.
    /// </summary>
    public class Sample : BaseEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier string for the sample, often provided by the instrument or LIMS.
        /// </summary>
        public string SampleId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-defined name for the sample.
        /// </summary>
        public string SampleName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any relevant notes or additional information about the sample.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Gets or sets the date and time the sample was analyzed or run.
        /// </summary>
        public DateTime RunDate { get; set; }

        /// <summary>
        /// Gets or sets the current processing status of the sample.
        /// </summary>
        public SampleStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the weight of the sample, used in concentration calculations.
        /// </summary>
        public decimal Weight { get; set; }

        /// <summary>
        /// Gets or sets the volume of the sample, used in concentration calculations.
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// Gets or sets the dilution factor applied to the sample before analysis.
        /// </summary>
        public decimal DilutionFactor { get; set; }

        // Foreign Keys
        /// <summary>
        /// Gets or sets the foreign key for the project this sample belongs to.
        /// </summary>
        public Guid ProjectId { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the parent Project entity.
        /// </summary>
        public Project Project { get; set; } = null!;

        /// <summary>
        /// Gets or sets the collection of measurements taken for this sample.
        /// </summary>
        public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();

        /// <summary>
        /// Gets or sets the collection of quality control checks performed on this sample.
        /// </summary>
        public ICollection<QualityCheck> QualityChecks { get; set; } = new List<QualityCheck>();
    }
}