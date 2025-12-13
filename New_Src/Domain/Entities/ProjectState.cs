using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Represents a snapshot or version of the project data state.
/// </summary>
public class ProjectState
{
    /// <summary>
    /// Gets or sets the unique identifier for the state.
    /// </summary>
    [Key]
    public int StateId { get; set; }

    /// <summary>
    /// Gets or sets the associated project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent state, enabling a tree structure.
    /// </summary>
    public int? ParentStateId { get; set; }

    /// <summary>
    /// Gets or sets the version number within the project sequence.
    /// </summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the type of processing that generated this state.
    /// </summary>
    public string ProcessingType { get; set; } = "Import";

    /// <summary>
    /// Gets or sets the full serialized JSON data of the project state.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp of the state.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets an optional description of the state (e.g., autosave, manual save).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the currently active version.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Gets or sets the associated project.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// Gets or sets the parent state.
    /// </summary>
    public ProjectState? ParentState { get; set; }

    /// <summary>
    /// Gets or sets the collection of child states derived from this state.
    /// </summary>
    public ICollection<ProjectState> ChildStates { get; set; } = new List<ProjectState>();
}

/// <summary>
/// Defines constants for various processing types.
/// </summary>
public static class ProcessingTypes
{
    /// <summary>Data import operation.</summary>
    public const string Import = "Import";

    /// <summary>Weight correction operation.</summary>
    public const string WeightCorrection = "Weight Correction";

    /// <summary>Volume correction operation.</summary>
    public const string VolumeCorrection = "Volume Correction";

    /// <summary>Dilution factor correction operation.</summary>
    public const string DfCorrection = "DF Correction";

    /// <summary>Drift correction operation.</summary>
    public const string DriftCorrection = "Drift Correction";

    /// <summary>CRM check operation.</summary>
    public const string CrmCheck = "CRM Check";

    /// <summary>Reference material check operation.</summary>
    public const string RmCheck = "RM Check";

    /// <summary>Operation to remove empty rows.</summary>
    public const string EmptyRowRemoval = "Empty Row Removal";

    /// <summary>Manual edit operation.</summary>
    public const string ManualEdit = "Manual Edit";

    /// <summary>Optimization operation.</summary>
    public const string Optimization = "Optimization";
}