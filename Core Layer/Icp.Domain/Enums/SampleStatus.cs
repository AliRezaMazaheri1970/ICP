namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// Defines the possible statuses of a sample during its lifecycle.
    /// </summary>
    public enum SampleStatus
    {
        /// <summary>
        /// The sample is awaiting processing.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// The sample is currently being processed.
        /// </summary>
        Processing = 2,

        /// <summary>
        /// The sample has been successfully processed.
        /// </summary>
        Processed = 3,

        /// <summary>
        /// The sample has been reviewed and approved.
        /// </summary>
        Approved = 4,

        /// <summary>
        /// The sample has been reviewed and rejected.
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// The sample requires further review or re-analysis.
        /// </summary>
        RequiresReview = 6
    }
}