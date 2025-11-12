namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// Defines the possible statuses of a project.
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// The project has been newly created but not yet processed.
        /// </summary>
        Created = 0,

        /// <summary>
        /// The project data is currently being processed.
        /// </summary>
        Processing = 1,

        /// <summary>
        /// The initial data processing for the project is complete.
        /// </summary>
        Processed = 2,

        /// <summary>
        /// The project is undergoing quality control checks.
        /// </summary>
        UnderQualityCheck = 3,

        /// <summary>
        /// The project has been reviewed and approved.
        /// </summary>
        Approved = 4,

        /// <summary>
        /// The project has been reviewed and rejected.
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// The project is archived and is no longer active.
        /// </summary>
        Archived = 6,

        /// <summary>
        /// The project is in a draft state and can be edited.
        /// </summary>
        Draft = 7
    }
}