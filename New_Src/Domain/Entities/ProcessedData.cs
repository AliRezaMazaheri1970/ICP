namespace Domain.Entities;

// Small entity to record processed/analysis outputs (optional)
public class ProcessedData
{
    public int ProcessedId { get; set; }
    public Guid ProjectId { get; set; }
    public string AnalysisType { get; set; } = string.Empty; // e.g. "CRM", "RM", "QC"
    public string Data { get; set; } = string.Empty; // JSON payload
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}