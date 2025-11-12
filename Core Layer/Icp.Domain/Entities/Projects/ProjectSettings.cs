namespace Core.Icp.Domain.Entities.Projects
{
    /// <summary>
    /// Defines the settings for a project, including validation thresholds and processing options.
    /// </summary>
    public class ProjectSettings
    {
        /// <summary>
        /// Gets or sets the minimum acceptable weight for samples, in grams.
        /// </summary>
        public double? MinAcceptableWeight { get; set; }

        /// <summary>
        /// Gets or sets the maximum acceptable weight for samples, in grams.
        /// </summary>
        public double? MaxAcceptableWeight { get; set; }

        /// <summary>
        /// Gets or sets the minimum acceptable volume for samples, in milliliters (mL).
        /// </summary>
        public double? MinAcceptableVolume { get; set; }

        /// <summary>
        /// Gets or sets the maximum acceptable volume for samples, in milliliters (mL).
        /// </summary>
        public double? MaxAcceptableVolume { get; set; }

        /// <summary>
        /// Gets or sets the minimum allowed dilution factor for sample preparation.
        /// </summary>
        public int? MinDilutionFactor { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed dilution factor for sample preparation.
        /// </summary>
        public int? MaxDilutionFactor { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed Relative Standard Deviation (RSD) in percent for quality control checks.
        /// </summary>
        public double? MaxRSDPercent { get; set; }

        /// <summary>
        /// Gets or sets the tolerance percentage for deviation from Certified Reference Material (CRM) values.
        /// </summary>
        public double? CRMTolerancePercent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic quality control checks should be performed.
        /// </summary>
        public bool AutoQualityControl { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether automatic correction for instrumental drift should be applied.
        /// </summary>
        public bool AutoDriftCorrection { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of decimal places to use when displaying result values.
        /// </summary>
        public int DecimalPlaces { get; set; } = 4;

        /// <summary>
        /// Gets or sets the default unit for displaying concentration results (e.g., "ppm", "ppb").
        /// </summary>
        public string DefaultConcentrationUnit { get; set; } = "ppm";

        /// <summary>
        /// Gets or sets any additional notes or comments related to the project settings.
        /// </summary>
        public string? Notes { get; set; }
    }
}