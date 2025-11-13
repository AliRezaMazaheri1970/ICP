namespace Infrastructure.Icp.Reports.Models
{
    /// <summary>
    /// بخش‌های مختلف گزارش
    /// </summary>
    public class ReportSection
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsVisible { get; set; } = true;
        public Dictionary<string, object> Data { get; set; } = new();
    }
}