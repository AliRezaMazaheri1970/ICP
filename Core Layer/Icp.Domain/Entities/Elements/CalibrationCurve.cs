using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Projects;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// Represents a calibration curve for a specific element, used to determine concentration from signal intensity.
    /// </summary>
    public class CalibrationCurve : BaseEntity
    {
        /// <summary>
        /// Gets or sets the foreign key for the element to which this calibration curve belongs.
        /// </summary>
        public Guid ElementId { get; set; }

        /// <summary>
        /// Gets or sets the foreign key for the project in which this calibration was performed.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the calibration was performed.
        /// </summary>
        public DateTime CalibrationDate { get; set; }

        /// <summary>
        /// Gets or sets the slope of the linear regression line (y = Slope * x + Intercept).
        /// </summary>
        public decimal Slope { get; set; }

        /// <summary>
        /// Gets or sets the y-intercept of the linear regression line.
        /// </summary>
        public decimal Intercept { get; set; }

        /// <summary>
        /// Gets or sets the R-squared value (coefficient of determination), indicating the goodness of fit of the curve.
        /// </summary>
        public decimal RSquared { get; set; }

        /// <summary>
        /// Gets or sets any relevant notes or additional information about the calibration curve.
        /// </summary>
        public string? Notes { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the associated Element entity.
        /// </summary>
        public Element Element { get; set; } = null!;

        /// <summary>
        /// Gets or sets the navigation property to the associated Project entity.
        /// </summary>
        public Project Project { get; set; } = null!;

        /// <summary>
        /// Gets or sets the collection of calibration points used to generate this curve.
        /// </summary>
        public ICollection<CalibrationPoint> Points { get; set; } = new List<CalibrationPoint>();
    }
}