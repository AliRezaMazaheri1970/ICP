namespace Domain.Models;

public class ProjectQualitySummary
{
    public Guid ProjectId { get; set; }
    public int TotalChecks { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int WarningCount { get; set; }
}