using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.QualityControl
{
    /// <summary>
    /// DTO برای نمایش کنترل کیفیت
    /// </summary>
    public class QualityCheckDto : BaseDto
    {
        public int SampleId { get; set; }
        public string SampleName { get; set; } = string.Empty;
        public string CheckType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? ExpectedValue { get; set; }
        public decimal? MeasuredValue { get; set; }
        public decimal? Deviation { get; set; }
        public decimal? AcceptableDeviationLimit { get; set; }
        public DateTime CheckDate { get; set; }
        public string? Message { get; set; }
    }
}