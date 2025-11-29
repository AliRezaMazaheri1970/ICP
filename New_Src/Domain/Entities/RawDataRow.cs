namespace Domain.Entities;

public class RawDataRow
{
    public int DataId { get; set; }
    public Guid ProjectId { get; set; }

    // Store a single raw row as JSON (flexible) or plain CSV/text
    public string ColumnData { get; set; } = string.Empty;

    // Optional: row index or sample identifier
    public string? SampleId { get; set; }

    // Navigation
    public Project? Project { get; set; }
}