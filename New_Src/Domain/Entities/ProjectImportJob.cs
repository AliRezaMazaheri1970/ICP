using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Persists import job status so it survives restarts.
/// Primary key is JobId (Guid).
/// </summary>
public class ProjectImportJob
{
    [Key]
    public Guid JobId { get; set; }

    // Optional: the project that will be created/associated by import
    public Guid? ResultProjectId { get; set; }

    // The project name requested by the user (optional, useful for listing)
    public string? ProjectName { get; set; }

    // State stored as int (map to Shared.Models.ImportJobState)
    public int State { get; set; }

    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int Percent { get; set; }

    public string? Message { get; set; }

    // NEW: path to temporary uploaded file on disk
    public string? TempFilePath { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}