namespace Shared.Icp.DTOs.Projects
{
    /// <summary>
    /// DTO برای خلاصه اطلاعات پروژه
    /// </summary>
    public class ProjectSummaryDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int TotalSamples { get; set; }
        public int ProcessedSamples { get; set; }
        public int PendingSamples { get; set; }
        public int TotalElements { get; set; }
        public int TotalMeasurements { get; set; }
        public int PassedQualityChecks { get; set; }
        public int FailedQualityChecks { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}