using Core.Icp.Domain.Base;

namespace Core.Icp.Domain.Entities.Elements
{
    /// <summary>
    /// Represents a single data point used in constructing a calibration curve.
    /// </summary>
    public class CalibrationPoint : BaseEntity
    {
        /// <summary>
        /// Gets or sets the foreign key for the calibration curve this point belongs to.
        /// </summary>
        public Guid CalibrationCurveId { get; set; }

        /// <summary>
        /// Gets or sets the known concentration of the standard solution for this point.
        /// </summary>
        public decimal Concentration { get; set; }

        /// <summary>
        /// Gets or sets the measured signal intensity corresponding to the known concentration.
        /// </summary>
        public decimal Intensity { get; set; }

        /// <summary>
        /// Gets or sets the order of this point in the calibration sequence.
        /// </summary>
        public int PointOrder { get; set; }

        // Navigation Properties
        /// <summary>
        /// Gets or sets the navigation property to the parent CalibrationCurve entity.
        /// </summary>
        public CalibrationCurve CalibrationCurve { get; set; } = null!;
    }
}