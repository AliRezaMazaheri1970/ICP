namespace Shared.Icp.DTOs.QualityControl
{
    /// <summary>
    /// DTO برای نتیجه کنترل کیفیت
    /// </summary>
    public class QualityCheckResultDto
    {
        public bool Passed { get; set; }
        public string CheckType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal? Deviation { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}