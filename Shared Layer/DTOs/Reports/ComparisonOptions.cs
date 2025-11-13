using Core.Icp.Domain.Enums;

namespace Shared.Icp.DTOs.Reports
{
    public class ComparisonOptions
    {
        public ComparisonType Type { get; set; } = ComparisonType.SideBySide;
        public List<string> ElementSymbols { get; set; } = new();
        public bool ShowDifferences { get; set; } = true;
        public bool ShowPercentageChange { get; set; } = true;
        public bool IncludeComparisonChart { get; set; } = true;
        public ChartType ChartType { get; set; } = ChartType.Bar;
        public double SignificantDifferenceThreshold { get; set; } = 5.0;
        public bool HighlightSignificantDifferences { get; set; } = true;
        public bool IncludeStatistics { get; set; } = true;
        public string? GroupBy { get; set; }
        public string? SortBy { get; set; }
        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    }
}