namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// Represents the status of a quality control check result.
    /// </summary>
    public enum CheckStatus
    {
        /// <summary>
        /// The check passed successfully.
        /// </summary>
        Pass = 1,

        /// <summary>
        /// The check failed.
        /// </summary>
        Fail = 2,

        /// <summary>
        /// The check resulted in a warning.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// The check is pending and has not been performed yet.
        /// </summary>
        Pending = 4
    }
}