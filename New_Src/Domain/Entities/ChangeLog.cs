namespace Domain.Entities;

/// <summary>
/// Audit log for tracking all changes to project data
/// Equivalent to Python changes_log SQLite table
/// </summary>
public class ChangeLog
{
    public int Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>
    /// Type of change: Weight, Volume, DF, CRM, Drift, BlankScale, etc.
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Solution Label / Sample ID that was changed
    /// </summary>
    public string? SolutionLabel { get; set; }

    /// <summary>
    /// Element that was affected
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Original value before change (JSON serialized if complex)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New value after change (JSON serialized if complex)
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// User who made the change
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Timestamp of the change
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional details about the change
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Batch ID for grouping related changes
    /// </summary>
    public Guid? BatchId { get; set; }

    // Navigation
    public virtual Project? Project { get; set; }
}
