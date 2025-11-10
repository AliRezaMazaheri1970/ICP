using Shared.Icp.DTOs.Common;

namespace Shared.Icp.DTOs.Elements
{
    /// <summary>
    /// DTO برای منحنی کالیبراسیون
    /// </summary>
    public class CalibrationCurveDto : BaseDto
    {
        public int ElementId { get; set; }
        public string ElementSymbol { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateTime CalibrationDate { get; set; }
        public decimal Slope { get; set; }
        public decimal Intercept { get; set; }
        public decimal RSquared { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public List<CalibrationPointDto> Points { get; set; } = new();
    }
}