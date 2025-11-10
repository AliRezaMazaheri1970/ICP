namespace Shared.Icp.DTOs.QualityControl
{
    /// <summary>
    /// DTO برای خلاصه کنترل‌های کیفیت
    /// </summary>
    public class QualityCheckSummaryDto
    {
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public int WarningChecks { get; set; }
        public Dictionary<string, int> ChecksByType { get; set; } = new();
        public List<QualityCheckDto> RecentFailures { get; set; } = new();
    }
}