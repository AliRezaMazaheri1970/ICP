using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Represents CRM (Certified Reference Material) data.
/// </summary>
public class CrmData
{
    /// <summary>
    /// Gets or sets the unique identifier for the CRM data entry.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the CRM identifier (e.g., "OREAS 258").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CrmId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the analysis method used (e.g., "4-Acid Digestion").
    /// </summary>
    [MaxLength(200)]
    public string? AnalysisMethod { get; set; }

    /// <summary>
    /// Gets or sets the type or category of the CRM.
    /// </summary>
    [MaxLength(100)]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the JSON string containing element concentration values.
    /// </summary>
    public string ElementValues { get; set; } = "{}";

    /// <summary>
    /// Gets or sets a value indicating whether this CRM is flagged as a frequently used one.
    /// </summary>
    public bool IsOurOreas { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}