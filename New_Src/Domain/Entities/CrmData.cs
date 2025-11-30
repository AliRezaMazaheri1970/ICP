using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// CRM (Certified Reference Material) data entity. 
/// Maps to pivot_crm table in the original Python code.
/// </summary>
public class CrmData
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// CRM identifier (e.g., "OREAS 258", "OREAS 252")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CrmId { get; set; } = string.Empty;

    /// <summary>
    /// Analysis method (e.g., "4-Acid Digestion", "Aqua Regia Digestion")
    /// </summary>
    [MaxLength(200)]
    public string? AnalysisMethod { get; set; }

    /// <summary>
    /// Type of CRM (optional categorization)
    /// </summary>
    [MaxLength(100)]
    public string? Type { get; set; }

    /// <summary>
    /// JSON containing element values (e.g., {"Fe": 45.2, "Cu": 0.12, "Zn": 0.05})
    /// This allows flexible storage of various element concentrations. 
    /// </summary>
    public string ElementValues { get; set; } = "{}";

    /// <summary>
    /// Whether this CRM is in "Our OREAS" list (frequently used CRMs)
    /// </summary>
    public bool IsOurOreas { get; set; } = false;

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}