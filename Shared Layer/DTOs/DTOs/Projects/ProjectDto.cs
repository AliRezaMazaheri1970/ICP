using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Projects
{
    /// <summary>
    /// DTO برای نمایش پروژه
    /// </summary>
    public class ProjectDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SourceFileName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "Active";
        public int SampleCount { get; set; }
        public int CalibrationCurveCount { get; set; }
        public string? CreatedBy { get; set; }
    }
}