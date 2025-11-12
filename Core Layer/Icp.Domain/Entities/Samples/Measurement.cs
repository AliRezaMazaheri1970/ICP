using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Entities.Samples
{
    /// <summary>
    /// Represents a single measurement of an element within a sample.
    /// </summary>
    public class Measurement : BaseEntity
    {
        /// <summary>
        /// Gets or sets the foreign key for the sample this measurement belongs to.
        /// </summary>
        public Guid SampleId { get; set; }

        /// <summary>
        /// Gets or sets the foreign key for the element that was measured.
        /// </summary>
        public Guid ElementId { get; set; }

        /// <summary>
        /// Gets or sets the specific isotope mass number that was measured.
        /// </summary>
        public int Isotope { get; set; }

        /// <summary>
        /// Gets or sets the net signal intensity recorded by the instrument.
        /// </summary>
        public decimal NetIntensity { get; set; }

        /// <summary>
        /// Gets or sets the calculated concentration based on the calibration curve, before applying any dilution factors.
        /// </summary>
        public decimal Concentration { get; set; }

        /// <summary>
        /// Gets or sets the final calculated concentration after applying dilution factors and other corrections.
        /// </summary>
        public decimal? FinalConcentration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the measurement is considered valid.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets or sets any relevant notes or additional information about the measurement.
        /// </summary>
        public string? Notes { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the parent Sample entity.
        /// </summary>
        public Sample Sample { get; set; } = null!;

        /// <summary>
        /// Gets or sets the navigation property to the associated Element entity.
        /// </summary>
        public Element Element { get; set; } = null!;
    }
}