namespace Domain.Entities;

/// <summary>
/// Represents an audit log entry for tracking changes to project data.
/// </summary>
public class ChangeLog
{
    /// <summary>
    /// Gets or sets the unique identifier for the change log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the type of change (e.g., Weight, Volume, Drift).
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the solution label or sample identifier that was changed.
    /// </summary>
    public string? SolutionLabel { get; set; }

    /// <summary>
    /// Gets or sets the element that was affected by the change.
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Gets or sets the original value before the change.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value after the change.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets the username or identifier of the user who made the change.
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional details describing the change.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the batch identifier for grouping related changes.
    /// </summary>
    public Guid? BatchId { get; set; }

    /// <summary>
    /// Gets or sets the associated project navigation property.
    /// </summary>
    public virtual Project? Project { get; set; }
}
