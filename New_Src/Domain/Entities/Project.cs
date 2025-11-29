namespace Domain.Entities;

public class Project
{
    public Guid ProjectId { get; set; } = Guid.NewGuid();
    public string ProjectName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    public string? Owner { get; set; }

    // Navigation
    public List<RawDataRow> RawDataRows { get; set; } = new();
    public List<ProjectState> ProjectStates { get; set; } = new();
    public List<ProcessedData> ProcessedDatas { get; set; } = new();
}