using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Represents a record of processed analysis outputs.
/// </summary>
public class ProcessedData
{
    /// <summary>
    /// Gets or sets the unique database identifier.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets a user-defined or scoped identifier for this processing run.
    /// </summary>
    public int ProcessedId { get; set; }

    /// <summary>
    /// Gets or sets the associated project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the type of analysis (e.g., "CRM", "QC").
    /// </summary>
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON payload containing the processed results.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the associated project navigation property.
    /// </summary>
    public Project? Project { get; set; }
}