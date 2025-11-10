namespace Shared.Icp.DTOs.Elements
{
    /// <summary>
    /// DTO برای نقطه کالیبراسیون
    /// </summary>
    public class CalibrationPointDto
    {
        public int Id { get; set; }
        public decimal StandardConcentration { get; set; }
        public decimal MeasuredIntensity { get; set; }
        public int PointOrder { get; set; }
    }
}