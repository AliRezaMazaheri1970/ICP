using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Samples
{
    /// <summary>
    /// DTO برای نمایش اندازه‌گیری
    /// </summary>
    public class MeasurementDto : BaseDto
    {
        public int SampleId { get; set; }
        public string SampleName { get; set; } = string.Empty;
        public int ElementId { get; set; }
        public string ElementSymbol { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public int? Isotope { get; set; }
        public decimal NetIntensity { get; set; }
        public decimal? Concentration { get; set; }
        public string Unit { get; set; } = "ppm";
        public decimal? FinalConcentration { get; set; }
        public decimal? StandardError { get; set; }
        public bool IsValid { get; set; }
        public string? Notes { get; set; }
    }
}