namespace Domain.Entities;

public class ProjectState
{
    public int StateId { get; set; }
    public Guid ProjectId { get; set; }

    // Full serialized project state (JSON)
    public string Data { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Optional description (e.g. "autosave", "manual save")
    public string? Description { get; set; }

    // Navigation
    public Project? Project { get; set; }
}