using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Represents a project containing analysis data and states.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the unique identifier for the project.
    /// </summary>
    [Key]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owner of the project.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the project was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the project was last modified.
    /// </summary>
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of raw data rows associated with the project.
    /// </summary>
    public ICollection<RawDataRow> RawDataRows { get; set; } = new List<RawDataRow>();

    /// <summary>
    /// Gets or sets the collection of project states (history/versions).
    /// </summary>
    public ICollection<ProjectState> ProjectStates { get; set; } = new List<ProjectState>();

    /// <summary>
    /// Gets or sets the collection of processed data records.
    /// </summary>
    public ICollection<ProcessedData> ProcessedDatas { get; set; } = new List<ProcessedData>();
}