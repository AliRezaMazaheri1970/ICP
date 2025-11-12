namespace Core.Icp.Domain.Base
{
    /// <summary>
    /// Represents the base class for all domain entities, providing common properties 
    /// for identity, auditing, and soft-delete functionality.
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity, which serves as the primary key.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the Coordinated Universal Time (UTC) date and time when the entity was created.
        /// This is automatically initialized to the current UTC time.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the Coordinated Universal Time (UTC) date and time when the entity was last updated.
        /// This value is nullable and should be set upon modification of the entity.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity has been marked as deleted.
        /// This enables a soft-delete mechanism, allowing data to be retained for auditing or recovery purposes.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Gets or sets the identifier (e.g., username or user ID) of the user who created the entity.
        /// This is used for tracking the origin of the data.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the identifier (e.g., username or user ID) of the user who last modified the entity.
        /// This provides an audit trail for data changes.
        /// </summary>
        public string? UpdatedBy { get; set; }
    }
}