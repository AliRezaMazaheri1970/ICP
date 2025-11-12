namespace Shared.Icp.DTOs.Projects
{
    /// <summary>
    /// DTO کامل برای نمایش اطلاعات پروژه
    /// </summary>
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SourceFileName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Computed Properties
        public int SampleCount { get; set; }
        public int ProcessedSamples { get; set; }
        public int ApprovedSamples { get; set; }
        public int RejectedSamples { get; set; }
        public int PendingSamples { get; set; }
        public double ProgressPercentage { get; set; }
        public int? DurationInDays { get; set; }
    }
}