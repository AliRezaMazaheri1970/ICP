using Core.Icp.Domain.Enums;

namespace Shared.Icp.DTOs.Reports
{
    public class PageSettings
    {
        public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
        public PageSize Size { get; set; } = PageSize.A4;
        public PageMargins Margins { get; set; } = new();
        public bool ShowPageNumbers { get; set; } = true;
        public bool ShowHeader { get; set; } = true;
        public bool ShowFooter { get; set; } = true;
        public string? HeaderText { get; set; }
        public string? FooterText { get; set; }
    }
}
