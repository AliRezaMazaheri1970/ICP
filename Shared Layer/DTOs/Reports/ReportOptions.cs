namespace Shared.Icp.DTOs.Reports
{
    /// <summary>
    /// تنظیمات تولید گزارش
    /// </summary>
    public class ReportOptions
    {
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeRawData { get; set; } = false;
        public bool IncludeStatistics { get; set; } = true;
        public bool IncludeQualityControl { get; set; } = true;
        public bool IncludeRejectedSamples { get; set; } = true;
        public bool IncludeCalibrationCurves { get; set; } = true;
        public bool IncludeCrmDetails { get; set; } = true;
        public bool IncludeDriftCorrections { get; set; } = true;
        public int DecimalPlaces { get; set; } = 3;
        public string Language { get; set; } = "fa-IR";
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Organization { get; set; }
        public string? OrganizationLogo { get; set; }
        public PageSettings PageSettings { get; set; } = new();
        public Dictionary<string, object> AdditionalFilters { get; set; } = new();
    }
}