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
        /// Gets or sets the chemical symbol of the measured element (e.g., "Fe", "Cu").
        /// This field is denormalized to simplify reporting/exports without requiring joins.
        /// </summary>
        public string ElementSymbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific isotope mass number that was measured (e.g., 56 for Fe-56).
        /// A null value indicates that the measurement is not isotope-specific.
        /// </summary>
        public int? Isotope { get; set; }

        /// <summary>
        /// Gets or sets the raw signal intensity read from the instrument before any
        /// background subtraction, normalization, or drift correction is applied.
        /// </summary>
        public decimal RawIntensity { get; set; }

        /// <summary>
        /// Gets or sets the net signal intensity recorded by the instrument after background
        /// subtraction and other pre-processing corrections.
        /// </summary>
        public decimal NetIntensity { get; set; }

        /// <summary>
        /// Gets or sets the calculated concentration based on the calibration curve,
        /// before applying any sample-level dilution factors.
        /// </summary>
        public decimal Concentration { get; set; }

        /// <summary>
        /// Gets or sets the final calculated concentration after applying dilution factors
        /// and any additional post-processing corrections.
        /// </summary>
        public decimal? FinalConcentration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the measurement is considered valid
        /// according to the project's quality control rules.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets or sets any relevant notes or additional information about the measurement.
        /// </summary>
        public string? Notes { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the parent <see cref="Sample"/> entity.
        /// </summary>
        public Sample Sample { get; set; } = null!;

        /// <summary>
        /// Gets or sets the navigation property to the associated <see cref="Element"/> entity.
        /// </summary>
        public Element Element { get; set; } = null!;
    }
}