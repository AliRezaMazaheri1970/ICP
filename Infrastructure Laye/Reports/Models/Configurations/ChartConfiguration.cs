using Core.Icp.Domain.Enums;
using Shared.Icp.DTOs.Reports;

namespace Infrastructure.Icp.Reports.Models.Configurations
{
    /// <summary>
    /// تنظیمات نمودار
    /// </summary>
    public class ChartConfiguration
    {
        /// <summary>
        /// عنوان نمودار
        /// </summary>
        public string ChartTitle { get; set; } = string.Empty;

        /// <summary>
        /// نوع نمودار
        /// </summary>
        public ChartType Type { get; set; } = ChartType.Bar;

        /// <summary>
        /// عنوان محور X
        /// </summary>
        public string XAxisTitle { get; set; } = string.Empty;

        /// <summary>
        /// عنوان محور Y
        /// </summary>
        public string YAxisTitle { get; set; } = string.Empty;

        /// <summary>
        /// نمایش راهنما (Legend)
        /// </summary>
        public bool ShowLegend { get; set; } = true;

        /// <summary>
        /// محل نمایش راهنما
        /// </summary>
        public LegendPosition LegendPosition { get; set; } = LegendPosition.Bottom;

        /// <summary>
        /// نمایش برچسب داده‌ها
        /// </summary>
        public bool ShowDataLabels { get; set; } = false;

        /// <summary>
        /// نمایش خطوط شبکه
        /// </summary>
        public bool ShowGridLines { get; set; } = true;

        /// <summary>
        /// عرض نمودار (پیکسل)
        /// </summary>
        public int Width { get; set; } = 800;

        /// <summary>
        /// ارتفاع نمودار (پیکسل)
        /// </summary>
        public int Height { get; set; } = 400;

        /// <summary>
        /// رنگ‌های نمودار
        /// </summary>
        public List<string> Colors { get; set; } = new()
    {
        "#2E86AB", "#A23B72", "#F18F01", "#C73E1D", "#6A994E",
        "#BC4B51", "#5B8E7D", "#8B5A3C", "#4A5899", "#D4A373"
    };

        /// <summary>
        /// فونت عنوان
        /// </summary>
        public string TitleFontFamily { get; set; } = "B Nazanin";

        /// <summary>
        /// اندازه فونت عنوان
        /// </summary>
        public int TitleFontSize { get; set; } = 16;

        /// <summary>
        /// فونت محورها
        /// </summary>
        public string AxisFontFamily { get; set; } = "B Nazanin";

        /// <summary>
        /// اندازه فونت محورها
        /// </summary>
        public int AxisFontSize { get; set; } = 12;

        /// <summary>
        /// شفافیت (0-100)
        /// </summary>
        public int Opacity { get; set; } = 80;

        /// <summary>
        /// انیمیشن
        /// </summary>
        public bool EnableAnimation { get; set; } = false;

        /// <summary>
        /// قابل تعامل (Interactive)
        /// </summary>
        public bool IsInteractive { get; set; } = false;
    }

    /// <summary>
    /// محل نمایش راهنما
    /// </summary>
    public enum LegendPosition
    {
        Top,
        Bottom,
        Left,
        Right,
        None
    }
}