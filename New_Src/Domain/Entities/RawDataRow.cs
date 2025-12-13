using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Represents a raw data row associated with a project.
/// </summary>
public class RawDataRow
{
    /// <summary>
    /// Gets or sets the unique identifier for the data row.
    /// </summary>
    [Key]
    public int DataId { get; set; }

    /// <summary>
    /// Gets or sets the associated project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the raw column data, stored as a JSON string.
    /// </summary>
    public string ColumnData { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sample identifier or row index.
    /// </summary>
    public string? SampleId { get; set; }

    /// <summary>
    /// Gets or sets the associated project navigation property.
    /// </summary>
    public Project? Project { get; set; }
}