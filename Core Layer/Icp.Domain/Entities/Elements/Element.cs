using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// Represents a chemical element used in analyses (e.g., Fe, Cu).
    /// </summary>
    /// <remarks>
    /// This entity captures static metadata about an element that is shared across projects and samples.
    /// The element can optionally define a preferred calculation method that downstream components may use
    /// when building calibration curves or calculating concentrations.
    /// </remarks>
    public class Element : BaseEntity
    {
        /// <summary>
        /// Gets or sets the chemical symbol of the element (e.g., "Fe", "Cu").
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the element (e.g., "Iron", "Copper").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the atomic number of the element.
        /// </summary>
        public int AtomicNumber { get; set; }

        /// <summary>
        /// Gets or sets the standard atomic mass of the element (in atomic mass units, u).
        /// </summary>
        public decimal AtomicMass { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the element is currently active and available for analysis.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the order in which the element should be displayed in user interfaces.
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Gets or sets the calculation method to be used for this element (e.g., "LinearRegression").
        /// Consumers can use this hint to select an appropriate calibration or quantification strategy.
        /// </summary>
        public string Method { get; set; } = "LinearRegression";

        // Navigation Properties
        /// <summary>
        /// Gets or sets the collection of isotopes associated with this element.
        /// </summary>
        public ICollection<Isotope> Isotopes { get; set; } = new List<Isotope>();
        public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    }
}