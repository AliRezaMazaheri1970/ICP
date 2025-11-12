using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// Represents a chemical element.
    /// </summary>
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
        /// Gets or sets the atomic mass of the element.
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
        /// Gets or sets the calculation method to be used for this element.
        /// The default value is "LinearRegression".
        /// </summary>
        public string Method { get; set; } = "LinearRegression";

        // Navigation Properties
        /// <summary>
        /// Gets or sets the collection of isotopes associated with this element.
        /// </summary>
        public ICollection<Isotope> Isotopes { get; set; } = new List<Isotope>();
    }
}