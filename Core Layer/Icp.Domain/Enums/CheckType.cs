namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// Defines the different types of quality control checks that can be performed.
    /// </summary>
    public enum CheckType
    {
        /// <summary>
        /// A check of the sample's weight.
        /// </summary>
        WeightCheck = 1,

        /// <summary>
        /// A check of the sample's volume.
        /// </summary>
        VolumeCheck = 2,

        /// <summary>
        /// A check of the sample's dilution factor.
        /// </summary>
        DilutionFactorCheck = 3,

        /// <summary>
        /// A check for empty or blank values in the sample data.
        /// </summary>
        EmptyCheck = 4,

        /// <summary>
        /// A check against a Certified Reference Material (CRM).
        /// </summary>
        CRMCheck = 5,

        /// <summary>
        /// A check for instrument drift using a calibration standard.
        /// </summary>
        DriftCalibration = 6
    }
}