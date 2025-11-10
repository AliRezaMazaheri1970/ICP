using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Samples
{
    /// <summary>
    /// DTO برای نمایش نمونه
    /// </summary>
    public class SampleDto : BaseDto
    {
        public string SampleId { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public DateTime? RunDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Volume { get; set; }
        public decimal? DilutionFactor { get; set; }
        public string? Notes { get; set; }
        public int MeasurementCount { get; set; }
        public int QualityCheckCount { get; set; }
    }
}